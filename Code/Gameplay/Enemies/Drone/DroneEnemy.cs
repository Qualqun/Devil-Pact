using Sandbox;
using System;
using System.Threading;
using System.Threading.Tasks;

public class DroneEnemy : BaseEnemyBehaviour
{

	[Property, Group( "Bullet stats" ), Order( 21 )] public float bulletSpeed { get; set; } = 180f;
	[Property, Group( "Bullet stats" )] public float bulletDamage { get; set; } = 6f;
	[Property, Group( "Bullet stats" )] public float burstFrequency { get; set; } = 1.2f;
	[Property, Group( "Bullet stats" )] public float burstDuration { get; set; } = 3;
	[Property, Group( "Bullet stats" )] public int nbBulletBurst { get; set; } = 3;


	[Property, Group( "Stats" ), Order( 20 )] float rotationSpeed { get; set; } = 500f;
	[Property, Group( "Stats" )] float fallDuration { get; set; } = 1f;
	[Property, Group( "Stats" )] float heightFly { get; set; } = 100f;

	[Property, Group( "Refs" )] public GameObject gunPoint { get; set; }
	[Property, Group( "Refs Objects" ), Order( 200 )] public GameObject bullet { get; set; }

	[Property, Group( "Debug" ), Order( 1000 )] bool showGizmo = false;

	Task fallTask = null;
	Task moveTask = null;

	DroneInteraction playerLocked = null;
	CancellationTokenSource cancellation;

	bool canShoot = false;
	int rotationDirection = 1;
	int iPos;

	public override void InitEnemy( int roundNb )
	{
		base.InitEnemy( roundNb );

		fallTask = DroneFall();
	}


	protected override void OnUpdate()
	{

		if ( fallTask != null )
			return;

		if ( playerLocked == null )
		{
			bool isInsideDist = targetDist <= target?.droneInteraction.distance;

			base.OnUpdate();

			FollowPlayer();


			if ( isInsideDist )
			{
				Vector3 firstPosition;

				playerLocked = target.droneInteraction;
				agent.Enabled = false;

				firstPosition = playerLocked.GetClosestPosition( WorldPosition, out iPos );

				moveTask = GoTo( firstPosition, iPos );
				cancellation = new CancellationTokenSource();

				_ = AttackBehaviour( cancellation.Token );
			}


		}
		else
		{
			WorldRotation = Rotation.LookAt( target.WorldPosition - WorldPosition.WithZ( 0 ) );

			if ( moveTask == null )
			{
				if ( !GetNextPosition() )
				{
					FindNewPosition();
				}
			}
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		cancellation?.Cancel();
		cancellation?.Dispose();
		cancellation = null;
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		if ( showGizmo )
		{
			Gizmo.Transform = global::Transform.Zero;
			Gizmo.Draw.LineSphere( WorldPosition.WithZ( 0 ) + Vector3.Up * heightFly, 32f );

		}

	}

	bool GetNextPosition()
	{
		if ( playerLocked.NextPositionAvailable( iPos, rotationDirection, out int direction, out int iNextPos, out Vector3 nextPosition ) )
		{
			moveTask = GoTo( nextPosition, iNextPos );
			rotationDirection = direction;
			iPos = iNextPos;

			return true;
		}

		return false;
	}

	void FindNewPosition()
	{
		if ( playerLocked.GetNewPosition( iPos, rotationDirection, out int iNewPos, out Vector3 newLocalPosition ) )
		{
			float distance = Vector3.DistanceBetween( newLocalPosition.WithZ( 0 ), LocalPosition.WithZ( 0 ) );

			Vector3 droneLocalPositon = (WorldPosition - target.WorldPosition).WithZ( 0 );
			Vector3 direction = (newLocalPosition - droneLocalPositon).WithZ( 0 ).Normal;
			Vector3 control = droneLocalPositon + direction * distance / 2f + Vector3.Up * heightFly;

			moveTask = GoToBezier( newLocalPosition, control, iNewPos );
			iPos = iNewPos;
		}
	}
	void Shoot()
	{
		Vector3 direction = target.WorldPosition.WithZ( 0 ) - WorldPosition.WithZ( 0 );

		GameObject newBullet = bullet.Clone( gunPoint.WorldPosition );
		EnemyBulletBehaviour bulletBehaviour = newBullet.GetComponent<EnemyBulletBehaviour>();

		bulletBehaviour.speed = bulletSpeed;
		bulletBehaviour.damage = bulletDamage;
		bulletBehaviour.direction = direction.Normal;
		bulletBehaviour.gameManager = gameManager;

		newBullet.NetworkSpawn();
	}

	async Task DroneFall()
	{
		float timer = 0f;

		Vector3 startPosition = WorldPosition;
		Vector3 endPosition = WorldPosition.WithZ( 0 );

		while ( timer <= fallDuration )
		{
			float t = timer / fallDuration;
			float easedT = 1f - (float)Math.Pow( 1f - t, 3f );

			WorldPosition = Vector3.Lerp( startPosition, endPosition, easedT );
			timer += Time.Delta;

			await Task.FrameEnd();
		}

		agent.Enabled = true;


		WorldPosition = endPosition;
		fallTask = null;
	}

	async Task GoTo( Vector3 localTarget, int iNextPos )
	{
		Vector3 startPosition = WorldPosition - target.WorldPosition;
		Vector3 endPosition = localTarget;

		float timer = 0f;
		float duration = Vector3.DistanceBetween( startPosition, endPosition ) / rotationSpeed;

		while ( timer <= duration )
		{
			float t = timer / duration;

			WorldPosition = target.WorldPosition + Vector3.Lerp( startPosition, endPosition, t );
			timer += Time.Delta;

			canShoot = true;

			if ( playerLocked.PositionIsAvailable( iNextPos ) )
			{
				FindNewPosition();

				return;
			}


			await Task.FrameEnd();
		}

		WorldPosition = target.WorldPosition + localTarget;
		moveTask = null;

	}

	async Task GoToBezier( Vector3 localTarget, Vector3 localControl, int iNextPos )
	{
		Vector3 startPosition = WorldPosition - target.WorldPosition;
		Vector3 endPosition =  localTarget;
		Vector3 control =  localControl;

		float timer = 0f;
		float duration = Vector3.DistanceBetween( startPosition, endPosition ) / rotationSpeed;

		while ( timer <= duration )
		{
			float t = timer / duration;
			float easedT = 1f - (float)Math.Pow( 1f - t, 3f );

			WorldPosition = target.WorldPosition + Vector3.CubicBezier( startPosition, endPosition, control, control, easedT );

			timer += Time.Delta;
			canShoot = false;

			if ( playerLocked.PositionIsAvailable( iNextPos ) )
			{
				FindNewPosition();

				return;
			}


			await Task.FrameEnd();
		}

		WorldPosition = target.WorldPosition + localTarget;
		moveTask = null;
	}

	async Task AttackBehaviour( CancellationToken token )
	{
		Task burstShoot = null;
		float timer = 0f;

		while ( !token.IsCancellationRequested )
		{
			timer += canShoot ? Time.Delta : 0f;

			if ( timer >= burstFrequency && burstShoot == null )
			{
				burstShoot = BurstShoot();

				if ( burstShoot.IsCompleted )
				{
					timer = 0f;
					burstShoot = null;
				}
			}

			await Task.FrameEnd();
		}
	}

	async Task BurstShoot()
	{
		float timer = 0f;
		float shootDuration = burstDuration / nbBulletBurst;

		int nbShoot = 0;

		while ( true )
		{
			timer += canShoot ? Time.Delta : 0f;

			if ( timer >= shootDuration )
			{
				Shoot();

				timer = 0;
				nbShoot++;

				if ( nbShoot >= nbBulletBurst )
					return;

			}

			await Task.FrameEnd();
		}
	}



}
