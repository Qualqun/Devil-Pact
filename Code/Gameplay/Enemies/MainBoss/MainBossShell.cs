using System;

public sealed class MainBossShell : Component
{
	GameManager manager;
	Vector3 start;
	Vector3 impact;
	float height;
	float duration;
	float radius;
	float damage;
	float elapsed;

	public void Launch( GameManager gameManager, Vector3 destination, float arcHeight, float flightTime,
		float blastRadius, float blastDamage )
	{
		manager = gameManager;

		start = WorldPosition;
		impact = destination;

		height = Math.Max( 0f, arcHeight );
		duration = Math.Max( 0.05f, flightTime );

		radius = blastRadius;
		damage = blastDamage;
	}

	protected override void OnUpdate()
	{
		float t;

		if ( IsProxy )
			return;

		elapsed += Time.Delta;
		t = Math.Min( elapsed / duration, 1f );

		WorldPosition = Vector3.Lerp( start, impact, t ) + Vector3.Up * (4f * height * t * (1f - t));
		if ( t < 1f )
			return;

		if ( manager.IsValid() && manager.GameState.IsValid() )
		{
			foreach ( var player in manager.GameState.Players )
			{
				if ( player.IsValid() && !player.isDead && impact.Distance( player.WorldPosition ) <= radius )
					manager.PlayerTakeDamage( player, damage );
			}
		}
		GameObject.Destroy();
	}
}
