using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;

namespace CitizenFX.Shared.Core;

public static unsafe class Library
{
	private static bool IsInitialized { get; set; }
	private static readonly Dictionary<ulong, GCHandle> PinnedHandlers = new();
	private static readonly ConcurrentDictionary<ulong, IntPtr> NativeHandlers = new();
	
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate bool ReadAssemblyDelegate(
		IntPtr runtimePointer,
		string assemblyName,
		out IntPtr assemblyDataPtr,
		out int dataSize);
	
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate void PrintDelegate(
		string channel,
		string message);
	
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void RuntimeEventHandlerDelegate(IntPtr eventName, IntPtr eventPayload, uint payloadSize, IntPtr eventSource);
	
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void RuntimeTickHandlerDelegate(ulong currentSchedulerTime);
	
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate void SetEventHandlerDelegate(
		IntPtr runtimePointer,
		RuntimeEventHandlerDelegate eventHandler);
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	
	private delegate void SetTickHandlerDelegate(
		IntPtr runtimePointer,
		RuntimeTickHandlerDelegate tickHandler);
	
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate IntPtr GetNativeHandlerDelegate(ulong nativeHash);
	
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate void InvokeNativeDelegate(IntPtr nativeHandler, FxScriptContext* context, ulong nativeHash);
	
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate void InvokeFunctionReferenceDelegate(IntPtr runtimePointer, IntPtr refId,
		IntPtr argsSerialized, int argsSize);
	
	private static ReadAssemblyDelegate _readyAssemblyWrapperFn = null!;
	private static PrintDelegate _printFn = null!;
	private static SetEventHandlerDelegate _setEventHandlerFn = null!;
	private static SetTickHandlerDelegate _setTickHandlerFn = null!;
	private static GetNativeHandlerDelegate _getNativeHandlerFn = null!;
	private static InvokeNativeDelegate _invokeNativeFn = null!;
	private static InvokeFunctionReferenceDelegate _invokeFunctionReferenceFn = null!;
	
	[UnmanagedCallersOnly]
	private static void Initialize(IntPtr readyAssemblyWrapperFnPtr, IntPtr printFnPtr, IntPtr setEventHandlerFnPtr, IntPtr setTickHandlerFnPtr, IntPtr getNativeHandlerFnPtr, IntPtr invokeNativeFnPtr, IntPtr invokeFunctionReferenceFnPtr)
	{
		_readyAssemblyWrapperFn = Marshal.GetDelegateForFunctionPointer<ReadAssemblyDelegate>(readyAssemblyWrapperFnPtr);
		_printFn = Marshal.GetDelegateForFunctionPointer<PrintDelegate>(printFnPtr);
		_setEventHandlerFn = Marshal.GetDelegateForFunctionPointer<SetEventHandlerDelegate>(setEventHandlerFnPtr);
		_setTickHandlerFn = Marshal.GetDelegateForFunctionPointer<SetTickHandlerDelegate>(setTickHandlerFnPtr);
		_getNativeHandlerFn = Marshal.GetDelegateForFunctionPointer<GetNativeHandlerDelegate>(getNativeHandlerFnPtr);
		_invokeNativeFn = Marshal.GetDelegateForFunctionPointer<InvokeNativeDelegate>(invokeNativeFnPtr);
		_invokeFunctionReferenceFn = Marshal.GetDelegateForFunctionPointer<InvokeFunctionReferenceDelegate>(invokeFunctionReferenceFnPtr);
		
		IsInitialized = true;
	}
	
	private static IntPtr GetNativeHandler(ulong nativeHash)
	{
		if (!IsInitialized)
		{
			throw new InvalidOperationException("Library is not initialized.");
		}
    
		return NativeHandlers.GetOrAdd(nativeHash, hash =>
		{
			var handler = _getNativeHandlerFn(hash);
			if (handler == IntPtr.Zero)
			{
				throw new Exception($"Failed to get native handler for hash {hash}.");
			}

			var gcHandle = GCHandle.Alloc(handler, GCHandleType.Pinned);
			PinnedHandlers[hash] = gcHandle;
        
			return handler;
		});
	}

	public static byte[] ReadAssemblyRaw(IntPtr runtimePointer, string assemblyName) 
	{
		if (!IsInitialized)
		{
			throw new InvalidOperationException("Library is not initialized.");
		}
		
		bool success = _readyAssemblyWrapperFn(runtimePointer, assemblyName, out IntPtr assemblyDataPtr,
			out int dataSize);

		if (!success)
		{
			throw new Exception($"Failed to read assembly {assemblyName}.");
		}

		byte[] assemblyData = new byte[dataSize];
		Marshal.Copy(assemblyDataPtr, assemblyData, 0, dataSize);

		return assemblyData;
	}
	
	public static void Print(string channel, string message)
	{
		if (!IsInitialized)
		{
			throw new InvalidOperationException("Library is not initialized.");
		}
		
		_printFn(channel, message);
	}
	
	public static void SetEventHandler(IntPtr runtimePointer, RuntimeEventHandlerDelegate eventHandler)
	{
		if (!IsInitialized)
		{
			throw new InvalidOperationException("Library is not initialized.");
		}
		
		_setEventHandlerFn(runtimePointer, eventHandler);
	}
	
	public static void SetTickHandler(IntPtr runtimePointer, RuntimeTickHandlerDelegate tickHandler)
	{
		if (!IsInitialized)
		{
			throw new InvalidOperationException("Library is not initialized.");
		}
		
		_setTickHandlerFn(runtimePointer, tickHandler);
	}

public static void InvokeNative(ulong nativeHash, params object[] args)
{
    if (!IsInitialized)
    {
        throw new InvalidOperationException("Library is not initialized.");
    }

    var nativeHandler = GetNativeHandler(nativeHash);
    if (nativeHandler == IntPtr.Zero)
    {
        throw new Exception($"Failed to get native handler for hash {nativeHash}.");
    }
    
    var scriptContext = new FxScriptContext
    {
        numArguments = args.Length,
        numResults = 0
    };
    
    byte* ptr = scriptContext.initialArguments;
    scriptContext.functionDataPtr = (IntPtr)ptr;
    scriptContext.retDataPtr = (IntPtr)ptr;
    
    List<IntPtr> allocatedPointers = new List<IntPtr>();
    
    try
    {
        for (int i = 0; i < args.Length; i++)
        {
            byte* slotPtr = ptr + i * 8;
            if (args[i] is string strValue)
            {
                IntPtr strPtr = Marshal.StringToHGlobalAnsi(strValue);
                allocatedPointers.Add(strPtr);
                *(IntPtr*)slotPtr = strPtr;
            }
            else if (args[i] is ulong ulongValue)
            {
                *(ulong*)slotPtr = ulongValue;
            }
            else if (args[i] is int intValue)
            {
                *(int*)slotPtr = intValue;
            }
            else if (args[i] is float floatValue)
            {
                *(float*)slotPtr = floatValue;
            }
            else if (args[i] is double doubleValue)
            {
                *(double*)slotPtr = doubleValue;
            }
            else
            {
                throw new NotSupportedException($"Unsupported argument type at index {i}: {args[i].GetType()}");
            }
        }
        
        _invokeNativeFn(nativeHandler, &scriptContext, nativeHash);
    }
    finally
    {
        foreach (IntPtr pointer in allocatedPointers)
        {
            Marshal.FreeHGlobal(pointer);
        }
    }
}

	public static void InvokeFunctionReference(IntPtr runtimePointer, string functionReference, byte[] argsSerialized) 
	{
		if (!IsInitialized)
		{
			throw new InvalidOperationException("Library is not initialized.");
		}
		
		var refIdBytes = Encoding.UTF8.GetBytes(functionReference);

		fixed (byte* refIdBytesPtr = refIdBytes)
		fixed (byte* argsSerializedPtr = argsSerialized)
		{
			_invokeFunctionReferenceFn(runtimePointer,
				new IntPtr(refIdBytesPtr),
				new IntPtr(argsSerializedPtr),
				argsSerialized.Length);
		}
	}
}
