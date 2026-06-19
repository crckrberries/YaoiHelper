#define DECLARE_TEXTURE(Name, index) \
    texture Name: register(t##index); \
    sampler Name##Sampler: register(s##index)

#define SAMPLE_TEXTURE(Name, texCoord) tex2D(Name##Sampler, texCoord)

uniform float Time; 
uniform float2 CamPos; 
uniform float2 Dimensions; 

uniform float4x4 ViewMatrix;
uniform float4x4 TransformMatrix;

DECLARE_TEXTURE(text1, 0);
DECLARE_TEXTURE(text2, 1);
DECLARE_TEXTURE(mask, 2);

float4 SpritePixelShader(float2 uv : TEXCOORD0) : COLOR0
{
    return lerp(SAMPLE_TEXTURE(text1, uv), SAMPLE_TEXTURE(text2, uv), SAMPLE_TEXTURE(mask, uv).r);
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
