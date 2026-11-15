HEADER
{
	Description = "Faceted thruster exhaust with additive light falloff";
}

MODES
{
	Forward();
}

COMMON
{
	#define S_TRANSLUCENT 1
	#include "common/shared.hlsl"
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
	float flIntensity : COLOR0;
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput v )
	{
		PixelInput i = ProcessVertex( v );
		i.flIntensity = GetExtraPerInstanceShaderData( v.nInstanceTransformID ).vTint.a;
		return FinalizeVertex( i );
	}
}

PS
{
	#include "common/pixel.hlsl"

	RenderState( BlendEnable, true );
	RenderState( SrcBlend, ONE );
	RenderState( DstBlend, ONE );
	RenderState( DepthWriteEnable, false );

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float x = saturate( i.vTextureCoords.x );
		float layer = i.vTextureCoords.y;
		float3 blue = float3( 0.33, 0.67, 1.0 );
		float3 white = float3( 1.0, 0.95, 0.87 );
		float3 gold = float3( 1.0, 0.56, 0.12 );
		float3 orange = float3( 1.0, 0.20, 0.035 );

		float3 color = lerp( blue, white, smoothstep( 0.0, 0.16, x ) );
		color = lerp( color, gold, smoothstep( 0.24, 0.49, x ) );
		color = lerp( color, orange, smoothstep( 0.52, 0.96, x ) );
		float heat = lerp( 1.75, 0.72, smoothstep( 0.13, 0.9, x ) );
		heat *= 1.0 - smoothstep( 0.87, 1.0, x );

		if ( layer > 1.5 )
		{
			color = lerp( gold, orange, x );
			heat = 0.9 * (1.0 - 0.7 * x);
		}
		else if ( layer > 0.5 )
		{
			float3 view = normalize( CalculatePositionToCameraDirWs( i.vPositionWithOffsetWs.xyz + g_vHighPrecisionLightingOffsetWs.xyz ) );
			float facing = saturate( abs( dot( normalize( i.vNormalWs ), view ) ) );
			heat *= 0.16 * facing * facing * smoothstep( 0.0, 0.13, x );
		}

		return float4( color * heat * i.flIntensity, 0.0 );
	}
}
