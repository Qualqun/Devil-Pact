using Sandbox;

public class MeleeEnemy : BaseEnemyBehaviour
{

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if(!noTarget)
		{
			FollowPlayer();
		}
	}


}
