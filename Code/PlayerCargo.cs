using Sandbox;

public sealed class PlayerCargo : Component, ICargoReceiver
{
    [Property, Sync] public int CargoCount { get; set; } = 0;

    [Property, Group( "Drop" ), Description( "Input action name for dropping cargo (set in Project Settings → Input)" )]
    public string DropAction { get; set; } = "drop";

    [Property, Group( "Drop" )]
    public int DropAmount { get; set; } = 1;

    [Property, Group( "Drop" ), Description( "Color tint of the dropped cargo bag" )]
    public Color DropBagColor { get; set; } = new Color( 0.7f, 0.5f, 0.15f );

    public void AddCargo( int amount )
    {
        if ( IsProxy )
        {
            Log.Info( $"[PlayerCargo] AddCargo({amount}) → routing to owner via RPC" );
            AddCargoRpc( amount );
            return;
        }
        CargoCount += amount;
        Log.Info( $"[PlayerCargo] AddCargo({amount}) → CargoCount = {CargoCount}" );
    }

    [Rpc.Owner]
    private void AddCargoRpc( int amount )
    {
        CargoCount += amount;
    }

    public void RemoveCargo( int amount )
    {
        if ( IsProxy )
        {
            Log.Info( $"[PlayerCargo] RemoveCargo({amount}) → routing to owner via RPC" );
            RemoveCargoRpc( amount );
            return;
        }
        CargoCount -= amount;
        Log.Info( $"[PlayerCargo] RemoveCargo({amount}) → CargoCount = {CargoCount}" );
    }

    [Rpc.Owner]
    private void RemoveCargoRpc( int amount )
    {
        CargoCount -= amount;
    }

    protected override void OnStart()
    {
        if ( !IsProxy && Networking.IsHost )
        {
            CargoCount = 5;
            Log.Info( $"[Хост] Инициализация: выдано {CargoCount} коробок." );
        }
    }

    protected override void OnAwake()
    {
        Log.Info( $"[PlayerCargo] OnAwake on {GameObject.Name}, IsProxy={IsProxy}" );
        var interactable = Components.GetInChildrenOrSelf<InteractableObject>();
        if ( interactable != null )
        {
            interactable.OnServerInteractionSuccess += HandleStolenBy;
            Log.Info( $"[PlayerCargo] Subscribed to InteractableObject on {interactable.GameObject.Name}" );
        }
        else
        {
            Log.Warning( $"[PlayerCargo] InteractableObject NOT found in children of {GameObject.Name}" );
        }
    }

    protected override void OnUpdate()
    {
        if ( IsProxy ) return;

        if ( !string.IsNullOrEmpty( DropAction ) && Input.Pressed( DropAction ) && CargoCount > 0 )
        {
            Log.Info( $"[PlayerCargo] Drop key pressed, requesting drop..." );
            RequestDropCargo();
        }
    }

    [Rpc.Host]
    private void RequestDropCargo()
    {
        if ( CargoCount <= 0 )
        {
            Log.Warning( "[PlayerCargo] Drop rejected: no cargo" );
            return;
        }

        int amount = System.Math.Min( DropAmount, CargoCount );
        RemoveCargo( amount );

        var dropPos = WorldPosition + WorldRotation.Backward * 40f;
        SpawnDroppedCargo( dropPos, amount );
        Log.Info( $"[PlayerCargo] Dropped {amount} cargo at {dropPos}" );
    }

    private void SpawnDroppedCargo( Vector3 position, int amount )
    {
        var go = new GameObject();
        go.Name = $"DroppedCargo ({amount})";
        go.WorldPosition = position;
        go.Tags.Add( "pickup" );

        var renderer = go.Components.Create<ModelRenderer>();
        renderer.Model = Model.Load( "models/dev/box.vmdl" );
        renderer.Tint = DropBagColor;

        var collider = go.Components.Create<BoxCollider>();
        collider.Scale = new Vector3( 25, 25, 25 );

        var interactable = go.Components.Create<InteractableObject>();
        interactable.InteractionTime = 0.8f;
        interactable.UseHighlight = true;
        interactable.HighlightColor = new Color( 0.3f, 1f, 0.4f );
        interactable.HoldColor = new Color( 1f, 0.9f, 0.2f );

        var dropped = go.Components.Create<DroppedCargo>();
        dropped.CargoCount = amount;

        go.NetworkSpawn();
    }

    private void HandleStolenBy( GameObject thief )
    {
        if ( CargoCount > 0 )
        {
            RemoveCargo( 1 );
            Log.Info( $"[Сервер] Утверждена кража у {GameObject.Name}." );

            var receiver = thief.Components.GetInAncestorsOrSelf<ICargoReceiver>();
            if ( receiver != null )
            {
                receiver.AddCargo( 1 );
            }
        }
    }
}
