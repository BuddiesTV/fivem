using System.Reflection;
using System.Runtime.Loader;
using CitizenFX.Shared;
using CitizenFX.Shared.Core;
using CitizenFX.Shared.Interfaces;

namespace CitizenFX.Host.Server.Core;

internal class Resource(string name, IntPtr runtime) : IResource
{
	public string Name { get; init; } = name;
	public IntPtr Runtime { get; init; } = runtime;
	public AssemblyLoadContext? AssemblyLoadContext { get; set; }
	public Assembly? WrapperAssembly { get; set; }
	public CitizenResource? CitizenResource { get; set; }

	public void LoadAssembly(Assembly wrapperAssembly, string assemblyName)
	{
		if (WrapperAssembly != null) throw new Exception($"Resource {Name} is already loaded.");
		WrapperAssembly = wrapperAssembly;

		var data = Library.ReadAssemblyRaw(Runtime, assemblyName);

		if (data.Length == 0) throw new Exception($"Failed to load assembly {assemblyName}.");

		var entryPoint = wrapperAssembly.GetType("CitizenFX.Wrapper.EntryPoint")?.GetMethod("Main");
		if (entryPoint == null) throw new Exception($"Failed to find entry point in assembly {assemblyName}.");

		entryPoint.Invoke(null, [this, data]);
	}
}
