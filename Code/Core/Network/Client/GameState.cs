using Sandbox;

public enum GameStateType
{
	WaitingForPlayers,
	Starting,
	Playing,
	WaitingForNextRound,
	GameOver
}

public struct RoundGrowth
{

}

public sealed class GameState : Component
{
	[Sync( SyncFlags.FromHost )]
	public GameStateType State { get; private set; } = GameStateType.WaitingForPlayers;
																																		
	[Sync( SyncFlags.FromHost )]
	public int PlayerCount { get; private set; }

	[Sync( SyncFlags.FromHost )]
	public int PlayerReadyCount { get; private set; }

	[Sync( SyncFlags.FromHost )]
	public int CurrentRound { get; private set; }

	[Sync( SyncFlags.FromHost )]
	public float PhaseTimer { get; private set; }

	[Sync( SyncFlags.FromHost )]
	public int EnemyCount { get; private set; }

	[Sync( SyncFlags.FromHost )]
	public bool LastRoundLost { get; private set; }

	[Sync( SyncFlags.FromHost )]
	public float RunTime { get; private set; }

	public List<PlayerBehaviour> Players { get; private set; } = new List<PlayerBehaviour>();
	[Sync] public NetList<GameObject> Enemies { get; set; } = new();

	#region Server Setters

	public void Server_SetPhaseTimer( float timer )
	{
		if ( !Networking.IsHost )
			return;
		PhaseTimer = timer;
	}
	public void Server_SetGameState( GameStateType state )
	{
		if ( !Networking.IsHost )
			return;

		State = state;
	}


	public void Server_SetPlayerCount( int count )
	{
		if ( !Networking.IsHost )
			return;

		PlayerCount = count;
	}

	public void Server_SetCurrentRound( int round )
	{
		if ( !Networking.IsHost )
			return;

		CurrentRound = round;
	}

	public void Server_AddPlayer( PlayerBehaviour player )
	{
		if ( !Networking.IsHost )
			return;

		Players.Add( player );
	}

	public void Server_ClearEnemies()
	{
		if ( !Networking.IsHost )
			return;

		Enemies.Clear();
	}

	public void Server_RemovePlayer( PlayerBehaviour player )
	{
		if ( !Networking.IsHost )
			return;

		Players.Remove( player );
	}

	public void Server_AddEnemy( GameObject enemy )
	{
		if ( !Networking.IsHost )
			return;

		Enemies.Add( enemy );
	}

	public void Server_RemoveEnemy( GameObject enemy )
	{
		if ( !Networking.IsHost )
			return;

		Enemies.Remove( enemy );
	}

	public void Server_SetEnemyCount( int count )
	{
		if ( !Networking.IsHost )
			return;

		EnemyCount = count;
	}

	public void Server_SetRunTime( float seconds )
	{
		if ( !Networking.IsHost )
			return;

		RunTime = seconds;
	}

	public void Server_SetLastRoundLost( bool lost )
	{
		if ( !Networking.IsHost )
			return;

		LastRoundLost = lost;
	}

	public void Server_SetPlayerReadyCount( int count )
	{
		if ( !Networking.IsHost )
			return;

		PlayerReadyCount = count;
	}

	#endregion

}
