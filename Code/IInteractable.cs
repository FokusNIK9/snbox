using Sandbox;
using System;

public interface IInteractable
{
    float InteractionTime { get; }
    bool CanInteract( GameObject interactor );
    void OnInteract( Guid interactorNetworkId );
}

public interface IHoldProgressInteractable
{
    void SetHoldProgress( float progress );
}
