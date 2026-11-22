using Sandbox;
using System;

/// <summary>
/// Draws a player's laser on every machine from the beam state the owner syncs on PlayerBehaviour.
/// Lives on a local, non networked child built at runtime, so the player prefab doesn't need editing.
/// </summary>
public sealed class PlayerLaserBeam : Component
{
	public PlayerBehaviour Player { get; set; }
	public GameObject GunPoint { get; set; }

	static readonly Color GlowColor = new Color( 1f, 0.12f, 0.18f );
	static readonly Color CoreColor = new Color( 1f, 0.85f, 0.9f );

	const float GlowWidth = 14f;
	const float CoreWidth = 4f;

	LineRenderer glow;
	LineRenderer core;
	PointLight muzzleLight;
	PointLight impactLight;
	GameObject impact;

	// Grows in when the trigger is pressed and fades out on release instead of popping
	float strength;

	public static PlayerLaserBeam Create( PlayerBehaviour player, GameObject gunPoint )
	{
		var go = new GameObject( player.GameObject, true, "Laser Beam" );
		go.NetworkMode = NetworkMode.Never;

		var beam = go.Components.Create<PlayerLaserBeam>();
		beam.Player = player;
		beam.GunPoint = gunPoint;
		beam.Build();

		return beam;
	}

	void Build()
	{
		glow = CreateLine( "Glow", GlowColor );
		core = CreateLine( "Core", CoreColor );

		muzzleLight = GameObject.Components.Create<PointLight>();
		muzzleLight.LightColor = GlowColor;
		muzzleLight.Radius = 120f;
		muzzleLight.Shadows = false;

		impact = new GameObject( GameObject, true, "Impact" );
		impact.NetworkMode = NetworkMode.Never;
		impactLight = impact.Components.Create<PointLight>();
		impactLight.LightColor = GlowColor;
		impactLight.Radius = 180f;
		impactLight.Shadows = false;

		SetVisible( false );
	}

	LineRenderer CreateLine( string name, Color color )
	{
		var go = new GameObject( GameObject, true, name );
		go.NetworkMode = NetworkMode.Never;

		var line = go.Components.Create<LineRenderer>();
		line.UseVectorPoints = true;
		line.VectorPoints = new List<Vector3>();
		line.Color = color;
		line.Additive = true;
		line.Lighting = false;
		line.CastShadows = false;

		return line;
	}

	protected override void OnUpdate()
	{
		if ( Player == null || !Player.IsValid() || GunPoint == null || !GunPoint.IsValid() )
			return;

		float target = Player.LaserFiring ? 1f : 0f;
		strength = strength.Approach( target, Time.Delta * (Player.LaserFiring ? 12f : 6f) );

		if ( strength <= 0.01f )
		{
			SetVisible( false );
			return;
		}

		SetVisible( true );

		Vector3 start = GunPoint.WorldPosition;
		Vector3 end = Player.LaserEnd;

		// A quick flicker keeps the beam alive, the core stays steadier than the glow
		float flicker = 0.85f + 0.15f * MathF.Sin( Time.Now * 60f ) + Game.Random.Float( -0.08f, 0.08f );

		SetPoints( glow, start, end );
		SetPoints( core, start, end );

		glow.Width = GlowWidth * strength * flicker;
		core.Width = CoreWidth * strength * (0.9f + 0.1f * flicker);

		muzzleLight.WorldPosition = start;
		muzzleLight.Intensity = 2f * strength * flicker;

		impact.WorldPosition = end;
		impactLight.Intensity = 3f * strength * flicker;
	}

	static void SetPoints( LineRenderer line, Vector3 start, Vector3 end )
	{
		line.VectorPoints.Clear();
		line.VectorPoints.Add( start );
		line.VectorPoints.Add( end );
	}

	void SetVisible( bool visible )
	{
		glow.Enabled = visible;
		core.Enabled = visible;
		muzzleLight.Enabled = visible;
		impactLight.Enabled = visible;
	}
}
