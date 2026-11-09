#ifndef PIGMENT_COMMON_INCLUDED
#define PIGMENT_COMMON_INCLUDED

float  PigmentMod(float  x, float  y) { return x - y * floor(x / y); }
float2 PigmentMod(float2 x, float2 y) { return x - y * floor(x / y); }

float PigmentLuma(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

float2 FlipbookUV(float2 tileUV, float frame, float cols, float rows, float halfTexel)
{
    float2 c   = clamp(frac(tileUV), halfTexel, 1.0 - halfTexel);
    float  col = PigmentMod(frame, cols);
    float  row = floor(frame / cols);
    return float2((col + c.x) / cols, 1.0 - (row + 1.0 - c.y) / rows);
}

float3 SampleFlipbook(TEXTURE2D_PARAM(atlas, smp), float2 tileUV, float frame, float4 layout)
{
    return SAMPLE_TEXTURE2D_LOD(atlas, smp, FlipbookUV(tileUV, frame, layout.x, layout.y, layout.z), 0).rgb;
}

float3 TriplanarWeights(float3 n, float sharpness)
{
    float3 w = pow(abs(n), sharpness);
    return w / max(w.x + w.y + w.z, 1e-5);
}

float3 SampleFlipbookTriplanar(TEXTURE2D_PARAM(atlas, smp), float3 p, float3 n, float scale, float frame, float4 layout)
{
    float3 w = TriplanarWeights(n, 4.0);
    return SampleFlipbook(TEXTURE2D_ARGS(atlas, smp), p.zy * scale + 0.13, frame, layout) * w.x
         + SampleFlipbook(TEXTURE2D_ARGS(atlas, smp), p.xz * scale + 0.37, frame, layout) * w.y
         + SampleFlipbook(TEXTURE2D_ARGS(atlas, smp), p.xy * scale + 0.71, frame, layout) * w.z;
}

float3 PerturbNormalFromHeightFiniteDiff(
    TEXTURE2D_PARAM(atlas, smp),
    float3 p,
    float3 n,
    float scale,
    float frame,
    float4 layout,
    float eps,
    float strength)
{
    float h0 = PigmentLuma(SampleFlipbookTriplanar(TEXTURE2D_ARGS(atlas, smp), p, n, scale, frame, layout));
    float3 up = abs(n.y) < 0.99 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
    float3 t1 = normalize(cross(up, n));
    float3 t2 = cross(n, t1);
    float h1 = PigmentLuma(SampleFlipbookTriplanar(TEXTURE2D_ARGS(atlas, smp), p + t1 * eps, n, scale, frame, layout));
    float h2 = PigmentLuma(SampleFlipbookTriplanar(TEXTURE2D_ARGS(atlas, smp), p + t2 * eps, n, scale, frame, layout));
    float dh1 = (h1 - h0) * strength;
    float dh2 = (h2 - h0) * strength;
    return normalize(n - dh1 * t1 - dh2 * t2);
}

float GlintMask(float hLinear, float threshold)
{
    float g    = smoothstep(threshold, threshold + 0.08, hLinear);
    float core = smoothstep(threshold + 0.08, 1.0, hLinear);
    return g * 1.6 + core * 2.5;
}

float CausticTerm(float hLinear, float contrast) { return pow(hLinear, contrast) * 4.0; }

#endif
