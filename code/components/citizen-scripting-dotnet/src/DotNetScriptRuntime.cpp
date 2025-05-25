#include "StdInc.h"
#include "DotNetScriptRuntime.h"
#include "DotNetHost.h"

#include <CoreConsole.h>
#include <Error.h>

#include <filesystem>

uint64_t GetCurrentSchedulerTime()
{
	// TODO: replace this when the bookmark scheduler follows frame time instead of real time.
	return std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::steady_clock::now().time_since_epoch()).count();
}

namespace fx::dotnet
{
struct DotNetBoundary
{
	std::thread::id threadId;
};

#define DOTNET_BOUNDARY_START                                             \
	DotNetBoundary boundary{ std::this_thread::get_id() }; \
	m_scriptHost->SubmitBoundaryStart((char*)&boundary, sizeof(DotNetBoundary));

#define DOTNET_BOUNDARY_END m_scriptHost->SubmitBoundaryEnd((char*)&boundary, sizeof(DotNetBoundary));

static DotNetHost g_dotNetHost;
static std::string g_hostFxrPath;

static std::string GetHostFxrPath()
{
	// These are most likely the dotnet root directories,
	// we might want to add a option to change it manually, in case someone installed it somewhere else.
#ifdef _WIN32
	const std::filesystem::path dotnetRoot = "C:/Program Files/dotnet/host/fxr";
#else
	const std::filesystem::path dotnetRoot = "/usr/share/dotnet/host/fxr";
#endif

	// Check if the root directory even exists.
	if (!std::filesystem::exists(dotnetRoot))
	{
		return "";
	}

	// Getting the 'latest' directory with hostfxr.dll(libhostfxr.so) inside,
	// sorted by folder name as these are for e.g. 8.0.13, 9.0.2 depending on the installed dotnet version.
	std::string latestVersion;

	try
	{
		for (const auto& entry : std::filesystem::directory_iterator(dotnetRoot))
		{
			if (entry.is_directory())
			{
				const std::string version = entry.path().filename().string();
				if (version > latestVersion)
					latestVersion = version;
			}
		}
	}
	catch (const std::filesystem::filesystem_error& e)
	{
		FatalError("Error scanning dotnet directories: %s\n", e.what());
		return "";
	}

	if (latestVersion.empty())
	{
		return "";
	}

#ifdef _WIN32
	return (dotnetRoot / latestVersion / "hostfxr.dll").string();
#else
	return (dotnetRoot / latestVersion / "libhostfxr.so").string();
#endif
}

struct DotNetPushEnvironment
{
	fx::PushEnvironment m_pushEnvironment;

	DotNetPushEnvironment(DotNetScriptRuntime* runtime)
		: m_pushEnvironment(runtime)
	{
	}
};

static InitFunction initFunction([]()
{
	// Storing the hostFxr path, so if DotNetScriptRuntime::Create gets called & g_hostFxrLibrary is not initialized, we can check + log the path.
	g_hostFxrPath = GetHostFxrPath();

	if (g_hostFxrPath.empty())
	{
		FatalError("Could not locate hostfxr library\n");
		return;
	}

	// DotNet Host should only be initialized once.
	try
	{
		g_dotNetHost.Initialize(g_hostFxrPath);
		g_dotNetHost.RunApp();
		g_dotNetHost.InitializeLibrary(reinterpret_cast<void*>(&DotNetScriptRuntime::ReadAssemblyWrapper), reinterpret_cast<void*>(&DotNetScriptRuntime::Print), reinterpret_cast<void*>(&DotNetScriptRuntime::SetEventHandler), reinterpret_cast<void*>(&DotNetScriptRuntime::SetTickHandler), reinterpret_cast<void*>(&DotNetScriptRuntime::GetNative), reinterpret_cast<void*>(&DotNetScriptRuntime::InvokeNative), reinterpret_cast<void*>(&DotNetScriptRuntime::InvokeFunctionReference));
	}
	catch (const std::exception& e)
	{
		FatalError("Failed to initialize DotNet host: %s\n", e.what());
	}
});

void* CORECLR_DELEGATE_CALLTYPE DotNetScriptRuntime::GetNative(uint64_t nativeHash)
{
	auto nativeHandler = ScriptEngine::GetNativeHandlerPtr(nativeHash);

	return reinterpret_cast<void*>(nativeHandler);
}

void CORECLR_DELEGATE_CALLTYPE DotNetScriptRuntime::InvokeNative(NativeHandler nativeHandler, ScriptContext* context, uint64_t nativeHash)
{
	try
	{
		(*nativeHandler)(*context);
	}
	catch (const std::exception& e)
	{
		std::string error = fmt::sprintf(
		"Error executing native 0x%016llx (Handler Ptr: %p), exception: %s\n",
		nativeHash,
		(void*)nativeHandler,
		e.what());
		trace(error);
	}
}

void CORECLR_DELEGATE_CALLTYPE DotNetScriptRuntime::SetEventHandler(void* runtimePointer, triggerRuntimeEventDelegate triggerRuntimeEventFnPtr)
{
	DotNetScriptRuntime* runtime = static_cast<DotNetScriptRuntime*>(runtimePointer);

	runtime->g_triggerRuntimeEventDelegate = triggerRuntimeEventFnPtr;
}

void CORECLR_DELEGATE_CALLTYPE DotNetScriptRuntime::SetTickHandler(void* runtimePointer, tickRuntimeDelegate tickRuntimeDelegateFnPtr)
{
	DotNetScriptRuntime* runtime = static_cast<DotNetScriptRuntime*>(runtimePointer);

	runtime->g_tickRuntimeDelegate = tickRuntimeDelegateFnPtr;
}

bool DotNetScriptRuntime::ReadAssembly(char* assemblyName, void** assemblyData, int* dataSize)
{
	fx::OMPtr<fxIStream> stream;
	result_t hr = m_scriptHost->OpenHostFile(assemblyName, stream.GetAddressOf());

	if (FX_SUCCEEDED(hr))
	{
		size_t length = 0;
		uint32_t read;

		stream->GetLength(&length);
		*dataSize = static_cast<int>(length);

		*assemblyData = malloc(length);
		if (!*assemblyData)
		{
			stream->Release();

			return false;
		}

		hr = stream->Read(*assemblyData, length, &read);
		if (!FX_SUCCEEDED(hr) || read != length)
		{
			free(*assemblyData);
			*assemblyData = nullptr;
			
			stream->Release();

			return false;
		}

		return true;
	}

	return false;
}

bool CORECLR_DELEGATE_CALLTYPE DotNetScriptRuntime::ReadAssemblyWrapper(void* runtimePointer, char* assemblyName, void** assemblyData, int* dataSize)
{
	DotNetScriptRuntime* runtime = static_cast<DotNetScriptRuntime*>(runtimePointer);

	return runtime->ReadAssembly(assemblyName, assemblyData, dataSize);
}

void CORECLR_DELEGATE_CALLTYPE DotNetScriptRuntime::InvokeFunctionReference(void* runtimePointer, std::string refId, char* argsSerialized, uint32_t argsSize)
{
	DotNetScriptRuntime* runtime = static_cast<DotNetScriptRuntime*>(runtimePointer);

	fx::PushEnvironment env(runtime);

	fx::OMPtr<IScriptBuffer> retvalBuffer;
	runtime->GetScriptHost()->InvokeFunctionReference(const_cast<char*>(refId.c_str()), argsSerialized, argsSize, retvalBuffer.GetAddressOf());
	size_t size = (retvalBuffer.GetRef()) ? retvalBuffer->GetLength() : 0;
}

result_t DotNetScriptRuntime::Create(IScriptHost* host)
{
	if (!host)
	{
		return FX_E_INVALIDARG;
	}

	m_scriptHost = host;

	{
		fx::OMPtr<IScriptHost> ptr(host);

		fx::OMPtr<IScriptHostWithResourceData> resourcePtr;
		if (ptr.As(&resourcePtr) == FX_S_OK)
		{
			m_resourceHost = resourcePtr.GetRef();
		}

		fx::OMPtr<IScriptHostWithManifest> manifestPtr;
		if (ptr.As(&manifestPtr) == FX_S_OK)
		{
			m_manifestHost = manifestPtr.GetRef();
		}
	}

	char* resourceName = nullptr;
	if (m_resourceHost)
	{
		m_resourceHost->GetResourceName(&resourceName);
		if (resourceName)
		{
			m_resourceName = resourceName;
		}
	}

	fx::PushEnvironment env(this);

	fx::ResourceManager* resourceManager = fx::ResourceManager::GetCurrent();
	fwRefContainer<fx::Resource> resource = resourceManager->GetResource(m_resourceName.c_str());
	std::string resourcePath = resource->GetPath();

	if (g_dotNetHost.IsInitialized())
	{
		g_dotNetHost.RegisterResource(m_resourceName, this);
	}
	else
	{
		FatalError("DotNet host not initialized, failed to register resource %s\n", m_resourceName.c_str());
		return FX_E_NOTIMPL;
	}

	return FX_S_OK;
}

result_t DotNetScriptRuntime::Destroy()
{
	if (g_dotNetHost.IsInitialized())
	{
		g_dotNetHost.ShutdownResource(GetResourceName());
	}

	m_scriptHost = nullptr;
	m_resourceHost = nullptr;
	m_manifestHost = nullptr;
	m_parentObject = nullptr;

	return FX_S_OK;
}

int32_t DotNetScriptRuntime::GetInstanceId()
{
	return m_instanceId;
}

void* DotNetScriptRuntime::GetParentObject()
{
	return m_parentObject;
}

void DotNetScriptRuntime::SetParentObject(void* ptr)
{
	m_parentObject = reinterpret_cast<fx::Resource*>(ptr);
}

result_t DotNetScriptRuntime::LoadFile(char* scriptName)
{
	if (!scriptName)
	{
		return FX_E_INVALIDARG;
	}

	if (g_dotNetHost.IsInitialized())
	{
		fx::PushEnvironment env(this);

		g_dotNetHost.LoadResourceFile(m_resourceName, scriptName);
		return FX_S_OK;
	}

	return FX_E_NOTIMPL;
}

result_t DotNetScriptRuntime::Tick()
{
	if (g_tickRuntimeDelegate)
	{
		DOTNET_BOUNDARY_START

		g_tickRuntimeDelegate(GetCurrentSchedulerTime());

		DOTNET_BOUNDARY_END
	}

	return FX_S_OK;
}

result_t DotNetScriptRuntime::TriggerEvent(char* eventName, char* eventPayload, uint32_t payloadSize, char* eventSource)
{
	if (!eventName)
	{
		return FX_E_INVALIDARG;
	}

	if (g_triggerRuntimeEventDelegate)
	{
		fx::PushEnvironment env(this);

		DOTNET_BOUNDARY_START

		g_triggerRuntimeEventDelegate(eventName, eventPayload, payloadSize, eventSource);

		DOTNET_BOUNDARY_END

		return FX_S_OK;
	}

	return FX_E_NOTIMPL;
}

result_t DotNetScriptRuntime::CallRef(int32_t refIdx, char* argsSerialized, uint32_t argsLength, IScriptBuffer** retval)
{
	return FX_S_OK;
}

result_t DotNetScriptRuntime::DuplicateRef(int32_t refIdx, int32_t* outRefIdx)
{
	return FX_S_OK;
}

result_t DotNetScriptRuntime::RemoveRef(int32_t refIdx)
{
	return FX_S_OK;
}

result_t DotNetScriptRuntime::WalkStack(char* boundaryStart, uint32_t boundaryStartLength, char* boundaryEnd, uint32_t boundaryEndLength, IScriptStackWalkVisitor* visitor)
{
	return FX_S_OK;
}

result_t DotNetScriptRuntime::EmitWarning(char* channel, char* message)
{
	return FX_S_OK;
}

int DotNetScriptRuntime::HandlesFile(char* filename, IScriptHostWithResourceData* metadata)
{
	if (!filename || !metadata)
	{
		return 0;
	}

	int dotNetFlagCount = 0;
	metadata->GetNumResourceMetaData("dotnet", &dotNetFlagCount);
	if (dotNetFlagCount <= 0)
		return 0;

	if (!g_dotNetHost.IsInitialized())
		return 0;

	constexpr std::string_view EXTENSION = "dotnet.dll";
	const std::string_view filenameView = filename;

	if (filenameView.size() < EXTENSION.size())
		return 0;

	return filenameView.substr(filenameView.size() - EXTENSION.size()) == EXTENSION ? 1 : 0;
}

// {1697CE87-02BC-1D08-2B07-0398178B1C26}
FX_DEFINE_GUID(CLSID_DotNetScriptRuntime, 0x1697ce87, 0x02bc, 0x1d08, 0x2b, 0x07, 0x03, 0x98, 0x17, 0x8b, 0x1c, 0x26);

FX_NEW_FACTORY(DotNetScriptRuntime);

FX_IMPLEMENTS(CLSID_DotNetScriptRuntime, IScriptRuntime);
FX_IMPLEMENTS(CLSID_DotNetScriptRuntime, IScriptFileHandlingRuntime);
}
