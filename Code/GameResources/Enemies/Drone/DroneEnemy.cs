using Sandbox;
using System;

public class DroneEnemy : BaseEnemyBehaviour
{



	[Property, Group( "Stats" )] float distanceLock { get; set; } = 500f;

	bool playerLock = false;
	int rotDir = 1;

	protected override void OnStart()
	{
		base.OnStart();
	}

	protected override void OnUpdate()
	{

		if(!playerLock)
		{
			base.OnUpdate();

			if ( !noTarget )
			{
				FollowPlayer();
			}

			playerLock = targetDist <= distanceLock;

		}
		
	}




	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		Gizmo.Transform = global::Transform.Zero;
		Gizmo.Draw.Color = Color.Yellow;
		Gizmo.Draw.LineSphere( WorldPosition, distanceLock );

	}

}
