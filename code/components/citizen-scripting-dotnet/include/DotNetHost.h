#pragma once

#include "StdInc.h"

#include "DotNetHostFxrLibrary.h"

typedef void (*resourceRegisterDelegate)(const char* resourceName, fx::dotnet::DotNetScriptRuntime* runtime);
typedef void (*resourceShutdownDelegate)(const char* resourceName);
typedef void (*resourceLoadFileDelegate)(const char* resourceName, const char* resourceFile);
typedef void (*resourceTickDelegate)();

typedef void (*libraryInitializeDelegate)(void* readyAssemblyWrapperFnPtr, void* printFnPtr, void* setEventHandlerFnPtr, void* setTickHandlerFnPtr, void* getNativeHandlerFnPtr, void* invokeNativeFnPtr, void* invokeFunctionReferenceFnPtr);

namespace fx::dotnet
{
class DotNetHost
{
private:
	bool m_initialized = false;
	std::shared_ptr<hostfxr_handle> m_hostFxrHandle;

public:
	DotNetHost() = default;

	void Initialize(std::string hostFxrPath);
	void InitializeLibrary(void* readyAssemblyWrapperFnPtr, void* printFnPtr, void* setEventHandlerFnPtr, void* setTickHandlerFnPtr, void* getNativeHandlerFnPtr, void* invokeNativeFnPtr, void* invokeFunctionReferenceFnPtr);
	void RunApp();
	void Shutdown();

	void Tick();

	void RegisterResource(std::string resourceName, fx::dotnet::DotNetScriptRuntime* runtime);
	void ShutdownResource(std::string resourceName);
	void LoadResourceFile(std::string resourceName, std::string resourceFile);

	bool IsInitialized() const
	{
		return m_initialized;
	}
};
}
