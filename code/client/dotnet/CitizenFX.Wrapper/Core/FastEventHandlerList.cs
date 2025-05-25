using System.Runtime.CompilerServices;

namespace CitizenFX.Wrapper.Core;

internal class FastEventHandlerList
{
	private volatile Delegate[] _handlers = new Delegate[4];
	private volatile int _count;
	private readonly Lock _lock = new Lock();
	
	public int Count => _count;
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void AddHandler(Delegate handler)
	{
		lock (_lock)
		{
			if (_count >= _handlers.Length)
			{
				var newHandlers = new Delegate[_handlers.Length * 2];
				Array.Copy(_handlers, newHandlers, _count);
				_handlers = newHandlers;
			}

			_handlers[_count] = handler;
			
			Interlocked.Increment(ref _count);
		}
	}

	public bool RemoveHandler(Delegate handler)
	{
		lock (_lock)
		{
			for (int i = 0; i < _count; i++)
			{
				if (ReferenceEquals(_handlers[i], handler))
				{
					for (int j = i; j < _count - 1; j++)
					{
						_handlers[j] = _handlers[j + 1];
					}
					_handlers[_count - 1] = null!;
					
					Interlocked.Decrement(ref _count);
					return true;
				}
			}
		}
		return false;
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void TriggerHandlers(object[] args)
	{
		var handlers = _handlers;
		var count = _count;

		for (int i = 0; i < count; i++)
		{
			var handler = handlers[i];
			handler.DynamicInvoke(args);
		}
	}
}
