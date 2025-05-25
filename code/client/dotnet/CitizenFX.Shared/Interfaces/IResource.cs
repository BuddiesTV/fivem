using System.Reflection;
using System.Runtime.Loader;

namespace CitizenFX.Shared.Interfaces;

public interface IResource
{
	public string Name { get; init; }
	public IntPtr Runtime { get; init; }
	public AssemblyLoadContext? AssemblyLoadContext { get; set; }
	public Assembly? WrapperAssembly { get; set; }
	public CitizenResource? CitizenResource { get; set; }

	void LoadAssembly(Assembly wrapperAssembly, string assemblyName);
}
