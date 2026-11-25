using Sandbox;
using System;

/// <summary>
/// The Laser power: a continuous beam fired while the button is held, with a heat gauge that
/// overheats when drained. Lives on the player prefab next to PlayerBehaviour, which only forwards
/// Fire() here once the player has the Laser.
/// </summary>
public sealed class PlayerLaser : Component
{
	[Property, Group( "Refs" )] public PlayerBehaviour Player { get; set; }

	#region Beam

	// Written by the owner, read by every machine to draw the beam
	[Sync] public bool IsFiring { get; set; }
	[Sync] public Vector3 BeamEnd { get; set; }

	const float Range = 3000f;
	const float Radius = 12f;
	const float TicksPerSecond = 15f;
	const float ShootFxInterval = 0.25f;
	const string HitFxPrefab = "art/fx/prefab/gunhit.prefab";

	static readonly string[] IgnoredTags = { "player", "ground", "bullet" };

	bool triggerHeld;
	TimeUntil nextTick;
	TimeSince sinceTick;
	TimeUntil nextShootFx;

	#endregion

	#region Hit numbers

	// Many small ticks would flood the screen with numbers, so each enemy's ticks are summed and shown this often
	const float NumberInterval = 0.25f;

	class HitNumber
	{
		public float Damage;
		public Vector3 Position;
		public TimeSince SinceShown;
	}

	readonly Dictionary<GameObject, HitNumber> hitNumbers = new();

	#endregion

	#region Heat gauge

	// Firing drains the gauge, releasing refills it after a short delay.
	// Draining it completely overheats the laser, which then stays locked until the gauge is full again.
	const float DrainTime = 3f;
	const float RefillTime = 2f;
	const float RefillDelay = 0.3f;

	[Description( "Charge left, 1 is full. Local to the owner, read by its HUD." )]
	public float Charge { get; private set; } = 1f;
	public bool Overheated { get; private set; }

	TimeSince sinceFired;

	#endregion

	PlayerState State => Player?.State;

	protected override void OnStart()
	{
		base.OnStart();

		Player ??= Components.Get<PlayerBehaviour>();

		// Every machine draws this player's beam
		PlayerLaserBeam.Create( this, Player.gunPoint );
	}

	/// <summary>
	/// Called every frame the fire button is down, so the trigger counts as released when a frame skips it.
	/// </summary>
	public void Fire()
	{
		triggerHeld = true;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( !IsProxy )
			UpdateBeam();
	}

	void UpdateBeam()
	{
		bool firing = triggerHeld && State.HasLaser && Player.CanShoot && !Overheated;
		triggerHeld = false;

		UpdateHeat( ref firing );

		IsFiring = firing;

		if ( !firing )
		{
			FlushHitNumbers( Vector3.Zero, true );
			return;
		}

		Vector3 start = Player.gunPoint.WorldPosition;
		Vector3 direction = Player.AimDirection;
		var (end, target) = Trace( start, direction );

		BeamEnd = end;

		if ( nextShootFx <= 0f )
		{
			nextShootFx = ShootFxInterval;
			Player.playerPresentation.speedShoot = State.FireRate;
			Player.playerPresentation.Shoot( Player.gunPoint );
		}

		if ( nextTick <= 0f )
			Tick( target, end );

		FlushHitNumbers( direction, false );
	}

	void UpdateHeat( ref bool firing )
	{
		if ( firing )
		{
			sinceFired = 0f;
			Charge = MathF.Max( 0f, Charge - Time.Delta / DrainTime );

			if ( Charge <= 0f )
			{
				Overheated = true;
				firing = false;
			}
		}
		else if ( sinceFired > RefillDelay )
		{
			Charge = MathF.Min( 1f, Charge + Time.Delta / RefillTime );

			if ( Charge >= 1f )
				Overheated = false;
		}
	}

	// The beam stops on the first thing it meets, enemy or wall, and only an enemy takes damage
	(Vector3 End, GameObject Target) Trace( Vector3 start, Vector3 direction )
	{
		var hits = Scene.Trace.Sphere( Radius, start, start + direction * Range )
			.WithoutTags( IgnoredTags )
			.RunAll()
			.OrderBy( hit => hit.Distance );

		foreach ( SceneTraceResult hit in hits )
		{
			GameObject hitObj = hit.Collider?.GameObject;

			if ( hitObj == null )
				continue;

			return (hit.HitPosition, hit.HasTag( "enemy" ) ? hitObj : null);
		}

		return (start + direction * Range, null);
	}

	void Tick( GameObject target, Vector3 position )
	{
		// Damage follows the time really spent since the last tick, so frame timing never eats into the DPS
		float damage = State.LaserDps * MathF.Min( sinceTick, 1f / TicksPerSecond );

		nextTick = 1f / TicksPerSecond;
		sinceTick = 0f;

		if ( target == null )
			return;

		Player.gameManager.EnemyTakeDamage( target, damage );
		State.AddDamageDealt( damage );

		if ( !hitNumbers.TryGetValue( target, out var number ) )
		{
			// A new target shows its first number right away
			number = new HitNumber { SinceShown = NumberInterval };
			hitNumbers[target] = number;
		}

		number.Damage += damage;
		number.Position = position;
	}

	// Shows each enemy's summed damage as one number, with the impact FX, every NumberInterval
	void FlushHitNumbers( Vector3 direction, bool all )
	{
		if ( hitNumbers.Count == 0 )
			return;

		foreach ( var enemy in hitNumbers.Keys.ToList() )
		{
			var number = hitNumbers[enemy];
			bool valid = enemy.IsValid();

			if ( valid && number.Damage > 0f && (all || number.SinceShown >= NumberInterval) )
			{
				Player.gameManager.UiManager.HitNumbers.ShowNumberForEnemy( number.Damage, number.Position, enemy );

				if ( !all )
					GameObject.GetPrefab( HitFxPrefab )?.Clone( number.Position, Rotation.LookAt( -direction ) );

				number.Damage = 0f;
				number.SinceShown = 0f;
			}

			// Forget enemies that died or left the beam once their last number is out
			if ( !valid || all || (number.Damage <= 0f && number.SinceShown > NumberInterval * 2f) )
				hitNumbers.Remove( enemy );
		}
	}
}
