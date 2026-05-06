using Sandbox;

public sealed class CargoTextDisplay : Component
{
    public TextRenderer TextComp { get; set; }
    private PlayerCargo _cargoData;

    protected override void OnStart()
    {
        TextComp = Components.Get<TextRenderer>();
        _cargoData = Components.GetInAncestorsOrSelf<PlayerCargo>();
    }

    protected override void OnUpdate()
    {
        if ( TextComp != null && _cargoData != null )
        {
            TextComp.Text = $"{_cargoData.CargoCount}";
            if ( Scene.Camera != null )
            {
                WorldRotation = Rotation.LookAt( Scene.Camera.WorldPosition - WorldPosition );
            }
        }
    }
}
