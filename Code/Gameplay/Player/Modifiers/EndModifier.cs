using Sandbox;

public class EndModifier
{
	public ModifierType modifierType;
	public int level = 1;

	public virtual bool EndBehaviour( SceneTraceResult traceResult, BulletBehaviour bullet, out bool updateNextStep )
	{
		updateNextStep = false;
		return true;
	}

	public virtual void AddLevel()
	{
		level++;
	}

	public virtual EndModifier Clone()
	{
		return new EndModifier();
	}

}

public class Bounce : EndModifier
{
	int nbBounce { get; set; } = 1;

	public Bounce()
	{
		modifierType = ModifierType.Bounce;
	}

	public override bool EndBehaviour( SceneTraceResult traceResult, BulletBehaviour bullet, out bool updateNextStep )
	{
		if ( !traceResult.HasTag( "enemy" ) && nbBounce > 0 )
		{
			Vector3 newDir = Vector3.Reflect( bullet.bulletInfo.direction, traceResult.Normal );

			newDir = newDir.WithZ( 0 );

			bullet.SetNewDirection( newDir );

			nbBounce--;

			updateNextStep = true;

			return false;
		}

		updateNextStep = false;
		return true;
	}

	public override void AddLevel()
	{
		level++;
		nbBounce = level;
	}

	public override EndModifier Clone()
	{
		Bounce bounceClone = new Bounce();

		bounceClone.level = level;
		bounceClone.nbBounce = level;

		return bounceClone;
	}

}

public class Percing : EndModifier
{
	int nbPercing = 1;

	public Percing()
	{
		modifierType = ModifierType.Percing;
	}

	public override bool EndBehaviour( SceneTraceResult traceResult, BulletBehaviour bullet, out bool updateNextStep )
	{
		GameObject collisionObj = traceResult.Collider.GameObject;
		bool alreadyEncountered = bullet.enemyHit.Contains( collisionObj );

		if ( traceResult.HasTag( "enemy" ) )
		{
			updateNextStep = true;


			if ( !alreadyEncountered )
			{
				nbPercing--;

				return nbPercing < 0;
			}

			return false;
		}

		updateNextStep = false;
		return true;
	}

	public override void AddLevel()
	{
		level++;
		nbPercing = level;
	}


	public override EndModifier Clone()
	{
		Percing bounceClone = new Percing();

		bounceClone.level = level;
		bounceClone.nbPercing = level;

		return bounceClone;
	}

}

public class EndExplosion : EndModifier
{
	public EndExplosion()
	{
		modifierType = ModifierType.Explosion;
	}

	public override bool EndBehaviour( SceneTraceResult traceResult, BulletBehaviour bullet, out bool updateNextStep )
	{
		GameObject collisionObj = traceResult.Collider.GameObject;
		GameObject bulletObj = bullet.GameObject;
		BulletInfo bulletInfo = bullet.bulletInfo;

		bool alreadyEncountered = bullet.enemyHit.Contains( collisionObj );


		if ( !alreadyEncountered )
		{
			int nbBullets = 4;
			int anglePerBullet = 360 / nbBullets;

			bulletInfo.damage = (bulletInfo.damage / 10f * level).Clamp( 0.5f, float.MaxValue );
			bulletInfo.endModifiers = null;
			bulletInfo.size /= 2f;
			bulletInfo.direction = Vector3.Forward;

			for ( int i = 0; i < nbBullets; i++ )
			{
				GameObject newBullet = bullet.poolManager.GetBullet();
				BulletBehaviour bulletBehaviour = newBullet.GetComponent<BulletBehaviour>();

				newBullet.WorldPosition = bulletObj.WorldPosition;

				bulletBehaviour.InitBall( bulletInfo, bullet.gameManager );
				bulletBehaviour.poolManager = bullet.poolManager;

				bulletInfo.direction = Rotation.FromYaw( anglePerBullet ) * bulletInfo.direction;

				newBullet.NetworkSpawn();
			}

		}

		updateNextStep = false;

		return true;
	}

	public override EndExplosion Clone()
	{
		return new EndExplosion();
	}
}
