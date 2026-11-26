using System;
using System.Threading;
using System.Threading.Tasks;

public sealed class MainBoss : BaseEnemyBehaviour
{
	enum Pattern { Burst, ThrowArm, Mortar }
	enum Phase { Chase, Burst, ThrowArm, ArmFlight, AimCharge, Charge, RecoverArm, Mortar }

	[Property, Group( "Combat" )] float advanceDuration { get; set; } = 4f;
	[Property, Group( "Combat" )] float aimTime { get; set; } = 0.55f;
	[Property, Group( "Combat" )] float bulletSpeed { get; set; } = 500f;
	[Property, Group( "Combat" )] float bulletDamage { get; set; } = 6f;
	[Property, Group( "Combat" ), MinMax( 0.1f, float.MaxValue )] int bulletPerSecond { get; set; } = 2;

	[Property, Group( "Arm and charges" )] float armDuration { get; set; } = 6f;
	[Property, Group( "Arm and charges" )] int armShots { get; set; } = 24;

	[Property, Group( "Arm and charges" )] int nbDash { get; set; } = 3;
	[Property, Group( "Arm and charges" )] float dashDistance { get; set; } = 300f;
	[Property, Group( "Arm and charges" )] float dashSpeed { get; set; } = 600f;
	[Property, Group( "Arm and charges" )] float rotationSpeed { get; set; } = 200f;
	[Property, Group( "Arm and charges" )] float armThrowSpeed { get; set; } = 650f;

	[Property, Group( "Mortar" )] int nbShell { get; set; } = 4;
	[Property, Group( "Mortar" )] float bombingDuration { get; set; } = 4f;
	[Property, Group( "Mortar" )] float arcHeight { get; set; } = 180f;
	[Property, Group( "Mortar" )] float durationShellTravel { get; set; } = 1.5f;
	[Property, Group( "Mortar" )] float blastRadius { get; set; } = 64f;
	[Property, Group( "Mortar" )] float blastDamage { get; set; } = 12f;

	[Property, Group( "Refs" )] MainBossArm arm { get; set; }
	[Property, Group( "Refs" )] GameObject mortarShell { get; set; }

	[Property, Group( "Debug" ), Order( 1000 )] bool showDebug { get; set; }
	[Property, Group( "Debug" )] bool showBurst { get; set; } = true;
	[Property, Group( "Debug" )] bool showArmAndCharges { get; set; } = true;
	[Property, Group( "Debug" )] bool showMortar { get; set; } = true;
	[Property, Group( "Debug" )] bool activePatternOnly { get; set; }

	MainBossPresentation Presentation => (MainBossPresentation)enemyPresentation;

	Phase phase;
	Pattern nextSpecial = Pattern.Mortar;
	bool specialAvailable;
	CancellationTokenSource patternCancellation;
	Task activePattern;

	Vector3 landingPoint;
	Vector3 chargeDirection;
	float chargeLeft;

	protected override void OnUpdate()
	{
		if ( IsProxy || !gameManager.IsValid() || !gameManager.GameState.IsValid() )
			return;

		base.OnUpdate();

		if ( noTarget )
		{
			if ( patternCancellation is { IsCancellationRequested: false } )
			{
				patternCancellation.Cancel();
				ResetPattern();
			}

			specialAvailable = false;
			return;
		}

		if ( activePattern is { IsCompleted: false } )
			return;

		if ( activePattern?.Exception is { } error )
			Log.Error( error );

		patternCancellation?.Dispose();
		patternCancellation = new CancellationTokenSource();
		activePattern = RunPattern( patternCancellation.Token );
	}

	bool CanContinue( CancellationToken token )
	{
		return !token.IsCancellationRequested && IsValid && target.IsValid() && !target.isDead;
	}

	async Task RunPattern( CancellationToken token )
	{
		bool useSpecial = specialAvailable;
		bool canBomb = mortarShell.IsValid() && arm.MortarPoint.IsValid();

		agent.UpdateRotation = false;

		try
		{
			if ( useSpecial && canBomb && nextSpecial == Pattern.Mortar )
			{
				specialAvailable = false;
				nextSpecial = Pattern.ThrowArm;
				await Bombard( token );
			}
			else if ( useSpecial )
			{
				specialAvailable = false;
				nextSpecial = Pattern.Mortar;
				await ThrowAndCharge( token );
			}
			else
			{
				await AdvanceAndShoot( token );
				specialAvailable = CanContinue( token );
			}
		}
		finally
		{
			if ( IsValid && !token.IsCancellationRequested )
				ResetPattern();
		}
	}

	void ResetPattern()
	{
		phase = Phase.Chase;

		if ( arm.IsValid() )
			arm.Attach();

		if ( agent.IsValid() )
		{
			agent.Stop();
			agent.UpdateRotation = true;
		}

		if ( Presentation.IsValid() )
			Presentation.CurrentAnimation = MainBossAnimation.Walking;
	}

	// Place each shot halfway through its share of the pattern duration.
	static float ShotTime( int index, int count, float duration )
	{
		return (index + 0.5f) * duration / count;
	}

	async Task AdvanceAndShoot( CancellationToken token )
	{
		float duration = Math.Max( 0.05f, advanceDuration );
		int fired = 0;
		float firerate = 1f / bulletPerSecond;
		TimeSince elapsed = 0;

		phase = Phase.Burst;
		Presentation.CurrentAnimation = MainBossAnimation.Shooting;

		while ( CanContinue( token ) )
		{
			FollowPlayer();
			FaceTarget();

			if ( elapsed >= firerate * fired )
			{
				var sight = Scene.Trace.Ray( arm.WorldPosition, target.WorldPosition + Vector3.Up * 1000f )
					.WithoutTags( "enemy", "player", "bullet" ).Run();

				if ( !sight.Hit )
					arm.Shoot( gameManager, bulletSpeed, bulletDamage );

				fired++;
			}

			if ( elapsed >= duration )
				break;

			await Task.FrameEnd();
		}
	}

	async Task Bombard( CancellationToken token )
	{
		float duration = Math.Max( 0.05f, bombingDuration );
		TimeSince elapsed;
		TimeSince preparation = 0;
		int fired = 0;

		phase = Phase.Mortar;
		agent.Stop();
		FaceTarget();
		Presentation.CurrentAnimation = MainBossAnimation.Mortar;

		// Local Z follows the animated barrel. Wait for its raised pose.
		do
		{
			await Task.FrameEnd();

			if ( !CanContinue( token ) )
				return;

			//FaceTarget();

			if ( preparation > Math.Max( 2f, aimTime * 3f ) )
				return;
		}
		while ( arm.WorldRotation.Up.z < 0.93f );

		elapsed = 0;

		while ( CanContinue( token ) )
		{
			//FaceTarget();
			while ( fired < nbShell && elapsed >= ShotTime( fired, nbShell, duration ) )
			{
				FireMortar();
				fired++;
			}

			if ( elapsed >= duration )
				break;

			await Task.FrameEnd();
		}
	}

	async Task ThrowAndCharge( CancellationToken token )
	{
		float duration = Math.Max( 0.05f, armDuration );
		float chargeWindow = duration * 0.75f;
		int charges = Math.Max( 1, nbDash );
		float slot = chargeWindow / charges;
		float firerate = 1f / bulletPerSecond;
		int fired = 0;
		int currentCharge = -1;
		bool grounded = false;
		bool recovering = false;
		TimeSince elapsed;
		TimeSince preparation = 0;

		phase = Phase.ThrowArm;
		agent.Stop();
		Presentation.CurrentAnimation = MainBossAnimation.ShootArm;

		while ( preparation < aimTime && CanContinue( token ) )
		{
			FaceTarget();
			await Task.FrameEnd();
		}

		if ( !CanContinue( token ) )
			return;

		FaceTarget();
		ThrowArm();
		elapsed = 0;

		while ( CanContinue( token ) && elapsed < duration )
		{
			float now = elapsed;

			if ( !grounded )
			{
				grounded = arm.FlyTo( landingPoint, armThrowSpeed );

				if ( grounded )
					fired = (int)(elapsed / firerate);
			}

			if ( grounded && arm.Detached )
			{
				arm.Spin( rotationSpeed );

				if ( elapsed >= firerate * fired )
				{
					arm.Shoot( gameManager, bulletSpeed, bulletDamage );
					fired++;
				}
			}


			if ( now < chargeWindow )
			{
				int index = Math.Min( charges - 1, (int)(now / slot) );
				float withinSlot = now - index * slot;

				if ( index != currentCharge )
				{
					currentCharge = index;
					phase = Phase.AimCharge;
					Presentation.CurrentAnimation = MainBossAnimation.Walking;
				}


				if ( phase == Phase.AimCharge )
				{
					FaceTarget();

					if ( withinSlot >= slot * 0.3f )
					{
						chargeDirection = WorldRotation.Forward;
						chargeLeft = dashDistance;
						phase = Phase.Charge;
						Presentation.CurrentAnimation = MainBossAnimation.Running;
					}
				}

				if ( phase == Phase.Charge && chargeLeft > 0f )
				{
					UpdateCharge();
				}
				else
				{
					FaceTarget();
				}
			}
			else if ( !recovering )
			{
				recovering = true;
				phase = Phase.RecoverArm;
				Presentation.CurrentAnimation = MainBossAnimation.Walking;
				agent.UpdateRotation = true;
				agent.MoveTo( landingPoint );
			}
			else if ( grounded && arm.Detached && WorldPosition.WithZ( 0 ).Distance( landingPoint.WithZ( 0 ) ) < agent.Radius + 12f )
			{
				arm.Attach();
				agent.Stop();
			}

			await Task.FrameEnd();
		}
	}

	void FaceTarget()
	{
		var direction = (target.WorldPosition - WorldPosition).WithZ( 0 );
		if ( direction.LengthSquared > 0.01f )
			WorldRotation = Rotation.LookAt( direction );
	}

	void FireMortar()
	{
		var gameObjectShell = mortarShell.Clone( arm.MortarPoint.WorldPosition );
		EnemyShell shell = gameObjectShell.GetComponent<EnemyShell>();

		shell.damage = blastDamage;
		shell.radiusExplosion = blastRadius;
		shell.InitProjectile( gameManager, target.WorldPosition, arcHeight, durationShellTravel );

		gameObjectShell.NetworkSpawn();
	}

	void ThrowArm()
	{
		var forward = WorldRotation.Forward;
		var drop = GetThrowDestination( forward, targetDist, true );

		landingPoint = arm.Detach( drop, forward );
		phase = Phase.ArmFlight;
	}

	Vector3 GetThrowDestination( Vector3 forward, float targetDistance, bool useNavigation )
	{
		// Leave enough room for the barrels to turn on our side of a wall.
		float clearance = arm.Length;
		var start = WorldPosition + Vector3.Up * agent.Radius;

		var wall = Scene.Trace.Sphere( agent.Radius, start, start + forward * (targetDistance + clearance) )
			.WithoutTags( "enemy", "player", "ground", "bullet" ).Run();

		float distance = wall.Hit ? Math.Max( 0, wall.Distance - clearance ) : targetDistance;
		var drop = WorldPosition + forward * Math.Min( distance, targetDistance );

		if ( !useNavigation )
		{
			return drop;
		}
		else
		{
			var path = Scene.NavMesh.CalculatePath( new Sandbox.Navigation.CalculatePathRequest
			{
				Start = WorldPosition,
				Target = drop,
				Agent = agent
			} );

			if ( path.Points is { Count: > 0 } points )
				return points[^1].Position;

			return drop;
		}
	}

	protected override void DrawGizmos()
	{
		bool filter = activePatternOnly && !Scene.IsEditor;
		bool armPhase = phase is Phase.ThrowArm or Phase.ArmFlight or Phase.AimCharge or Phase.Charge or Phase.RecoverArm;
		Vector3 aim;

		base.DrawGizmos();

		if ( !showDebug || !arm.IsValid() )
			return;

		aim = target.IsValid() ? target.WorldPosition : WorldPosition + WorldRotation.Forward * 300f;
		Gizmo.Transform = global::Transform.Zero;

		if ( showBurst && (!filter || phase == Phase.Burst) )
		{
			Gizmo.Draw.Color = Color.Red;
			arm.DrawBulletGizmos( 300f );
		}

		if ( showArmAndCharges && agent.IsValid() && arm.MortarPoint.IsValid() && (!filter || armPhase) )
			DrawArmGizmos( aim );

		if ( showMortar && arm.MortarPoint.IsValid() && (!filter || phase == Phase.Mortar) )
			DrawMortarGizmos( aim );
	}

	void DrawArmGizmos( Vector3 aim )
	{
		var offset = aim - WorldPosition;
		var forward = offset.WithZ( 0 ).Normal;
		var drop = arm.Detached ? landingPoint : GetThrowDestination( forward, offset.Length, false );
		var direction = phase == Phase.Charge ? chargeDirection : forward;
		float distance = phase == Phase.Charge ? chargeLeft : dashDistance;
		var start = WorldPosition + Vector3.Up * agent.Radius;
		var end = start + direction * distance;
		var wall = Scene.Trace.Sphere( agent.Radius, start, end )
			.WithoutTags( "enemy", "player", "ground", "bullet" ).Run();

		Gizmo.Draw.Color = Color.Orange;
		Gizmo.Draw.Line( arm.WorldPosition, drop );
		Gizmo.Draw.LineSphere( drop, 6f );

		Gizmo.Draw.Line( start, wall.Hit ? wall.EndPosition : end );
		Gizmo.Draw.LineSphere( wall.Hit ? wall.EndPosition : end, agent.Radius );
	}

	void DrawMortarGizmos( Vector3 aim )
	{
		const int segments = 24;
		var start = arm.MortarPoint.WorldPosition;
		var previous = start;

		Gizmo.Draw.Color = Color.Blue;

		for ( int i = 1; i <= segments; i++ )
		{
			float t = (float)i / segments;
			var point = Vector3.Lerp( start, aim, t ) + Vector3.Up * (4f * Math.Max( 0f, arcHeight ) * t * (1f - t));

			Gizmo.Draw.Line( previous, point );
			previous = point;
		}

		Gizmo.Draw.LineSphere( aim, blastRadius );
	}

	void UpdateCharge()
	{
		float step = Math.Min( chargeLeft, Math.Max( 1, dashSpeed ) * Time.Delta );
		var position = agent.AgentPosition;
		var start = position + Vector3.Up * agent.Radius;
		var hit = Scene.Trace.Sphere( agent.Radius, start, start + chargeDirection * step )
			.WithoutTags( "enemy", "player", "ground", "bullet" ).Run();
		float travel = hit.StartedSolid ? 0f : hit.Distance;

		agent.SetAgentPosition( position + chargeDirection * (hit.Hit ? travel : step) );
		chargeLeft -= step;

		if ( !hit.Hit && chargeLeft > 0f )
			return;

		Presentation.CurrentAnimation = hit.Hit ? MainBossAnimation.HitWall : MainBossAnimation.Walking;
		chargeLeft = 0f;
	}

	protected override void OnDisabled()
	{
		patternCancellation?.Cancel();

		if ( !IsProxy )
			ResetPattern();

		specialAvailable = false;
	}

	protected override void OnDestroy()
	{
		patternCancellation?.Cancel();
		patternCancellation?.Dispose();
		patternCancellation = null;

		if ( !IsProxy && arm.IsValid() && arm.Detached )
			arm.GameObject.Destroy();
		base.OnDestroy();
	}
}
