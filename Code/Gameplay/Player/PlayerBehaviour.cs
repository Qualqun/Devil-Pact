using System;
using System.Threading;
using System.Threading.Tasks;


public sealed class PlayerBehaviour : Component, Component.ICollisionListener
{
	[Sync( SyncFlags.FromHost )] public bool isInvulnerable { get; set; }
	[Sync( SyncFlags.FromHost )] public bool isDead { get; set; } = false;
	[Sync( SyncFlags.FromHost )] public bool isPermanentlyDead { get; set; } = false;
	[Sync( SyncFlags.FromHost )] public bool mustDevilPact { get; set; } = false;
	[Sync( SyncFlags.FromHost )] public bool isInStartZone { get; set; } = false;
	[Sync( SyncFlags.FromHost )] public GameManager gameManager { get; set; }

	[Property, Group( "Refs" )] public PlayerState State { get; set; }
	[Property, Group( "Refs" )] public GameObject gunPoint { get; set; }
	[Property, Group( "Refs" )] public PlayerPresentation playerPresentation { get; set; }

	[Property, Group( "Refs" )] GameObject cameraPivot { get; set; }
	[Property, Group( "Refs" )] GameObject body { get; set; }
	[Property, Group( "Refs" )] PlayerUI playerUI { get; set; }
	[Property, Group( "Refs" )] MPlayerController controller { get; set; }

	public DroneInteraction droneInteraction { get; set; }

	PoolManager poolManager { get; set; }
	bool canShoot = true;
	CancellationTokenSource cancellation;

	#region Laser state

	// Written by the owner, read by every machine to draw the beam
	[Sync] public bool LaserFiring { get; set; }
	[Sync] public Vector3 LaserEnd { get; set; }

	const float LaserRange = 3000f;
	const float LaserRadius = 12f;
	const float LaserTicksPerSecond = 15f;
	const float LaserShootFxInterval = 0.25f;
	const string LaserHitFxPrefab = "art/fx/prefab/gunhit.prefab";

	// Many small ticks would flood the screen with numbers, so each enemy's ticks are summed and shown this often
	const float LaserNumberInterval = 0.25f;

	static readonly string[] LaserIgnoredTags = { "player", "ground", "bullet" };

	bool laserTriggerHeld;
	TimeUntil nextLaserTick;
	TimeSince sinceLaserTick;
	TimeUntil nextLaserShootFx;

	class LaserHitNumber
	{
		public float Damage;
		public Vector3 Position;
		public TimeSince SinceShown;
	}

	readonly Dictionary<GameObject, LaserHitNumber> laserHitNumbers = new();

	// Heat gauge: firing drains it, releasing refills it after a short delay.
	// Draining it completely overheats the laser, which then stays locked until the gauge is full again.
	const float LaserDrainTime = 3f;
	const float LaserRefillTime = 2f;
	const float LaserRefillDelay = 0.3f;

	[Description( "Laser charge left, 1 is full. Local to the owner, read by its HUD." )]
	public float LaserCharge { get; private set; } = 1f;
	public bool LaserOverheated { get; private set; }

	TimeSince sinceLaserFired;

	#endregion

	protected override void OnStart()
	{
		if ( State == null )
			State = Components.Get<PlayerState>();

		droneInteraction = GetComponentInChildren<DroneInteraction>();

		if ( IsProxy )
		{
			cameraPivot.Destroy();
			controller.Destroy();
			playerUI.Destroy();
		}
		else
		{
			playerUI.GameState = gameManager.GameState;
			playerUI?.SetHealth( State.Hp, State.MaxHp );
			playerUI?.ShowArrowToWorldPosition( gameManager.StartZonePoint.WorldPosition );

			poolManager = gameManager.PoolManager;

		}

		// Every machine draws this player's beam
		PlayerLaserBeam.Create( this, gunPoint );

		base.OnStart();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( gameManager.GameState.State == GameStateType.Playing )
		{
			playerUI?.HideWorldArrow();
		}

		if ( !IsProxy )
			UpdateLaser();
	}

	#region Laser

	// Fire() is called every frame the button is down, so the trigger counts as released when a frame skips it
	void UpdateLaser()
	{
		bool firing = laserTriggerHeld && State.HasLaser && canShoot && !isDead && !LaserOverheated;
		laserTriggerHeld = false;

		if ( firing )
		{
			sinceLaserFired = 0f;
			LaserCharge = MathF.Max( 0f, LaserCharge - Time.Delta / LaserDrainTime );

			if ( LaserCharge <= 0f )
			{
				LaserOverheated = true;
				firing = false;
			}
		}
		else if ( sinceLaserFired > LaserRefillDelay )
		{
			LaserCharge = MathF.Min( 1f, LaserCharge + Time.Delta / LaserRefillTime );

			if ( LaserCharge >= 1f )
				LaserOverheated = false;
		}

		LaserFiring = firing;

		if ( !firing )
		{
			FlushLaserHitNumbers( Vector3.Zero, true );
			return;
		}

		Vector3 start = gunPoint.WorldPosition;
		Vector3 direction = (Rotation.FromYaw( body.WorldRotation.Yaw() ) * Vector3.Forward).WithZ( 0 ).Normal;
		Vector3 end = start + direction * LaserRange;

		var hits = Scene.Trace.Sphere( LaserRadius, start, end )
			.WithoutTags( LaserIgnoredTags )
			.RunAll()
			.OrderBy( hit => hit.Distance )
			.ToList();

		// The beam stops on the first thing it meets, enemy or wall, and only an enemy takes damage
		GameObject target = null;
		Vector3 targetPosition = default;

		foreach ( SceneTraceResult hit in hits )
		{
			GameObject hitObj = hit.Collider?.GameObject;

			if ( hitObj == null )
				continue;

			end = hit.HitPosition;

			if ( hit.HasTag( "enemy" ) )
			{
				target = hitObj;
				targetPosition = hit.HitPosition;
			}

			break;
		}

		LaserEnd = end;

		if ( nextLaserShootFx <= 0f )
		{
			nextLaserShootFx = LaserShootFxInterval;
			playerPresentation.speedShoot = State.FireRate;
			playerPresentation.Shoot( gunPoint );
		}

		if ( nextLaserTick > 0f )
		{
			FlushLaserHitNumbers( direction, false );
			return;
		}

		// Damage follows the time really spent since the last tick, so frame timing never eats into the DPS
		float tickTime = MathF.Min( sinceLaserTick, 1f / LaserTicksPerSecond );
		float tickDamage = State.LaserDps * tickTime;

		nextLaserTick = 1f / LaserTicksPerSecond;
		sinceLaserTick = 0f;

		if ( target != null )
		{
			gameManager.EnemyTakeDamage( target, tickDamage );
			State.AddDamageDealt( tickDamage );

			if ( !laserHitNumbers.TryGetValue( target, out var number ) )
			{
				// A new target shows its first number right away
				number = new LaserHitNumber { SinceShown = LaserNumberInterval };
				laserHitNumbers[target] = number;
			}

			number.Damage += tickDamage;
			number.Position = targetPosition;
		}

		FlushLaserHitNumbers( direction, false );
	}

	// Shows each enemy's summed laser damage as one number, with the impact FX, every LaserNumberInterval
	void FlushLaserHitNumbers( Vector3 direction, bool all )
	{
		if ( laserHitNumbers.Count == 0 )
			return;

		foreach ( var enemy in laserHitNumbers.Keys.ToList() )
		{
			var number = laserHitNumbers[enemy];
			bool valid = enemy.IsValid();

			if ( valid && number.Damage > 0f && (all || number.SinceShown >= LaserNumberInterval) )
			{
				gameManager.UiManager.HitNumbers.ShowNumberForEnemy( number.Damage, number.Position, enemy );

				if ( !all )
					GameObject.GetPrefab( LaserHitFxPrefab )?.Clone( number.Position, Rotation.LookAt( -direction ) );

				number.Damage = 0f;
				number.SinceShown = 0f;
			}

			// Forget enemies that died or left the beam once their last number is out
			if ( !valid || all || (number.Damage <= 0f && number.SinceShown > LaserNumberInterval * 2f) )
				laserHitNumbers.Remove( enemy );
		}
	}

	#endregion

	BulletInfo InitBaseBullet()
	{
		Vector3 direction = (Rotation.FromYaw( body.WorldRotation.Yaw() ) * Vector3.Forward).WithZ( 0 ).Normal;

		BulletInfo newBulletInfo = new BulletInfo
		{
			size = State.BulletSize,
			speed = State.BulletSpeed,
			damage = State.BulletDamage,
			direction = direction,
			onAirModifier = new List<OnAirModifier>(),
			endModifiers = new List<EndModifier>(),
			shooter = State
		};

		foreach ( ModifierType mod in State.ActiveModifiers )
		{
			if ( mod == ModifierType.HomingShot )
				newBulletInfo.AddAirModifier( new HomingShot() );

			if ( mod == ModifierType.Bounce )
				newBulletInfo.AddEndModifiers( new Bounce() );

			if ( mod == ModifierType.Percing )
				newBulletInfo.AddEndModifiers( new Percing() );

			if ( mod == ModifierType.Explosion )
				newBulletInfo.AddEndModifiers( new EndExplosion() );
		}

		return newBulletInfo;
	}

	async Task StartTimer( CancellationToken token )
	{
		float fireRate = (1f / State.FireRate).Clamp( 0.01f, float.MaxValue );
		canShoot = false;
		await Task.DelaySeconds( fireRate );

		if ( token.IsCancellationRequested )
			return;

		canShoot = true;
	}

	async Task TimerHit()
	{
		Broadcast_SetInvulnerability( true );

		await Task.DelaySeconds( State.TimeInvulnerability );

		Broadcast_SetInvulnerability( false );
	}

	public void OnCollisionStart( Collision collision )
	{
		if ( IsProxy )
			return;

		GameObject otherObj = collision.Other.Collider.GameObject;

		if ( otherObj.Tags.Has( "enemy" ) )
			gameManager.PlayerTakeDamageFromEnemy( this, otherObj );
	}

	public void Fire()
	{
		// The laser fires continuously from UpdateLaser while the button is held
		if ( State.HasLaser )
		{
			laserTriggerHeld = true;
			return;
		}

		// canShoot is local, a pending fire rate timer would hand the gun back to a ghost
		if ( canShoot && !isDead )
		{
			List<BulletInfo> bullets = new();
			List<GunOutPut> gunModifiers = new();

			BulletInfo baseBullet = InitBaseBullet();

			playerPresentation.speedShoot = State.FireRate;
			playerPresentation.Shoot( gunPoint );

			bullets.Add( baseBullet );

			foreach ( ModifierType mod in State.ActiveModifiers )
			{
				if ( mod == ModifierType.Shotgun )
				{
					GunOutPut shotgunMod = new ShotgunOutPut();
					bool levelUp = false;

					foreach ( GunOutPut modifier in gunModifiers )
					{
						if ( modifier.modifierType == shotgunMod.modifierType )
						{
							modifier.AddLevel();
							levelUp = true;
							break;
						}
					}

					if ( !levelUp )
					{
						gunModifiers.Add( shotgunMod );
					}
				}
			}

			foreach ( GunOutPut mod in gunModifiers )
			{
				List<BulletInfo> newBullets = new();
				mod.OutPutBehaviour( baseBullet, newBullets );
				bullets = newBullets;
			}

			foreach ( BulletInfo bulletInfo in bullets )
			{
				GameObject newBullet = poolManager.playerBullet.GetObject();
				PlayerBullet bulletBehaviour = newBullet.GetComponent<PlayerBullet>();

				newBullet.WorldPosition = gunPoint.WorldPosition;

				bulletBehaviour.InitBall( bulletInfo, gameManager );
				bulletBehaviour.poolManager = poolManager;
			}

			cancellation = new CancellationTokenSource();
			_ = StartTimer( cancellation.Token );
		}
	}

	public void SetDead( bool permanent = false )
	{
		playerPresentation.DeadTint( permanent );

		Tags.Add( "invulnerability" );

		canShoot = false;
	}


	#region RPC Broadcast

	[Rpc.Broadcast]
	public void Broadcast_RefreshHealth( float hp, float maxHp )
	{
		playerUI?.SetHealth( hp, maxHp );
	}

	[Rpc.Broadcast]
	public void Broadcast_AddExperience( int amount )
	{
		bool levelUp = State.AddExperience( amount );

		if ( levelUp && !IsProxy )
			State.AddLevelGained();

		playerUI.ShowExperienceNotification( levelUp );
	}

	[Rpc.Broadcast]
	public void Broadcast_ShowLifeUP()
	{
		//bool levelUp = State.AddExperience( amount );

		playerUI.ShowLifeNotification();
	}


	[Rpc.Broadcast]
	public void Broadcast_Revive( float hp, float maxHp )
	{
		canShoot = true;
		Tags.Remove( "invulnerability" );

		playerPresentation.ResetTint();

		playerUI?.SetHealth( hp, maxHp );
	}

	[Rpc.Broadcast]
	public void Broadcast_FullReset( Vector3 spawnPosition )
	{
		canShoot = true;
		Tags.Remove( "invulnerability" );

		playerPresentation.ResetTint();

		if ( !IsProxy )
		{
			WorldPosition = spawnPosition;

			Rigidbody body = Components.Get<Rigidbody>();

			if ( body != null )
			{
				body.Velocity = Vector3.Zero;
				body.AngularVelocity = Vector3.Zero;
			}

			playerUI?.SetHealth( State.SpawnMaxHp, State.SpawnMaxHp );

			if ( gameManager?.StartZonePoint != null )
				playerUI?.ShowArrowToWorldPosition( gameManager.StartZonePoint.WorldPosition );
		}
	}

	[Rpc.Broadcast]
	public void Broadcast_SetInvulnerability( bool mode, bool dash = false )
	{
		if ( !isDead )
		{
			playerPresentation.InvulnerabilityTint( mode, dash );
			isInvulnerable = mode;

			if ( mode )
			{
				Tags.Add( "invulnerability" );
			}
			else
			{
				Tags.Remove( "invulnerability" );
			}
		}
	}

	[Rpc.Broadcast]
	public void Broadcast_SetDead( bool permanent )
	{
		SetDead( permanent );

		if ( !IsProxy )
			State.AddDeath();
	}

	#endregion

	#region Server methods

	public void Server_TakeHit( float amount )
	{
		if ( !Networking.IsHost )
			return;

		if ( isDead || isInvulnerable )
			return;

		State.Hp = MathF.Max( 0f, State.Hp - amount );
		Broadcast_RefreshHealth( State.Hp, State.MaxHp );

		if ( State.Hp <= 0f )
		{
			Server_Die();
		}
		else
		{
			Broadcast_SetInvulnerability( true );
			_ = TimerHit();
		}
	}
	public void Server_Die()
	{
		if ( !Networking.IsHost )
			return;

		if ( isDead )
			return;

		isDead = true;
		State.Hp = 0f;

		State.LoseLife();
		State.LoseHalfLevels();

		isPermanentlyDead = State.Lives <= 0;
		mustDevilPact = !isPermanentlyDead;

		cancellation?.Cancel();
		cancellation?.Dispose();
		cancellation = null;

		Broadcast_SetDead( isPermanentlyDead );
		Broadcast_RefreshHealth( State.Hp, State.MaxHp );

		gameManager?.Server_OnPlayerDied( this );
	}

	public void Server_Revive()
	{
		if ( !Networking.IsHost )
			return;

		if ( isPermanentlyDead )
			return;

		isDead = false;
		isInvulnerable = false;
		State.Hp = State.MaxHp;

		Broadcast_Revive( State.Hp, State.MaxHp );
	}

	public void Server_FullReset( Vector3 spawnPosition )
	{
		if ( !Networking.IsHost )
			return;

		isDead = false;
		isPermanentlyDead = false;
		mustDevilPact = false;
		isInvulnerable = false;
		isInStartZone = false;

		State.Broadcast_ResetAll();
		Broadcast_FullReset( spawnPosition );
	}
	#endregion
}
