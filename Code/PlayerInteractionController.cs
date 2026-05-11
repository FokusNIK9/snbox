using Sandbox;
using System;

[Title( "Контроллер взаимодействия игрока" )]
[Category( "Box Collector/Игрок" )]
public sealed class PlayerInteractionController : Component
{
	[Property, Group( "Настройка" ), Description( "Курсор игрока, из которого читается текущий интерактивный объект. Если не задан, будет найден в дочерних объектах." )]
	public TargetCursor TargetCursor { get; set; }

	private GameObject PlayerObject { get; set; }
	private IInteractable _currentInteractable;
	private float _holdTimer;

	protected override void OnStart()
	{
		PlayerObject = Components.Get<PlayerUnitController>()?.GameObject ?? GameObject;

		TargetCursor ??= Components.GetInChildren<TargetCursor>();
		if ( TargetCursor == null )
		{
			Log.Warning( $"{GameObject.Name}: PlayerInteractionController requires TargetCursor." );
		}
	}

	protected override void OnUpdate()
	{
		if ( PlayerObject == null ) return;
		if ( PlayerObject.Network.IsProxy ) return;

		if ( TargetCursor == null )
		{
			TargetCursor = Components.GetInChildren<TargetCursor>();
			if ( TargetCursor == null ) return;
		}

		UpdateInteraction();
	}

	private void UpdateInteraction()
	{
		var interactable = TargetCursor.CurrentInteractable;
		if ( !IsInteractableValid( interactable ) || !interactable.CanInteract( PlayerObject ) )
		{
			ClearInteractionState();
			return;
		}

		if ( _currentInteractable != interactable )
		{
			_currentInteractable = interactable;
			_holdTimer = 0f;
		}

		if ( Input.Down( "attack1" ) )
		{
			UpdateHold();
			return;
		}

		if ( _holdTimer > 0f )
		{
			ResetHoldProgress();
		}
		_holdTimer = 0f;
	}

	private void UpdateHold()
	{
		if ( _currentInteractable == null ) return;

		float interactionTime = _currentInteractable.InteractionTime;
		if ( interactionTime <= 0f )
		{
			Interact();
			return;
		}

		_holdTimer += Time.Delta;

		float progress = MathF.Min( _holdTimer / interactionTime, 1f );
		if ( _currentInteractable is IHoldProgressInteractable holdProgress )
		{
			holdProgress.SetHoldProgress( progress );
		}

		if ( _holdTimer >= interactionTime )
		{
			Interact();
		}
	}

	private void Interact()
	{
		if ( _currentInteractable == null ) return;

		_currentInteractable.OnInteract( PlayerObject.Id );
		ResetHoldProgress();
		_holdTimer = 0f;
	}

	private void ResetHoldProgress()
	{
		if ( _currentInteractable is IHoldProgressInteractable holdProgress )
		{
			holdProgress.SetHoldProgress( 0f );
		}
	}

	private void ClearInteractionState()
	{
		_currentInteractable = null;
		_holdTimer = 0f;
	}

	private bool IsInteractableValid( IInteractable interactable )
	{
		if ( interactable == null ) return false;

		if ( interactable is Component component )
		{
			return component.GameObject != null;
		}

		return true;
	}
}
