using Sandbox;
using Sandbox.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Sandbox.UI.PanelTransform;

public struct StatsPerRound
{
	[Description( "Number of enemy in one second" )]
	public float spawnRate { get; set; }
	public float enemyHp { get; set; }
	public float enemyDamage { get; set; }
}

public sealed class GameManager : Component
{


	[Description( "Phases timer values" )]
	[Property, Group( "Timers" )] public float TimePerRound { get; private set; }

	[Property, Group( "Timers" )] public float TimePerWaitingRound { get; private set; }

	[Property, Group( "Timers" )] public float TimeGameOver { get; private set; }

	[Property, Group( "Timers" )] public float TimeStart { get; set; } = 5f;

	[Property, Group( "Stats" )] public int ExperienceBase { get; set; } = 100;
	[Property, Group( "Stats" )] public int ExperiencePerRound { get; set; } = 100;
	[Property, Group( "Stats" )] public float ExperienceSpawnDist { get; set; } = 32f;

	[Property, Group( "Refs" )] public GameState GameState { get; set; }
	[Property, Group( "Refs" )] public UIManager UiManager { get; set; }
	[Property, Group( "Refs" )] EnemySpawner EnemySpawner { get; set; }
	[Property, Group( "Refs" )] PoolManager PoolManager { get; set; }

	[Property, Group( "Refs" )] public GameObject StartZonePoint { get; set; }

	[Property, Group( "Refs" )] GameObject PlayerPrefab { get; set; }
	[Property, Group( "Refs" )] GameObject BonusPrefab { get; set; }

	CancellationTokenSource Cancellation;
	bool enemyCountShown;

	int AmountExperienceThisRound => ExperienceBase + (ExperiencePerRound * GameState.CurrentRound);

	protected override void OnStart()
	{
		LobbyConfig config = new();

		config.MaxPlayers = 4;

		Networking.CreateLobby( config );

		if ( !Networking.IsHost ) return;

		if ( GameState == null )
		{
			Log.Error( "[GameManager] GameState is not assigned." );
			return;
		}

		EnemySpawner.gameManager = this;

		GameState.Server_SetGameState( GameStateType.WaitingForPlayers );
		GameState.Server_SetCurrentRound( 0 );
		GameState.Server_SetPhaseTimer( 0f );

		Log.Info( "[GameManager] Initialized." );
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( !Networking.IsHost )
			return;

		if ( GameState == null )
			return;

		const float FixedDeltaTime = 0.02f;

		if ( GameState.State != GameStateType.WaitingForPlayers )
			GameState.Server_SetPhaseTimer( MathF.Max( 0f, GameState.PhaseTimer - FixedDeltaTime ) );

		GameState.Server_SetEnemyCount( GameState.Enemies.Count( enemy => enemy != null && enemy.IsValid() ) );

		switch ( GameState.State )
		{
			case GameStateType.Starting:
				StartRoutine();
				break;

			case GameStateType.Playing:
				PlayRoutine();
				break;

			case GameStateType.WaitingForNextRound:
				WaitingForNextRoundRoutine();
				break;

			case GameStateType.GameOver:
				GameOverRoutine();
				break;
		}
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		if ( GameState == null )
			return;

		if ( GameState.State == GameStateType.Starting )
		{
			float remaining = MathF.Max( 0f, TimeStart - GameState.PhaseTimer );
			Log.Info( $"Start countdown: {remaining:F1}s" );
		}

		if ( GameState.State == GameStateType.WaitingForNextRound )
		{
			Log.Info( $"Next round in: {GameState.PhaseTimer:F1}s / {TimePerWaitingRound:F1}s" );
		}
	}

	public List<PlayerBehaviour> GetAllPlayers()
	{
		return Scene.GetAllComponents<PlayerBehaviour>()
			.Where( player => player != null && player.IsValid() )
			.ToList();
	}

	public bool AreAllPlayersDead()
	{
		if ( !Networking.IsHost )
			return false;

		var allPlayers = GetAllPlayers();

		if ( allPlayers.Count == 0 )
			return false;

		return allPlayers.All( player => player.isDead );
	}

	public bool AreAllPlayersPermanentlyDead()
	{
		if ( !Networking.IsHost )
			return false;

		var allPlayers = GetAllPlayers();

		if ( allPlayers.Count == 0 )
			return false;

		return allPlayers.All( player => player.isPermanentlyDead );
	}

	[Rpc.Broadcast]
	public void Broadcast_ShowDeathRewards()
	{
		DeathRewards panel = Scene.GetAllComponents<DeathRewards>().FirstOrDefault();

		if ( panel != null )
			panel.Show();
		else
			Log.Warning( "DeathRewards not found in the scene!" );
	}

	[Rpc.Broadcast]
	public void Broadcast_ShowEnemyCount()
	{
		GameHUD panel = Scene.GetAllComponents<GameHUD>().FirstOrDefault();

		if ( panel != null )
			panel.ShowEnemyCount();
		else
			Log.Warning( "ShowEnemyCount not found in the scene!" );
	}

	[Rpc.Broadcast]
	public void Broadcast_HideEnemyCount()
	{
		GameHUD panel = Scene.GetAllComponents<GameHUD>().FirstOrDefault();

		if ( panel != null )
			panel.HideEnemyCount();
		else
			Log.Warning( "ShowEnemyCount not found in the scene!" );
	}

	[Rpc.Broadcast]
	public void Broadcast_HideAllPanels()
	{
		foreach ( var panel in Scene.GetAllComponents<DeathRewards>() )
			panel.Hide();

		foreach ( var panel in Scene.GetAllComponents<ContinuePrompt>() )
			panel.Hide();
	}

	#region Game Initialization

	public void SpawnPlayer( Connection connection )
	{
		Log.Info( $"[GameManager] Start spawn player for {connection.DisplayName}" );

		if ( !Networking.IsHost )
			return;

		if ( PlayerPrefab == null )
		{
			Log.Error( "[GameManager] PlayerPrefab is not assigned." );
			return;
		}

		GameObject player = PlayerPrefab.Clone( WorldTransform );
		PlayerBehaviour playerBehaviour = player.GetComponent<PlayerBehaviour>();

		GameState.Server_AddPlayer( playerBehaviour );

		playerBehaviour.gameManager = this;
		playerBehaviour.poolManager = PoolManager;

		player.NetworkSpawn( connection );
		Log.Info( $"[GameManager] Spawned player for {connection.DisplayName}" );
	}

	public void PlayerEnteredStartArea( PlayerBehaviour player )
	{
		if ( !Networking.IsHost )
			return;

		if ( GameState == null )
			return;

		if ( player.isInStartZone )
			return;

		player.isInStartZone = true;
		GameState.Server_SetPlayerReadyCount( GameState.PlayerReadyCount + 1 );

		if ( GameState.State == GameStateType.WaitingForPlayers )
		{
			if ( GameState.Players.Count > 0 && GameState.PlayerReadyCount >= GameState.Players.Count )
			{
				GameState.Server_SetPhaseTimer( TimeStart );
				GameState.Server_SetGameState( GameStateType.Starting );
				Log.Info( "[GameManager] Game starting countdown..." );
			}
		}
	}

	public void PlayerLeftStartArea( PlayerBehaviour player )
	{
		if ( !Networking.IsHost )
			return;

		if ( GameState == null )
			return;

		if ( !player.isInStartZone )
			return;

		player.isInStartZone = false;
		GameState.Server_SetPlayerReadyCount( GameState.PlayerReadyCount - 1 );

		if ( GameState.State == GameStateType.Starting )
		{
			if ( GameState.PlayerReadyCount < GameState.Players.Count )
			{
				GameState.Server_SetPhaseTimer( 0f );
				GameState.Server_SetGameState( GameStateType.WaitingForPlayers );
				Log.Info( "[GameManager] Game starting countdown canceled." );
			}
		}
	}

	#endregion

	#region Gameplay loop

	public bool AllEnemiesDead()
	{
		return GameState.Enemies.All( enemy => enemy == null || !enemy.IsValid() );
	}

	public void ResetAllRewardTaken()
	{
		if ( !Networking.IsHost )
			return;

		var allPlayers = Scene.GetAllComponents<PlayerBehaviour>().ToList();

		foreach ( var player in allPlayers )
		{
			if ( player == null || !player.IsValid() || player.State == null )
				continue;

			player.State.ResetRewardTaken();
		}
	}


	public void Server_OnPlayerDied( PlayerBehaviour player )
	{
		if ( !Networking.IsHost )
			return;

		if ( GameState == null )
			return;

		if ( GameState.State == GameStateType.WaitingForPlayers || GameState.State == GameStateType.GameOver )
			return;

		if ( !AreAllPlayersDead() )
			return;

		if ( AreAllPlayersPermanentlyDead() )
		{
			GameOver();
			return;
		}

		if ( GameState.State != GameStateType.Playing && GameState.State != GameStateType.Starting )
			return;

		Log.Info( "[GameManager] The whole team is down, the round is lost..." );
		StartNextRound( true );
	}

	public void GameOver()
	{
		if ( !Networking.IsHost )
			return;

		if ( GameState == null )
			return;

		if ( GameState.State == GameStateType.GameOver || GameState.State == GameStateType.WaitingForPlayers )
			return;

		StopRoundSpawner();
		RemoveAllEnemies();
		Broadcast_HideEnemyCount();
		Broadcast_HideAllPanels();

		GameState.Server_SetPhaseTimer( TimeGameOver > 0f ? TimeGameOver : 5f );
		GameState.Server_SetGameState( GameStateType.GameOver );

		Log.Info( "[GameManager] Game over, nobody has a life left." );
	}

	public void Server_ResetGame()
	{
		if ( !Networking.IsHost )
			return;

		if ( GameState == null )
			return;

		StopRoundSpawner();
		RemoveAllEnemies();
		Broadcast_HideEnemyCount();
		Broadcast_HideAllPanels();

		GameState.Server_SetCurrentRound( 0 );
		GameState.Server_SetPhaseTimer( 0f );
		GameState.Server_SetPlayerReadyCount( 0 );

		RemoveAllExperience();

		Vector3 spawnPosition = WorldPosition;

		foreach ( var player in GetAllPlayers() )
		{
			player.Server_FullReset( spawnPosition );
		}

		GameState.Server_SetGameState( GameStateType.WaitingForPlayers );

		Log.Info( "[GameManager] Game reset, waiting for players again." );
	}

	public void StartNextRound( bool roundLost = false )
	{
		if ( !Networking.IsHost )
			return;

		if ( GameState == null )
			return;

		if ( GameState.State != GameStateType.Playing && GameState.State != GameStateType.Starting )
			return;

		GameState.Server_SetPhaseTimer( 0f );
		GameState.Server_SetGameState( GameStateType.WaitingForNextRound );

		StopRoundSpawner();
		RemoveAllEnemies();
		Broadcast_HideEnemyCount();

		ResetAllRewardTaken();
		Server_ReviveDownedPlayers();

		if ( roundLost )
		{
			Log.Info( "[GameManager] Round lost, the survivors get one more chance..." );
		}
		else
		{
			Log.Info( "[GameManager] Starting next round all enemies are dead..." );
		}
	}

	[Description( "Brings every downed player back for the inter round. Players out of lives stay ghosts." )]
	public void Server_ReviveDownedPlayers()
	{
		if ( !Networking.IsHost )
			return;

		foreach ( var player in GetAllPlayers() )
		{
			if ( !player.isDead )
				continue;

			player.Server_Revive();
		}
	}

	public void StartRound()
	{
		if ( !Networking.IsHost )
			return;

		if ( GameState == null )
			return;

		ResetAllRewardTaken();

		foreach ( var player in GetAllPlayers() )
		{
			player.mustDevilPact = false;
		}

		enemyCountShown = false;

		GameState.Server_SetCurrentRound( GameState.CurrentRound + 1 );
		GameState.Server_SetPhaseTimer( TimePerRound );
		GameState.Server_SetGameState( GameStateType.Playing );
		StartRoundSpawner();
	}

	void StartRoutine()
	{
		if ( GameState.PhaseTimer <= 0f )
			StartRound();
	}

	private void WaitingForNextRoundRoutine()
	{
		if ( AreAllPlayersPermanentlyDead() )
		{
			GameOver();
			return;
		}

		if ( AreAllPlayersTakedBoost() )
		{
			if ( GameState.PhaseTimer <= 0f )
			{
				float waitTime = TimePerWaitingRound > 0f ? TimePerWaitingRound : 5f;
				GameState.Server_SetPhaseTimer( waitTime );
				Log.Info( $"[GameManager] All players took their boost! Next round starts in {waitTime}s" );
				return;
			}

			if ( GameState.PhaseTimer <= 0.05f )
			{
				StartRound();
			}
		}
	}

	private void PlayRoutine()
	{
		if ( GameState.PhaseTimer > 0f )
			return;

		StopRoundSpawner();

		if ( !enemyCountShown )
		{
			enemyCountShown = true;
			Broadcast_ShowEnemyCount();
		}

		if ( AllEnemiesDead() )
			StartNextRound();
	}

	public bool AreAllPlayersTakedBoost()
	{
		if ( !Networking.IsHost )
			return false;

		var allPlayers = Scene.GetAllComponents<PlayerBehaviour>().ToList();
		int readyCount = 0;
		int validPlayers = 0;

		foreach ( var player in allPlayers )
		{
			if ( player == null || !player.IsValid() )
				continue;

			if ( player.isPermanentlyDead )
				continue;

			validPlayers++;

			if ( player.State != null && player.State.RewardTaken )
			{
				readyCount++;
			}
		}

		if ( validPlayers == 0 )
			return false;

		return readyCount == validPlayers;
	}

	private void GameOverRoutine()
	{
		if ( GameState.PhaseTimer <= 0f )
			Server_ResetGame();
	}



	public void RemoveAllExperience()
	{
		if ( !Networking.IsHost )
			return;

		foreach ( var experience in Scene.GetAllComponents<BonusBehaviour>().ToList() )
		{
			if ( experience != null && experience.IsValid() )
				experience.GameObject.Destroy();
		}
	}

	public void RemoveAllEnemies()
	{
		foreach ( var enemy in GameState.Enemies )
		{
			if ( enemy != null && enemy.IsValid() )
			{
				enemy.Destroy();
			}
		}
		GameState.Server_ClearEnemies();

		foreach ( var bullet in Scene.GetAllComponents<EnemyBulletBehaviour>().ToList() )
		{
			if ( bullet != null && bullet.IsValid() )
				bullet.GameObject.Destroy();
		}
	}

	void StartRoundSpawner()
	{
		StopRoundSpawner();
		Cancellation = new CancellationTokenSource();

		_ = EnemySpawner.RoundSpawner( Cancellation.Token );
	}

	public void StopRoundSpawner()
	{
		if ( Cancellation == null ) return;
		Cancellation.Cancel();
		Cancellation.Dispose();
		Cancellation = null;
	}

	#endregion

	[Rpc.Host]
	public void PlayerTakeDamage( PlayerBehaviour player, float amount )
	{
		if ( player == null || !player.IsValid() )
			return;

		player.Server_TakeHit( amount );
	}

	[Rpc.Host]
	public void PlayerTakeDamageFromEnemy( PlayerBehaviour player, GameObject enemyObj )
	{
		if ( player == null || !player.IsValid() )
			return;

		if ( enemyObj == null || !enemyObj.IsValid() )
			return;

		BaseEnemyBehaviour enemy = enemyObj.GetComponent<BaseEnemyBehaviour>();

		if ( enemy == null )
			return;

		player.Server_TakeHit( enemy.meleDamage );
	}

	#region Enemies methods


	[Rpc.Host]
	public void EnemyTakeDamage( GameObject enemyObj, float amount )
	{
		if ( enemyObj == null && !enemyObj.IsValid ) return;

		BaseEnemyBehaviour enemy = enemyObj.GetComponent<BaseEnemyBehaviour>();

		if ( enemy == null )
			return;

		enemy.TakeDamage( amount );
	}

	#endregion

	#region Experience methods

	public void SpawnBonus( Vector3 spawnPoint )
	{
		int nbPlayer;
		Vector3 offSet;
		float angle;

		if ( !Networking.IsHost || BonusPrefab == null ) return;

		if ( GameState == null || GameState.State != GameStateType.Playing ) return;

		nbPlayer = GameState.Players.Count;
		offSet = Vector3.Forward * ExperienceSpawnDist;
		angle = 360f / nbPlayer;

		spawnPoint += Vector3.Up * ExperienceSpawnDist;

		for ( int i = 0; i < nbPlayer; i++ )
		{
			GameObject xpObject;
			BonusBehaviour xpBehaviour;

			offSet = Rotation.FromYaw( angle ) * offSet;

			xpObject = BonusPrefab.Clone( spawnPoint + offSet );
			xpBehaviour = xpObject.GetComponent<BonusBehaviour>();


			xpBehaviour.InitBonus();
			xpBehaviour.amountExperience = AmountExperienceThisRound / EnemySpawner.NbEnemyThisRound;
			xpBehaviour.offset = ExperienceSpawnDist;
			xpBehaviour.center = spawnPoint;
			xpBehaviour.angle = angle * i;

			xpObject.NetworkSpawn();
		}

	}


	#endregion
}
