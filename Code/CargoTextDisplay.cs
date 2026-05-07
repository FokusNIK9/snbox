using Sandbox;

public sealed class CargoTextDisplay : Component
{
    private TextRenderer _textComp;
    private PlayerCargo _cargoData;

    protected override void OnUpdate()
    {
        if ( _textComp == null )
            _textComp = Components.Get<TextRenderer>();

        if ( _cargoData == null )
            _cargoData = Components.GetInAncestorsOrSelf<PlayerCargo>();

        if ( _textComp == null || _cargoData == null )
            return;

        _textComp.Text = $"{_cargoData.CargoCount}";

        if ( Scene.Camera != null )
        {
            WorldRotation = Rotation.LookAt( Scene.Camera.WorldPosition - WorldPosition );
        }
    }
}
