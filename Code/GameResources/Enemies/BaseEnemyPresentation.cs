using Sandbox;
using System.Threading.Tasks;

public struct VibrationInfo
{
	public Vector3 size { get; set; }
	public int nbVibration { get; set; }
	public float time { get; set; }
	public bool random { get; set; }
}

public class BaseEnemyPresentation : Component
{
	[Property, Group( "Visual hit" )] protected float visualHitDuration { get; set; } = 0.2f;
	[Property, Group( "Visual hit" )] protected VibrationInfo vibration { get; set; }




	[Property] protected SkinnedModelRenderer model { get; set; }


	bool hit;
	Task visualHitTask = null;

	protected override void OnStart()
	{
		base.OnStart();

		if ( !model.IsValid() || model.MaterialOverride is null )
		{
			return;
		}

		model.SceneObject.Batchable = false;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		AnimationUpdate();
	}


	protected virtual void AnimationUpdate()
	{
		model.Set( "EnemyHit", hit );

		hit = false;
	}

	[Rpc.Broadcast]
	public virtual void TakeHit()
	{
		hit = true;

		if ( visualHitTask == null )
		{
			visualHitTask = VisualTakeHit();
			_ = Shake( model.GameObject );
		}
	}

	async Task VisualTakeHit()
	{
		float timer = 0f;

		while ( timer <= visualHitDuration )
		{
			model.Attributes.Set( "PercentFlash", (timer / visualHitDuration).Clamp( 0f, 1f ) );
			timer += Time.Delta;

			await Task.FrameEnd();
		}

		timer = visualHitDuration;

		while ( timer >= 0 )
		{

			model.Attributes.Set( "PercentFlash", (timer / visualHitDuration).Clamp( 0f, 1f ) );
			timer -= Time.Delta * 2f;

			await Task.FrameEnd();
		}

		model.Attributes.Set( "PercentFlash", 0f );
		visualHitTask = null;
	}

	async Task Shake( GameObject obj )
	{
		float timePerVibration = vibration.time / vibration.nbVibration;
		float timer = 0f;

		int nbVibration = 1;
		int sideVibration = 1;

		Vector3 mainPos = obj.LocalPosition;
		Vector3 lastPos = obj.LocalPosition;

		Vector3 baseVibration = vibration.size;
		Vector3 vibrationGoal = vibration.random ? Vector3.Random.Abs().WithZ( 0 ) * baseVibration : baseVibration;

		vibrationGoal += lastPos;

		while ( timer <= vibration.time )
		{
			float percentVibration = timer / timePerVibration;

			obj.LocalPosition = Vector3.Lerp( lastPos, vibrationGoal, percentVibration );
			timer += Time.Delta;

			if ( nbVibration * timePerVibration >= timer )
			{
				timer = nbVibration * timePerVibration;
				lastPos = obj.LocalPosition;

				nbVibration++;
				sideVibration *= -1;

				vibrationGoal = vibration.random ? Vector3.Random.Abs() * baseVibration : baseVibration;
				vibrationGoal += lastPos;
				vibrationGoal *= sideVibration;
			}

			await Task.FrameEnd();
		}

		obj.LocalPosition = mainPos;
	}
}
