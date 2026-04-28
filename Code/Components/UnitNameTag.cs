using Sandbox;

/// <summary>
/// Displays the owning player's name above the unit.
/// Syncs the display name across the network via INetworkSpawn.
/// Uses Gizmo overlay for rendering (simple, no UI dependency).
/// </summary>
public sealed class UnitNameTag : Component, Component.INetworkSpawn
{
	// ── Configuration ─────────────────────────────────────

	[Property, Group( "Appearance" ), Range( 50f, 300f )]
	public float HeightOffset { get; set; } = 80f;

	[Property, Group( "Behavior" ), Description( "Hide name tag for the local player" )]
	public bool HideForOwner { get; set; } = false;

	// ── Network state ─────────────────────────────────────

	[Sync] public string DisplayName { get; set; } = "";

	public void OnNetworkSpawn( Connection owner )
	{
		if ( owner is not null )
		{
			DisplayName = owner.DisplayName;
		}
	}

	protected override void DrawGizmos()
	{
		if ( !Active )
			return;

		if ( HideForOwner && !IsProxy )
			return;

		if ( string.IsNullOrEmpty( DisplayName ) )
			return;

		Gizmo.Draw.Color = Color.White;
		Gizmo.Draw.Text( DisplayName, new Transform( Vector3.Up * HeightOffset ) );
	}

	protected override void OnValidate()
	{
		HeightOffset = HeightOffset.Clamp( 50f, 300f );
	}
}
