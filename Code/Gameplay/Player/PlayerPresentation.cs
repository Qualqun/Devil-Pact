using Sandbox;
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


	bool shootAnim = false;
	bool dashAnim = false;


	protected override void OnStart()
	{
		base.OnStart();

		if ( !IsProxy )
		{
			Game.Music.Play( mainMusic, fade: 1.0f, loop: true, volume: 0.05f );
		}

	}

	protected override void OnUpdate()
	{
		AnimationUpdate();
	}

	void AnimationUpdate()
	{
		model.Set( "move_x", move.x );
		model.Set( "move_y", move.y );
		model.Set( "ShootSpeed", speedShoot );

		model.Set( "Shoot", shootAnim );
		model.Set( "Dash", dashAnim );

		shootAnim = false;
		dashAnim = false;
	}


	[Rpc.Broadcast]
	public void Shoot(GameObject gunPoint)
	{
		GameObject muzleFlash = muzzleFlash.Clone();

		muzleFlash.WorldPosition = gunPoint.WorldPosition;
		muzleFlash.LocalRotation = gunPoint.WorldRotation;

		GameObject.PlaySound( shootSound );
		shootAnim = true;
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
