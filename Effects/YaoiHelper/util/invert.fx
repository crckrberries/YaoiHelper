#define DECLARE_TEXTURE(Name, index) \
    texture Name: register(t##index); \
    sampler Name##Sampler: register(s##index)

#define SAMPLE_TEXTURE(Name, texCoord) tex2D(Name##Sampler, texCoord)

DECLARE_TEXTURE(text, 0);

float4 SpritePixelShader(float2 uv : TEXCOORD0) : COLOR0
{
	float4 color = SAMPLE_TEXTURE(text, uv);
	float4 icolor = float4(1. - color.rgb, color.a);

	return icolor;
}

technique Shader
{
    pass pass0
    {
        PixelShader = compile ps_3_0 SpritePixelShader();
    }
}
