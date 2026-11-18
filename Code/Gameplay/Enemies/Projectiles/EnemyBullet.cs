using Sandbox;
using static Sandbox.Services.Stats;

public enum EnemyProjectile
{
	NORMAL,
	PISTOL,

	[Hide] NB_PROJECTILE
}

public sealed class EnemyBullet : Component
{
	[Property, WideMode] TagSet noCollideTag { get; set; }
	[Property] EnemyProjectile type { get; set; }
	[Property] public float size { get; set; } = 32f;

	public float speed { get; set; }
	public float damage { get; set; }
	[Sync] public GameManager gameManager { get; set; }

	public Vector3 direction { get; set; }

	protected override void OnStart()
	{
		base.OnStart();
		
		if(IsProxy)
		{
			Destroy();
		}
	}

	protected override void OnUpdate()
	{
		LinearDirection();
	}

	void LinearDirection()
	{
		Vector3 nextStep = WorldPosition + direction * speed * Time.Delta;
		SceneTraceResult traceResult = Scene.Trace.Sphere( size * WorldScale.x, WorldPosition, nextStep ).WithoutTags( noCollideTag ).Run();

		if ( traceResult.Hit )
		{
			if ( traceResult.HasTag( "player" ) && !IsProxy )
			{
				PlayerBehaviour player = traceResult.Collider.GetComponent<PlayerBehaviour>();

				if ( player != null )
					gameManager.PlayerTakeDamage( player, damage );
			}

			gameManager.PoolManager.enemyProjectile[(int)type].DisposeObject( GameObject );
			return;
		}

		WorldPosition = nextStep;
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();
		Gizmo.Draw.LineSphere( WorldPosition, size );
	}
}
