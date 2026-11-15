using System;
using System.Threading;
using System.Threading.Tasks;

public sealed class MainBoss : BaseEnemyBehaviour
{
	#region Bombing values

	[Property, Group( "Bombing" ), Order( 40 )] float bombingDuration { get; set; } = 4f;
	[Property, Group( "Bombing" ), Order( 40 )] float arcHeight { get; set; } = 400f;
	[Property, Group( "Bombing" ), Order( 40 )] float durationShellTravel { get; set; } = 2f;
	[Property, Group( "Bombing" ), Order( 40 )] float rangeAttack { get; set; } = 300f;
	[Property, Group( "Bombing" ), Order( 40 )] int nbShell { get; set; } = 4;

	[Property, Group( "Refs" )] GameObject mortarPoint { get; set; }
	[Property, Group( "Refs" )] GameObject mortarShell { get; set; }

	#endregion

	#region Dash values
	[Property, Group( "Dash" ), Order( 50 )] float dashPaternDuration { get; set; } = 4f;
	[Property, Group( "Dash" ), Order( 50 )] float dashDistance { get; set; } = 100f;
	[Property, Group( "Dash" ), Order( 50 )] float dashSpeed { get; set; } = 100f;
	[Property, Group( "Dash" ), Order( 50 )] int nbDash { get; set; } = 1;

	[Property, Group( "Dash" ), Order( 50 )] float rotationSpeed { get; set; } = 100f;
	[Property, Group( "Dash" ), Order( 50 )] int fireRate { get; set; } = 5;

	[Property, Group( "Refs" )] GameObject pivotGunPoint { get; set; }

	#endregion

	#region Default Patern value

	#endregion

	[Property, Group( "Stats" )] float bulletSpeed { get; set; } = 180f;
	[Property, Group( "Stats" )] float bulletDamage { get; set; } = 6f;

	[Property, Group( "Refs" )] GameObject gunPoint { get; set; }
	[Property, Group( "Refs" )] GameObject bullet { get; set; }
	[Property, Group( "Refs" )] CapsuleCollider collider { get; set; }

	[Property, Group( "Debug" ), Order( 1000 )] bool showGizmo = true;

	Task paternTask = null;
	bool canMove = true;

	protected override void OnUpdate()
	{
		if ( paternTask == null )
		{
			base.OnUpdate();

			if ( Vector3.DistanceBetween( target.WorldPosition, WorldPosition ) <= rangeAttack )
			{
				ChoosePatern();
			}
		}

		if ( canMove )
		{
			FollowPlayer();
		}

	}
	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		if ( showGizmo )
		{
			Gizmo.Transform = global::Transform.Zero;
			Gizmo.Draw.LineSphere( WorldPosition + Vector3.Up * arcHeight, 32f );

			Gizmo.Draw.Color = Color.Orange;
			Gizmo.Draw.LineSphere( WorldPosition, rangeAttack );

		}

	}

	void Shoot()
	{
		Vector3 dir = (gunPoint.WorldPosition - pivotGunPoint.WorldPosition).Normal;

		GameObject newBullet = bullet.Clone( gunPoint.WorldPosition, Rotation.LookAt( dir ) );
		EnemyBullet bulletBehaviour = newBullet.GetComponent<EnemyBullet>();

		bulletBehaviour.speed = bulletSpeed;
		bulletBehaviour.damage = bulletDamage;
		bulletBehaviour.direction = dir;
		bulletBehaviour.gameManager = gameManager;

		newBullet.NetworkSpawn();
	}
	void ChoosePatern()
	{
		paternTask = DashPatern();
	}
	void StopMove()
	{
		canMove = false;
		agent.Stop();
	}

	#region Bombing methods
	void MortarShoot()
	{
		GameObject newShell = mortarShell.Clone( mortarPoint.WorldPosition );
		EnemyShell enemyShell = newShell.GetComponent<EnemyShell>();

		enemyShell.InitProjectile( target.WorldPosition, arcHeight, durationShellTravel );

		newShell.NetworkSpawn();
	}

	async Task BombingPatern()
	{
		float durationShoot = bombingDuration / nbShell;

		StopMove();

		for ( int i = 0; i < nbShell; i++ )
		{
			await Task.DelaySeconds( durationShoot );

			MortarShoot();
		}


		paternTask = null;
		canMove = true;
	}

	#endregion

	#region Dash methods


	async Task FireCover( CancellationToken token )
	{
		float timer = 0;
		float shootTimer = 1f / Math.Clamp( fireRate, 1, int.MaxValue );

		while ( !token.IsCancellationRequested )
		{
			timer += Time.Delta;

			pivotGunPoint.WorldRotation = Rotation.FromYaw( rotationSpeed * Time.Delta ) * pivotGunPoint.WorldRotation;


			if ( timer > shootTimer )
			{
				Shoot();
				timer = 0;
			}

			await Task.FrameEnd();
		}
	}

	async Task Dash()
	{
		Vector3 startPosition = WorldPosition;
		Vector3 dirDash = (target.WorldPosition - startPosition).WithZ( 0 ).Normal;

		Ray ray = new Ray( WorldPosition + Vector3.Up * collider.Radius, dirDash );
		SceneTraceResult hit = Scene.Trace.Sphere( collider.Radius, ray, dashDistance ).WithoutTags( "enemy", "ground", "player" ).Run();

		Vector3 endPosition = hit.EndPosition.WithZ( startPosition.z );

		float dashDuration = Vector3.DistanceBetween( startPosition, endPosition ) / dashSpeed;
		float timer = 0;

		while ( timer < dashDuration )
		{
			timer += Time.Delta;

			agent.SetAgentPosition( Vector3.Lerp( startPosition, endPosition, timer / dashDuration ) );

			await Task.FrameEnd();
		}

	}

	async Task DashWait( float dashDuration )
	{
		float timer = 0;

		while ( timer < dashDuration )
		{
			WorldRotation = Rotation.LookAt( (target.WorldPosition - WorldPosition).WithZ( 0 ) );

			timer += Time.Delta;

			await Task.FrameEnd();
		}

		await Dash();

	}

	async Task DashPatern()
	{
		CancellationTokenSource cancellation = new CancellationTokenSource();
		float dashDuration = dashPaternDuration / nbDash;

		StopMove();
		_ = FireCover( cancellation.Token );

		for ( int i = 0; i < nbDash; i++ )
		{
			await DashWait( dashDuration );
		}

		cancellation.Cancel();
		cancellation.Dispose();
		cancellation = null;

		paternTask = null;
		canMove = true;
	}

	#endregion

	#region Default Patern value

	async Task DefaultPatern()
	{

	}

	#endregion

}
