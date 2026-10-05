using Sandbox;
using System;
using System.Threading;
using System.Threading.Tasks;

public sealed class EnemySpawner : Component
{
	[Property, Group( "Stats" )] public int baseNbEnemy { get; set; } = 10;
	[Property, Group( "Stats" )] public float factorEnemyPerRound { get; set; } = 1.4f;
	[Property, Group( "Stats" )] public float rangeSpawnAirEnemies { get; set; } = 1000f;

	[Property, Group( "List Refs" )] public List<GameObject> enemiesPrefabs { get; set; }
	[Property, Group( "List Refs" )] public List<GameObject> spawnPoints { get; set; }


	public GameManager gameManager { get; set; }

	public int NbEnemyThisRound => (int)(baseNbEnemy * MathF.Pow( factorEnemyPerRound, gameManager.GameState.CurrentRound - 1 ));


	public async Task RoundSpawner( CancellationToken token )
	{
		float spawnDelay = gameManager.TimePerRound / NbEnemyThisRound;

		while ( !token.IsCancellationRequested )
		{
			SpawnEnemy();

			await Task.DelaySeconds( spawnDelay );
			if ( token.IsCancellationRequested ) break;

		}
	}

	void SpawnEnemy()
	{
		if ( enemiesPrefabs == null || enemiesPrefabs.Count == 0 ) return;
		if ( spawnPoints == null || spawnPoints.Count == 0 ) return;

		int enemyType = Game.Random.Int( enemiesPrefabs.Count - 1 );

		GameObject enemyPrefab = enemiesPrefabs[enemyType];
		GameObject enemy = enemyPrefab.Clone();

		BaseEnemyBehaviour enemyBehaviour = enemy.GetComponent<BaseEnemyBehaviour>();

		switch ( enemyBehaviour.spawnType )
		{
			case SpawnType.Doors:
				enemyPrefab.WorldPosition = DoorSpawnPoint();
				break;
			case SpawnType.Air:
				enemyPrefab.WorldPosition = AirSpawnPoint();

				break;
			default:
				break;
		}


		enemy.NetworkSpawn();

		enemyBehaviour.gameManager = gameManager;

		enemyBehaviour.SetPlayers( gameManager.GameState.Players );
		enemyBehaviour.InitStats( gameManager.GameState.CurrentRound );

		gameManager.GameState.Server_AddEnemy( enemy );
	}

	Vector3 DoorSpawnPoint()
	{
		int spawnPoint = Game.Random.Int( spawnPoints.Count - 1 );
		Vector3 position = spawnPoints[spawnPoint].WorldPosition;
		return position;
	}

	Vector3 AirSpawnPoint()
	{
		while ( true )
		{
			Vector3 randGroundPosition = Game.Random.VectorInSphere( rangeSpawnAirEnemies ).WithZ( 0 );
			List<SceneTraceResult> results = Scene.Trace.Sphere( MathX.MeterToInch( 1f ), randGroundPosition, randGroundPosition ).RunAll().ToList();

			if ( results.Count > 0 )
			{
				bool isOnlyGround = true;

				foreach ( SceneTraceResult result in results )
				{
					if ( !result.HasTag( "ground" ) )
					{
						isOnlyGround = false;
						break;
					}
				}

				if( isOnlyGround )
				{
					return randGroundPosition;
				}
			}
		}
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		Gizmo.Transform = global::Transform.Zero;
		Gizmo.Draw.Color = Color.Yellow;
		Gizmo.Draw.LineSphere( WorldPosition, rangeSpawnAirEnemies );

	}
}
