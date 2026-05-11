using Sandbox;

public sealed class CargoBackpackAnchor : Component
{
	[Property, Group( "Follow" )]
	public Vector3 LocalOffset { get; set; } = new Vector3( -18.9f, 0f, 32f );

	[Property, Group( "Physics" )]
	public bool ForceTriggerColliders { get; set; } = true;

	protected override void OnStart()
	{
		if ( ForceTriggerColliders )
		{
			var collider = Components.Get<BoxCollider>();
			if ( collider is not null )
				collider.IsTrigger = true;
		}
	}

	protected override void OnUpdate()
	{
		LocalPosition = LocalOffset;
		LocalRotation = Rotation.Identity;
	}
}
