using Sandbox;
using System;

[Title( "Переносимый физический объект" )]
[Category( "Box Collector/Объекты" )]
public sealed class PhysicsCarryableObject : Component
{
	[Property, Group( "Перенос" ), Description( "Максимальная масса объекта, который можно переносить." )]
	public float MaxAllowedMass { get; set; } = 80f;

	[Property, Group( "Перенос" ), Description( "Опорная масса для расчёта силы подтягивания. Чем объект тяжелее относительно этого значения, тем медленнее он двигается." )]
	public float ReferenceMass { get; set; } = 20f;

	[Property, Group( "Перенос" ), Description( "Базовое ускорение, с которым объект тянется к позиции курсора." )]
	public float PullAcceleration { get; set; } = 24f;

	[Property, Group( "Перенос" ), Description( "Максимальная скорость переносимого объекта." )]
	public float MaxCarrySpeed { get; set; } = 420f;

	[Property, Group( "Перенос" ), Description( "Дистанция до целевой точки, на которой объект начинает мягко тормозить." )]
	public float StopDistance { get; set; } = 6f;

	[Property, Group( "Перенос" ), Description( "Затухание вращения объекта во время переноса." )]
	public float AngularDampingWhileCarried { get; set; } = 0.86f;

	[Property, Group( "Перенос" ), Description( "Если включено, перенос не изменяет вертикальную скорость объекта." )]
	public bool FreezeVerticalVelocity { get; set; } = true;

	[Sync( SyncFlags.FromHost )]
	public bool IsCarried { get; private set; }

	[Sync( SyncFlags.FromHost )]
	public Guid CarrierId { get; private set; }

	public Action<PhysicsCarryableObject> OnCarryStarted;
	public Action<PhysicsCarryableObject> OnCarryStopped;

	private Rigidbody _rigidbody;
	private Vector3 _targetWorldPosition;
	private bool _hasTarget;
	private TimeSince _lastLogTime;
	private bool _diagnosticsLogged;

	protected override void OnStart()
	{
		_rigidbody = Components.Get<Rigidbody>();

		if ( _rigidbody is null )
		{
			Log.Warning( $"{nameof( PhysicsCarryableObject )} on {GameObject.Name} requires a Rigidbody component." );
			Enabled = false;
			return;
		}

		LogNetworkDiagnostics( "OnStart" );
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;

		if ( !IsCarried )
			return;

		if ( _rigidbody is null )
			return;

		if ( !_hasTarget )
			return;

		MoveTowardTarget();
	}

	public bool CanBeCarriedBy( GameObject carrier )
	{
		if ( carrier is null )
			return false;

		if ( IsCarried )
			return false;

		if ( _rigidbody is null )
			return false;

		if ( _rigidbody.Mass > MaxAllowedMass )
			return false;

		return true;
	}

	public bool TryBeginCarry( GameObject carrier, Vector3 initialTargetWorldPosition )
	{
		if ( !CanBeCarriedBy( carrier ) )
		{
			Log.Info( $"[Carry-Physics] {GameObject.Name} cannot be carried. IsCarried: {IsCarried}, Mass: {_rigidbody?.Mass}" );
			return false;
		}

		IsCarried = true;
		CarrierId = carrier.Id;
		_targetWorldPosition = initialTargetWorldPosition;
		_hasTarget = true;
		LogNetworkDiagnostics( "TryBeginCarry" );

		if ( _rigidbody is not null )
		{
			_rigidbody.Sleeping = false;
			_rigidbody.MotionEnabled = true;
		}

		Log.Info( $"[Carry-Physics] {GameObject.Name} carry started by {carrier.Name}" );
		OnCarryStarted?.Invoke( this );
		return true;
	}

	public void SetCarryTarget( Vector3 targetWorldPosition )
	{
		if ( !IsCarried )
			return;

		_targetWorldPosition = targetWorldPosition;
		_hasTarget = true;
	}

	public void StopCarry( Guid carrierId )
	{
		if ( !IsCarried )
			return;

		if ( CarrierId != carrierId )
			return;

		IsCarried = false;
		CarrierId = Guid.Empty;
		_hasTarget = false;

		Log.Info( $"[Carry-Physics] {GameObject.Name} carry stopped" );
		OnCarryStopped?.Invoke( this );
	}

	public void ForceStopCarry()
	{
		if ( !IsCarried )
			return;

		IsCarried = false;
		CarrierId = Guid.Empty;
		_hasTarget = false;

		Log.Info( $"[Carry-Physics] {GameObject.Name} carry forced stop" );
		OnCarryStopped?.Invoke( this );
	}

	private void MoveTowardTarget()
	{
		var currentPosition = WorldPosition;
		var toTarget = _targetWorldPosition - currentPosition;
		var distance = toTarget.Length;

		if ( _lastLogTime > 0.5f )
		{
			Log.Info( $"[Carry-Physics] {GameObject.Name} move: Dist={distance:F1}, Target={_targetWorldPosition}, Vel={_rigidbody.Velocity.Length:F1}" );
			_lastLogTime = 0;
		}

		if ( distance <= StopDistance )
		{
			_rigidbody.Velocity *= 0.7f;
			_rigidbody.AngularVelocity *= AngularDampingWhileCarried;
			return;
		}

		var mass = MathF.Max( _rigidbody.Mass, 1f );
		var massFactor = Math.Clamp( ReferenceMass / mass, 0.15f, 1.0f );

		// Higher force when far, but very soft when close to prevent jitter
		float forceMultiplier = distance > 20f ? PullAcceleration : PullAcceleration * 0.5f;
		
		var desiredVelocity = toTarget.Normal * forceMultiplier * distance * massFactor;
		desiredVelocity = ClampLength( desiredVelocity, MaxCarrySpeed * massFactor );

		if ( FreezeVerticalVelocity )
		{
			desiredVelocity.z = 0f;
		}

		// Smoother lerp to prevent snappy oscillations
		_rigidbody.Velocity = Vector3.Lerp( _rigidbody.Velocity, desiredVelocity, 0.2f );
		_rigidbody.AngularVelocity *= AngularDampingWhileCarried;
	}

	private static Vector3 ClampLength( Vector3 value, float maxLength )
	{
		var length = value.Length;

		if ( length <= maxLength )
			return value;

		if ( length <= 0.001f )
			return Vector3.Zero;

		return value / length * maxLength;
	}

	private void LogNetworkDiagnostics( string source )
	{
		if ( _diagnosticsLogged && source == "OnStart" )
			return;

		_diagnosticsLogged = true;

		Log.Info(
			$"[Carry-Diag:{source}] {GameObject.Name} " +
			$"Id={GameObject.Id}, NetworkMode={GameObject.NetworkMode}, " +
			$"IsNetworkRoot={GameObject.IsNetworkRoot}, GameObject.IsProxy={GameObject.IsProxy}, Component.IsProxy={IsProxy}, " +
			$"Networking.IsHost={Networking.IsHost}, " +
			$"Rigidbody={_rigidbody != null}, BoxCollider={Components.Get<BoxCollider>() != null}, " +
			$"WorldPosition={WorldPosition}" );
	}
}
