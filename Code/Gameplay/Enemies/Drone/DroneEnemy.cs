using Sandbox;
using System;
using System.Threading;
using System.Threading.Tasks;

public class DroneEnemy : BaseEnemyBehaviour
{

	[Property, Group( "Stats" )] float distanceLock { get; set; } = 500f;
	[Property, Group( "Stats" )] float fallDuration { get; set; } = 1f;


	[Property, Group( "Debug" )] bool showGizmo = false;




	Task fallTask = null;
	DroneInteraction playerLocked = null;
	int rotDir = 1;

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

			Log.Info( "Update" );

			if ( isInsideDist)
			{
				playerLocked = target.droneInteraction;
				agent.Enabled = false;
			}
		}
		else
		{
			WorldPosition = playerLocked.GetClosestPosition( WorldPosition );
		}



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


}
