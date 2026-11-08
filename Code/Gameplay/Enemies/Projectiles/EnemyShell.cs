using System.Threading.Tasks;

public sealed class EnemyShell : Component
{
	[Property] float damage;
	[Property] float radiusExplosion;

	public void InitProjectile( Vector3 targetPos, float height, float duration )
	{
		Vector3 targetDir = (targetPos - WorldPosition).WithZ( 0 );
		Vector3 control = WorldPosition.WithZ( 0 ) + targetDir / 2f + Vector3.Up * height;

		targetPos = targetPos.WithZ( 0 );

		_ = GoToBezier( targetPos, control, duration );
	}

	async Task GoToBezier( Vector3 target, Vector3 control, float duration )
	{
		Vector3 startPosition = WorldPosition;
		Vector3 endPosition = target;

		float timer = 0f;

		while ( timer <= duration )
		{
			float t = timer / duration;
			float easedT = CustomEasing.EaseInOutCirc( t );

			WorldPosition = Vector3.CubicBezier( startPosition, endPosition, control, control, easedT );

			timer += Time.Delta;

			await Task.FrameEnd();
		}

	}
}
