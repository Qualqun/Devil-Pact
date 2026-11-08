using Sandbox;
using System;


public static class CustomEasing
{
	public static float EaseInOutCirc( float x )
	{
		return (float)(x < 0.5f
			? (1 - Math.Sqrt( 1 - Math.Pow( 2f * x, 2f ) )) / 2f
			: (Math.Sqrt( 1 - Math.Pow( -2f * x + 2f, 2f ) ) + 1) / 2f);
	}
}
