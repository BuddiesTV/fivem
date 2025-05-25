#include "StdInc.h"
#include "DotNetHost.h"
#include <codecvt>
#include <locale>

#ifdef _WIN32
typedef wchar_t char_t;
#define CH(str) L##str
#else
typedef char char_t;
#define CH(str) str
#endif

namespace fx::dotnet
{
namespace
{
	HostFxrLibrary g_hostFxrLibrary;
	resourceRegisterDelegate g_resourceRegisterDelegate = nullptr;
	resourceShutdownDelegate g_resourceShutdownDelegate = nullptr;
	resourceLoadFileDelegate g_resourceLoadFileDelegate = nullptr;
	resourceTickDelegate g_resourceTickDelegate = nullptr;
	libraryInitializeDelegate g_libraryInitializeDelegate = nullptr;

	std::string MakeRelativeNarrowPath(const std::string& path)
	{
		const auto citPath = GetAbsoluteCitPath();
#ifdef _WIN32
		return ToNarrow(citPath) + path;
#else
		return citPath + path;
#endif
	}

#ifdef _WIN32
	std::wstring ConvertUtf8ToWide(const std::string& str)
	{
		if (str.empty())
			return L"";

		const int size = MultiByteToWideChar(CP_UTF8, 0, str.data(),
		static_cast<int>(str.size()), nullptr, 0);
		std::wstring result(size, 0);
		MultiByteToWideChar(CP_UTF8, 0, str.data(),
		static_cast<int>(str.size()), result.data(), size);
		return result;
	}
#endif
}

void DotNetHost::InitializeLibrary(void* readyAssemblyWrapperFnPtr, void* printFnPtr, void* setEventHandlerFnPtr, void* setTickHandlerFnPtr, void* getNativeHandlerFnPtr, void* invokeNativeFnPtr, void* invokeFunctionReferenceFnPtr)
{
	g_libraryInitializeDelegate(readyAssemblyWrapperFnPtr, printFnPtr, setEventHandlerFnPtr, setTickHandlerFnPtr, getNativeHandlerFnPtr, invokeNativeFnPtr, invokeFunctionReferenceFnPtr);
}

void DotNetHost::Initialize(std::string hostFxrPath)
{
	if (m_initialized)
		return;

	// HostFxr Library should only be initialized once.
	if (!g_hostFxrLibrary.IsInitialized())
	{
		g_hostFxrLibrary.Initialize(hostFxrPath);
	}

	const std::string basePath = MakeRelativeNarrowPath("citizen\\dotnet\\CitizenFX.Host.Server.dll");

#ifdef _WIN32
	const std::wstring wideBasePath = ConvertUtf8ToWide(basePath);
#else
	const std::string_view basePathView = basePath;
	const std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> converter;
	const std::wstring wideBasePath = converter.from_bytes(basePath.data());
#endif

	const wchar_t* args[] = { wideBasePath.c_str() };
	hostfxr_handle handle = nullptr;

	if (g_hostFxrLibrary.m_hostFxrInitialize(1, args, nullptr, &handle) != 0 || !handle)
	{
		throw std::runtime_error("Failed to initialize hostfxr");
	}

	m_hostFxrHandle = std::shared_ptr<hostfxr_handle>(
	new hostfxr_handle(handle),
	[](hostfxr_handle* ptr)
	{
		if (ptr && *ptr)
		{
			g_hostFxrLibrary.m_hostFxrClose(*ptr);
		}
		delete ptr;
	});

	g_hostFxrLibrary.InitializeExtra(handle);

	void* resourceRegisterDelegatePtr = nullptr;
	void* resourceShutdownDelegatePtr = nullptr;
	void* resourceLoadFileDelegatePtr = nullptr;
	void* resourceEventTriggeredDelegatePtr = nullptr;
	void* resourceTickDelegatePtr = nullptr;
	void* libraryInitializeDelegatePtr = nullptr;

	g_hostFxrLibrary.m_getFunctionPointer(
	CH("CitizenFX.Host.Server.Host, CitizenFX.Host.Server"),
	CH("RegisterResourceCallback"),
	UNMANAGEDCALLERSONLY_METHOD, nullptr, nullptr,
	&resourceRegisterDelegatePtr);

	g_hostFxrLibrary.m_getFunctionPointer(
	CH("CitizenFX.Host.Server.Host, CitizenFX.Host.Server"),
	CH("ShutdownResourceCallback"),
	UNMANAGEDCALLERSONLY_METHOD, nullptr, nullptr,
	&resourceShutdownDelegatePtr);

	g_hostFxrLibrary.m_getFunctionPointer(
	CH("CitizenFX.Host.Server.Host, CitizenFX.Host.Server"),
	CH("LoadResourceFileCallback"),
	UNMANAGEDCALLERSONLY_METHOD, nullptr, nullptr,
	&resourceLoadFileDelegatePtr);

	g_hostFxrLibrary.m_getFunctionPointer(
	CH("CitizenFX.Host.Server.Host, CitizenFX.Host.Server"),
	CH("Tick"),
	UNMANAGEDCALLERSONLY_METHOD, nullptr, nullptr,
	&resourceTickDelegatePtr);

	g_hostFxrLibrary.m_getFunctionPointer(
	CH("CitizenFX.Shared.Core.Library, CitizenFX.Shared"),
	CH("Initialize"),
	UNMANAGEDCALLERSONLY_METHOD, nullptr, nullptr,
	&libraryInitializeDelegatePtr);

	g_resourceRegisterDelegate = reinterpret_cast<resourceRegisterDelegate>(resourceRegisterDelegatePtr);
	g_resourceShutdownDelegate = reinterpret_cast<resourceShutdownDelegate>(resourceShutdownDelegatePtr);
	g_resourceLoadFileDelegate = reinterpret_cast<resourceLoadFileDelegate>(resourceLoadFileDelegatePtr);
	g_resourceTickDelegate = reinterpret_cast<resourceTickDelegate>(resourceTickDelegatePtr);
	g_libraryInitializeDelegate = reinterpret_cast<libraryInitializeDelegate>(libraryInitializeDelegatePtr);

	m_initialized = true;
}

void DotNetHost::Tick()
{
	if (!m_initialized || !g_resourceTickDelegate)
		return;

	g_resourceTickDelegate();
}

void DotNetHost::RunApp()
{
	if (!m_initialized || !m_hostFxrHandle)
		return;

	g_hostFxrLibrary.m_hostFxrRunApp(*m_hostFxrHandle);
}

void DotNetHost::Shutdown()
{
	if (!m_initialized || !m_hostFxrHandle)
		return;

	g_hostFxrLibrary.m_hostFxrClose(*m_hostFxrHandle);
}

void DotNetHost::RegisterResource(std::string resourceName, fx::dotnet::DotNetScriptRuntime* runtime)
{
	if (!m_initialized || !g_resourceRegisterDelegate)
		return;

	g_resourceRegisterDelegate(resourceName.c_str(), runtime);
}

void DotNetHost::ShutdownResource(std::string resourceName)
{
	if (!m_initialized || !g_resourceShutdownDelegate)
		return;

	g_resourceShutdownDelegate(resourceName.c_str());
}

void DotNetHost::LoadResourceFile(std::string resourceName, std::string resourceFile)
{
	if (!m_initialized || !g_resourceLoadFileDelegate)
		return;

	g_resourceLoadFileDelegate(resourceName.c_str(), resourceFile.c_str());
}
}
