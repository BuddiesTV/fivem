using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using CitizenFX.Shared.Core;
using CitizenFX.Shared.Interfaces;
using MessagePack;

namespace CitizenFX.Wrapper.Core;

public static class Citizen
{
	// ToDo: Check this out, somehow ConcurrentDictionary has a very high performance-overhead for first TryGetValue
	private static readonly ConcurrentDictionary<string, FastEventHandlerList> EventHandlers = new();

	public static IResource Resource = null!;

	public static void Log(string message)
	{
		Library.Print($"script:{Resource.Name}", $"{message}\n");
	}

	public static void InvokeFunctionReference(string functionReference, object[] args)
	{
		var argsSerialized = MessagePackSerializer.Serialize(args);

		Library.InvokeFunctionReference(Resource.Runtime, functionReference, argsSerialized);
	}

	internal static void TriggerEvent(string eventName, object[] args)
	{
		if (EventHandlers.TryGetValue(eventName, out var handlerList)) handlerList.TriggerHandlers(args);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void RegisterEvent(string eventName, Delegate handler)
	{
		Library.InvokeNative(0xD233A168, eventName);

		GetOrCreateHandlerList(eventName).AddHandler(handler);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static FastEventHandlerList GetOrCreateHandlerList(string eventName)
	{
		return EventHandlers.GetOrAdd(eventName, _ => new FastEventHandlerList());
	}
}
