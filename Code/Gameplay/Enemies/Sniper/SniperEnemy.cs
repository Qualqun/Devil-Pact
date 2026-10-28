using Sandbox;
using System.Threading.Tasks;


public class SniperEnemy : BaseEnemyBehaviour
{

	[Property, Group( "Stats" ), Order( 20 )] float range { get; set; } = 100f;
	[Property, Group( "Stats" )] float bulletSpeed { get; set; } = 180f;
	[Property, Group( "Stats" )] float bulletDamage { get; set; } = 6f;
	[Property, Group( "Stats" )] float aimTime { get; set; } = 0.4f;
	[Property, Group( "Stats" )] float shootTime { get; set; } = 1.2f;
	[Property, Group( "Stats" )] float reloadTime { get; set; } = 1.4f;

	[Property, Group( "Growth stats" )] float bulletDamagePerRound { get; set; } = 6f;

	[Property, Group( "Refs" )] new SniperEnemyPresentation enemyPresentation { get; set; }
	[Property, Group( "Refs" )] GameObject gunPoint { get; set; }

	[Property, Group( "Refs Prefabs" ), Order( 200 )] GameObject bullet { get; set; }


	Task attackTask;
	float bulletSize;

	protected override void OnStart()
	{
		base.OnStart();

		bulletSize = bullet.GetComponent<EnemyBulletBehaviour>().size;
	}


	protected override void OnUpdate()
	{
		if ( IsProxy )
		{
			return;
		}

		base.OnUpdate();


		if ( !noTarget )
		{
			bool canShoot = CanShoot();
			
			enemyPresentation.stand = canShoot || attackTask != null;

			if ( canShoot || attackTask != null )
			{
				agent.Stop();

				if ( attackTask == null )
				{
					attackTask = AttackBehaviour();
				}
			}
			else
			{
				FollowPlayer();
			}

		}
	}

	bool CanShoot()
	{
		bool canShoot = targetDist < range && attackTask == null;

		if ( canShoot )
		{
			SceneTraceResult hit = Scene.Trace
				.Sphere( bulletSize, gunPoint.WorldPosition, target.WorldPosition + Vector3.Up * 32f )
				.WithoutTags( "enemy" ).Run();

			canShoot = false;

			if ( hit.Hit && !hit.StartedSolid )
			{
				canShoot = hit.Collider.Tags.Has( "player" );
			}

		}

		return canShoot;
	}


	void Shoot( Vector3 direction )
	{
		GameObject newBullet = bullet.Clone( gunPoint.WorldPosition );
		EnemyBulletBehaviour bulletBehaviour = newBullet.GetComponent<EnemyBulletBehaviour>();

		bulletBehaviour.speed = bulletSpeed;
		bulletBehaviour.damage = bulletDamage;
		bulletBehaviour.direction = direction.Normal;
		bulletBehaviour.gameManager = gameManager;

		newBullet.NetworkSpawn();
	}

	async Task AttackBehaviour()
	{
		Vector3 direction = (target.WorldPosition - WorldPosition).WithZ( 0 );

		_ = AimRotate( direction, aimTime );

		await Task.DelaySeconds( aimTime );

		enemyPresentation.UpdateLaser( gunPoint.WorldPosition, gunPoint.WorldPosition + direction * 10000f );

		await Task.DelaySeconds( shootTime / 4 * 3 );

		enemyPresentation.StartBlink();

		await Task.DelaySeconds( shootTime / 4 );

		enemyPresentation.ResetLaser();
		enemyPresentation.Shoot();

		Shoot( direction );

		await Task.DelaySeconds( reloadTime );

		attackTask = null;
	}




	async Task AimRotate( Vector3 direction, float duration )
	{
		float time = 0f;

		Rotation baseRotation = WorldRotation;
		Rotation targetRotation = Rotation.LookAt( direction );

		while ( time < duration )
		{
			float amount;

			time += Time.Delta;
			amount = (time / duration).Clamp( 0f, 1f );

			WorldRotation = Rotation.Slerp( baseRotation, targetRotation, amount );

			await Task.Frame();
		}

		WorldRotation = targetRotation;

	}

	public override void InitEnemy( int roundNb )
	{
		base.InitEnemy( roundNb );
		bulletDamage += bulletDamagePerRound * roundNb;
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		Gizmo.Transform = global::Transform.Zero;
		Gizmo.Draw.Color = Color.Green;
		Gizmo.Draw.LineSphere( WorldPosition, range );


	}


}
