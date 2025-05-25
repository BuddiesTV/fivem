#include "StdInc.h"
#include "DotNetHostFxrLibrary.h"

namespace fx::dotnet
{
void HostFxrLibrary::InitializeExtra(hostfxr_handle handle)
{
	if (!m_initialized || !handle || !m_getRuntimeDelegate)
	{
		throw std::runtime_error("HostFxrLibrary not properly initialized");
	}

	int result = m_getRuntimeDelegate(handle,
	hdt_get_function_pointer,
	reinterpret_cast<void**>(&m_getFunctionPointer));

	if (result != 0 || !m_getFunctionPointer)
	{
		throw std::runtime_error("Failed to get hdt_get_function_pointer export");
	}
}

void HostFxrLibrary::Initialize(std::string hostFxrPath)
{
	if (m_initialized)
		return;

	if (hostFxrPath.empty())
	{
		throw std::runtime_error("Invalid hostfxr path");
	}

#ifdef _WIN32
	m_hostFxrLibraryHandle = LoadLibraryExA(hostFxrPath.c_str(), nullptr, 0);
	if (!m_hostFxrLibraryHandle)
	{
		DWORD error = GetLastError();
		throw std::runtime_error("Failed to load hostfxr library: Error " + std::to_string(error));
	}
#else
	m_hostFxrLibraryHandle = dlopen(hostFxrPath.c_str(), RTLD_LAZY);
	if (!m_hostFxrLibraryHandle)
	{
		const char* errorMsg = dlerror();
		throw std::runtime_error(errorMsg ? errorMsg : "Unknown error loading hostfxr library");
	}
#endif

	m_hostFxrInitialize = reinterpret_cast<hostfxr_initialize_for_dotnet_command_line_fn>(
	GetExport("hostfxr_initialize_for_dotnet_command_line"));
	m_hostFxrRunApp = reinterpret_cast<hostfxr_run_app_fn>(
	GetExport("hostfxr_run_app"));
	m_hostFxrClose = reinterpret_cast<hostfxr_close_fn>(
	GetExport("hostfxr_close"));
	m_getRuntimeDelegate = reinterpret_cast<hostfxr_get_runtime_delegate_fn>(
	GetExport("hostfxr_get_runtime_delegate"));

	if (!m_hostFxrInitialize || !m_hostFxrRunApp || !m_hostFxrClose || !m_getRuntimeDelegate)
	{
#ifdef _WIN32
		if (m_hostFxrLibraryHandle)
		{
			FreeLibrary(static_cast<HMODULE>(m_hostFxrLibraryHandle));
			m_hostFxrLibraryHandle = nullptr;
		}
#else
		if (m_hostFxrLibraryHandle)
		{
			dlclose(m_hostFxrLibraryHandle);
			m_hostFxrLibraryHandle = nullptr;
		}
#endif
		throw std::runtime_error("Failed to get required hostfxr exports");
	}

	m_initialized = true;
}
}
