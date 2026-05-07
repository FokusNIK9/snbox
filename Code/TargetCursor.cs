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

    [Property, Group( "Behavior" ), Description( "GroundPlane = cursor stays on ground, no sticking. SurfaceSnap = cursor follows hit surfaces." )]
    public CursorMode Mode { get; set; } = CursorMode.GroundPlane;

    [Property, Group( "Behavior" ), Description( "Ground plane Z height for GroundPlane mode" )]
    public float GroundHeight { get; set; } = 2f;

    [Property, Group( "Behavior" ), Description( "Cursor visual follow speed (higher = snappier)" )]
    public float CursorSpeed { get; set; } = 25f;

    [Property, Group( "Behavior" ), Description( "Normal offset for SurfaceSnap mode" )]
    public float NormalOffset { get; set; } = 5f;

    [Property, Group( "Behavior" ), Description( "Hover offset for SurfaceSnap mode" )]
    public float HoverOffset { get; set; } = 20f;

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
        UpdateSharedVisualsAndMovement();
    }

    private void UpdateLocalLogic()
    {
        if ( Scene.Camera == null ) return;

        switch ( Mode )
        {
            case CursorMode.GroundPlane:
                UpdateGroundPlane();
                break;
            case CursorMode.SurfaceSnap:
                UpdateSurfaceSnap();
                break;
        }

        HandleInteraction();
    }

    // ── GroundPlane: cursor on ground, interaction via separate trace ──
    private void UpdateGroundPlane()
    {
        var mouseRay = Scene.Camera.ScreenPixelToRay( Mouse.Position );

        // Cursor position: ray vs horizontal plane at GroundHeight
        float denom = mouseRay.Forward.z;
        if ( MathF.Abs( denom ) > 0.0001f )
        {
            float t = ( GroundHeight - mouseRay.Position.z ) / denom;
            if ( t > 0f )
                NetCursorPosition = mouseRay.Position + mouseRay.Forward * t;
        }

        // Interaction detection: separate full trace
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
            }
            else ClearInteractable();
        }
        else ClearInteractable();
    }

    // ── SurfaceSnap: cursor follows hit surface (improved original) ──
    private void UpdateSurfaceSnap()
    {
        var mouseRay = Scene.Camera.ScreenPixelToRay( Mouse.Position );
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
                NetCursorPosition = tr.HitPosition + tr.Normal * HoverOffset;
            }
            else
            {
                ClearInteractable();
                NetCursorPosition = tr.HitPosition + tr.Normal * NormalOffset;
            }
        }
        else ClearInteractable();
    }

    // ── Highlight helpers ──
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

    // ── Hold interaction ──
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

    // ── Shared visuals (runs on all clients) ──
    private void UpdateSharedVisualsAndMovement()
    {
        WorldPosition = Vector3.Lerp( WorldPosition, NetCursorPosition, Time.Delta * CursorSpeed );
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
