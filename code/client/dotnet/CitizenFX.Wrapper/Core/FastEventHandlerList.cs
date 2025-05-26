using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace CitizenFX.Wrapper.Core;

internal class FastEventHandlerList(int initialCapacity = 4)
{
	private Delegate[] _handlers = new Delegate[initialCapacity];
	private int _count;
	private readonly ReaderWriterLockSlim _lock = new();
	private static readonly ConcurrentDictionary<Type, Action<Delegate, object[]>> InvokerCache = new();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void AddHandler(Delegate handler)
	{
		if (handler is null) throw new ArgumentNullException(nameof(handler));
		_lock.EnterWriteLock();
		try
		{
			if (_count >= _handlers.Length)
			{
				var newHandlers = new Delegate[_handlers.Length * 2];
				Array.Copy(_handlers, newHandlers, _count);
				_handlers = newHandlers;
			}
			_handlers[_count] = handler;
			_count++;
		}
		finally { _lock.ExitWriteLock(); }
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool RemoveHandler(Delegate handler)
	{
		if (handler is null) throw new ArgumentNullException(nameof(handler));
		_lock.EnterWriteLock();
		try
		{
			for (var i = 0; i < _count; i++)
			{
				if (ReferenceEquals(_handlers[i], handler))
				{
					for (var j = i; j < _count - 1; j++)
						_handlers[j] = _handlers[j + 1];
					_handlers[_count - 1] = null!;
					_count--;
					return true;
				}
			}
		}
		finally { _lock.ExitWriteLock(); }
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void TriggerHandlers(object[] args)
	{
		if (args is null) throw new ArgumentNullException(nameof(args));
		Delegate[] snapshot;
		int count;
		
		_lock.EnterReadLock();
		try
		{
			count = _count;
			snapshot = new Delegate[count];
			Array.Copy(_handlers, snapshot, count);
		}
		finally { _lock.ExitReadLock(); }

		for (var i = 0; i < count; i++)
		{
			var handler = snapshot[i];
			var invoker = GetOrCreateInvoker(handler);
			invoker(handler, args);
		}
	}
	
	private static Action<Delegate, object[]> GetOrCreateInvoker(Delegate handler)
	{
		var delegateType = handler.GetType();
		return InvokerCache.GetOrAdd(delegateType, CreateInvoker);
	}
	
	private static Action<Delegate, object[]> CreateInvoker(Type delegateType)
	{
		var invokeMethod = delegateType.GetMethod("Invoke")!;
		var parameters = invokeMethod.GetParameters();

		var delParam = Expression.Parameter(typeof(Delegate), "del");
		var argsParam = Expression.Parameter(typeof(object[]), "args");
		var castedDel = Expression.Convert(delParam, delegateType);
		var callArgs = new Expression[parameters.Length];
		for (int i = 0; i < parameters.Length; i++)
		{
			var arg = Expression.ArrayIndex(argsParam, Expression.Constant(i));
			callArgs[i] = Expression.Convert(arg, parameters[i].ParameterType);
		}
		var invokeExpr = Expression.Call(castedDel, invokeMethod, callArgs);

		var lambda = Expression.Lambda<Action<Delegate, object[]>>(invokeExpr, delParam, argsParam);
		return lambda.Compile();
	}
}
