using Sandbox;
using System;

public enum CursorMode
{
    GroundPlane,
    SurfaceSnap
}

public sealed class TargetCursor : Component
{
    [Property] public ModelRenderer CursorRenderer { get; set; }
    [Property] public Color NormalColor { get; set; } = Color.White;
    [Property] public Color HoverColor { get; set; } = Color.Green;
    [Property] public Vector3 NormalScale { get; set; } = Vector3.One;
    [Property] public Vector3 HoverScale { get; set; } = new Vector3( 1.5f );

    [Property, Group( "Behavior" ), Description( "GroundPlane = cursor on ground, never sticks to objects. SurfaceSnap = cursor snaps to object center when hovering." )]
    public CursorMode Mode { get; set; } = CursorMode.GroundPlane;

    [Property, Group( "Behavior" ), Description( "Ground plane Z height (match your floor height)" )]
    public float GroundHeight { get; set; } = 2f;

    [Property, Group( "Behavior" ), Description( "Lerp speed for remote players only (local = instant)" )]
    public float RemoteLerpSpeed { get; set; } = 25f;

    [Property, Group( "Behavior" ), Description( "SurfaceSnap: height above object center when hovering" )]
    public float SnapHeight { get; set; } = 30f;

    [Sync] public Vector3 NetCursorPosition { get; set; }
    [Sync] public bool IsHovering { get; set; }

    private GameObject PlayerObject;
    private IInteractable _currentInteractable;
    private float _holdTimer;

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

        // ── Interaction: separate trace, cursor stays on ground ──
        var tr = Scene.Trace.Ray( mouseRay, 5000f )
            .IgnoreGameObjectHierarchy( PlayerObject )
            .Run();

        if ( tr.Hit )
        {
            var interactable = tr.GameObject?.Components.GetInAncestorsOrSelf<IInteractable>();
            if ( interactable != null && interactable.CanInteract( PlayerObject ) )
            {
                if ( _currentInteractable != interactable )
                {
                    UnhighlightCurrent();
                    _currentInteractable = interactable;
                    _holdTimer = 0f;
                    HighlightCurrent();
                }
                IsHovering = true;

                // SurfaceSnap: override cursor to object center
                if ( Mode == CursorMode.SurfaceSnap )
                {
                    var objPos = tr.GameObject.WorldPosition;
                    NetCursorPosition = new Vector3( objPos.x, objPos.y, objPos.z + SnapHeight );
                }
            }
            else ClearInteractable();
        }
        else ClearInteractable();

        HandleInteraction();
    }

    private void HighlightCurrent()
    {
        if ( _currentInteractable is InteractableObject obj )
            obj.SetHighlighted( true );
    }

    private void UnhighlightCurrent()
    {
        if ( _currentInteractable is InteractableObject obj )
            obj.SetHighlighted( false );
    }

    private void ClearInteractable()
    {
        UnhighlightCurrent();
        _currentInteractable = null;
        _holdTimer = 0f;
        IsHovering = false;
    }

    private void HandleInteraction()
    {
        if ( _currentInteractable == null ) return;

        if ( Input.Down( "attack1" ) )
        {
            _holdTimer += Time.Delta;

            if ( _currentInteractable is InteractableObject holdObj )
            {
                float progress = _holdTimer / _currentInteractable.InteractionTime;
                holdObj.SetHoldProgress( progress );
            }

            if ( _holdTimer >= _currentInteractable.InteractionTime )
            {
                _currentInteractable.OnInteract( PlayerObject.Id );
                _holdTimer = 0f;
            }
        }
        else
        {
            _holdTimer = 0f;
            HighlightCurrent();
        }
    }

    private void UpdateVisuals()
    {
        // Local player: instant position. Remote: smooth lerp.
        if ( !PlayerObject.Network.IsProxy )
            WorldPosition = NetCursorPosition;
        else
            WorldPosition = Vector3.Lerp( WorldPosition, NetCursorPosition, Time.Delta * RemoteLerpSpeed );

        if ( CursorRenderer == null ) return;

        Vector3 targetScale = IsHovering ? HoverScale : NormalScale;
        Color targetColor = IsHovering ? HoverColor : NormalColor;

        if ( !PlayerObject.Network.IsProxy && IsHovering && _holdTimer > 0f && _currentInteractable != null )
        {
            float progress = _holdTimer / _currentInteractable.InteractionTime;
            targetScale = Vector3.Lerp( HoverScale, NormalScale * 1.1f, progress );
        }

        CursorRenderer.Tint = Color.Lerp( CursorRenderer.Tint, targetColor, Time.Delta * 15f );
        WorldScale = Vector3.Lerp( WorldScale, targetScale, Time.Delta * 15f );
    }
}
