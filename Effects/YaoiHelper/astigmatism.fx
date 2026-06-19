#define DECLARE_TEXTURE(Name, index) \
    texture Name: register(t##index); \
    sampler Name##Sampler: register(s##index)

#define SAMPLE_TEXTURE(Name, texCoord) tex2D(Name##Sampler, texCoord)

uniform float Time; 
uniform float2 CamPos; 
uniform float2 Dimensions; 

uniform float4x4 ViewMatrix;
uniform float4x4 TransformMatrix;

DECLARE_TEXTURE(text, 0);

float4 SpritePixelShader(float2 uv : TEXCOORD0) : COLOR0
{
	float4 color = SAMPLE_TEXTURE(text, uv);

	// this is insanely inefficient but i don't really know what i can do about it 
	for (int i = -50; i < 50; i++) {
		float4 sampled = SAMPLE_TEXTURE(text, uv + float2(i + i * cos(Time) * 0.4, i + i * sin(Time) * 0.4) / Dimensions);
		color += (sampled * sampled * sampled * sampled) / (25 + abs(i) * 2);
	}

	return color;
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
