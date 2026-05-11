using Sandbox;
using System;

public enum CursorMode
{
    GroundPlane,
    SurfaceSnap
}

[Title( "Целевой курсор" )]
[Category( "Box Collector/Игрок" )]
public sealed class TargetCursor : Component
{
    [Property, Group( "Визуал" ), Description( "Рендерер модели, который отображает курсор в мире." )]
    public ModelRenderer CursorRenderer { get; set; }

    [Property, Group( "Визуал" ), Description( "Цвет курсора в обычном состоянии." )]
    public Color NormalColor { get; set; } = Color.White;

    [Property, Group( "Визуал" ), Description( "Цвет курсора при наведении на интерактивный или переносимый объект." )]
    public Color HoverColor { get; set; } = Color.Green;

    [Property, Group( "Визуал" ), Description( "Обычный размер курсора." )]
    public Vector3 NormalScale { get; set; } = Vector3.One;

    [Property, Group( "Визуал" ), Description( "Размер курсора при наведении на цель." )]
    public Vector3 HoverScale { get; set; } = new Vector3( 1.5f );

    [Property, Group( "Поведение" ), Description( "GroundPlane держит курсор на плоскости пола. SurfaceSnap визуально притягивает курсор к объекту при наведении." )]
    public CursorMode Mode { get; set; } = CursorMode.GroundPlane;

    [Property, Group( "Поведение" ), Description( "Высота плоскости пола по Z, на которой рассчитывается позиция курсора." )]
    public float GroundHeight { get; set; } = 2f;

    [Property, Group( "Поведение" ), Description( "Скорость сглаживания курсора удалённых игроков. Локальный курсор двигается мгновенно." )]
    public float RemoteLerpSpeed { get; set; } = 25f;

    [Property, Group( "Поведение" ), Description( "Высота визуального курсора над центром объекта в режиме SurfaceSnap." )]
    public float SnapHeight { get; set; } = 30f;

    [Sync] public Vector3 NetCursorPosition { get; set; }
    [Sync] public bool IsHovering { get; set; }
    public Guid HoveredObjectId { get; private set; }
    public Vector3 VisualCursorPosition { get; private set; }
    public IInteractable CurrentInteractable => _currentInteractable;
    public GameObject PlayerObject { get; private set; }

    public bool IsLocked { get; set; }
    public Guid LockedObjectId { get; set; }

    private IInteractable _currentInteractable;

    protected override void OnStart()
    {
        PlayerObject = Components.GetInAncestorsOrSelf<PlayerUnitController>()?.GameObject;
        if ( PlayerObject == null ) PlayerObject = GameObject.Parent;
    }

    protected override void OnUpdate()
    {
        if ( PlayerObject == null ) return;
        if ( !PlayerObject.Network.IsProxy ) UpdateLocalLogic();
        UpdateVisuals();
    }

    private void UpdateLocalLogic()
    {
        if ( Scene.Camera == null ) return;

        var mouseRay = Scene.Camera.ScreenPixelToRay( Mouse.Position );

        // ── Cursor position: always from ray-plane intersection ──
        float denom = mouseRay.Forward.z;
        if ( MathF.Abs( denom ) > 0.0001f )
        {
            float t = ( GroundHeight - mouseRay.Position.z ) / denom;
            if ( t > 0f )
                NetCursorPosition = mouseRay.Position + mouseRay.Forward * t;
        }

        if ( IsLocked )
        {
            ClearInteractable();
            IsHovering = LockedObjectId != Guid.Empty;
            return;
        }

        // ── Interaction: separate trace, cursor stays on ground ──
        var tr = Scene.Trace.Ray( mouseRay, 5000f )
            .IgnoreGameObjectHierarchy( PlayerObject )
            .Run();

        HoveredObjectId = tr.Hit && tr.GameObject is not null ? tr.GameObject.Id : Guid.Empty;

        if ( tr.Hit )
        {
            var interactable = tr.GameObject?.Components.GetInAncestorsOrSelf<IInteractable>();
            var carryable = tr.GameObject?.Components.GetInAncestorsOrSelf<PhysicsCarryableObject>();

            if ( (IsInteractableValid( interactable ) && interactable.CanInteract( PlayerObject )) || carryable != null )
            {
                if ( interactable != null && _currentInteractable != interactable )
                {
                    UnhighlightCurrent();
                    _currentInteractable = interactable;
                    HighlightCurrent();
                }
                else if ( interactable == null && _currentInteractable != null )
                {
                    UnhighlightCurrent();
                    _currentInteractable = null;
                }
                IsHovering = true;
            }
            else ClearInteractable();
        }
        else ClearInteractable();
    }

    private void HighlightCurrent()
    {
        if ( !IsInteractableValid( _currentInteractable ) ) return;

        if ( _currentInteractable is InteractableObject obj )
            obj.SetHighlighted( true );
    }

    private void UnhighlightCurrent()
    {
        if ( !IsInteractableValid( _currentInteractable ) ) return;

        if ( _currentInteractable is InteractableObject obj )
            obj.SetHighlighted( false );
    }

    private void ClearInteractable()
    {
        UnhighlightCurrent();
        _currentInteractable = null;
        IsHovering = false;
    }

    private void UpdateVisuals()
    {
        Vector3 visualTargetPos = NetCursorPosition;

        // Visual snap logic (Separate from physics target)
        if ( Mode == CursorMode.SurfaceSnap )
        {
            Guid snapId = IsLocked ? LockedObjectId : (IsHovering ? HoveredObjectId : Guid.Empty);
            if ( snapId != Guid.Empty )
            {
                var snapObj = Scene.Directory.FindByGuid( snapId );
                if ( snapObj.IsValid() )
                {
                    var objPos = snapObj.WorldPosition;
                    visualTargetPos = new Vector3( objPos.x, objPos.y, objPos.z + SnapHeight );
                }
            }
        }

        VisualCursorPosition = visualTargetPos;

        // Local player: instant position. Remote: smooth lerp.
        if ( !PlayerObject.Network.IsProxy )
            WorldPosition = VisualCursorPosition;
        else
            WorldPosition = Vector3.Lerp( WorldPosition, VisualCursorPosition, Time.Delta * RemoteLerpSpeed );

        if ( CursorRenderer == null ) return;

        Vector3 targetScale = IsHovering ? HoverScale : NormalScale;
        Color targetColor = IsHovering ? HoverColor : NormalColor;

        CursorRenderer.Tint = Color.Lerp( CursorRenderer.Tint, targetColor, Time.Delta * 15f );
        WorldScale = Vector3.Lerp( WorldScale, targetScale, Time.Delta * 15f );
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
