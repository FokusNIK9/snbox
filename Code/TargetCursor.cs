using Sandbox;

public sealed class TargetCursor : Component
{
    [Property] public ModelRenderer CursorRenderer { get; set; }
    [Property] public Color NormalColor { get; set; } = Color.White;
    [Property] public Color HoverColor { get; set; } = Color.Green;
    [Property] public Vector3 NormalScale { get; set; } = Vector3.One;
    [Property] public Vector3 HoverScale { get; set; } = new Vector3( 1.5f );
    [Property] public float NormalOffset { get; set; } = 5f;
    [Property] public float HoverOffset { get; set; } = 20f;

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

    private void UpdateSharedVisualsAndMovement()
    {
        WorldPosition = Vector3.Lerp( WorldPosition, NetCursorPosition, Time.Delta * 20f );
        if ( CursorRenderer == null ) return;

        Vector3 targetScale = IsHovering ? HoverScale : NormalScale;
        Color targetColor = IsHovering ? HoverColor : NormalColor;

        if ( !PlayerObject.Network.IsProxy && IsHovering && _holdTimer > 0f )
        {
            float progress = _holdTimer / _currentInteractable.InteractionTime;
            targetScale = Vector3.Lerp( HoverScale, NormalScale * 1.1f, progress );
        }

        CursorRenderer.Tint = Color.Lerp( CursorRenderer.Tint, targetColor, Time.Delta * 15f );
        WorldScale = Vector3.Lerp( WorldScale, targetScale, Time.Delta * 15f );
    }
}
