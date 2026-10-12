using Sandbox;
using static Sandbox.Services.Stats;

public sealed class EnemyBulletBehaviour : Component
{
	[Property, WideMode] TagSet noCollideTag { get; set; }
	[Property] public float size { get; set; } = 32f;

	public float speed { get; set; }
	public float damage { get; set; }
	[Sync] public GameManager gameManager { get; set; }

	public Vector3 direction { get; set; }

	protected override void OnStart()
	{
		base.OnStart();
		WorldRotation = Rotation.LookAt( direction );
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

			GameObject.Destroy();

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
