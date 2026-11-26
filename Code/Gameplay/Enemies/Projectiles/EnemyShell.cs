using System.Threading.Tasks;

public sealed class EnemyShell : Component
{

	[Property] GameObject explosionFX { get; set; }
	[Property] Vector3 shellTargetOffset { get; set; }
	[Property] float radiusReference { get; set; } = 100f;


	public float damage { get; set; } = 10f;
	public float radiusExplosion { get; set; } = 50f;

	GameManager manager = null;

	public void InitProjectile( GameManager gameManager, Vector3 targetPos, float arcHeight, float duration )
	{
		Vector3 targetDir = (targetPos - WorldPosition).WithZ( 0 );
		Vector3 control = WorldPosition.WithZ( 0 ) + targetDir / 2f + Vector3.Up * arcHeight;

		targetPos = targetPos.WithZ( 0 );
		manager = gameManager;

		_ = GoToBezier( targetPos, control, duration );
	}

	async Task GoToBezier( Vector3 target, Vector3 control, float duration )
	{
		GameObject newExplosionFX;

		Vector3 startPosition = WorldPosition;
		Vector3 endPosition = target + shellTargetOffset;

		float timer = 0f;

		while ( timer <= duration )
		{
			float t = timer / duration;
			//float easedT = CustomEasing.EaseInOutCirc( t );

			WorldPosition = Vector3.CubicBezier( startPosition, endPosition, control, control, t );

			timer += Time.Delta;

			await Task.FrameEnd();
		}

		newExplosionFX = explosionFX.Clone( WorldPosition );
		newExplosionFX.WorldScale = radiusExplosion / radiusReference;
		newExplosionFX.NetworkSpawn();

		foreach ( var player in manager.GameState.Players )
		{
			if ( player.IsValid() && !player.isDead && target.Distance( player.WorldPosition ) <= radiusExplosion )
			{
				manager.PlayerTakeDamage( player, damage );
			}
		}

		GameObject.Destroy();
	}
}
