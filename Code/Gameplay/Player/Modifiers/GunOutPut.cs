using Sandbox;

public class GunOutPut
{
	public ModifierType modifierType;
	public int level = 1;

	public virtual void OutPutBehaviour( BulletInfo baseInfo, List<BulletInfo> bullets ) { }

	public virtual void AddLevel()
	{
		level++;
	}
}


public class ShotgunOutPut : GunOutPut
{
	public ShotgunOutPut()
	{
		modifierType = ModifierType.Shotgun;
	}

	public override void OutPutBehaviour( BulletInfo baseInfo, List<BulletInfo> bullets )
	{
		int nbBullets = 2 + level;
		int degRange = 120;

		BulletInfo bulletInfo = baseInfo;
		Vector3 startDirection = Rotation.FromYaw( -(degRange / 2) ) * bulletInfo.direction;

		bullets.Add( bulletInfo );

		for ( int i = 0; i < nbBullets - 1; i++ )
		{
			bulletInfo.direction = Rotation.FromYaw( Game.Random.Float( degRange ) ) * startDirection;
			bullets.Add( bulletInfo );
		}

	}
}
