using Sandbox;
using System;

public sealed class DroneInteraction : Component
{

	[Property] public float distance { get; set; } = 100f;
	[Property] float radius { get; set; } = 15f;
	[Property] float height { get; set; } = 30f;
	[Property, Range( 1f, 2f )] float spacingMultiplier { get; set; } = 1f;

	[Property, Group( "Debug" )] bool showGizmo = false;


	bool[] hitObstacle = null;
	Vector3[] positions = null;


	protected override void OnFixedUpdate()
	{
		positions = GetSpherePosition();
		hitObstacle = new bool[positions.Length];

		for ( int i = 0; i < positions.Length; i++ )
		{
			SceneTraceResult sphereTrace = Scene.Trace.Sphere( radius, positions[i], positions[i] ).WithoutTags( "player", "enemy" ).Run();

			hitObstacle[i] = sphereTrace.Hit;
		}

		base.OnFixedUpdate();
	}


	protected override void DrawGizmos()
	{
		base.DrawGizmos();


		if ( showGizmo )
		{
			Vector3[] positions = GetSpherePosition();

			Gizmo.Transform = global::Transform.Zero;

			for ( int i = 0; i < positions.Length; i++ )
			{

				if ( hitObstacle != null )
				{
					Gizmo.Draw.Color = hitObstacle[i] ? Color.Red : Color.Green;
				}

				Gizmo.Draw.LineSphere( positions[i], radius );
			}

		}
	}

	Vector3[] GetSpherePosition()
	{
		float anglePerSphere = MathX.RadianToDegree( MathF.Atan( radius / distance ) ) * spacingMultiplier;
		int nbSpheres = (int)(360 / anglePerSphere);

		Vector3[] positions = new Vector3[nbSpheres];
		Vector3 startPosition = Vector3.Forward * distance + Vector3.Up * height;

		for ( int i = 0; i < nbSpheres; i++ )
		{
			positions[i] = WorldPosition + Rotation.FromYaw( anglePerSphere * i ) * startPosition;
		}

		return positions;
	}

	public Vector3 GetClosestPosition( Vector3 worldPos )
	{
		Vector3 closestPos = Vector3.Zero;
		float closestDist = float.MaxValue;

		if ( hitObstacle == null )
			OnFixedUpdate();

		for ( int i = 0; i < positions.Length; i++ )
		{
			float distance = Vector3.DistanceBetween( worldPos, positions[i] );

			if( !hitObstacle[i] && distance < closestDist)
			{
				closestPos = positions[i];
				closestDist = distance;
			}
		}

		return closestPos - Vector3.Up * height;
	}
}
