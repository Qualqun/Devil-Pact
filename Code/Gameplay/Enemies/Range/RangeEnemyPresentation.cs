using Sandbox;
using System.Threading;
using System.Threading.Tasks;

public class RangeEnemyPresentation : BaseEnemyPresentation
{
	[Property, Group( "Visual shoot" )] float blinkSpeed { get; set; } = 0.01f;
	[Property, Group( "Visual shoot" )] LineRenderer laserInfo { get; set; }

	[Sync] public bool stand { get; set; } = false;
	bool shoot = false;

	CancellationTokenSource laserCancelationToken;

	protected override void AnimationUpdate()
	{

		model.Set( "Standing", stand );
		model.Set( "Shoot", shoot );

		shoot = false;
	}


	[Rpc.Broadcast]
	public void Shoot()
	{
		shoot = true;
	}


	[Rpc.Broadcast]
	public void UpdateLaser( Vector3 start, Vector3 end )
	{
		laserInfo.VectorPoints = new List<Vector3>
		{
			start,
			end
		};
	}


	[Rpc.Broadcast]
	public void StartBlink()
	{
		laserCancelationToken = new CancellationTokenSource();
		_ = LaserBlink( laserCancelationToken.Token );
	}

	[Rpc.Broadcast]
	public void ResetLaser()
	{
		laserCancelationToken.Cancel();
		laserCancelationToken.Dispose();
		laserCancelationToken = null;

		laserInfo.VectorPoints.Clear();
	}

	async Task LaserBlink( CancellationToken token )
	{
		float timer = 0;
		Gradient laserColor = laserInfo.Color;

		while ( !token.IsCancellationRequested )
		{
			timer += Time.Delta;

			if ( timer > blinkSpeed )
			{
				Color[] color = new Color[1];
				bool isRed = laserColor.Colors[0].Value == Color.Red;

				timer = 0f;
				color[0] = isRed ? Color.Yellow : Color.Red;

				laserColor = Gradient.FromColors( color );
				laserInfo.Color = laserColor;
			}

			await Task.FrameEnd();
		}
	}
}
