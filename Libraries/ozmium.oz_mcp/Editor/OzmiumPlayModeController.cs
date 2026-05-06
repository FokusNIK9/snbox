using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Editor;

namespace SboxMcpServer;

internal sealed class OzmiumPlayModeResult
{
	public bool Success { get; init; }
	public bool IsPlaying { get; init; }
	public string Message { get; init; }
}

internal static class OzmiumPlayModeController
{
	public static OzmiumPlayModeResult Start()
	{
		var isPlaying = IsPlaying();

		if ( isPlaying )
			return MakeResult( true, true, "Play Mode is already running." );

		var click = OzmiumNativePlayButton.ClickToolbarPlayButton();

		return MakeResult( click.Success, false, click.Message );
	}

	public static OzmiumPlayModeResult Stop()
	{
		var isPlaying = IsPlaying();

		if ( !isPlaying )
			return MakeResult( true, false, "Play Mode is already stopped." );

		var click = OzmiumNativePlayButton.ClickToolbarPlayButton();

		return MakeResult( click.Success, true, click.Message );
	}

	public static OzmiumPlayModeResult GetState()
	{
		var isPlaying = IsPlaying();
		return MakeResult( true, isPlaying, isPlaying ? "Playing." : "Stopped." );
	}

	private static bool IsPlaying()
	{
		var session = SceneEditorSession.Active;
		return session != null && session.IsPlaying;
	}

	private static OzmiumPlayModeResult MakeResult( bool success, bool isPlaying, string message )
	{
		return new OzmiumPlayModeResult
		{
			Success = success,
			IsPlaying = isPlaying,
			Message = message == null ? string.Empty : message
		};
	}
}

internal sealed class OzmiumNativeClickResult
{
	public bool Success { get; init; }
	public string Message { get; init; }
}

internal static class OzmiumNativePlayButton
{
	private const int SW_RESTORE = 9;
	private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
	private const uint MOUSEEVENTF_LEFTUP = 0x0004;

	public static OzmiumNativeClickResult ClickToolbarPlayButton()
	{
		try
		{
			var hWnd = FindBestWindow();

			if ( hWnd == IntPtr.Zero )
				return MakeResult( false, "Could not find a visible s&box editor window to click Play/Stop." );

			var title = GetTitle( hWnd );

			if ( !GetWindowRect( hWnd, out var rect ) )
				return MakeResult( false, $"Could not get window rect for: {title}" );

			if ( rect.Right <= rect.Left || rect.Bottom <= rect.Top )
				return MakeResult( false, $"Invalid window rect for: {title}" );

			var x = rect.Left + ((rect.Right - rect.Left) / 2) - 30;
			var y = rect.Top - 35;

			ShowWindow( hWnd, SW_RESTORE );
			SetForegroundWindow( hWnd );
			SetFocus( hWnd );
			Thread.Sleep( 300 ); // Увеличил задержку для фокуса

			SetCursorPos( x, y );
			Thread.Sleep( 100 );

			mouse_event( MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero );
			Thread.Sleep( 50 );
			mouse_event( MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero );

			return MakeResult( true, $"Clicked toolbar Play/Stop at x={x}, y={y} for window: {title}. Verify with get_play_state after 1-2 seconds." );
		}
		catch ( Exception exception )
		{
			return MakeResult( false, $"Toolbar Play/Stop click failed: {exception.GetType().Name}: {exception.Message}" );
		}
	}

	private static IntPtr FindBestWindow()
	{
		var currentProcessId = Process.GetCurrentProcess().Id;
		var best = IntPtr.Zero;
		var firstVisible = IntPtr.Zero;

		EnumWindows( ( hWnd, lParam ) =>
		{
			GetWindowThreadProcessId( hWnd, out var windowProcessId );

			if ( windowProcessId != currentProcessId )
				return true;

			if ( !IsWindowVisible( hWnd ) )
				return true;

			var title = GetTitle( hWnd );

			if ( string.IsNullOrWhiteSpace( title ) )
				return true;

			if ( firstVisible == IntPtr.Zero )
				firstVisible = hWnd;

			if ( title.IndexOf( "s&box", StringComparison.OrdinalIgnoreCase ) >= 0 ||
				title.IndexOf( "sbox", StringComparison.OrdinalIgnoreCase ) >= 0 ||
				title.IndexOf( "sbox-dev", StringComparison.OrdinalIgnoreCase ) >= 0 ||
				title.IndexOf( "Box Collector", StringComparison.OrdinalIgnoreCase ) >= 0 ||
				title.IndexOf( "Start Fresh", StringComparison.OrdinalIgnoreCase ) >= 0 ||
				title.IndexOf( "Minimal", StringComparison.OrdinalIgnoreCase ) >= 0 )
			{
				best = hWnd;
				return false;
			}

			return true;
		}, IntPtr.Zero );

		return best != IntPtr.Zero ? best : firstVisible;
	}

	private static OzmiumNativeClickResult MakeResult( bool success, string message )
	{
		return new OzmiumNativeClickResult
		{
			Success = success,
			Message = message == null ? string.Empty : message
		};
	}

	private static string GetTitle( IntPtr hWnd )
	{
		var length = GetWindowTextLength( hWnd );

		if ( length <= 0 )
			return string.Empty;

		var builder = new StringBuilder( length + 1 );
		GetWindowText( hWnd, builder, builder.Capacity );
		return builder.ToString();
	}

	private delegate bool EnumWindowsProc( IntPtr hWnd, IntPtr lParam );

	[DllImport( "user32.dll" )]
	private static extern bool EnumWindows( EnumWindowsProc enumProc, IntPtr lParam );

	[DllImport( "user32.dll" )]
	private static extern bool IsWindowVisible( IntPtr hWnd );

	[DllImport( "user32.dll", CharSet = CharSet.Unicode )]
	private static extern int GetWindowText( IntPtr hWnd, StringBuilder text, int maxCount );

	[DllImport( "user32.dll", CharSet = CharSet.Unicode )]
	private static extern int GetWindowTextLength( IntPtr hWnd );

	[DllImport( "user32.dll" )]
	private static extern uint GetWindowThreadProcessId( IntPtr hWnd, out int processId );

	[DllImport( "user32.dll" )]
	private static extern bool GetWindowRect( IntPtr hWnd, out RECT rect );

	[DllImport( "user32.dll" )]
	private static extern bool SetForegroundWindow( IntPtr hWnd );

	[DllImport( "user32.dll" )]
	private static extern IntPtr SetFocus( IntPtr hWnd );

	[DllImport( "user32.dll" )]
	private static extern bool ShowWindow( IntPtr hWnd, int nCmdShow );

	[DllImport( "user32.dll" )]
	private static extern bool SetCursorPos( int x, int y );

	[DllImport( "user32.dll" )]
	private static extern void mouse_event( uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo );

	[StructLayout( LayoutKind.Sequential )]
	private struct RECT
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}
}
