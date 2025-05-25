#pragma once

#include "StdInc.h"
#include "corehost/hostfxr.h"
#include "corehost/coreclr_delegates.h"

#include "DotNetScriptRuntime.h"

#ifdef _WIN32
#include <windows.h>
#else
#include <dlfcn.h>
#endif

namespace fx::dotnet
{
class HostFxrLibrary
{
private:
	bool m_initialized = false;
	void* m_hostFxrLibraryHandle = nullptr;

	void* GetExport(const char* name)
	{
		if (!m_hostFxrLibraryHandle)
		{
			return nullptr;
		}

		#ifdef _WIN32
		return GetProcAddress(static_cast<HMODULE>(m_hostFxrLibraryHandle), name);
#else
		return dlsym(m_hostFxrLibraryHandle, name);
#endif
	}

public:
	hostfxr_run_app_fn m_hostFxrRunApp = nullptr;
	hostfxr_initialize_for_dotnet_command_line_fn m_hostFxrInitialize = nullptr;
	hostfxr_close_fn m_hostFxrClose = nullptr;
	hostfxr_get_runtime_delegate_fn m_getRuntimeDelegate = nullptr;
	get_function_pointer_fn m_getFunctionPointer = nullptr;

	HostFxrLibrary() = default;

	void Initialize(std::string hostFxrPath);
	void InitializeExtra(hostfxr_handle handle);

	bool IsInitialized() const
	{
		return m_initialized;
	}
};
}
