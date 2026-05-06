using System;
using System.Reflection;
using Editor;
using Sandbox;

namespace SboxMcpServer;

public static class BoxCollectorMenu
{
	[Menu( "Box Collector", "My Menu Option " )]
	public static void MyMenuOption()
	{
		var message = "[BOX COLLECTOR MENU] hi";

		Log.Info( message );
		WriteToMcpActivityLog( message );
	}

	private static void WriteToMcpActivityLog( string message )
	{
		try
		{
			var field = typeof( McpServer ).GetField( "OnLogMessage", BindingFlags.Static | BindingFlags.NonPublic );
			var handler = field?.GetValue( null ) as Action<string>;

			handler?.Invoke( message );
		}
		catch ( Exception exception )
		{
			Log.Warning( $"[BOX COLLECTOR MENU] Could not write to MCP Activity Log: {exception.Message}" );
		}
	}
}
