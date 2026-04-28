using Sandbox;
using System.Collections.Generic;

/// <summary>
/// Manages lobby creation, player connections, and unit spawning.
/// Place on a single GameObject in the scene. Configure PlayerPrefab and SpawnPoints in the inspector.
/// </summary>
public sealed class GameNetworkManager : Component, Component.INetworkListener
{
	// ── Lobby ──────────────────────────────────────────────

	[Property, Group( "Lobby" ), Range( 1, 32 )]
	public int MaxPlayers { get; set; } = 20;

	[Property, Group( "Lobby" )]
	public LobbyPrivacy Privacy { get; set; } = LobbyPrivacy.Public;

	[Property, Group( "Lobby" )]
	public string LobbyName { get; set; } = "TD Game";

	[Property, Group( "Lobby" ), Description( "Automatically create a lobby when the scene starts" )]
	public bool AutoCreateLobby { get; set; } = true;

	// ── Spawning ──────────────────────────────────────────

	[Property, Group( "Spawning" )]
	public GameObject PlayerPrefab { get; set; }

	[Property, Group( "Spawning" ), Description( "Spawn points cycled round-robin. If empty, spawns at this object's position." )]
	public List<GameObject> SpawnPoints { get; set; } = new();

	[Property, Group( "Spawning" ), Range( 0f, 500f ), Description( "Random offset applied to spawn position" )]
	public float SpawnRandomRadius { get; set; } = 50f;

	// ── Runtime state ─────────────────────────────────────

	private int _nextSpawnIndex;

	protected override void OnStart()
	{
		if ( AutoCreateLobby && !Networking.IsActive )
		{
			CreateLobby();
		}
	}

	public async void CreateLobby()
	{
		await Networking.CreateLobby( new LobbyConfig
		{
			MaxPlayers = MaxPlayers,
			Privacy = Privacy,
			Name = LobbyName
		} );
	}

	public void OnActive( Connection connection )
	{
		if ( PlayerPrefab is null )
		{
			Log.Warning( "GameNetworkManager: PlayerPrefab is not assigned!" );
			return;
		}

		var spawnPos = GetNextSpawnPosition();
		var player = PlayerPrefab.Clone( new Transform( spawnPos ) );
		player.NetworkSpawn( connection );
	}

	public void OnDisconnected( Connection connection )
	{
		Log.Info( $"Player disconnected: {connection.DisplayName}" );
	}

	private Vector3 GetNextSpawnPosition()
	{
		Vector3 basePosition;

		if ( SpawnPoints.Count > 0 )
		{
			var point = SpawnPoints[_nextSpawnIndex % SpawnPoints.Count];
			basePosition = point.IsValid() ? point.WorldPosition : WorldPosition;
			_nextSpawnIndex++;
		}
		else
		{
			basePosition = WorldPosition;
		}

		if ( SpawnRandomRadius > 0f )
		{
			var offset = new Vector3(
				Game.Random.Float( -SpawnRandomRadius, SpawnRandomRadius ),
				Game.Random.Float( -SpawnRandomRadius, SpawnRandomRadius ),
				0f
			);
			basePosition += offset;
		}

		return basePosition;
	}

	protected override void OnValidate()
	{
		MaxPlayers = MaxPlayers.Clamp( 1, 32 );
		SpawnRandomRadius = SpawnRandomRadius.Clamp( 0f, 500f );
	}
}
