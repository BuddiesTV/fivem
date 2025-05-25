using System.Reflection;
using System.Runtime.Loader;
using CitizenFX.Host.Server.Core;
using CitizenFX.Shared.Interfaces;

namespace CitizenFX.Host.Server.Services;

internal class ResourceService
{
	private Dictionary<string, IResource> RegisteredResources { get; } = new();

	public void RegisterResource(string resourceName, IntPtr runtime)
	{
		var resource = new Resource(resourceName, runtime);

		if (!RegisteredResources.TryAdd(resourceName, resource))
			throw new Exception($"Tried to register resource {resourceName} which is already registered.");
	}

	public void ShutdownResource(string resourceName)
	{
		var resource = RegisteredResources.GetValueOrDefault(resourceName);
		if (resource == null)
			throw new Exception($"Tried to shutdown resource {resourceName} which is not registered.");

		if (resource.CitizenResource != null) resource.CitizenResource.OnStop();

		if (resource.AssemblyLoadContext != null)
		{
			resource.AssemblyLoadContext.Unload();
			resource.AssemblyLoadContext = null;
			resource.WrapperAssembly = null;
		}

		if (!RegisteredResources.Remove(resourceName))
			throw new Exception($"Failed to remove resource {resourceName} from registered resources.");
	}

	public void LoadResourceFile(string resourceName, string fileName)
	{
		var resource = RegisteredResources.GetValueOrDefault(resourceName);
		if (resource == null)
			throw new Exception(
				$"Tried to load resource file {fileName} for resource {resourceName} which is not registered.");

		var currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		if (currentPath == null) throw new Exception($"Failed to get current path.");

		if (resource.AssemblyLoadContext != null) throw new Exception($"Resource {resourceName} is already loaded.");

		var assemblyLoadContext = new AssemblyLoadContext(resourceName, true);
		assemblyLoadContext.LoadFromAssemblyPath(Path.Combine(currentPath, "MessagePack.dll"));
		var wrapperAssembly =
			assemblyLoadContext.LoadFromAssemblyPath(Path.Combine(currentPath, "CitizenFX.Wrapper.dll"));

		resource.AssemblyLoadContext = assemblyLoadContext;
		resource.LoadAssembly(wrapperAssembly, fileName);
	}
}
