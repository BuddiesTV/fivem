using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using CitizenFX.Shared;
using CitizenFX.Shared.Core;
using CitizenFX.Shared.Interfaces;
using MessagePack;

namespace CitizenFX.Wrapper.Core;

public static class Citizen
{
	private static readonly ConcurrentDictionary<string, FastEventHandlerList> EventHandlers = new();
	
	public static IResource Resource = null!;

	public static void Log(string message)
	{
		Library.Print($"script:{Resource.Name}", $"{message}\n");
	}
	
	public static void InvokeFunctionReference(string functionReference, object[] args)
	{
		var argsSerialized = MessagePackSerializer.Serialize<object[]>(args);
		
		Library.InvokeFunctionReference(Resource.Runtime, functionReference, argsSerialized);
	}
	
	internal static void TriggerEvent(string eventName, object[] args)
	{
		if (EventHandlers.TryGetValue(eventName, out var handlerList))
		{
			handlerList.TriggerHandlers(args);
		}
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void RegisterEvent(string eventName, Delegate handler)
	{
		Library.InvokeNative(0xD233A168, eventName);

		GetOrCreateHandlerList(eventName).AddHandler(handler);
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void RegisterEvent<T1>(string eventName, Action<T1> handler)
	{
		Library.InvokeNative(0xD233A168, eventName);
		
		GetOrCreateHandlerList(eventName).AddHandler(handler);
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void RegisterEvent<T1, T2>(string eventName, Action<T1, T2> handler)
	{
		Library.InvokeNative(0xD233A168, eventName);

		GetOrCreateHandlerList(eventName).AddHandler(handler);
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void RegisterEvent<T1, T2, T3>(string eventName, Action<T1, T2, T3> handler)
	{
		Library.InvokeNative(0xD233A168, eventName);

		GetOrCreateHandlerList(eventName).AddHandler(handler);
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void RegisterEvent<T1, T2, T3, T4>(string eventName, Action<T1, T2, T3, T4> handler)
	{
		Library.InvokeNative(0xD233A168, eventName);

		GetOrCreateHandlerList(eventName).AddHandler(handler);
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void RegisterEvent<T1, T2, T3, T4, T5>(string eventName, Action<T1, T2, T3, T4, T5> handler)
	{
		Library.InvokeNative(0xD233A168, eventName);

		GetOrCreateHandlerList(eventName).AddHandler(handler);
	}
	
	public static bool UnregisterEvent(string eventName, Delegate handler)
	{
		if (EventHandlers.TryGetValue(eventName, out var handlerList))
		{
			return handlerList.RemoveHandler(handler);
		}
		return false;
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static FastEventHandlerList GetOrCreateHandlerList(string eventName)
	{
		return EventHandlers.GetOrAdd(eventName, static _ => new FastEventHandlerList());
	}
}
