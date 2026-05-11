using Sandbox;
using System;

[Title( "Перенос физических объектов игроком" )]
[Category( "Box Collector/Игрок" )]
public sealed class PlayerPhysicsCarryController : Component
{
	[Property, Group( "Ввод" ), Description( "Кнопка input action для захвата и удержания физического объекта." )]
	public string CarryButton { get; set; } = "attack2";

	[Property, Group( "Правила" ), Description( "Максимальная дистанция, с которой игрок может начать перенос объекта." )]
	public float MaxGrabDistance { get; set; } = 220f;

	[Property, Group( "Правила" ), Description( "Максимальная дистанция до переносимого объекта, после которой перенос будет сброшен." )]
	public float MaxMaintainDistance { get; set; } = 340f;

	[Property, Group( "Правила" ), Description( "Интервал отправки новой целевой позиции на host во время переноса." )]
	public float TargetUpdateInterval { get; set; } = 0.05f;

	[Property, Group( "Правила" ), Description( "Минимальная задержка между попытками начать перенос." )]
	public float InteractionCooldown { get; set; } = 0.25f;

	[Property, Group( "Настройка" ), Description( "Курсор игрока, из которого берётся наведённый объект и позиция цели." )]
	public TargetCursor TargetCursor { get; set; }

	private Guid _localHoveredObjectId;
	private Guid _activeCarryableId;
	private TimeSince _timeSinceTargetUpdate;
	private TimeSince _timeSinceLastInteraction;

	protected override void OnStart()
	{
		TargetCursor ??= Components.Get<TargetCursor>( FindMode.EverythingInSelfAndDescendants );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		if ( TargetCursor is null )
			return;

		if ( _activeCarryableId == Guid.Empty )
			_localHoveredObjectId = TargetCursor.HoveredObjectId;

		if ( Input.Pressed( CarryButton ) && _timeSinceLastInteraction > InteractionCooldown )
		{
			TryStartCarry();
		}

		if ( Input.Down( CarryButton ) )
		{
			UpdateCarryTarget();
		}

		if ( Input.Released( CarryButton ) )
		{
			StopCarry();
		}
	}

	private void TryStartCarry()
	{
		if ( _localHoveredObjectId == Guid.Empty )
			return;

		var targetObject = Scene.Directory.FindByGuid( _localHoveredObjectId );
		var carryable = targetObject?.Components.GetInAncestorsOrSelf<PhysicsCarryableObject>();
		if ( carryable is null )
			return;

		_activeCarryableId = carryable.GameObject.Id;
		_timeSinceLastInteraction = 0f;
		SetCursorLock( _activeCarryableId );

		var cursorWorldPosition = TargetCursor.NetCursorPosition;
		Log.Info( $"[Carry] Requesting carry for {_activeCarryableId}" );
		RequestStartCarry( _activeCarryableId, cursorWorldPosition );
	}

	private void UpdateCarryTarget()
	{
		if ( _activeCarryableId == Guid.Empty )
			return;

		if ( _timeSinceTargetUpdate < TargetUpdateInterval )
			return;

		_timeSinceTargetUpdate = 0f;

		var cursorWorldPosition = TargetCursor.NetCursorPosition;
		RequestUpdateCarryTarget( cursorWorldPosition );
	}

	private void StopCarry()
	{
		if ( _activeCarryableId == Guid.Empty )
			return;

		Log.Info( "[Carry] Releasing object" );
		RequestStopCarry();
		_activeCarryableId = Guid.Empty;
		_timeSinceLastInteraction = 0f;
		SetCursorLock( Guid.Empty );
	}

	[Rpc.Host]
	private void RequestStartCarry( Guid targetObjectId, Vector3 cursorWorldPosition )
	{
		void RejectCarry()
		{
			_activeCarryableId = Guid.Empty;
			ConfirmCarryStopped();
		}

		if ( targetObjectId == Guid.Empty )
		{
			RejectCarry();
			return;
		}

		var targetObject = Scene.Directory.FindByGuid( targetObjectId );
		if ( targetObject is null )
		{
			RejectCarry();
			return;
		}

		var carryable = targetObject.Components.GetInAncestorsOrSelf<PhysicsCarryableObject>();
		if ( carryable is null )
		{
			RejectCarry();
			return;
		}

		Log.Info(
			$"[Carry-Server-Diag] Found target: Name={targetObject.Name}, Id={targetObject.Id}, " +
			$"NetworkMode={targetObject.NetworkMode}, IsNetworkRoot={targetObject.IsNetworkRoot}, " +
			$"GameObject.IsProxy={targetObject.IsProxy}, Component.IsProxy={carryable.IsProxy}, " +
			$"Networking.IsHost={Networking.IsHost}, WorldPosition={targetObject.WorldPosition}" );

		if ( !IsCloseEnoughToStart( carryable ) )
		{
			RejectCarry();
			return;
		}

		if ( !carryable.TryBeginCarry( GameObject, cursorWorldPosition ) )
		{
			RejectCarry();
			return;
		}

		Log.Info( $"[Carry-Server] Carry started for {carryable.GameObject.Name}" );
		_activeCarryableId = carryable.GameObject.Id;
		ConfirmCarryStarted( _activeCarryableId );
	}

	[Rpc.Host]
	private void RequestUpdateCarryTarget( Vector3 cursorWorldPosition )
	{
		if ( _activeCarryableId == Guid.Empty )
			return;

		var carryable = FindActiveCarryable();
		if ( carryable is null ) return;

		if ( !IsCloseEnoughToMaintain( carryable ) )
		{
			carryable.ForceStopCarry();
			_activeCarryableId = Guid.Empty;
			ConfirmCarryStopped();
			return;
		}

		carryable.SetCarryTarget( cursorWorldPosition );
	}

	[Rpc.Host]
	private void RequestStopCarry()
	{
		if ( _activeCarryableId == Guid.Empty )
			return;

		var carryable = FindActiveCarryable();
		if ( carryable is not null )
		{
			carryable.StopCarry( GameObject.Id );
		}

		_activeCarryableId = Guid.Empty;
		ConfirmCarryStopped();
	}

	[Rpc.Owner]
	private void ConfirmCarryStarted( Guid carryableId )
	{
		_activeCarryableId = carryableId;
		_timeSinceLastInteraction = 0f;
		SetCursorLock( carryableId );
	}

	[Rpc.Owner]
	private void ConfirmCarryStopped()
	{
		_activeCarryableId = Guid.Empty;
		_timeSinceLastInteraction = 0f;
		SetCursorLock( Guid.Empty );
	}

	private void SetCursorLock( Guid carryableId )
	{
		if ( TargetCursor != null )
		{
			TargetCursor.IsLocked = carryableId != Guid.Empty;
			TargetCursor.LockedObjectId = carryableId;
		}
	}

	private PhysicsCarryableObject FindActiveCarryable()
	{
		if ( _activeCarryableId == Guid.Empty ) return null;
		var obj = Scene.Directory.FindByGuid( _activeCarryableId );
		return obj?.Components.Get<PhysicsCarryableObject>();
	}

	private bool IsCloseEnoughToStart( PhysicsCarryableObject carryable )
	{
		return (carryable.WorldPosition - WorldPosition).Length <= MaxGrabDistance;
	}

	private bool IsCloseEnoughToMaintain( PhysicsCarryableObject carryable )
	{
		return (carryable.WorldPosition - WorldPosition).Length <= MaxMaintainDistance;
	}
}
