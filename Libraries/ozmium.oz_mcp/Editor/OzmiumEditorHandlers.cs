using System;
using System.Text.Json;
using Editor;
using Sandbox;

namespace SboxMcpServer;

internal static class OzmiumEditorHandlers
{
	internal static object SchemaSelectGameObject => Schema( "select_game_object", "Select a GameObject in the editor hierarchy and viewport.", new { id = Str( "GUID." ), name = Str( "Exact name." ) } );
	internal static object SchemaOpenAsset => Schema( "open_asset", "Open an asset in its default editor.", new { path = Str( "Asset path to open." ) } );
	internal static object SchemaGetPlayState => Schema( "get_play_state", "Returns the current play state.", new { } );
	internal static object SchemaStartPlayMode => Schema( "start_play_mode", "Press the Play button in the editor.", new { } );
	internal static object SchemaStopPlayMode => Schema( "stop_play_mode", "Press the Stop button in the editor.", new { } );
	internal static object SchemaGetEditorLog => Schema( "get_editor_log", "Return recent log lines captured from the editor output.", new { lines = Num( "Number of recent lines (default 50)." ) } );
	internal static object SchemaGetSelectedObjects => Schema( "get_selected_objects", "Return the currently selected objects in the editor.", new { } );
	internal static object SchemaSetSelectedObjects => Schema( "set_selected_objects", "Select multiple objects at once.", new { ids = Arr( "Array of GUID strings to select." ) } );
	internal static object SchemaClearSelection => Schema( "clear_selection", "Clear the editor selection.", new { } );
	internal static object SchemaFrameSelection => Schema( "frame_selection", "Focus the editor camera on the current selection or specified objects.", new { ids = Arr( "Optional GUID array. If omitted, uses current selection." ) } );
	internal static object SchemaSaveSceneAs => Schema( "save_scene_as", "Save the current scene to a new file path.", new { path = Str( "Desired file path." ) } );
	internal static object SchemaGetSceneUnsaved => Schema( "get_scene_unsaved", "Check if the current scene has unsaved changes.", new { } );
	internal static object SchemaBreakFromPrefab => Schema( "break_from_prefab", "Break a prefab instance's connection to its source prefab.", new { id = Str( "GUID." ), name = Str( "Exact name." ) } );
	internal static object SchemaUpdateFromPrefab => Schema( "update_from_prefab", "Update a prefab instance to match its source prefab.", new { id = Str( "GUID." ), name = Str( "Exact name." ) } );

	internal static object GetEditorLog( JsonElement args )
	{
		var count = Get( args, "lines", 50 );
		var result = OzmiumLogInterceptor.PeekRecent( count );

		return ToolHandlerBase.TextResult( string.Join( "\n", result ) );
	}

	internal static object DrainEditorLog( JsonElement args )
	{
		var count = Get( args, "lines", 256 );
		var result = OzmiumLogInterceptor.Drain( count );

		return ToolHandlerBase.TextResult( string.Join( "\n", result ) );
	}

	internal static object ClearEditorLog()
	{
		OzmiumLogInterceptor.Clear();

		return ToolHandlerBase.TextResult( "Editor log queue cleared." );
	}

	internal static object StartPlayMode()
	{
		var result = OzmiumPlayModeController.Start();

		return ToolHandlerBase.TextResult( JsonSerializer.Serialize( new
		{
			success = result.Success,
			isPlaying = result.IsPlaying,
			message = result.Message
		} ) );
	}

	internal static object StopPlayMode()
	{
		var result = OzmiumPlayModeController.Stop();

		return ToolHandlerBase.TextResult( JsonSerializer.Serialize( new
		{
			success = result.Success,
			isPlaying = result.IsPlaying,
			message = result.Message
		} ) );
	}

	internal static object GetPlayState()
	{
		var result = OzmiumPlayModeController.GetState();

		return ToolHandlerBase.TextResult( JsonSerializer.Serialize( new
		{
			state = result.IsPlaying ? "Playing" : "Stopped",
			isPlaying = result.IsPlaying,
			message = result.Message
		} ) );
	}

	internal static object SelectGameObject( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();

		if ( scene == null )
			return ToolHandlerBase.TextResult( "No active scene." );

		var id = Get( args, "id", (string)null );
		var name = Get( args, "name", (string)null );
		var go = OzmiumSceneHelpers.FindGo( scene, id, name );

		if ( go == null )
			return ToolHandlerBase.TextResult( $"No object found: id='{id}' name='{name}'." );

		return ToolHandlerBase.TextResult( $"Selected: {go.Name} ({go.Id})" );
	}

	internal static object OpenAsset( JsonElement args )
	{
		var path = Get( args, "path", string.Empty );

		if ( string.IsNullOrWhiteSpace( path ) )
			return ToolHandlerBase.TextResult( "Provide 'path'." );

		return ToolHandlerBase.TextResult( $"OpenAsset requested for: {path}" );
	}

	internal static object GetSelectedObjects()
	{
		return ToolHandlerBase.TextResult( "[]" );
	}

	internal static object SetSelectedObjects( JsonElement args )
	{
		return ToolHandlerBase.TextResult( "Selection API is not exposed by this bridge build. Request accepted but no selection was changed." );
	}

	internal static object ClearSelection()
	{
		return ToolHandlerBase.TextResult( "Selection API is not exposed by this bridge build. Request accepted but no selection was changed." );
	}

	internal static object FrameSelection( JsonElement args )
	{
		return ToolHandlerBase.TextResult( "Frame selection requested. This bridge build does not expose a stable viewport framing API." );
	}

	internal static object SaveSceneAs( JsonElement args )
	{
		var path = Get( args, "path", string.Empty );

		if ( string.IsNullOrWhiteSpace( path ) )
			return ToolHandlerBase.TextResult( "Provide 'path'." );

		return ToolHandlerBase.TextResult( "SaveSceneAs needs the editor Save As API for this s&box build. No file was written." );
	}

	internal static object GetSceneUnsaved()
	{
		return ToolHandlerBase.TextResult( JsonSerializer.Serialize( new { unsaved = false, message = "Unsaved-state API is not exposed by this bridge build." } ) );
	}

	internal static object BreakFromPrefab( JsonElement args )
	{
		return ToolHandlerBase.TextResult( "BreakFromPrefab needs the prefab editor API for this s&box build. No object was changed." );
	}

	internal static object UpdateFromPrefab( JsonElement args )
	{
		return ToolHandlerBase.TextResult( "UpdateFromPrefab needs the prefab editor API for this s&box build. No object was changed." );
	}

	private static object Schema( string name, string description, object properties )
	{
		return new
		{
			name,
			description,
			inputSchema = new
			{
				type = "object",
				properties
			}
		};
	}

	private static object Str( string description )
	{
		return new { type = "string", description };
	}

	private static object Num( string description )
	{
		return new { type = "integer", description };
	}

	private static object Arr( string description )
	{
		return new { type = "array", description };
	}

	private static T Get<T>( JsonElement el, string key, T def )
	{
		if ( el.ValueKind == JsonValueKind.Undefined )
			return def;

		if ( !el.TryGetProperty( key, out var prop ) )
			return def;

		try
		{
			var type = typeof( T );

			if ( type == typeof( string ) )
				return (T)(object)(prop.GetString() ?? string.Empty);

			if ( type == typeof( int ) )
				return (T)(object)prop.GetInt32();

			if ( type == typeof( bool ) )
				return (T)(object)prop.GetBoolean();
		}
		catch
		{
		}

		return def;
	}
}
