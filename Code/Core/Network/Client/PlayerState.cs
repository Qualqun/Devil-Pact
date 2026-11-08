using Sandbox;
using System.Collections.Generic;
using System.Linq;

public enum BoostType
{
	Hp,
	Lives,
	MoveSpeed,
	FireRate,
	TimeInvulnerability,
	BulletDamage,
	BulletSpeed,
	Shotgun,
	Uzi,
	Sniper,
	Percing,
	Bounce,
	Explosion,
	HomingShot
}

public enum ModifierType
{
	Shotgun,
	Uzi,
	Sniper,
	Percing,
	Bounce,
	Explosion,
	HomingShot
}

public sealed class PlayerState : Component
{
	[Property, Sync] public float MaxHp { get; set; } = 100f;
	[Sync( SyncFlags.FromHost )] public float Hp { get; set; } = 100f;
	[Property, Sync] public int MaxLives { get; set; } = 5;
	[Sync( SyncFlags.FromHost )] public int Lives { get; set; } = 5;
	[Property, Sync] public int Experience { get; set; } = 0;
	[Property, Sync] public bool Dead { get; set; } = false;
	[Property, Sync] public int MaxExperience { get; set; } = 100;
	[Property, Sync] public int XpMultiplier { get; set; } = 3;
	[Property, Sync] public float MoveSpeed { get; set; } = 250f;
	[Property, Sync] public float TimeInvulnerability { get; set; } = 1f;
	[Property, Sync] public float BaseFireRate { get; set; } = 4f;
	[Property, Sync] public float BaseBulletDamage { get; set; } = 20f;
	[Property, Sync] public float BaseBulletSize { get; set; } = 1f;
	[Property, Sync] public float BaseBulletSpeed { get; set; } = 700f;
	[Property, Sync( SyncFlags.FromHost )] public bool RewardTaken { get; set; } = false;
	[Property, Sync] public float FireRateMultiplier { get; set; } = 1f;
	[Property, Sync] public float DamageMultiplier { get; set; } = 1f;
	[Property, Sync] public float SizeMultiplier { get; set; } = 1f;
	[Property, Sync] public NetList<ModifierType> ActiveModifiers { get; set; } = new();
	[Sync] public Vector3 Velocity { get; set; } = Vector3.Zero;

	public float FireRate => BaseFireRate * FireRateMultiplier;
	public float BulletDamage => BaseBulletDamage * DamageMultiplier;
	public float BulletSize => BaseBulletSize * SizeMultiplier;
	public float BulletSpeed => BaseBulletSpeed;

	#region Spawn snapshot

	[Description( "Values the prefab was spawned with, restored when the whole game is reset." )]
	bool spawnStatsCaptured;
	float spawnMaxHp;
	int spawnMaxLives;
	int spawnExperience;
	int spawnMaxExperience;
	int spawnXpMultiplier;
	float spawnMoveSpeed;
	float spawnTimeInvulnerability;
	float spawnBaseFireRate;
	float spawnBaseBulletDamage;
	float spawnBaseBulletSize;
	float spawnBaseBulletSpeed;

	[Description( "Max hp the prefab spawns with, readable from any machine." )]
	public float SpawnMaxHp => spawnMaxHp;

	protected override void OnAwake()
	{
		base.OnAwake();

		CaptureSpawnStats();
	}

	void CaptureSpawnStats()
	{
		if ( spawnStatsCaptured )
			return;

		spawnStatsCaptured = true;

		Lives = MaxLives;
		spawnMaxHp = MaxHp;
		spawnMaxLives = MaxLives;
		spawnExperience = Experience;
		spawnMaxExperience = MaxExperience;
		spawnXpMultiplier = XpMultiplier;
		spawnMoveSpeed = MoveSpeed;
		spawnTimeInvulnerability = TimeInvulnerability;
		spawnBaseFireRate = BaseFireRate;
		spawnBaseBulletDamage = BaseBulletDamage;
		spawnBaseBulletSize = BaseBulletSize;
		spawnBaseBulletSpeed = BaseBulletSpeed;
	}

	#endregion

	#region Run stats

	public int Kills { get; private set; }
	public float DamageDealt { get; private set; }
	public int Deaths { get; private set; }
	public int PactsMade { get; private set; }
	public int LevelsGained { get; private set; }
	public List<BoostType> ClaimedBoosts { get; private set; } = new();

	public void AddDamageDealt( float amount ) => DamageDealt += amount;
	public void AddDeath() => Deaths++;
	public void AddLevelGained() => LevelsGained++;

	public void AddPactMade()
	{
		PactsMade++;
		RunAchievements.OnPactMade();
	}

	[Rpc.Owner]
	public void Owner_AddKill()
	{
		Kills++;
		RunAchievements.OnKill();
	}

	void ResetRunStats()
	{
		Kills = 0;
		DamageDealt = 0f;
		Deaths = 0;
		PactsMade = 0;
		LevelsGained = 0;
		ClaimedBoosts.Clear();
	}

	#endregion

	private void AddModifier( ModifierType modifier )
	{
		ActiveModifiers.Add( modifier );
	}

	public void TakeDamage( float amount )
	{
		if ( !Networking.IsHost )
			return;

		Hp -= amount;
	}

	public void WinLife()
	{
		Lives++;
	}

	public void LoseLife()
	{
		Lives = System.Math.Max( 0, Lives - 1 );
	}

	
	public bool AddExperience( int amount )
	{
		Experience += amount;

		if( Experience >= MaxExperience)
		{
			Experience -= MaxExperience;
			XpMultiplier += 1;
			return true;
		}

		return false;
	}

	
	[Rpc.Host]
	public void Host_LoseLife()
	{
		LoseLife();
	}

	public void LoseHalfLevels()
	{
		if ( !Networking.IsHost )
			return;

		XpMultiplier = System.Math.Max( 0, XpMultiplier / 2 );
	}

	[Rpc.Host]
	public void Host_ConsumeLevel()
	{
		XpMultiplier = System.Math.Max( 0, XpMultiplier - 1 );
	}

	[Rpc.Host]
	public void Host_TakeReward()
	{
		RewardTaken = true;
	}
	[Rpc.Host]
	public void Host_ApplyBoost( BoostType boost )
	{
		switch ( boost )
		{
			case BoostType.Hp:
				Hp += 30f;
				break;
			case BoostType.Lives:
				Lives += 1;
				break;
		}
	}

	[Rpc.Broadcast]
	public void Broadcast_ResetAll()
	{
		CaptureSpawnStats();

		if ( Networking.IsHost )
		{
			Hp = spawnMaxHp;
			Lives = spawnMaxLives;
			Experience = spawnExperience;
			XpMultiplier = spawnXpMultiplier;
			RewardTaken = false;
		}

		if ( IsProxy )
			return;

		MaxHp = spawnMaxHp;
		MaxLives = spawnMaxLives;
		MaxExperience = spawnMaxExperience;
		MoveSpeed = spawnMoveSpeed;
		TimeInvulnerability = spawnTimeInvulnerability;

		BaseFireRate = spawnBaseFireRate;
		BaseBulletDamage = spawnBaseBulletDamage;
		BaseBulletSize = spawnBaseBulletSize;
		BaseBulletSpeed = spawnBaseBulletSpeed;

		FireRateMultiplier = 1f;
		DamageMultiplier = 1f;
		SizeMultiplier = 1f;

		Dead = false;

		ActiveModifiers.Clear();
		ResetRunStats();
	}

	public void ResetRewardTaken()
	{
		if ( !Networking.IsHost )
			return;

		RewardTaken = false;
	}

	#region Authority Methods

	[Authority]
	public void ApplyBoost( BoostType boost )
	{
		Host_ConsumeLevel();
		Host_ApplyBoost( boost );

		ClaimedBoosts.Add( boost );
		RunAchievements.OnPowerClaimed( ClaimedBoosts );

		switch ( boost )
		{
			case BoostType.Hp:
				MaxHp += 30f;
				break;
			case BoostType.Lives:
				MaxLives += 1;
				break;
			case BoostType.MoveSpeed:
				MoveSpeed += 25f;
				break;
			case BoostType.FireRate:
				BaseFireRate += 0.25f;
				break;
			case BoostType.TimeInvulnerability:
				TimeInvulnerability += 1f;
				break;
			case BoostType.BulletDamage:
				BaseBulletDamage += 5f;
				break;
			case BoostType.BulletSpeed:
				BaseBulletSpeed += 100f;
				break;

			case BoostType.Shotgun:
				FireRateMultiplier *= 0.75f;
				DamageMultiplier *= 0.75f;
				AddModifier( ModifierType.Shotgun );
				break;
			case BoostType.Uzi:
				FireRateMultiplier *= 2f;
				DamageMultiplier *= 0.6f;
				AddModifier( ModifierType.Uzi );
				break;
			case BoostType.Sniper:
				FireRateMultiplier *= 0.4f;
				DamageMultiplier *= 1.9f;
				BaseBulletSpeed += 100f;
				AddModifier( ModifierType.Sniper );
				AddModifier( ModifierType.Percing );
				break;

			case BoostType.Percing:
				AddModifier( ModifierType.Percing );
				break;
			case BoostType.Bounce:
				AddModifier( ModifierType.Bounce );
				break;
			case BoostType.Explosion:
				AddModifier( ModifierType.Explosion );
				break;
			case BoostType.HomingShot:
				AddModifier( ModifierType.HomingShot );
				break;
		}
	}

	public List<(string Label, string From, string To)> GetBoostPreview( BoostType boost )
	{
		var lines = new List<(string Label, string From, string To)>();

		void Add( string label, float from, float to ) => lines.Add( (label, from.ToString( "0.##" ), to.ToString( "0.##" )) );
		void AddStacks( ModifierType modifier )
		{
			int stacks = ActiveModifiers.Count( m => m == modifier );
			Add( "Stacks", stacks, stacks + 1 );
		}

		switch ( boost )
		{
			case BoostType.Hp:
				Add( "Max HP", MaxHp, MaxHp + 30f );
				break;
			case BoostType.Lives:
				Add( "Lives", MaxLives, MaxLives + 1 );
				break;
			case BoostType.MoveSpeed:
				Add( "Move Speed", MoveSpeed, MoveSpeed + 25f );
				break;
			case BoostType.FireRate:
				Add( "Fire Rate", FireRate, (BaseFireRate + 0.25f) * FireRateMultiplier );
				break;
			case BoostType.TimeInvulnerability:
				Add( "Invulnerability", TimeInvulnerability, TimeInvulnerability + 1f );
				break;
			case BoostType.BulletDamage:
				Add( "Damage", BulletDamage, (BaseBulletDamage + 5f) * DamageMultiplier );
				break;
			case BoostType.BulletSpeed:
				Add( "Bullet Speed", BulletSpeed, BulletSpeed + 100f );
				break;

			case BoostType.Shotgun:
				Add( "Fire Rate", FireRate, FireRate * 0.75f );
				Add( "Damage", BulletDamage, BulletDamage * 0.75f );
				break;
			case BoostType.Uzi:
				Add( "Fire Rate", FireRate, FireRate * 2f );
				Add( "Damage", BulletDamage, BulletDamage * 0.6f );
				break;
			case BoostType.Sniper:
				Add( "Fire Rate", FireRate, FireRate * 0.4f );
				Add( "Damage", BulletDamage, BulletDamage * 1.9f );
				Add( "Bullet Speed", BulletSpeed, BulletSpeed + 100f );
				break;

			case BoostType.Percing:
				AddStacks( ModifierType.Percing );
				break;
			case BoostType.Bounce:
				AddStacks( ModifierType.Bounce );
				break;
			case BoostType.Explosion:
				AddStacks( ModifierType.Explosion );
				break;
			case BoostType.HomingShot:
				AddStacks( ModifierType.HomingShot );
				break;
		}

		return lines;
	}

	#endregion
}
