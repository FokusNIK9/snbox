using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Editor;

namespace Ozmium.OzMcp.Editor;

internal static class OzmiumEditorMainThread
{
	private sealed class WorkItem
	{
		public Func<object> Callback { get; set; }
		public TaskCompletionSource<object> Completion { get; set; }
	}

	private static readonly ConcurrentQueue<WorkItem> Queue = new();
	private static int _mainThreadId;
	private static int _hasQueuedWork;

	public static bool IsMainThread => _mainThreadId != 0 && Environment.CurrentManagedThreadId == _mainThreadId;

	public static Task<T> InvokeAsync<T>( Func<T> callback )
	{
		if ( callback == null )
			throw new ArgumentNullException( nameof( callback ) );

		if ( IsMainThread )
		{
			try
			{
				return Task.FromResult( callback() );
			}
			catch ( Exception exception )
			{
				return Task.FromException<T>( exception );
			}
		}

		var completion = new TaskCompletionSource<object>( TaskCreationOptions.RunContinuationsAsynchronously );

		Queue.Enqueue( new WorkItem
		{
			Callback = () => callback(),
			Completion = completion
		} );

		Interlocked.Exchange( ref _hasQueuedWork, 1 );

		return AwaitTyped<T>( completion.Task );
	}

	public static Task InvokeAsync( Action callback )
	{
		if ( callback == null )
			throw new ArgumentNullException( nameof( callback ) );

		return InvokeAsync<object>( () =>
		{
			callback();
			return null;
		} );
	}

	private static async Task<T> AwaitTyped<T>( Task<object> task )
	{
		var result = await task.ConfigureAwait( false );

		if ( result is T typed )
			return typed;

		return default;
	}

	[EditorEvent.Frame]
	private static void OnEditorFrame()
	{
		_mainThreadId = Environment.CurrentManagedThreadId;

		if ( Interlocked.Exchange( ref _hasQueuedWork, 0 ) == 0 )
			return;

		var processed = 0;

		while ( processed < 128 && Queue.TryDequeue( out var item ) )
		{
			processed++;

			try
			{
				item.Completion.TrySetResult( item.Callback() );
			}
			catch ( Exception exception )
			{
				item.Completion.TrySetException( exception );
			}
		}

		if ( !Queue.IsEmpty )
			Interlocked.Exchange( ref _hasQueuedWork, 1 );
	}
}
