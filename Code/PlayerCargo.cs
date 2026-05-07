using Sandbox;

public sealed class PlayerCargo : Component, ICargoReceiver
{
    [Property, Sync] public int CargoCount { get; set; } = 0;

    // Вызывается на сервере для вора
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

    // Вызывается на сервере для жертвы
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
        // Выдаем хосту стартовый груз для теста кражи
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

    private void HandleStolenBy( GameObject thief )
    {
        // Выполняется строго на сервере (после валидации дистанции в InteractableObject)
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
