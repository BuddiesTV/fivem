using System.Reflection;
using System.Runtime.InteropServices;
using CitizenFX.Shared;
using CitizenFX.Shared.Core;
using CitizenFX.Shared.Interfaces;
using CitizenFX.Wrapper.Core;
using CitizenFX.Wrapper.Core.MessagePack;
using CitizenFX.Wrapper.Core.Tasks;
using MessagePack;
using MessagePack.Resolvers;

namespace CitizenFX.Wrapper;

public static class EntryPoint
{
	private static readonly TickTaskScheduler Scheduler = new TickTaskScheduler();
	private static readonly TickSynchronizationContext SyncContext = new TickSynchronizationContext(Scheduler);
	
	private static void TriggerEvent(IntPtr eventNamePtr, IntPtr eventPayload, uint payloadSize, IntPtr eventSource)
	{
		var eventName = Marshal.PtrToStringAnsi(eventNamePtr)!;
		
		byte[] byteArray = new byte[payloadSize];
		Marshal.Copy(eventPayload, byteArray, 0, (int)payloadSize);
		
		var args = MessagePackSerializer.Deserialize<object[]>(byteArray);
		
		Citizen.TriggerEvent(eventName, args);
	}
	
	private static void Tick(ulong currentSchedulerTime)
	{
		SynchronizationContext? previousContext = SynchronizationContext.Current;
        
		try
		{
			SynchronizationContext.SetSynchronizationContext(SyncContext);
			
			Scheduler.ExecutePendingTasks();
		}
		finally
		{
			SynchronizationContext.SetSynchronizationContext(previousContext);
		}
	}
	
	public static void Main(IResource resource, byte[] assemblyData)
	{
		Citizen.Resource = resource;

		if(resource.AssemblyLoadContext == null)
		{
			throw new Exception($"Resource {resource.Name} is not loaded.");
		}
		
		using var ms = new MemoryStream(assemblyData);
		Assembly assembly = resource.AssemblyLoadContext.LoadFromStream(ms);
		
		var resourceMain = assembly.GetTypes().FirstOrDefault(x => x.IsClass && typeof(CitizenResource).IsAssignableFrom(x));
		if (resourceMain == null)
		{
			throw new Exception($"Failed to find resource main class in assembly {assembly.FullName}.");
		}
		
		var resourceInstance = (CitizenResource?)Activator.CreateInstance(resourceMain);
		resource.CitizenResource = resourceInstance ?? throw new Exception($"Failed to create instance of resource main class {resourceMain.FullName}.");
		
		var options = MessagePackSerializerOptions.Standard.WithResolver(
			CompositeResolver.Create(
				CitizenMessagePackResolver.Instance,
				StandardResolver.Instance
			)
		);

		MessagePackSerializer.DefaultOptions = options;
		
		SynchronizationContext.SetSynchronizationContext(SyncContext);
		
		Library.SetTickHandler(resource.Runtime, Tick);
		Library.SetEventHandler(resource.Runtime, TriggerEvent);
		
		resourceInstance.OnStart();
	}
}
