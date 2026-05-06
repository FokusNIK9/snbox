using Sandbox;

public sealed class PlayerCargo : Component, ICargoReceiver
{
    [Property, Sync] public int CargoCount { get; set; } = 0;

    // Вызывается на сервере для вора
    public void AddCargo( int amount )
    {
        if ( IsProxy )
        {
            AddCargoRpc( amount );
            return;
        }
        CargoCount += amount;
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
            RemoveCargoRpc( amount );
            return;
        }
        CargoCount -= amount;
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
        var interactable = Components.GetInChildrenOrSelf<InteractableObject>();
        if ( interactable != null )
        {
            interactable.OnServerInteractionSuccess += HandleStolenBy;
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
