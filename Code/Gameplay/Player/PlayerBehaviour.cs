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


	public PoolManager poolManager { get; set; }
	bool canShoot = true;
	CancellationTokenSource cancellation;

	protected override void OnStart()
	{
		if ( State == null )
			State = Components.Get<PlayerState>();


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
		}

		base.OnStart();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( gameManager.GameState.State == GameStateType.Playing )
		{
			playerUI?.HideWorldArrow();
		}
	}

	BulletInfo InitBaseBullet()
	{
		Vector3 direction = Rotation.FromYaw( body.WorldRotation.Yaw() ) * Vector3.Forward;

		direction = direction.WithZ( 0 ).Normal;

		BulletInfo newBulletInfo = new BulletInfo
		{
			size = State.BulletSize,
			speed = State.BulletSpeed,
			damage = State.BulletDamage,
			direction = direction,
			onAirModifier = new List<OnAirModifier>(),
			endModifiers = new List<EndModifier>()
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
				GameObject newBullet = poolManager.GetBullet();
				BulletBehaviour bulletBehaviour = newBullet.GetComponent<BulletBehaviour>();

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
