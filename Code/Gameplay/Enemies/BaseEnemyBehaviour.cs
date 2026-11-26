using Sandbox;
using System.Threading.Tasks;

public struct EnemyStats
{
	public float moveSpeed { get; set; }
	public bool isRange { get; set; }
	public float ballSpeed { get; set; }
}

public enum SpawnType
{
	Doors,
	Air,
}



public class BaseEnemyBehaviour : Component
{
	[Property, Order( 0 )] public SpawnType spawnType { get; set; } = SpawnType.Doors;
	[Property, Group( "Base stats" ), Order( 10 )] public float hp { get; set; } = 25f;
	[Property, Group( "Base stats" )] public float meleDamage { get; set; } = 8f;
	[Property, Group( "Base stats" )] protected float repathDistance { get; set; } = 64f;

	[Property, Group( "Growth stats" ), Order( 30 )] public float hpPerRound { get; set; } = 25f;

	[Property, Group( "Refs" ), Order( 100 )] protected NavMeshAgent agent { get; set; }
	[Property, Group( "Refs" )] protected BaseEnemyPresentation enemyPresentation { get; set; }

	protected List<PlayerBehaviour> players;
	public GameManager gameManager { get; set; }
	protected PlayerBehaviour target;
	protected float targetDist = float.MaxValue;
	protected bool noTarget = true;

	protected override void OnStart()
	{
		base.OnStart();

		if ( IsProxy )
		{
			Destroy();
		}
	}
	protected override void OnUpdate()
	{
		base.OnUpdate();

		CheckTarget();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		if ( Networking.IsHost )
		{
			gameManager?.GameState?.Enemies.Remove( GameObject );
			gameManager?.SpawnBonus( WorldPosition );
		}
	}

	protected void FollowPlayer()
	{
		if ( noTarget )
			return;

		if ( agent.TargetPosition.HasValue )
		{
			float targetDistance = Vector3.DistanceBetween( agent.TargetPosition.Value, target.WorldPosition );

			if ( targetDistance > repathDistance )
			{
				agent.MoveTo( target.WorldPosition );
			}
		}
		else
		{
			agent.MoveTo( target.WorldPosition );
		}
	}

	public void SetPlayers( List<PlayerBehaviour> allPlayers )
	{
		players = new List<PlayerBehaviour>( allPlayers );
	}

	#region Virtual Methods

	public virtual void InitEnemy( int roundNb )
	{
		roundNb -= 1;

		hp += hpPerRound * roundNb;
	}

	public virtual void TakeDamage( float amount )
	{
		hp -= amount;

		enemyPresentation?.TakeHit();

		if ( hp <= 0f )
		{
			GameObject.Destroy();
		}
	}

	protected virtual void CheckTarget()
	{
		target = null;
		targetDist = float.MaxValue;

		foreach ( var player in gameManager.GameState.Players )
		{
			float distance;

			if ( !player.IsValid() || player.isDead )
				continue;

			distance = WorldPosition.Distance( player.WorldPosition );

			if ( distance >= targetDist )
				continue;

			target = player;
			targetDist = distance;
		}

		noTarget = target is null;
	}

	#endregion

}
