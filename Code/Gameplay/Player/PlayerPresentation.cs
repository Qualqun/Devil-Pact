using Sandbox;
using System;

public sealed class PlayerPresentation : Component
{
	#region Animation
	[Property] SkinnedModelRenderer model { get; set; }
	[Sync] public Vector2 move { get; set; }
	[Sync] public float speedShoot { get; set; } = 1f;
	#endregion

	#region FX
	[Property] GameObject muzzleFlash { get; set; }
	#endregion

	#region Audio
	[Property] SoundEvent shootSound { get; set; }
	[Property] SoundFile mainMusic { get; set; }

	#endregion

	bool dashAnim = false;

	// The upper body stays in the shooting pose between shots of a burst, and only leaves it after a short pause.
	// A one frame pulse made the graph flip in and out of the shooting state on every shot.
	const float ShootPoseMinHold = 0.3f;
	TimeSince sinceShot = float.MaxValue;

	float ShootPoseHold => MathF.Max( ShootPoseMinHold, 1.2f / MathF.Max( speedShoot, 0.01f ) );

	// AnimGraph 2 version of the player's animations; the legacy .vanmgrph on the prefab stays as a fallback
	const string AnimGraphPath = "animgraphs/player/player.ag";
	Sandbox.Animation.Graph animGraph;

	protected override void OnStart()
	{
		base.OnStart();

		UseAnimGraph2();

		if ( !IsProxy )
		{
			Game.Music.Play( mainMusic, fade: 1.0f, loop: true, volume: 0.05f );
		}

	}

	void UseAnimGraph2()
	{
		animGraph = Sandbox.Animation.Graph.Load( AnimGraphPath );

		if ( animGraph == null || animGraph.IsError )
		{
			Log.Warning( $"[PlayerPresentation] Couldn't load {AnimGraphPath}, keeping the legacy animgraph" );
			animGraph = null;
			return;
		}

		model.Animation.Graph = animGraph;
	}

	protected override void OnUpdate()
	{
		AnimationUpdate();
	}
	void AnimationUpdate()
	{
		bool shooting = sinceShot < ShootPoseHold;

		// Parameters can only be written while the AG2 instance runs, which isn't the case if the graph was swapped out
		if ( animGraph != null && model.Animation.Graph == animGraph )
		{
			var parameters = model.Animation.Parameters;
			parameters.Set( "move_x", move.x, false );
			parameters.Set( "move_y", move.y, false );
			parameters.Set( "ShootSpeed", speedShoot, false );
			parameters.Set( "Shoot", shooting, false );
			parameters.Set( "Dash", dashAnim, false );
		}
		else
		{
			model.Set( "move_x", move.x );
			model.Set( "move_y", move.y );
			model.Set( "ShootSpeed", speedShoot );
			model.Set( "Shoot", shooting );
			model.Set( "Dash", dashAnim );
		}

		dashAnim = false;
	}


	[Rpc.Broadcast]
	public void Shoot(GameObject gunPoint)
	{
		GameObject muzleFlash = muzzleFlash.Clone();

		muzleFlash.WorldPosition = gunPoint.WorldPosition;
		muzleFlash.LocalRotation = gunPoint.WorldRotation;

		GameObject.PlaySound( shootSound );
		sinceShot = 0f;
	}

	[Rpc.Broadcast]
	public void Dash()
	{
		dashAnim = true;
	}


	#region Visuals
	public void InvulnerabilityTint( bool mode, bool dash = false )
	{
		Color colorTint;

		if ( mode )
		{
			colorTint = dash ? model.Tint : Color.Red;
			colorTint.a = 0.35f;
		}
		else
		{
			colorTint = Color.White;
			colorTint.a = 1f;
		}

		model.Tint = colorTint;
	}

	public void DeadTint( bool permanent )
	{
		Color colorTint;

		colorTint = permanent ? Color.Black : Color.Blue;
		colorTint.a = 0.35f;

		model.Tint = colorTint;
	}

	public void ResetTint()
	{
		Color colorTint = Color.White;
		colorTint.a = 1f;

		model.Tint = colorTint;
	}
	#endregion
}
