using Sandbox;

public sealed class CargoTextDisplay : Component
{
    private TextRenderer _textComp;
    private PlayerCargo _cargoData;
    private bool _loggedFound;

    protected override void OnUpdate()
    {
        if ( _textComp == null )
        {
            _textComp = Components.Get<TextRenderer>();
            if ( _textComp != null )
                Log.Info( $"[CargoTextDisplay] TextRenderer found on {GameObject.Name}" );
        }

        if ( _cargoData == null )
        {
            _cargoData = Components.GetInAncestorsOrSelf<PlayerCargo>();
            if ( _cargoData == null )
            {
                // Fallback: search from root
                _cargoData = GameObject.Root.Components.GetInChildrenOrSelf<PlayerCargo>();
            }
            if ( _cargoData != null )
                Log.Info( $"[CargoTextDisplay] PlayerCargo found! CargoCount = {_cargoData.CargoCount}" );
        }

        if ( _textComp == null || _cargoData == null )
        {
            if ( !_loggedFound )
            {
                Log.Warning( $"[CargoTextDisplay] Missing: TextRenderer={_textComp != null}, PlayerCargo={_cargoData != null}" );
                _loggedFound = true;
            }
            return;
        }

        if ( !_loggedFound )
        {
            Log.Info( $"[CargoTextDisplay] All components found, rendering cargo count." );
            _loggedFound = true;
        }

        _textComp.Text = $"{_cargoData.CargoCount}";

        if ( Scene.Camera != null )
        {
            WorldRotation = Rotation.LookAt( Scene.Camera.WorldPosition - WorldPosition );
        }
    }

    protected override void DrawGizmos()
    {
        if ( _cargoData == null ) return;

        Gizmo.Draw.Color = Color.Yellow;
        Gizmo.Draw.Text( $"Cargo: {_cargoData.CargoCount}", new Transform( Vector3.Up * 10f ) );
    }
}
