using Sandbox;
using Sandbox.Citizen;

[Title( "Контроллер юнита игрока" )]
[Category( "Box Collector/Игрок" )]
public sealed class PlayerUnitController : Component
{
	[Property, Group( "Настройка" ), Description( "CharacterController, который отвечает за физическое движение игрока." )]
	public CharacterController Controller { get; set; }

	[Property, Group( "Настройка" ), Description( "Помощник анимации Citizen. Если не задан, будет найден в дочерних объектах." )]
	public CitizenAnimationHelper AnimationHelper { get; set; }

	[Property, Group( "Движение" ), Description( "Скорость движения игрока в world units в секунду." )]
	public float MoveSpeed { get; set; } = 300f;

	private TargetCursor _cursor;

	protected override void OnStart()
	{
		if ( Controller == null ) Controller = Components.Get<CharacterController>();

		if ( AnimationHelper == null )
			AnimationHelper = Components.GetInChildren<CitizenAnimationHelper>();

		if ( AnimationHelper != null && AnimationHelper.Target == null )
		{
			AnimationHelper.Target = AnimationHelper.Components.Get<SkinnedModelRenderer>();
		}

		_cursor = Components.GetInChildren<TargetCursor>();
	}

	protected override void OnUpdate()
	{
		if ( Controller == null ) return;

		// ── Owner-only: input, physics, rotation ──
		Vector3 wishVelocity = Vector3.Zero;
		if ( !IsProxy )
		{
			var moveInput = Input.AnalogMove;
			var camRot = Scene.Camera.WorldRotation;
			var moveDir = camRot * new Vector3( moveInput.x, moveInput.y, 0 );
			moveDir.z = 0;

			wishVelocity = moveDir.Normal * MoveSpeed;

			Controller.Accelerate( wishVelocity );
			Controller.ApplyFriction( 4f );

			if ( !Controller.IsOnGround )
				Controller.Velocity += Scene.PhysicsWorld.Gravity * Time.Delta;
			else
				Controller.Velocity = Controller.Velocity.WithZ( 0 );

			Controller.Move();

			var cursorPos = _cursor != null ? _cursor.NetCursorPosition : WorldPosition + WorldRotation.Forward * 100f;
			var lookDir = ( cursorPos - WorldPosition ).WithZ( 0 ).Normal;
			if ( lookDir.LengthSquared > 0.01f )
			{
				WorldRotation = Rotation.Slerp( WorldRotation, Rotation.LookAt( lookDir, Vector3.Up ), Time.Delta * 15f );
			}
		}

		// ── All clients: animation from synced data (Rule 2.2) ──
		if ( AnimationHelper != null )
		{
			var cursorPos = _cursor != null ? _cursor.NetCursorPosition : WorldPosition + WorldRotation.Forward * 100f;
			var animLookDir = ( cursorPos - WorldPosition ).WithZ( 0 ).Normal;

			AnimationHelper.WithVelocity( Controller.Velocity );
			AnimationHelper.WithWishVelocity( wishVelocity );
			AnimationHelper.IsGrounded = Controller.IsOnGround;
			AnimationHelper.WithLook( animLookDir, 1f, 1f, 0.5f );
		}
	}
}
