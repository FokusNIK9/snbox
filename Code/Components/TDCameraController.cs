using Sandbox;

/// <summary>
/// Top-down camera for a TD game. Follows the local player's unit smoothly.
/// Place on a GameObject with a CameraComponent. Auto-finds the local player's unit.
/// </summary>
public sealed class TDCameraController : Component
{
	// ── Follow ────────────────────────────────────────────

	[Property, Group( "Follow" ), Range( 100f, 2000f ), Description( "Camera height above the target" )]
	public float Height { get; set; } = 600f;

	[Property, Group( "Follow" ), Range( 0f, 500f ), Description( "Backward offset for angled view" )]
	public float BackOffset { get; set; } = 200f;

	[Property, Group( "Follow" ), Range( 30f, 90f ), Description( "Camera pitch angle (90 = straight down)" )]
	public float PitchAngle { get; set; } = 60f;

	[Property, Group( "Follow" ), Range( 1f, 30f ), Description( "Smoothing speed for camera follow" )]
	public float FollowSpeed { get; set; } = 8f;

	// ── Zoom ──────────────────────────────────────────────

	[Property, Group( "Zoom" )]
	public bool AllowZoom { get; set; } = true;

	[Property, Group( "Zoom" ), Range( 200f, 800f ), ShowIf( "AllowZoom", true )]
	public float MinHeight { get; set; } = 300f;

	[Property, Group( "Zoom" ), Range( 800f, 3000f ), ShowIf( "AllowZoom", true )]
	public float MaxHeight { get; set; } = 1200f;

	[Property, Group( "Zoom" ), Range( 10f, 200f ), ShowIf( "AllowZoom", true ), Description( "Height change per scroll step" )]
	public float ZoomStep { get; set; } = 50f;

	[Property, Group( "Zoom" ), Range( 1f, 20f ), ShowIf( "AllowZoom", true )]
	public float ZoomSmoothing { get; set; } = 6f;

	// ── Internal ──────────────────────────────────────────

	private GameObject _target;
	private float _targetHeight;

	protected override void OnAwake()
	{
		_targetHeight = Height;
	}

	protected override void OnUpdate()
	{
		FindTargetIfNeeded();
		HandleZoomInput();
		UpdateCameraPosition();
	}

	private void FindTargetIfNeeded()
	{
		if ( _target.IsValid() )
			return;

		foreach ( var unit in Scene.GetAllComponents<PlayerUnitController>() )
		{
			if ( !unit.IsProxy )
			{
				_target = unit.GameObject;
				break;
			}
		}
	}

	private void HandleZoomInput()
	{
		if ( !AllowZoom )
			return;

		var scroll = Input.MouseWheel.y;
		if ( scroll == 0f )
			return;

		_targetHeight -= scroll * ZoomStep;
		_targetHeight = _targetHeight.Clamp( MinHeight, MaxHeight );
	}

	private void UpdateCameraPosition()
	{
		if ( !_target.IsValid() )
			return;

		Height = Height.LerpTo( _targetHeight, Time.Delta * ZoomSmoothing );

		var targetPos = _target.WorldPosition;
		var pitchRotation = Rotation.FromPitch( PitchAngle );

		var cameraPos = targetPos
			+ Vector3.Up * Height
			- pitchRotation.Forward * BackOffset;

		WorldPosition = WorldPosition.LerpTo( cameraPos, Time.Delta * FollowSpeed );
		WorldRotation = Rotation.LookAt( targetPos - WorldPosition, Vector3.Up );
	}

	protected override void OnValidate()
	{
		Height = Height.Clamp( 100f, 3000f );
		BackOffset = BackOffset.Clamp( 0f, 500f );
		PitchAngle = PitchAngle.Clamp( 30f, 90f );
		FollowSpeed = FollowSpeed.Clamp( 1f, 30f );
		MinHeight = MinHeight.Clamp( 200f, 800f );
		MaxHeight = MaxHeight.Clamp( 800f, 3000f );
		ZoomStep = ZoomStep.Clamp( 10f, 200f );
		ZoomSmoothing = ZoomSmoothing.Clamp( 1f, 20f );
	}
}
