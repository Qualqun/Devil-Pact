using Sandbox;

public enum PowerRarity
{
	Common,
	Rare,
	Epic,
	Legendary
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
