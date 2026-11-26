using System;

public sealed class MainBossArm : Component
{
	[Property] GameObject[] bulletPoints { get; set; }
	[Property] public GameObject MortarPoint { get; set; }

	public void DrawBulletGizmos( float distance )
	{
		if ( bulletPoints is null )
			return;

		foreach ( var muzzle in bulletPoints )
		{
			if ( !muzzle.IsValid() )
				continue;

			Gizmo.Draw.LineSphere( muzzle.WorldPosition, 1.5f );
			Gizmo.Draw.Line( muzzle.WorldPosition, muzzle.WorldPosition + WorldRotation.Up * distance );
		}
	}

	public float Length => WorldPosition.Distance( MortarPoint.WorldPosition ) + 5f;
	public bool Detached => socket is not null;
	GameObject socket;
	Transform heldPose;
	int nextBarrel;

	public Vector3 Detach( Vector3 drop, Vector3 forward )
	{
		SceneTraceResult floor;

		socket = GameObject.Parent;
		heldPose = LocalTransform;

		GameObject.SetParent( null, true );
		WorldRotation = Rotation.LookAt( forward ) * Rotation.FromPitch( 90 );

		floor = Scene.Trace.Ray( drop + Vector3.Up * 32f, drop + Vector3.Down * 100f )
			.WithoutTags( "enemy", "player", "bullet" ).Run();

		return (floor.Hit ? floor.EndPosition : drop) + Vector3.Up * (9f * WorldScale.x);
	}

	public bool FlyTo( Vector3 destination, float speed )
	{
		var offset = destination - WorldPosition;
		float step = Math.Max( 1f, speed ) * Time.Delta;

		if ( offset.Length <= step )
		{
			WorldPosition = destination;
			return true;
		}

		WorldPosition += offset.Normal * step;

		return false;
	}

	public void Spin( float speed )
	{
		WorldRotation = Rotation.FromYaw( speed * Time.Delta ) * WorldRotation;
	}

	public void Attach()
	{
		if ( !Detached )
			return;

		GameObject.SetParent( socket, false );
		LocalTransform = heldPose;
		socket = null;
	}

	public void Shoot( GameManager manager, float speed, float damage )
	{
		var muzzle = bulletPoints[nextBarrel];
		Vector3 direction;
		ItemPool pool;
		EnemyBullet shot;

		nextBarrel = (nextBarrel + 1) % bulletPoints.Length;

		if ( Scene.Trace.Ray( WorldPosition, muzzle.WorldPosition )
			.WithoutTags( "enemy", "player", "ground", "bullet" ).Run().Hit )
			return;

		// The barrels point along the arm model's local Z axis.
		direction = WorldRotation.Up.WithZ(0);
		pool = manager.PoolManager.enemyProjectile[(int)EnemyProjectile.NORMAL];
		shot = pool.GetObject( muzzle.WorldPosition, Rotation.LookAt( direction ) ).GetComponent<EnemyBullet>();

		shot.speed = speed;
		shot.damage = damage;
		shot.direction = direction;
		shot.gameManager = manager;
	}
}
