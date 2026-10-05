using Sandbox;
using System.Threading.Tasks;


public class RangeEnemy : BaseEnemyBehaviour
{

	[Property, Group( "Stats" )] public float range { get; set; } = 100f;
	[Property, Group( "Stats" )] public float bulletSpeed { get; set; } = 180f;
	[Property, Group( "Stats" )] public float bulletDamage { get; set; } = 6f;
	[Property, Group( "Stats" )] public float aimTime { get; set; } = 0.4f;
	[Property, Group( "Stats" )] public float shootTime { get; set; } = 1.2f;
	[Property, Group( "Stats" )] public float reloadTime { get; set; } = 1.4f;
	[Property, Group( "Stats" )] public float bulletSize { get; set; } = 32f;

	[Property, Group( "Growth stats" )] public float bulletDamagePerRound { get; set; } = 6f;

	[Property, Group( "Refs" )] new RangeEnemyPresentation enemyPresentation { get; set; }
	[Property, Group( "Refs" )] CapsuleCollider capsuleCollider { get; set; }

	[Property, Group( "Refs" )] public GameObject bullet { get; set; }
	[Property, Group( "Refs" )] public GameObject gunPoint { get; set; }

	Task attackTask;

	protected override void OnUpdate()
	{
		if ( IsProxy )
		{
			return;
		}

		base.OnUpdate();


		if ( !noTarget )
		{
			bool canShoot = targetDist < range && attackTask == null;

			if ( canShoot )
			{
				Capsule enemyCapsule = new Capsule( capsuleCollider.Start, capsuleCollider.End, capsuleCollider.Radius );
	
				SceneTraceResult hit = Scene.Trace
					.Sphere( bulletSize, gunPoint.WorldPosition, target.WorldPosition + Vector3.Up * 32f)
					.WithoutTags( "enemy" ).Run();

				canShoot = false;

				if ( hit.Hit && !hit.StartedSolid )
				{
					canShoot = hit.Collider.Tags.Has( "player" );
				}

			}

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

	async Task AttackBehaviour()
	{
		Vector3 direction = target.WorldPosition - WorldPosition;

		GameObject newBullet;
		EnemyBulletBehaviour bulletBehaviour;

		direction = direction.WithZ( 0 );

		_ = AimRotate( direction, aimTime );


		await Task.DelaySeconds( aimTime );

		enemyPresentation.UpdateLaser( gunPoint.WorldPosition, gunPoint.WorldPosition + direction * 10000f );

		await Task.DelaySeconds( shootTime / 4 * 3 );

		enemyPresentation.StartBlink();

		await Task.DelaySeconds( shootTime / 4 );

		enemyPresentation.ResetLaser();
		enemyPresentation.Shoot();

		newBullet = bullet.Clone( gunPoint.WorldPosition );
		bulletBehaviour = newBullet.GetComponent<EnemyBulletBehaviour>();

		bulletBehaviour.speed = bulletSpeed;
		bulletBehaviour.damage = bulletDamage;
		bulletBehaviour.direction = direction.Normal;
		bulletBehaviour.gameManager = gameManager;

		newBullet.NetworkSpawn();

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

	public override void InitStats( int roundNb )
	{
		base.InitStats( roundNb );
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
