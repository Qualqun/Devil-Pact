using Sandbox;

public class ThrusterControl : Component
{
	[Property, Range( 0f, 100f )] public float thrusterPower { get; private set; } = 100f;
	[Property] ParticleEffect coreParticle { get; set; }
	[Property] ParticleEffect plumeParticle { get; set; }

	ParticleFloat maxCoreLifeTime { get; set; }
	ParticleFloat maxPlumeLifeTime { get; set; }


	protected override void OnStart()
	{
		maxCoreLifeTime = coreParticle.Lifetime;
		maxPlumeLifeTime = plumeParticle.Lifetime;
	}

	public void SetThrusterPower( float thrusterPower )
	{
		var coreLifeTime = new ParticleFloat();
		var plumeLifeTime = new ParticleFloat();

		coreLifeTime.ConstantA = float.Lerp( 0, maxCoreLifeTime.ConstantA, thrusterPower / 100f );
		coreLifeTime.ConstantB = float.Lerp( 0, maxCoreLifeTime.ConstantB, thrusterPower / 100f );

		plumeLifeTime.ConstantA = float.Lerp( 0, maxPlumeLifeTime.ConstantA, thrusterPower / 100f );
		plumeLifeTime.ConstantB = float.Lerp( 0, maxPlumeLifeTime.ConstantB, thrusterPower / 100f );

		coreParticle.Lifetime = coreLifeTime;
		plumeParticle.Lifetime = plumeLifeTime;
	}

}


