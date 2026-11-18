using Sandbox;

public class ItemPool
{
	[KeyProperty] public GameObject item { get; set; }
	[Hide] public GameObject parent { get; set; }

	public int baseAmount { get; set; }
	[Hide] public Stack<GameObject> itemStack { get; set; }

	public void Init( GameObject newParent )
	{
		parent = newParent;
		itemStack = new Stack<GameObject>();

		AddObjectInStack();
	}

	public void AddObjectInStack()
	{
		for ( int i = 0; i < baseAmount; i++ )
		{
			GameObject obj = item.Clone();

			obj.SetParent( parent );
			obj.NetworkSpawn();

			obj.Enabled = false;

			itemStack.Push( obj );
		}
	}

	public GameObject GetObject()
	{
		GameObject obj;

		if ( itemStack.Count <= 0 )
		{
			AddObjectInStack();
		}

		obj = itemStack.Pop();
		obj.Enabled = true;

		return obj;
	}

	public GameObject GetObject(Vector3 worldPos, Rotation worldRotation)
	{
		GameObject obj;

		if ( itemStack.Count <= 0 )
		{
			AddObjectInStack();
		}

		obj = itemStack.Pop();

		obj.WorldPosition = worldPos;
		obj.WorldRotation = worldRotation;

		obj.Enabled = true;

		return obj;
	}

	public void DisposeObject( GameObject obj )
	{
		obj.Enabled = false;
		itemStack.Push( obj );
	}

}


public class PoolManager : Component
{
	[Property] public ItemPool playerBullet { get; set; }
	[Property] public ItemPool[] enemyProjectile { get; set; } = new ItemPool[(int)EnemyProjectile.NB_PROJECTILE];
	[Property] public ItemPool pistolEnemyBullet { get; set; }

	protected override void OnStart()
	{
		base.OnStart();

		playerBullet.Init( GameObject );

		if ( Networking.IsHost )
		{
			for ( int i = 0; i < enemyProjectile.Length; i++ )
			{
				enemyProjectile[i].Init( GameObject );
			}
		}
	}
}
