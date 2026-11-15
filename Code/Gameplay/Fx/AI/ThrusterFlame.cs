using System;

[Title( "Thruster Flame" ), Category( "Effects" )]
public sealed class ThrusterFlame : Component, Component.ExecuteInEditor
{
	private const int Sides = 8;
	private const int SparkCount = 8;

	private float thrustPower = 50f;
	private float smoothedPower;
	private float phase;
	private float emission;
	private SceneObject flame;
	private Model sparkModel;
	private readonly Spark[] sparks = new Spark[SparkCount];

	[Property, Title( "ThrustPower (%)" ), Range( 0, 100 )]
	public float ThrustPower
	{
		get => thrustPower;
		set => thrustPower = float.IsFinite( value ) ? Math.Clamp( value, 0f, 100f ) : 0f;
	}

	[Property]
	public Material FlameMaterial { get; set; }

	protected override void OnEnabled()
	{
		if ( FlameMaterial is null )
			return;

		var jet = BuildMesh(
			[(0f, 0.73f), (0.10f, 1f), (0.28f, 0.89f), (0.54f, 0.56f), (0.80f, 0.24f), (1f, 0f)], 0f );
		var halo = BuildMesh(
			[(0f, 0.76f), (0.12f, 1.4f), (0.32f, 1.28f), (0.62f, 0.70f), (0.84f, 0.27f), (1f, 0f)], 1f );
		flame = CreateVisual( Model.Builder.AddMesh( jet ).AddMesh( halo ).Create() );
		sparkModel = Model.Builder.AddMesh(
			BuildMesh( [(0f, 0f), (0.24f, 1f), (1f, 0f)], 2f ) ).Create();
		smoothedPower = ThrustPower * 0.01f;
		UpdateFlame();
	}

	protected override void OnDisabled()
	{
		flame?.Delete();
		flame = null;
		sparkModel = null;
		for ( int i = 0; i < sparks.Length; i++ )
		{
			sparks[i].Visual?.Delete();
			sparks[i] = default;
		}

		emission = 0f;
	}

	protected override void OnUpdate()
	{
		if ( flame is null )
			return;

		float dt = Math.Clamp( Time.Delta, 0f, 0.1f );
		float target = ThrustPower * 0.01f;
		smoothedPower += (target - smoothedPower) * (1f - MathF.Exp( -28f * dt ));
		if ( target == 0f && smoothedPower < 0.006f )
			smoothedPower = 0f;

		phase = (phase + dt) % (2f * MathF.PI);
		UpdateFlame();
		UpdateSparks( dt );

		if ( target <= 0f )
		{
			emission = 0f;
			return;
		}

		emission += 15f * smoothedPower * dt;
		while ( emission >= 1f )
		{
			emission -= 1f;
			EmitSpark();
		}
	}

	private float JetLength => smoothedPower * (34f - 7f * smoothedPower)
		* (1f + 0.036f * MathF.Sin( phase * 13f ) + 0.023f * MathF.Sin( phase * 21f ));

	private void UpdateFlame()
	{
		flame.RenderingEnabled = smoothedPower > 0f;
		if ( !flame.RenderingEnabled )
			return;

		float radius = (2.12f + 1.22f * smoothedPower) * MathF.Min( 1f, smoothedPower * 6f );
		flame.Transform = WorldTransform.ToWorld( new Transform(
			Vector3.Zero, Rotation.FromRoll( phase * (180f / MathF.PI) ),
			new Vector3( JetLength, radius, radius ) ) );
		float intensity = (0.37f + 0.63f * smoothedPower) * MathF.Min( 1f, smoothedPower * 5f );
		flame.ColorTint = Color.White.WithAlpha( intensity );
	}

	private void EmitSpark()
	{
		for ( int i = 0; i < sparks.Length; i++ )
		{
			ref var spark = ref sparks[i];
			if ( spark.Life > 0f )
				continue;

			var random = Random.Shared;
			float y = random.NextSingle() - 0.5f;
			float z = random.NextSingle() - 0.5f;
			spark.Visual ??= CreateVisual( sparkModel );
			spark.Origin = WorldTransform.ToWorld( new Transform(
				new Vector3( JetLength * 0.94f, y, z ), Rotation.Identity, Vector3.One ) );
			spark.Velocity = new Vector3( 21f + 15f * smoothedPower, y * 8f, z * 8f );
			spark.Duration = spark.Life = 0.11f + random.NextSingle() * 0.10f;
			spark.Size = 0.28f + random.NextSingle() * 0.16f;
			spark.Intensity = 0.5f + 0.45f * smoothedPower;
			DrawSpark( ref spark );
			return;
		}
	}

	private void UpdateSparks( float dt )
	{
		for ( int i = 0; i < sparks.Length; i++ )
		{
			ref var spark = ref sparks[i];
			if ( spark.Life <= 0f )
				continue;

			spark.Life = MathF.Max( 0f, spark.Life - dt );
			DrawSpark( ref spark );
		}
	}

	private static void DrawSpark( ref Spark spark )
	{
		float fade = spark.Life / spark.Duration;
		float radius = spark.Size * (0.35f + 0.65f * fade);
		spark.Visual.RenderingEnabled = spark.Life > 0f;
		spark.Visual.Transform = spark.Origin.ToWorld( new Transform(
			spark.Velocity * (spark.Duration - spark.Life), Rotation.LookAt( spark.Velocity ),
			new Vector3( 0.9f + 1.1f * fade, radius, radius ) ) );
		spark.Visual.ColorTint = Color.White.WithAlpha( spark.Intensity * fade );
	}

	private SceneObject CreateVisual( Model model )
	{
		var visual = new SceneObject( Scene.SceneWorld, model, WorldTransform );
		visual.Flags.CastShadows = false;
		return visual;
	}

	private Mesh BuildMesh( (float X, float Radius)[] rings, float layer )
	{
		var vertices = new List<Vertex>();
		var indices = new List<int>();
		foreach ( var (x, radius) in rings )
		{
			for ( int side = 0; side < Sides; side++ )
			{
				float angle = (side + 0.5f) * (2f * MathF.PI / Sides);
				var normal = new Vector3( 0f, MathF.Cos( angle ), MathF.Sin( angle ) );
				vertices.Add( new Vertex( new Vector3( x, 0f, 0f ) + normal * radius,
					normal, new Vector4( 1f, 0f, 0f, 1f ), new Vector2( x, layer ) ) );
			}
		}

		for ( int ring = 0; ring < rings.Length - 1; ring++ )
		{
			for ( int side = 0; side < Sides; side++ )
			{
				int a = ring * Sides + side;
				int b = ring * Sides + (side + 1) % Sides;
				indices.AddRange( [a, b, a + Sides, b, b + Sides, a + Sides] );
			}
		}

		// Close the jet where it meets the nozzle, including when viewed from upstream.
		if ( layer == 0f )
		{
			int center = vertices.Count;
			vertices.Add( new Vertex( Vector3.Zero, -Vector3.Forward,
				new Vector4( 0f, 1f, 0f, 1f ), Vector2.Zero ) );
			for ( int side = 0; side < Sides; side++ )
			{
				indices.AddRange( [center, (side + 1) % Sides, side] );
			}
		}

		var mesh = new Mesh( FlameMaterial );
		mesh.CreateVertexBuffer( vertices.Count, vertices );
		mesh.CreateIndexBuffer( indices.Count, indices );
		mesh.Bounds = new BBox( new Vector3( 0f, -1.4f, -1.4f ), new Vector3( 1f, 1.4f, 1.4f ) );
		return mesh;
	}

	private struct Spark
	{
		public SceneObject Visual;
		public Transform Origin;
		public Vector3 Velocity;
		public float Duration;
		public float Life;
		public float Size;
		public float Intensity;
	}
}
