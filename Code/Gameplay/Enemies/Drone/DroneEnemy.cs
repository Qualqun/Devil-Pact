using Sandbox;
using System;
using System.Threading;
using System.Threading.Tasks;

public class DroneEnemy : BaseEnemyBehaviour
{

	[Property, Group( "Stats" )] float rotationSpeed { get; set; } = 500f;
	[Property, Group( "Stats" )] float fallDuration { get; set; } = 1f;

	[Property, Group( "Debug" )] bool showGizmo = false;

	Task fallTask = null;
	Task moveTask = null;
	DroneInteraction playerLocked = null;



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
				playerLocked = target.droneInteraction;
				agent.Enabled = false;

				GameObject.SetParent( playerLocked.GameObject );
				moveTask = GoToLocalPosition( playerLocked.GetClosestPosition( WorldPosition, out iPos ) );
			}


		}
		else
		{
			WorldRotation = Rotation.LookAt( target.WorldPosition - WorldPosition );

			if ( moveTask == null )
			{

				Log.Info( "Check nextPosition  " + rotationDirection + " ipos " + iPos );
				if ( playerLocked.NextPositionAvailable( iPos, rotationDirection, out int direction, out int iNextPos , out Vector3 nextPosition ) )
				{
					moveTask = GoToLocalPosition( nextPosition );
					rotationDirection = direction;
					iPos = iNextPos;
					Log.Info( "find nextPosition " + rotationDirection + " ipos " + iPos );

				}
				else
				{
					Log.Info( "no  nextPosition" );

				}
			}

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

	async Task GoToLocalPosition( Vector3 localTarget )
	{
		Vector3 startPosition = LocalPosition;
		Vector3 endPosition = localTarget;

		float timer = 0f;
		float duration = Vector3.DistanceBetween( startPosition, endPosition ) / rotationSpeed;

		while ( timer <= duration )
		{
			float t = timer / duration;

			LocalPosition = Vector3.Lerp( startPosition, endPosition, t );
			timer += Time.Delta;

			await Task.FrameEnd();
		}

		LocalPosition = endPosition;
		moveTask = null;

	}


}
