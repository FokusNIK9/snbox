using Sandbox;
using System;

public sealed class InteractableObject : Component, IInteractable
{
    [Property] public float InteractionTime { get; set; } = 1.5f;
    [Property] public bool IsActive { get; set; } = true;

    public Action<GameObject> OnServerInteractionSuccess;

    public bool CanInteract( GameObject interactor )
    {
        if ( !IsActive ) return false;
        if ( interactor == GameObject.Root ) return false; 
        return true;
    }

    public void OnInteract( Guid interactorNetworkId )
    {
        PerformInteractionOnServer( interactorNetworkId );
    }

    [Rpc.Host]
    private void PerformInteractionOnServer( Guid interactorNetworkId )
    {
        var interactor = Scene.Directory.FindByGuid( interactorNetworkId );
        if ( interactor == null )
        {
            Log.Warning( $"[Security] Interaction rejected: Interactor {interactorNetworkId} not found on server." );
            return;
        }

        float distance = Vector3.DistanceBetween( interactor.WorldPosition, WorldPosition );
        if ( distance > 150f ) 
        {
            Log.Warning( $"[Security] Interaction rejected: {interactor.Name} is too far." );
            return;
        }

        OnServerInteractionSuccess?.Invoke( interactor );
    }
}
