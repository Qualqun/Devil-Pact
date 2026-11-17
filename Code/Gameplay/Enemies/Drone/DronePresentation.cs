using Sandbox;
using System;
using System.Threading;
using System.Threading.Tasks;


public class DronePresentation : BaseEnemyPresentation
{


	[Property, Range( 0f, 100f )] float normalPowerFlame { get; set; } = 50f;

	[Property, Group( "Fall" ), Range( 0f, 100f )] float startFallPowerFlame { get; set; } = 0f;
	[Property, Group( "Fall" ), Range( 0f, 100f )] float endFallPowerFlame { get; set; } = 100f;
	[Property, Group( "Fall" )] float flameResetDuration { get; set; } = 0.25f;

	[Property, Group( "Move" ), Range( 0f, 100f )] float minMovePowerFlame { get; set; } = 25f;
	[Property, Group( "Move" ), Range( 0f, 100f )] float maxMovePowerFlame { get; set; } = 75f;
	[Property, Group( "Move" )] float moveRotation { get; set; } = 15f;
	[Property, Group( "Move" )] float moveDuration { get; set; } = 0.5f;

	[Property, Group( "Hovering" )] float hoverAmplitude { get; set; } = 25f;
	[Property, Group( "Hovering" )] Vector3 hoverOffset { get; set; }

	//[Property, Group( "Refs" ), Order( 50 )] new protected SkinnedModelRenderer model { get; set; }
	[Property, Group( "Refs" )] ThrusterFlame leftFlame { get; set; } = null;
	[Property, Group( "Refs" )] ThrusterFlame rightFlame { get; set; } = null;


	[Property, Group( "Debug" ), Order( 100 )] bool showGizmo { get; set; } = true;

	[Sync] float leftFlamePower { get; set; }
	[Sync] float rightFlamePower { get; set; }

	[Sync] Angles visualModelAngles { get; set; }
	[Sync] Vector3 visualModelPosition { get; set; }


	protected override void OnStart()
	{
		base.OnStart();

		visualModelAngles = model.LocalRotation.Angles();
	}

	protected override void OnUpdate()
	{
		leftFlame.ThrustPower = leftFlamePower;
		rightFlame.ThrustPower = rightFlamePower;

		model.LocalRotation = visualModelAngles.ToRotation();
		model.LocalPosition = visualModelPosition;
	}

	protected override void DrawGizmos()
	{

		if ( showGizmo )
		{
			base.DrawGizmos();

			Gizmo.Transform = global::Transform.Zero;

			Gizmo.Draw.LineSphere( model.WorldPosition + hoverOffset + Vector3.Up * hoverAmplitude, 5f );
			Gizmo.Draw.LineSphere( model.WorldPosition + hoverOffset - Vector3.Up * hoverAmplitude, 5f );

		}
	}

	[Rpc.Broadcast]
	public override void TakeHit()
	{
		hit = true;

		if ( visualHitTask == null )
		{
			visualHitTask = VisualTakeHit();
			_ = Shake( model.GameObject.Parent );
		}
	}


	public async void VisualDroneFall( float duration )
	{
		float timer = 0.01f;

		duration = float.Max( duration, 0.01f );

		while ( timer <= duration )
		{
			float amount = (timer / duration).Clamp( 0f, 1f );

			leftFlamePower = float.Lerp( startFallPowerFlame, endFallPowerFlame, amount );
			rightFlamePower = float.Lerp( startFallPowerFlame, endFallPowerFlame, amount );

			timer += Time.Delta;

			await Task.FrameEnd();
		}

		timer = 0;

		while ( timer <= flameResetDuration )
		{
			float amount = (timer / flameResetDuration).Clamp( 0f, 1f );

			leftFlamePower = float.Lerp( endFallPowerFlame, normalPowerFlame, amount );
			rightFlamePower = float.Lerp( endFallPowerFlame, normalPowerFlame, amount );

			timer += Time.Delta;

			await Task.FrameEnd();
		}

	}

	public async void Move( int direction )
	{
		float timer = 0.01f;

		float startPercentLeft = leftFlame.ThrustPower;
		float startPercentRight = rightFlame.ThrustPower;

		Angles angles = model.LocalRotation.Angles();
		float startPitch = angles.pitch;

		while ( timer <= moveDuration )
		{
			float amount = (timer / float.Max( moveDuration, 0.01f )).Clamp( 0f, 1f );

			float endPercentLeft = direction == 1 ? maxMovePowerFlame : minMovePowerFlame;
			float endPercentRight = direction == 1 ? minMovePowerFlame : maxMovePowerFlame;

			leftFlamePower = float.Lerp( startPercentLeft, endPercentLeft, amount );
			rightFlamePower = float.Lerp( startPercentRight, endPercentRight, amount );

			angles.pitch = float.Lerp( startPitch, moveRotation * direction, amount );
			visualModelAngles = angles;

			timer += Time.Delta;

			await Task.FrameEnd();
		}
	}

	public async void FlyOverHead( float duration )
	{
		float timer = 0.01f;

		float halfFlyDuration = duration / 2f;

		float startPercentLeft = leftFlame.ThrustPower;
		float startPercentRight = rightFlame.ThrustPower;

		Angles angles = visualModelAngles;
		float startPitch = angles.pitch;

		while ( timer < halfFlyDuration )
		{
			float amount = (timer / float.Max( halfFlyDuration, 0.01f )).Clamp( 0f, 1f );

			leftFlamePower = float.Lerp( startPercentLeft, maxMovePowerFlame, amount );
			rightFlamePower = float.Lerp( startPercentRight, maxMovePowerFlame, amount );

			angles.pitch = float.Lerp( startPitch, 0f, amount );
			visualModelAngles = angles;


			timer += Time.Delta;

			await Task.FrameEnd();
		}

		timer = 0;

		while ( timer < halfFlyDuration )
		{
			float amount = (timer / float.Max( halfFlyDuration, 0.01f )).Clamp( 0f, 1f );

			leftFlamePower = float.Lerp( maxMovePowerFlame, normalPowerFlame, amount );
			rightFlamePower = float.Lerp( maxMovePowerFlame, normalPowerFlame, amount );

			timer += Time.Delta;

			await Task.FrameEnd();
		}

	}

	public async Task Hovering( CancellationToken token )
	{
		float timer = 0;

		while ( !token.IsCancellationRequested )
		{
			float amplitude = (float)Math.Sin( timer / 0.25f ) * hoverAmplitude;

			visualModelPosition = Vector3.Up * amplitude;
			timer += Time.Delta;

			await Task.FrameEnd();
		}

	}

}

