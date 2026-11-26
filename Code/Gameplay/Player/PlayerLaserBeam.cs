using Sandbox;
using System;

/// <summary>
/// Draws a player's laser on every machine from the beam state the owner syncs on PlayerLaser.
/// The look lives in three prefabs under art/fx/ai (beam, impact, muzzle), cloned locally and never networked.
/// </summary>
public sealed class PlayerLaserBeam : Component
{
	const string BeamPrefab = "art/fx/ai/laser_beam.prefab";
	const string ImpactPrefab = "art/fx/ai/laser_impact.prefab";
	const string MuzzlePrefab = "art/fx/ai/laser_muzzle.prefab";

	public PlayerLaser Laser { get; set; }
	public GameObject GunPoint { get; set; }

	GameObject beam;
	GameObject impact;
	GameObject muzzle;

	BeamEffect[] beamEffects = Array.Empty<BeamEffect>();
	(PointLight Light, float Intensity)[] lights = Array.Empty<(PointLight, float)>();

	// Grows in when the trigger is pressed and fades out on release instead of popping
	float strength;
	bool visible = true;

	public static PlayerLaserBeam Create( PlayerLaser source, GameObject gunPoint )
	{
		var go = new GameObject( source.GameObject, true, "Laser Beam" );
		go.NetworkMode = NetworkMode.Never;

		var laser = go.Components.Create<PlayerLaserBeam>();
		laser.Laser = source;
		laser.GunPoint = gunPoint;
		laser.Build();

		return laser;
	}

	void Build()
	{
		beam = Spawn( BeamPrefab );
		impact = Spawn( ImpactPrefab );
		muzzle = Spawn( MuzzlePrefab );

		beamEffects = beam?.GetComponentsInChildren<BeamEffect>( true ).ToArray() ?? Array.Empty<BeamEffect>();

		// Remember each light's authored intensity so the fade scales it instead of overwriting it
		lights = new[] { impact, muzzle }
			.Where( go => go != null )
			.SelectMany( go => go.GetComponentsInChildren<PointLight>( true ) )
			.Select( light => (light, light.Intensity) )
			.ToArray();

		SetVisible( false );
	}

	GameObject Spawn( string prefab )
	{
		var go = GameObject.GetPrefab( prefab )?.Clone( new CloneConfig { Parent = GameObject, StartEnabled = true } );

		if ( go == null )
		{
			Log.Warning( $"[Laser] Missing VFX prefab {prefab}" );
			return null;
		}

		go.NetworkMode = NetworkMode.Never;
		return go;
	}

	protected override void OnUpdate()
	{
		if ( Laser == null || !Laser.IsValid() || GunPoint == null || !GunPoint.IsValid() )
			return;

		bool firing = Laser.IsFiring;
		strength = strength.Approach( firing ? 1f : 0f, Time.Delta * (firing ? 12f : 8f) );

		if ( strength <= 0.05f )
		{
			SetVisible( false );
			return;
		}

		SetVisible( true );

		Vector3 start = GunPoint.WorldPosition;
		Vector3 end = Laser.BeamEnd;

		if ( beam != null )
		{
			beam.WorldPosition = start;

			foreach ( var effect in beamEffects )
				effect.TargetPosition = end;
		}

		if ( muzzle != null )
			muzzle.WorldPosition = start;

		// The impact faces back towards the gun, so its sparks spray at the shooter's side of the hit
		if ( impact != null )
		{
			impact.WorldPosition = end;
			impact.WorldRotation = Rotation.LookAt( start - end );
		}

		float flicker = 0.85f + 0.15f * MathF.Sin( Time.Now * 60f ) + Game.Random.Float( -0.08f, 0.08f );

		foreach ( var (light, intensity) in lights )
			light.Intensity = intensity * strength * flicker;
	}

	void SetVisible( bool show )
	{
		if ( visible == show )
			return;

		visible = show;

		if ( beam != null ) beam.Enabled = show;
		if ( impact != null ) impact.Enabled = show;
		if ( muzzle != null ) muzzle.Enabled = show;
	}
}
