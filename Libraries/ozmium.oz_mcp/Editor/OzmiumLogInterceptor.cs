using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Editor;
using Sandbox;

namespace SboxMcpServer;

[SkipHotload]
internal static class OzmiumLogInterceptor
{
	private const int MaxQueuedLogs = 4096;
	private const int MaxMessageLength = 8192;

	[SkipHotload]
	private static readonly ConcurrentQueue<string> Lines = new();

	[SkipHotload]
	private static readonly Action<LogEvent> Logger = OnLog;

	private static int _installed;
	private static int _installRequested;
	private static int _queueCount;
	private static int _insideLogger;

	public static void EnsureInstalled()
	{
		Interlocked.Exchange( ref _installRequested, 1 );
	}

	public static IReadOnlyList<string> PeekRecent( int maxCount = 50 )
	{
		EnsureInstalled();

		if ( maxCount <= 0 )
			maxCount = 50;

		maxCount = Math.Clamp( maxCount, 1, 1000 );

		var snapshot = Lines.ToArray();

		if ( snapshot.Length <= maxCount )
			return snapshot;

		var result = new List<string>( maxCount );
		var start = snapshot.Length - maxCount;

		for ( var i = start; i < snapshot.Length; i++ )
			result.Add( snapshot[i] );

		return result;
	}

	public static IReadOnlyList<string> Drain( int maxCount = 256 )
	{
		EnsureInstalled();

		if ( maxCount <= 0 )
			maxCount = 256;

		maxCount = Math.Clamp( maxCount, 1, 4096 );

		var result = new List<string>( maxCount );

		while ( result.Count < maxCount && Lines.TryDequeue( out var line ) )
		{
			Interlocked.Decrement( ref _queueCount );
			result.Add( line );
		}

		if ( Interlocked.CompareExchange( ref _queueCount, 0, 0 ) < 0 )
			Interlocked.Exchange( ref _queueCount, 0 );

		return result;
	}

	public static void Clear()
	{
		while ( Lines.TryDequeue( out _ ) )
		{
		}

		Interlocked.Exchange( ref _queueCount, 0 );
	}

	public static void Shutdown()
	{
		Interlocked.Exchange( ref _installRequested, 0 );

		if ( Interlocked.Exchange( ref _installed, 0 ) == 0 )
			return;

		try
		{
			EditorUtility.RemoveLogger( Logger );
		}
		catch
		{
			// Never log from the log interceptor.
		}
	}

	[EditorEvent.Frame]
	private static void OnEditorFrame()
	{
		if ( Interlocked.Exchange( ref _installRequested, 0 ) == 1 )
		{
			InstallOnMainThread();
			return;
		}

		if ( Interlocked.CompareExchange( ref _installed, 0, 0 ) == 0 )
			InstallOnMainThread();
	}

	private static void InstallOnMainThread()
	{
		if ( Interlocked.CompareExchange( ref _installed, 1, 0 ) != 0 )
			return;

		try
		{
			// Remove first so a hotload never leaves duplicate subscriptions
			// for the same static delegate.
			try
			{
				EditorUtility.RemoveLogger( Logger );
			}
			catch
			{
			}

			EditorUtility.AddLogger( Logger );
		}
		catch
		{
			Interlocked.Exchange( ref _installed, 0 );
		}
	}

	private static void OnLog( LogEvent logEvent )
	{
		// The logger callback must be tiny and must never call Log.*.
		if ( Interlocked.Exchange( ref _insideLogger, 1 ) == 1 )
			return;

		try
		{
			var line = Format( logEvent );

			if ( string.IsNullOrWhiteSpace( line ) )
				return;

			Lines.Enqueue( line );

			var count = Interlocked.Increment( ref _queueCount );

			while ( count > MaxQueuedLogs && Lines.TryDequeue( out _ ) )
				count = Interlocked.Decrement( ref _queueCount );
		}
		catch
		{
			// Never log from the log interceptor.
		}
		finally
		{
			Interlocked.Exchange( ref _insideLogger, 0 );
		}
	}

	private static string Format( LogEvent logEvent )
	{
		var level = SafeToString( logEvent.Level );
		var logger = SafeToString( logEvent.Logger );
		var message = SafeToString( logEvent.Message );

		if ( string.IsNullOrWhiteSpace( message ) && logEvent.Exception != null )
			message = SafeToString( logEvent.Exception.Message );

		message = Trim( message );

		if ( string.IsNullOrWhiteSpace( level ) )
			level = "Log";

		if ( string.IsNullOrWhiteSpace( logger ) )
			return $"[{level}] {message}";

		return $"[{level}] [{logger}] {message}";
	}

	private static string SafeToString( object value )
	{
		try
		{
			if ( value == null )
				return string.Empty;

			return value.ToString() ?? string.Empty;
		}
		catch
		{
			return string.Empty;
		}
	}

	private static string Trim( string value )
	{
		if ( string.IsNullOrEmpty( value ) )
			return string.Empty;

		value = value.Replace( "\r", "\\r" ).Replace( "\n", "\\n" );

		if ( value.Length <= MaxMessageLength )
			return value;

		return value.Substring( 0, MaxMessageLength ) + "...";
	}
}
