using Sandbox;

/// <summary>
/// Synchronizes animation parameters across the network based on unit state.
/// Reads movement data from CharacterController and pushes it to SkinnedModelRenderer.
/// Does not depend on PlayerUnitController directly — works from CharacterController velocity.
/// </summary>
public sealed class UnitAnimSync : Component
{
	// ── Configuration ─────────────────────────────────────

	[Property, Group( "Animation" ), Description( "SkinnedModelRenderer to drive. Auto-found if not set." )]
	public SkinnedModelRenderer TargetRenderer { get; set; }

	[Property, Group( "Parameters" ), Description( "Anim parameter name for movement speed (float)" )]
	public string SpeedParam { get; set; } = "move_speed";

	[Property, Group( "Parameters" ), Description( "Anim parameter name for grounded state (bool)" )]
	public string GroundedParam { get; set; } = "b_grounded";

	[Property, Group( "Parameters" ), Description( "Anim parameter name for alive state (bool)" )]
	public string AliveParam { get; set; } = "b_alive";

	[Property, Group( "Parameters" ), Description( "Anim parameter name for movement direction (Vector3)" )]
	public string WishDirParam { get; set; } = "wish_direction";

	// ── Network state ─────────────────────────────────────

	[Sync( SyncFlags.Interpolate )] public float SyncedSpeed { get; set; }
	[Sync] public bool SyncedGrounded { get; set; } = true;
	[Sync] public bool SyncedAlive { get; set; } = true;
	[Sync] public Vector3 SyncedWishDir { get; set; }

	// ── Internal ──────────────────────────────────────────

	private CharacterController _cc;
	private UnitHealth _health;

	protected override void OnAwake()
	{
		_cc = GetComponent<CharacterController>();
		_health = GetComponent<UnitHealth>();

		if ( TargetRenderer is null )
		{
			TargetRenderer = GetComponentInChildren<SkinnedModelRenderer>();
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;

		if ( _cc is not null )
		{
			SyncedSpeed = _cc.Velocity.WithZ( 0 ).Length;
			SyncedGrounded = _cc.IsOnGround;
		}

		if ( _health is not null )
		{
			SyncedAlive = _health.IsAlive;
		}

		var controller = GetComponent<PlayerUnitController>();
		if ( controller is not null )
		{
			SyncedWishDir = controller.WishDirection;
		}
	}

	protected override void OnUpdate()
	{
		if ( TargetRenderer is null )
			return;

		// Using the most compatible parameter names and types
		TargetRenderer.Set( "move_speed", SyncedSpeed );
		TargetRenderer.Set( "b_grounded", SyncedGrounded );
		TargetRenderer.Set( "b_alive", SyncedAlive );
		
		// If wish_direction causes errors, we calculate look direction instead
		if ( SyncedWishDir.LengthSquared > 0.01f )
		{
			TargetRenderer.Set( "aim_eyes", SyncedWishDir );
		}
	}
}
