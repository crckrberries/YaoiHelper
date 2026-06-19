#define DECLARE_TEXTURE(Name, index) \
    texture Name: register(t##index); \
    sampler Name##Sampler: register(s##index)

#define SAMPLE_TEXTURE(Name, texCoord) tex2D(Name##Sampler, texCoord)

DECLARE_TEXTURE(text, 0);

float4 Invert(float2 uv : TEXCOORD0) : COLOR0
{
	float4 color = SAMPLE_TEXTURE(text, uv);
	return float4(1. - color.rgb, color.a);
}

float4 Polarize(float2 uv : TEXCOORD0) : COLOR0
{
	float4 color = SAMPLE_TEXTURE(text, uv);

	return float4(color.r > 0., color.g > 0., color.b > 0., color.a > 0.);
}

float4 InvertPolarize(float2 uv : TEXCOORD0) : COLOR0
{
	float4 color = SAMPLE_TEXTURE(text, uv);

	float4 pcolor = float4(color.r > 0., color.g > 0., color.b > 0., color.a > 0.);
	return float4(1. - pcolor.rgb, pcolor.a);

}

technique invert
{
    pass pass0
    {
        PixelShader = compile ps_3_0 Invert();
    }
}

technique polarize
{
    pass pass0
    {
        PixelShader = compile ps_3_0 Polarize();
    }
}

technique invertpolarize
{
    pass pass0
    {
        PixelShader = compile ps_3_0 InvertPolarize();
    }
}
