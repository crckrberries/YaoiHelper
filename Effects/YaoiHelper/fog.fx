// ported from GLSL to HLSL
float hash( float n ) {
	return frac(sin(n)*43758.5453);
}

float noise( float3 x ) {
	// The noise function returns a value in the range -1.0f -> 1.0f
	float3 p = floor(x);
	float3 f = frac(x);

	f = f*f*(3.0-2.0*f);
	float n = p.x + p.y*57.0 + 113.0*p.z;

	return lerp(lerp(lerp( hash(n+0.0), hash(n+1.0),f.x),
				lerp( hash(n+57.0), hash(n+58.0),f.x),f.y),
			lerp(lerp( hash(n+113.0), hash(n+114.0),f.x),
				lerp( hash(n+170.0), hash(n+171.0),f.x),f.y),f.z);
}

float fbm(float3 x) {
	float v = 0.0;
	float a = 0.5;
	float3 shift = float3(100, 0, 0);
	for (int i = 0; i < 10; ++i) {
		v += a * noise(x);
		x = x * 2.0 + shift;
		a *= 0.5;
	}
	return v;
}

#define DECLARE_TEXTURE(Name, index) \
    texture Name: register(t##index); \
    sampler Name##Sampler: register(s##index)

#define SAMPLE_TEXTURE(Name, texCoord) tex2D(Name##Sampler, texCoord)

uniform float Time; // level.TimeActive
uniform float2 CamPos; // level.Camera.Position
uniform float2 Dimensions; // new Vector2(320, 180)

uniform float4x4 ViewMatrix;
uniform float4x4 TransformMatrix;

DECLARE_TEXTURE(text, 0);
DECLARE_TEXTURE(mask, 3);

float4 SpritePixelShader(float2 uv : TEXCOORD0) : COLOR0
{
	float3 p = float3(float2(uv.x * 2.5 + .5 * Time, uv.y * 2.5) + CamPos / Dimensions, .5 * Time);
	float n = fbm(p + fbm(p + fbm(p + fbm(p))));

	float4 fg = float4(1., 1., 1., 1.);
	if (SAMPLE_TEXTURE(mask, uv).r != 0.) {
		return SAMPLE_TEXTURE(text, uv);
	}

	return lerp(float4(0., 0., 0., 1.), fg, n * 1);
}

void SpriteVertexShader(inout float4 color    : COLOR0,
                        inout float2 texCoord : TEXCOORD0,
                        inout float4 position : SV_Position)
{
    position = mul(position, ViewMatrix);
    position = mul(position, TransformMatrix);
}

technique Shader
{
    pass pass0
    {
        VertexShader = compile vs_3_0 SpriteVertexShader();
        PixelShader = compile ps_3_0 SpritePixelShader();
    }
}
