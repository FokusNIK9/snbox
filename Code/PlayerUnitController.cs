using Sandbox;

/// <summary>
/// Top-down unit movement controller for a TD game.
/// Requires a CharacterController on the same GameObject (auto-created via RequireComponent).
/// Handles WASD input, smooth rotation, gravity, and network-synced movement state.
/// </summary>
public sealed class PlayerUnitController : Component
{
	// ── Movement ──────────────────────────────────────────

	[Property, Group( "Movement" ), Range( 50f, 800f )]
	public float MoveSpeed { get; set; } = 200f;

	[Property, Group( "Movement" ), Range( 1f, 20f ), Description( "How fast the unit rotates toward movement direction" )]
	public float RotationSmoothing { get; set; } = 10f;

	[Property, Group( "Movement" ), Range( 0f, 20f )]
	public float Friction { get; set; } = 6f;

	[Property, Group( "Movement" ), Range( 0f, 64f )]
	public float Acceleration { get; set; } = 10f;

	// ── Gravity ───────────────────────────────────────────

	[Property, Group( "Gravity" ), Range( 0f, 2000f )]
	public float GravityStrength { get; set; } = 800f;

	// ── Network state ─────────────────────────────────────

	[Sync] public Vector3 WishDirection { get; set; }
	[Sync] public bool IsMoving { get; set; }
	[Sync( SyncFlags.Interpolate )] public float CurrentSpeed { get; set; }

	// ── Internal ──────────────────────────────────────────

	[RequireComponent]
	public CharacterController CharacterController { get; set; }

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;

		BuildWishDirection();
		ApplyMovement();
		ApplyRotation();
	}

	private void BuildWishDirection()
	{
		var input = Input.AnalogMove;

		// AnalogMove: x = forward/backward, y = left/right
		var dir = new Vector3( input.x, input.y, 0f ).Normal;

		WishDirection = dir;
		IsMoving = dir.LengthSquared > 0.01f;
	}

	private void ApplyMovement()
	{
		if ( CharacterController is null )
			return;

		if ( CharacterController.IsOnGround )
		{
			CharacterController.ApplyFriction( Friction );

			if ( IsMoving )
			{
				CharacterController.Accelerate( WishDirection * MoveSpeed );
			}
		}
		else
		{
			// Apply gravity when airborne
			CharacterController.Velocity -= Vector3.Up * GravityStrength * Time.Delta;
		}

		CharacterController.Move();

		CurrentSpeed = CharacterController.Velocity.WithZ( 0 ).Length;
	}

	private void ApplyRotation()
	{
		if ( !IsMoving )
			return;

		var targetRotation = Rotation.LookAt( WishDirection, Vector3.Up );
		WorldRotation = Rotation.Slerp( WorldRotation, targetRotation, Time.Delta * RotationSmoothing );
	}

	protected override void OnValidate()
	{
		MoveSpeed = MoveSpeed.Clamp( 50f, 800f );
		RotationSmoothing = RotationSmoothing.Clamp( 1f, 20f );
		Friction = Friction.Clamp( 0f, 20f );
		Acceleration = Acceleration.Clamp( 0f, 64f );
		GravityStrength = GravityStrength.Clamp( 0f, 2000f );
	}
}
