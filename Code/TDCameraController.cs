using Sandbox;
using System;

/// <summary>
/// Top-down camera for a multiplayer TD game.
/// Place on a GameObject with a CameraComponent. Auto-finds the local player unit.
///
/// Features:
///   - Frame-rate independent smooth follow (exponential decay)
///   - Mouse-wheel zoom with smooth interpolation
///   - Cursor-offset: camera leans toward the cursor for better battlefield awareness
///   - Screen shake API: call Shake() from any component (damage, explosions, etc.)
///   - Pitch-angle driven geometry: BackOffset is auto-calculated from Height + PitchAngle
///   - Snap-on-acquire: no jarring lerp when the target first appears or respawns
/// </summary>
public sealed class TDCameraController : Component
{
	// ── Follow ────────────────────────────────────────────

	[Property, Group( "Follow" ), Range( 100f, 2000f ), Description( "Default camera height above target" )]
	public float Height { get; set; } = 600f;

	[Property, Group( "Follow" ), Range( 30f, 90f ), Description( "Pitch angle (90 = straight down, 60 = classic RTS angle)" )]
	public float PitchAngle { get; set; } = 60f;

	[Property, Group( "Follow" ), Range( 1f, 30f ), Description( "Follow responsiveness (higher = snappier)" )]
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

	[Property, Group( "Zoom" ), Range( 1f, 20f ), ShowIf( "AllowZoom", true ) ]
	public float ZoomSmoothing { get; set; } = 6f;

	// ── Cursor Offset ─────────────────────────────────────

	[Property, Group( "Cursor Offset" ), Description( "Shifts focus toward cursor for better vision in aim direction" )]
	public bool AllowCursorOffset { get; set; } = true;

	[Property, Group( "Cursor Offset" ), Range( 0f, 0.5f ), ShowIf( "AllowCursorOffset", true ), Description( "Blend strength (0 = disabled, 0.5 = halfway to cursor)" )]
	public float CursorInfluence { get; set; } = 0.15f;

	[Property, Group( "Cursor Offset" ), Range( 50f, 500f ), ShowIf( "AllowCursorOffset", true ), Description( "Max offset distance in world units" )]
	public float MaxCursorOffset { get; set; } = 150f;

	// ── Internal ──────────────────────────────────────────

	private GameObject _target;
	private float _currentHeight;
	private float _targetHeight;
	private Vector3 _focusPoint;
	private bool _hasSnapped;

	private float _shakeIntensity;
	private float _shakeDuration;
	private TimeSince _shakeStarted;

	protected override void OnAwake()
	{
		_currentHeight = Height;
		_targetHeight = Height;
	}

	protected override void OnUpdate()
	{
		AcquireTarget();
		HandleZoom();
		ApplyCamera();
	}

	// ── Public API ────────────────────────────────────────

	/// <summary>
	/// Trigger screen shake. Stronger shakes override weaker ones.
	/// </summary>
	public void Shake( float intensity, float duration = 0.3f )
	{
		float remaining = MathF.Max( 0f, _shakeDuration - _shakeStarted );
		float activePower = _shakeIntensity * ( remaining / MathF.Max( _shakeDuration, 0.001f ) );

		if ( intensity > activePower )
		{
			_shakeIntensity = intensity;
			_shakeDuration = duration;
			_shakeStarted = 0;
		}
	}

	// ── Target ────────────────────────────────────────────

	private void AcquireTarget()
	{
		if ( _target.IsValid() )
			return;

		foreach ( var unit in Scene.GetAllComponents<PlayerUnitController>() )
		{
			if ( !unit.IsProxy )
			{
				_target = unit.GameObject;
				_hasSnapped = false;
				break;
			}
		}
	}

	// ── Zoom ──────────────────────────────────────────────

	private void HandleZoom()
	{
		if ( !AllowZoom )
			return;

		var scroll = Input.MouseWheel.y;
		if ( scroll == 0f )
			return;

		_targetHeight -= scroll * ZoomStep;
		_targetHeight = _targetHeight.Clamp( MinHeight, MaxHeight );
	}

	// ── Camera transform ──────────────────────────────────

	private void ApplyCamera()
	{
		if ( !_target.IsValid() )
			return;

		// ── Height ──
		_currentHeight = _currentHeight.LerpTo( _targetHeight, Damp( ZoomSmoothing ) );

		// ── Focus point ──
		var targetPos = _target.WorldPosition;
		var desiredFocus = targetPos;

		if ( AllowCursorOffset )
		{
			var cursor = CursorWorldHit();
			if ( cursor.HasValue )
			{
				var delta = ( cursor.Value - targetPos ).WithZ( 0 );
				if ( delta.Length > MaxCursorOffset )
					delta = delta.Normal * MaxCursorOffset;

				desiredFocus += delta * CursorInfluence;
			}
		}

		if ( !_hasSnapped )
		{
			_focusPoint = desiredFocus;
			_hasSnapped = true;
		}
		else
		{
			_focusPoint = Vector3.Lerp( _focusPoint, desiredFocus, Damp( FollowSpeed ) );
		}

		// ── Position from pitch geometry ──
		float pitchRad = MathF.PI * PitchAngle / 180f;
		float backDist = PitchAngle >= 89.5f ? 0f : _currentHeight / MathF.Tan( pitchRad );

		var pos = _focusPoint
			+ Vector3.Up * _currentHeight
			- Vector3.Forward * backDist;

		// ── Screen shake ──
		if ( _shakeDuration > 0f && _shakeStarted < _shakeDuration )
		{
			float decay = 1f - ( _shakeStarted / _shakeDuration );
			float power = _shakeIntensity * decay;

			pos += new Vector3(
				Game.Random.Float( -1f, 1f ) * power,
				Game.Random.Float( -1f, 1f ) * power,
				0f
			);
		}

		// ── Apply ──
		WorldPosition = pos;
		WorldRotation = Rotation.LookAt( ( _focusPoint - pos ).Normal, Vector3.Up );
	}

	// ── Helpers ───────────────────────────────────────────

	private Vector3? CursorWorldHit()
	{
		var cam = Scene.Camera;
		if ( cam == null )
			return null;

		var ray = cam.ScreenPixelToRay( Mouse.Position );

		var tr = Scene.Trace.Ray( ray, 10000f )
			.WithoutTags( "player" )
			.Run();

		if ( tr.Hit )
			return tr.HitPosition;

		// Fallback: intersect Z = 0 ground plane
		if ( MathF.Abs( ray.Forward.z ) > 0.001f )
		{
			float dist = -ray.Position.z / ray.Forward.z;
			if ( dist > 0f )
				return ray.Position + ray.Forward * dist;
		}

		return null;
	}

	/// <summary>
	/// Frame-rate independent exponential decay.
	/// Unlike (Time.Delta * speed), this never overshoots and
	/// behaves identically at 30 fps and 300 fps.
	/// </summary>
	private static float Damp( float speed )
	{
		return 1f - MathF.Exp( -speed * Time.Delta );
	}

	protected override void OnValidate()
	{
		Height = Height.Clamp( 100f, 3000f );
		PitchAngle = PitchAngle.Clamp( 30f, 90f );
		FollowSpeed = FollowSpeed.Clamp( 1f, 30f );
		MinHeight = MinHeight.Clamp( 200f, 800f );
		MaxHeight = MaxHeight.Clamp( 800f, 3000f );
		ZoomStep = ZoomStep.Clamp( 10f, 200f );
		ZoomSmoothing = ZoomSmoothing.Clamp( 1f, 20f );
		CursorInfluence = CursorInfluence.Clamp( 0f, 0.5f );
		MaxCursorOffset = MaxCursorOffset.Clamp( 50f, 500f );
	}
}
