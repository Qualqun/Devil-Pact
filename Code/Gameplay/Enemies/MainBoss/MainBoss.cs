using Sandbox;
using System.Diagnostics;
using System.Threading.Tasks;

public sealed class MainBoss : BaseEnemyBehaviour
{
	#region Bombing values

	[Property, Group( "Bombing" ), Order( 40 )] float bombingDuration { get; set; } = 4f;
	[Property, Group( "Bombing" ), Order( 40 )] float arcHeight { get; set; } = 400f;
	[Property, Group( "Bombing" ), Order( 40 )] float durationShellTravel { get; set; } = 2f;
	[Property, Group( "Bombing" ), Order( 40 )] float rangeAttack { get; set; } = 300f;
	[Property, Group( "Bombing" ), Order( 40 )] int nbShell { get; set; } = 4;

	[Property, Group( "Refs" )] GameObject mortarPoint { get; set; }
	[Property, Group( "Refs" )] GameObject mortarShell { get; set; }

	#endregion


	#region Dash values
	[Property, Group( "Dash" ), Order( 50 )] float dashPaternDuration { get; set; } = 4f;
	[Property, Group( "Dash" ), Order( 50 )] float dashDistance { get; set; } = 100f;
	[Property, Group( "Dash" ), Order( 50 )] float dashSpeed { get; set; } = 100f;

	[Property, Group( "Dash" ), Order( 50 )] int nbDash { get; set; } = 1;


	#endregion

	[Property, Group( "Refs" )] GameObject gunPoint { get; set; }

	[Property, Group( "Debug" ), Order( 1000 )] bool showGizmo = true;

	Task paternTask = null;
	bool canMove = true;

	protected override void OnUpdate()
	{

		if ( paternTask == null )
		{
			base.OnUpdate();

			if ( Vector3.DistanceBetween( target.WorldPosition, WorldPosition ) <= rangeAttack )
			{
				ChoosePatern();
			}

		}

		if ( canMove )
		{
			FollowPlayer();
		}

	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		if ( showGizmo )
		{
			Gizmo.Transform = global::Transform.Zero;
			Gizmo.Draw.LineSphere( WorldPosition.WithZ( 0 ) + Vector3.Up * arcHeight, 32f );

			Gizmo.Draw.Color = Color.Orange;
			Gizmo.Draw.LineSphere( WorldPosition.WithZ( 0 ), rangeAttack );
		}

	}

	void ChoosePatern()
	{
		canMove = false;
		paternTask = BombingPatern();
	}


	#region Bombing methods
	void MortarShoot()
	{
		GameObject newShell = mortarShell.Clone( mortarPoint.WorldPosition );
		EnemyShell enemyShell = newShell.GetComponent<EnemyShell>();

		enemyShell.InitProjectile( target.WorldPosition, arcHeight, durationShellTravel );

		newShell.NetworkSpawn();
	}

	async Task BombingPatern()
	{
		float durationShoot = bombingDuration / nbShell;

		agent.Stop();

		for ( int i = 0; i < nbShell; i++ )
		{
			await Task.DelaySeconds( durationShoot );

			MortarShoot();
		}


		paternTask = null;

	}

	#endregion

	#region Dash methods


	async Task Dash()
	{
		Vector3 dirDash = (target.WorldPosition - WorldPosition).WithZ( 0 );
		Ray ray = new Ray( WorldPosition + Vector3.Up * MathX.MeterToInch( 1f ), dirDash );
		SceneTraceResult hit = Scene.Trace.Sphere( MathX.MeterToInch( 0.5f ), ray, dashDistance ).WithoutTags( "enemy" ).Run();

		Vector3 startPosition = WorldPosition;
		float dashDuration = Vector3.DistanceBetween( startPosition, hit.EndPosition.WithZ( 0 ) ) / dashSpeed;

		float timer = 0;

		while ( timer < dashDuration )
		{

			timer += Time.Delta;

			WorldPosition = Vector3.Lerp( startPosition, hit.EndPosition.WithZ( 0 ), timer / dashDuration );

			await Task.FrameEnd();


		}

	}

	async Task DashWait( float dashDuration )
	{
		float timer = 0;

		while ( timer < dashDuration )
		{
			WorldRotation = Rotation.LookAt( (target.WorldPosition - WorldPosition).WithZ( 0 ) );

			timer += Time.Delta;

			await Task.FrameEnd();
		}

		await Dash();

	}


	async Task DashPatern()
	{
		float dashDuration = dashPaternDuration / nbDash;


		for ( int i = 0; i < nbDash; i++ )
		{
			await DashWait( dashDuration );

		}

	}

	#endregion


}
