using System.Runtime.InteropServices;
using CitizenFX.Host.Server.Services;

namespace CitizenFX.Host.Server;

internal static class Host
{
	private static ResourceService _resourceService = null!;

	private static void Main()
	{
		_resourceService = new();
	}

	[UnmanagedCallersOnly]
	private static void RegisterResourceCallback(IntPtr resourceNamePtr, IntPtr runtime)
	{
		string resourceName = Marshal.PtrToStringUTF8(resourceNamePtr)!;
		
		_resourceService.RegisterResource(resourceName, runtime);
	}

	[UnmanagedCallersOnly]
	private static void ShutdownResourceCallback(IntPtr resourceNamePtr)
	{
		string resourceName = Marshal.PtrToStringUTF8(resourceNamePtr)!;
		
		_resourceService.ShutdownResource(resourceName);
	}

	[UnmanagedCallersOnly]
	private static void LoadResourceFileCallback(IntPtr resourceNamePtr, IntPtr fileNamePtr)
	{
		string resourceName = Marshal.PtrToStringUTF8(resourceNamePtr)!;
		string fileName = Marshal.PtrToStringUTF8(fileNamePtr)!;
		
		_resourceService.LoadResourceFile(resourceName, fileName);
	}
}
