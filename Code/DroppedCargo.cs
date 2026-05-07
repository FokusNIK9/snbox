using Sandbox;

public sealed class DroppedCargo : Component
{
    [Property, Sync] public int CargoCount { get; set; } = 1;

    protected override void OnStart()
    {
        var interactable = Components.GetInChildrenOrSelf<InteractableObject>();
        if ( interactable != null )
        {
            interactable.OnServerInteractionSuccess += HandlePickedUpBy;
            Log.Info( $"[DroppedCargo] Spawned with {CargoCount} cargo at {WorldPosition}" );
        }
        else
        {
            Log.Warning( "[DroppedCargo] No InteractableObject found!" );
        }
    }

    private void HandlePickedUpBy( GameObject picker )
    {
        if ( CargoCount <= 0 ) return;

        var receiver = picker.Components.GetInAncestorsOrSelf<ICargoReceiver>();
        if ( receiver != null )
        {
            int amount = CargoCount;
            receiver.AddCargo( amount );
            Log.Info( $"[DroppedCargo] {picker.Name} picked up {amount} cargo" );
            CargoCount = 0;
            GameObject.Destroy();
        }
    }
}
