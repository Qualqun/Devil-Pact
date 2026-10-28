using Sandbox;
using System;
using System.Threading.Tasks;

public class PistolEnemy : BaseEnemyBehaviour
{
	[Property, Group( "Stats" ), Order( 20 )] public float range { get; set; } = 100f;
	[Property, Group( "Stats" )] public float bulletSpeed { get; set; } = 180f;
	[Property, Group( "Stats" )] public float bulletDamage { get; set; } = 6f;
	[Property, Group( "Stats" )] public float burstFrequency { get; set; } = 1.2f;
	[Property, Group( "Stats" )] public float burstDuration { get; set; } = 3;
	[Property, Group( "Stats" )] public int nbBulletBurst { get; set; } = 3;

	[Property, Group( "Growth stats" )] public float bulletDamagePerRound { get; set; } = 6f;


	[Property, Group( "Refs" )] public GameObject gunPoint { get; set; }
	[Property, Group( "Refs Prefabs" ), Order( 200 )] public GameObject bullet { get; set; }


	Task attackTask;

	float bulletSize;
	float timerBurst = 0;


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
			timerBurst += Time.Delta;

			if ( CanShoot() && timerBurst > burstFrequency )
			{
				attackTask = BurstShoot();
				agent.Stop();
			}

			if ( attackTask == null )
			{
				FollowPlayer();
			}

		}
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		Gizmo.Transform = global::Transform.Zero;
		Gizmo.Draw.Color = Color.Green;
		Gizmo.Draw.LineSphere( WorldPosition, range );


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

	float CalculateInterceptTime()
	{
		Vector3 V = target.State.Velocity;
		Vector3 R = target.WorldPosition - gunPoint.WorldPosition;
		float s = bulletSpeed;

		float a = Vector3.Dot( V, V ) - s * s;

		if ( a == 0 )
		{
			// The equation start from that 2(R · V)t + R · R = 0 
			// to that
			return (-Vector3.Dot( R, R )) / (2 * Vector3.Dot( R, V ));
		}
		else
		{
			float b = 2 * Vector3.Dot( R, V );
			float c = Vector3.Dot( R, R );

			float discriminant = b * b - 4 * a * c;

			if ( discriminant > 0 )
			{
				float t1 = (-b - (float)Math.Sqrt( discriminant )) / (2 * a);
				float t2 = (-b + (float)Math.Sqrt( discriminant )) / (2 * a);

				if ( t1 > 0f && t2 > 0f )
					return Math.Min( t1, t2 );

				if ( t1 > 0f )
					return t1;

				if ( t2 > 0f )
					return t2;

			}
			else if ( discriminant == 0 )
			{
				return -b / (2 * a);
			}

		}

		return 0f;
	}
	void Shoot()
	{
		Vector3 shootPosition = target.WorldPosition + target.State.Velocity * CalculateInterceptTime();
		Vector3 direction = (shootPosition - gunPoint.WorldPosition).WithZ( 0 );

		GameObject newBullet = bullet.Clone( gunPoint.WorldPosition );
		EnemyBulletBehaviour bulletBehaviour = newBullet.GetComponent<EnemyBulletBehaviour>();

		bulletBehaviour.speed = bulletSpeed;
		bulletBehaviour.damage = bulletDamage;
		bulletBehaviour.direction = direction.Normal;
		bulletBehaviour.gameManager = gameManager;

		newBullet.NetworkSpawn();

		WorldRotation = Rotation.LookAt( direction );
	}

	async Task BurstShoot()
	{
		float timer = 0f;
		float shootDuration = burstDuration / nbBulletBurst;

		int nbShoot = 0;

		while ( true )
		{
			timer += Time.Delta;

			if ( timer >= shootDuration )
			{
				Shoot();

				timer = 0;
				nbShoot++;

				if ( nbShoot >= nbBulletBurst )
				{
					attackTask = null;
					timerBurst = 0f;
					return;
				}
			}

			await Task.FrameEnd();
		}

	}



}
