using Sandbox;

public enum MainBossAnimation
{
	Walking,
	Shooting,
	Mortar,
	Counter,
	Running,
	HitWall,
	ShootArm
}

public sealed class MainBossPresentation : BaseEnemyPresentation
{
	/// <summary>Set on the host. Use HitWall after Running when a dash hits an obstacle.</summary>
	[Sync] public MainBossAnimation CurrentAnimation { get; set; } = MainBossAnimation.Walking;

	protected override void AnimationUpdate()
	{
		if ( !model.IsValid() || !model.Animation.IsValid )
			return;

		var parameters = model.Animation.Parameters;
		parameters.Set( "isShooting", CurrentAnimation == MainBossAnimation.Shooting );
		parameters.Set( "isMortarFire", CurrentAnimation == MainBossAnimation.Mortar );
		parameters.Set( "isCounter", CurrentAnimation == MainBossAnimation.Counter );
		parameters.Set( "isRunning", CurrentAnimation == MainBossAnimation.Running );
		parameters.Set( "HitWall", CurrentAnimation == MainBossAnimation.HitWall );
		parameters.Set( "IsShootingArm", CurrentAnimation == MainBossAnimation.ShootArm );
	}
}
