using Sandbox;
using System;

[Title( "Интерактивный объект" )]
[Category( "Box Collector/Объекты" )]
public sealed class InteractableObject : Component, IInteractable, IHoldProgressInteractable
{
    [Property, Group( "Взаимодействие" ), Description( "Сколько секунд нужно удерживать кнопку для завершения взаимодействия." )]
    public float InteractionTime { get; set; } = 1.5f;

    [Property, Group( "Взаимодействие" ), Description( "Можно ли сейчас взаимодействовать с объектом." )]
    public bool IsActive { get; set; } = true;

    [Property, Group( "Подсветка" ), Description( "Включить изменение цвета объекта при наведении и удержании." )]
    public bool UseHighlight { get; set; } = true;

    [Property, Group( "Подсветка" ), Description( "Цвет объекта при наведении курсора." )]
    public Color HighlightColor { get; set; } = new Color( 0.2f, 1f, 0.3f );

    [Property, Group( "Подсветка" ), Description( "Цвет объекта во время удержания кнопки взаимодействия." )]
    public Color HoldColor { get; set; } = new Color( 1f, 0.8f, 0.1f );

    public Action<GameObject> OnServerInteractionSuccess;

    private ModelRenderer _modelRenderer;
    private Color _originalTint;
    private bool _tintSaved;

    public bool CanInteract( GameObject interactor )
    {
        if ( !IsActive ) return false;
        if ( interactor == null ) return false;
        if ( GameObject == null ) return false;
        if ( interactor == GameObject.Root ) return false;
        return true;
    }

    public void OnInteract( Guid interactorNetworkId )
    {
        PerformInteractionOnServer( interactorNetworkId );
    }

    public void SetHighlighted( bool highlighted )
    {
        if ( !UseHighlight ) return;

        if ( _modelRenderer == null )
        {
            _modelRenderer = Components.Get<ModelRenderer>();
            if ( _modelRenderer == null ) return;
        }

        if ( !_tintSaved )
        {
            _originalTint = _modelRenderer.Tint;
            _tintSaved = true;
        }

        _modelRenderer.Tint = highlighted ? HighlightColor : _originalTint;
    }

    public void SetHoldProgress( float progress )
    {
        if ( !UseHighlight || _modelRenderer == null ) return;
        _modelRenderer.Tint = Color.Lerp( HighlightColor, HoldColor, progress );
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

        Log.Info( $"[InteractableObject] Interaction success: {interactor.Name} → {GameObject.Name}" );
        OnServerInteractionSuccess?.Invoke( interactor );
    }
}
