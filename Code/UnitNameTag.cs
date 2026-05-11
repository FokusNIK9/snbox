using Sandbox;

/// <summary>
/// Displays the owning player's name above the unit.
/// Syncs the display name across the network via INetworkSpawn.
/// Uses Gizmo overlay for rendering (simple, no UI dependency).
/// </summary>
[Title( "Имя над юнитом" )]
[Category( "Box Collector/Визуал" )]
public sealed class UnitNameTag : Component, Component.INetworkSpawn
{
	// ── Configuration ─────────────────────────────────────

	[Property, Group( "Внешний вид" ), Range( 50f, 300f ), Description( "Высота текста имени над объектом." )]
	public float HeightOffset { get; set; } = 80f;

	[Property, Group( "Поведение" ), Description( "Скрывать имя над локальным игроком." )]
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
