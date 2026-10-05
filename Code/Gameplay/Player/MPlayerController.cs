using Sandbox;
using System.Threading.Tasks;

public sealed class MPlayerController : Component
{
	[Property, Group( "Stats" )] float dashDuration { get; set; }
	[Property, Group( "Stats" )] float cooldown { get; set; }
	[Property, Group( "Stats" )] float dashSpeed { get; set; }

	[Property, Group( "Refs" )] PlayerBehaviour playerBehaviour { get; set; }
	[Property, Group( "Refs" )] CameraComponent camera { get; set; }
	[Property, Group( "Refs" )] GameObject visual { get; set; }
	[Property, Group( "Refs" )] Rigidbody rigidbody { get; set; }

	Task dashTask;
	bool dashEnd = true;

	protected override void OnStart()
	{
		base.OnStart();

		Mouse.Visibility = MouseVisibility.Visible;
		Mouse.CursorType = "crosshair";
	}

	protected override void OnUpdate()
	{
		PlayerRotation();
		Inputs();
	}

	void PlayerRotation()
	{
		if ( dashEnd )
		{
			Vector3 gunPointOffSet = Vector3.Up * playerBehaviour.gunPoint.WorldPosition.z;
			Ray ray = camera.ScreenPixelToRay( Mouse.Position );
			SceneTraceResult trace = Scene.Trace.Ray( ray.Position - gunPointOffSet, ray.Position + ray.Forward * 5000f - gunPointOffSet ).WithTag( "ground" ).Run();

			Vector3 direction = trace.EndPosition - visual.WorldPosition;

			direction = direction.WithZ( 0f );

			visual.WorldRotation = Rotation.LookAt( direction );
		}
	}

	void Inputs()
	{
		Vector3 velocity = Vector3.Zero;
		Vector3 localMove;
		float speed = playerBehaviour.State.MoveSpeed;

		if ( Input.Down( "Forward" ) )
		{
			velocity += Vector3.Forward;
		}

		if ( Input.Down( "Backward" ) )
		{
			velocity -= Vector3.Forward;
		}

		if ( Input.Down( "Left" ) )
		{
			velocity -= Vector3.Right;
		}

		if ( Input.Down( "Right" ) )
		{
			velocity += Vector3.Right;
		}

		if ( Input.Down( "Attack1" ) )
		{
			playerBehaviour.Fire();
		}

		if ( Input.Down( "Jump" ) && velocity != Vector3.Zero && dashTask == null && !playerBehaviour.isDead )
		{
			playerBehaviour.playerPresentation.Dash();
			dashTask = Dash( velocity.Normal );
		}

		if ( dashEnd )
		{
			localMove = visual.WorldRotation.Inverse * velocity;

			playerBehaviour.playerPresentation.move = new Vector2(
				localMove.y.Clamp( -1f, 1f ),
				localMove.x.Clamp( -1f, 1f )
			);

			rigidbody.Velocity = velocity.Normal * speed;
		}
	}

	async Task Dash( Vector3 velocity )
	{
		rigidbody.Velocity = velocity * dashSpeed;
		dashEnd = false;
		playerBehaviour.Broadcast_SetInvulnerability( true , true);

		await Task.DelaySeconds( dashDuration );

		rigidbody.Velocity = Vector3.Zero;
		dashEnd = true;
		playerBehaviour.Broadcast_SetInvulnerability( false );

		await Task.DelaySeconds( cooldown );

		dashTask = null;
	}
}
