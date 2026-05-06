using Sandbox;
using System;

/// <summary>
/// Health system with network sync, damage handling, death, and respawn.
/// Fully self-contained — does not require other custom components.
/// Optionally disables PlayerUnitController on death if present.
/// </summary>
public sealed class UnitHealth : Component
{
	// ── Configuration ─────────────────────────────────────

	[Property, Group( "Health" ), Range( 1f, 10000f )]
	public float MaxHealth { get; set; } = 100f;

	[Property, Group( "Respawn" )]
	public bool AllowRespawn { get; set; } = true;

	[Property, Group( "Respawn" ), Range( 0.5f, 30f ), ShowIf( "AllowRespawn", true )]
	public float RespawnDelay { get; set; } = 3f;

	[Property, Group( "Respawn" ), ShowIf( "AllowRespawn", true ), Description( "If set, respawns here. Otherwise respawns at death position." )]
	public GameObject RespawnPoint { get; set; }

	[Property, Group( "Visual" ), Description( "Disable model renderers on death" )]
	public bool HideOnDeath { get; set; } = true;

	// ── Network state ─────────────────────────────────────

	[Sync] public float Health { get; set; }
	[Sync] public bool IsAlive { get; set; } = true;

	// ── Events (local, not networked) ─────────────────────

	public Action<float, float> OnHealthChanged { get; set; }
	public Action OnDeath { get; set; }
	public Action OnRespawned { get; set; }

	protected override void OnStart()
	{
		if ( Networking.IsHost )
		{
			Health = MaxHealth;
			IsAlive = true;
		}
	}

	/// <summary>
	/// Apply damage. Non-host callers route through RPC to the server.
	/// </summary>
	public void ApplyDamage( float damage )
	{
		if ( !Networking.IsHost )
		{
			RequestDamageRpc( damage );
			return;
		}

		ApplyDamageInternal( damage );
	}

	[Rpc.Host]
	private void RequestDamageRpc( float damage )
	{
		ApplyDamageInternal( damage );
	}

	private void ApplyDamageInternal( float damage )
	{
		if ( !IsAlive )
			return;

		var oldHealth = Health;
		Health = MathF.Max( Health - damage, 0f );
		OnHealthChanged?.Invoke( oldHealth, Health );

		NotifyDamage( damage );

		if ( Health <= 0f )
		{
			Die();
		}
	}

	/// <summary>
	/// Heal the unit. Clamped to MaxHealth.
	/// </summary>
	public void Heal( float amount )
	{
		if ( !Networking.IsHost || !IsAlive )
			return;

		var oldHealth = Health;
		Health = MathF.Min( Health + amount, MaxHealth );
		OnHealthChanged?.Invoke( oldHealth, Health );
	}

	private void Die()
	{
		IsAlive = false;

		SetUnitControllersActive( false );
		SetRenderersVisible( !HideOnDeath );

		OnDeath?.Invoke();
		NotifyDeath();

		if ( AllowRespawn )
		{
			Invoke( RespawnDelay, Respawn );
		}
	}

	private void Respawn()
	{
		Health = MaxHealth;
		IsAlive = true;

		if ( RespawnPoint.IsValid() )
		{
			WorldPosition = RespawnPoint.WorldPosition;
			GameObject.Network.ClearInterpolation();
		}

		SetUnitControllersActive( true );
		SetRenderersVisible( true );

		OnRespawned?.Invoke();
		NotifyRespawn();
	}

	private void SetUnitControllersActive( bool active )
	{
		var controller = GetComponent<PlayerUnitController>( includeDisabled: true );
		if ( controller is not null )
			controller.Enabled = active;
	}

	private void SetRenderersVisible( bool visible )
	{
		foreach ( var renderer in GetComponentsInChildren<ModelRenderer>() )
		{
			renderer.Enabled = visible;
		}
	}

	[Rpc.Broadcast]
	private void NotifyDamage( float damage )
	{
		// Hook point for VFX / SFX on all clients
	}

	[Rpc.Broadcast]
	private void NotifyDeath()
	{
		// Hook point for death VFX / SFX on all clients
	}

	[Rpc.Broadcast]
	private void NotifyRespawn()
	{
		// Hook point for respawn VFX / SFX on all clients
	}

	protected override void OnValidate()
	{
		MaxHealth = MaxHealth.Clamp( 1f, 10000f );
		RespawnDelay = RespawnDelay.Clamp( 0.5f, 30f );
	}
}
