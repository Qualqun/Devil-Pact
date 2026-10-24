using Sandbox;
using Sandbox.Services;
using System;
using System.Collections.Generic;
using System.Linq;

public static class RunAchievements
{
	public const string BestRoundStat = "best_round";
	public const string KillsStat = "kills";
	public const string DevilPactsStat = "devil_pacts";

	const string FirstPact = "first_pact";
	const string LastBreath = "last_breath";
	const string BackFromDead = "back_from_dead";
	const string Arsenal = "arsenal";
	const string Speedrunner = "speedrunner";

	const int ArsenalPowers = 6;
	const int SpeedrunnerRound = 10;
	const float SpeedrunnerSeconds = 15 * 60;

	static readonly HashSet<string> unlockedThisSession = new();
	static TimeSince timeSinceRunStart;

	public static void OnKill()
	{
		Stats.Increment( KillsStat, 1 );
	}

	public static void OnPactMade()
	{
		Stats.Increment( DevilPactsStat, 1 );
		Unlock( FirstPact );
	}

	public static void OnPowerClaimed( IEnumerable<BoostType> claimedThisRun )
	{
		if ( claimedThisRun.Distinct().Count() >= ArsenalPowers )
			Unlock( Arsenal );
	}

	public static void OnRoundStarted( int round )
	{
		if ( round == 1 )
			timeSinceRunStart = 0;

		Stats.SetValue( BestRoundStat, round );

		if ( round >= SpeedrunnerRound && timeSinceRunStart < SpeedrunnerSeconds )
			Unlock( Speedrunner );
	}

	public static void OnRoundWon( PlayerBehaviour localPlayer )
	{
		if ( localPlayer == null || localPlayer.isPermanentlyDead )
			return;

		// mustDevilPact is only set on players who went down this round
		if ( localPlayer.mustDevilPact )
			Unlock( BackFromDead );
		else if ( localPlayer.State?.Lives == 1 )
			Unlock( LastBreath );
	}

	static void Unlock( string ident )
	{
		if ( !unlockedThisSession.Add( ident ) )
			return;

		try
		{
			Achievements.Unlock( ident );
		}
		catch ( Exception e )
		{
			unlockedThisSession.Remove( ident );
			Log.Warning( $"[Achievements] Could not unlock {ident}: {e.Message}" );
		}
	}
}
