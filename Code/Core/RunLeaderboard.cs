using Sandbox;
using Sandbox.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
public static class RunLeaderboard
{
	public const string StatName = "run_score";

	const double RoundWeight = 1_000_000;
	const int MaxSeconds = 999_999;

	public struct Row
	{
		public long Rank;
		public string Name;
		public int Rounds;
		public int Seconds;
		public bool IsMe;
	}

	public struct LadderPage
	{
		public List<Row> Top;
		public Row? Me;
		public long TotalPlayers;
	}

	public static double Encode( int rounds, float seconds )
	{
		int clampedSeconds = Math.Clamp( (int)MathF.Ceiling( seconds ), 0, MaxSeconds );

		return rounds * RoundWeight + (MaxSeconds - clampedSeconds);
	}

	public static void Submit( int rounds, float seconds )
	{
		if ( rounds <= 0 )
			return;

		var data = new Dictionary<string, object>
		{
			["rounds"] = rounds,
			["seconds"] = (int)MathF.Ceiling( seconds )
		};

		Stats.SetValue( StatName, Encode( rounds, seconds ), data );
	}

	public static Task FlushAsync() => Stats.FlushAndWaitAsync();

	public static async Task<LadderPage> FetchAsync( int topCount )
	{
		var topBoard = CreateBoard();
		topBoard.MaxEntries = topCount;

		var meBoard = CreateBoard();
		meBoard.MaxEntries = 5;
		meBoard.CenterOnMe();


		var topRefresh = topBoard.Refresh();
		var meRefresh = meBoard.Refresh();

		await topRefresh;
		await meRefresh;

		long mySteamId = (long)Game.SteamId;
		var me = meBoard.Entries?.FirstOrDefault( entry => entry.SteamId == mySteamId );

		return new LadderPage
		{
			Top = topBoard.Entries?.Select( entry => ToRow( entry, mySteamId ) ).ToList() ?? new List<Row>(),
			Me = me?.Rank > 0 ? ToRow( me.Value, mySteamId ) : null,
			TotalPlayers = Math.Max( topBoard.TotalEntries, meBoard.TotalEntries )
		};
	}

	public static string FormatTime( int seconds )
	{
		var time = TimeSpan.FromSeconds( seconds );

		return time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time:mm\\:ss}" : $"{time:mm\\:ss}";
	}

	static Leaderboards.Board2 CreateBoard()
	{
		var board = Leaderboards.GetFromStat( StatName );
		board.SetAggregationMax();
		board.SetSortDescending();

		return board;
	}

	static Row ToRow( Leaderboards.Board2.Entry entry, long mySteamId )
	{
		int rounds = (int)(entry.Value / RoundWeight);
		int seconds = MaxSeconds - (int)(entry.Value % RoundWeight);

		return new Row
		{
			Rank = entry.Rank,
			Name = entry.DisplayName,
			Rounds = rounds,
			Seconds = seconds,
			IsMe = entry.SteamId == mySteamId
		};
	}
}
