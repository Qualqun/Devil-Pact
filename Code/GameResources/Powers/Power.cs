using Sandbox;

public enum PowerRarity
{
	Common,
	Rare,
	Epic,
	Legendary
}

// Powers in the same slot don't stack: taking one replaces the one held before. None stacks freely.
public enum PowerSlot
{
	None,
	Weapon
}

[GameResource( "Power", "power", "Description de la ressource", Icon = "bolt" )]
public partial class Power : GameResource
{
	[Property]
	public string Title { get; set; }

	[Property]
	public BoostType BoostType { get; set; }

	[Property]
	public PowerRarity Rarity { get; set; } = PowerRarity.Common;

	[Property]
	public Texture Image { get; set; }

	[Property, TextArea]
	public string Description { get; set; }

	public PowerSlot Slot => GetSlot( BoostType );

	public static string GetTitle( BoostType boost ) =>
		ResourceLibrary.GetAll<Power>().FirstOrDefault( p => p != null && p.BoostType == boost )?.Title ?? boost.ToString();

	public static PowerSlot GetSlot( BoostType boost )
	{
		switch ( boost )
		{
			case BoostType.Shotgun:
			case BoostType.Uzi:
			case BoostType.Sniper:
				return PowerSlot.Weapon;
			default:
				return PowerSlot.None;
		}
	}

	[Description( "Relative chance of a rarity being rolled for one card, before a power of that rarity is picked." )]
	public static float GetRarityWeight( PowerRarity rarity )
	{
		switch ( rarity )
		{
			case PowerRarity.Rare:
				return 28f;
			case PowerRarity.Epic:
				return 13f;
			case PowerRarity.Legendary:
				return 4f;
			default:
				return 55f;
		}
	}
}
