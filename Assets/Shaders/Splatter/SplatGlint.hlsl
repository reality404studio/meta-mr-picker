#ifndef SPLAT_GLINT_INCLUDED
#define SPLAT_GLINT_INCLUDED

float SplatGlint(float3 centerOS, float3 seed3, float3 normalWS, float3 viewWS, float4 glintParams, float sharpness, float nearHand, float detach)
{
    float3 sunWS = normalize(float3(-0.35, 0.80, -0.48));
    float glintTime = _Time.y * glintParams.y;
    float3 q = centerOS * glintParams.z;
    float3 wave = float3(
        sin(q.y * 1.7 + q.z * 1.1 + glintTime * 1.3),
        sin(q.z * 1.3 + q.x * 0.9 - glintTime * 1.1),
        sin(q.x * 1.9 + q.y * 0.7 + glintTime * 0.9));
    float3 facet = frac(seed3 * 7.13 + seed3.yzx * 3.71) * 2.0 - 1.0;
    float3 slope = facet * 1.1 + wave * 0.55;
    float roughness = glintParams.w * (1.0 + nearHand * 1.5 + detach);
    float3 microNormalWS = normalize(normalWS + slope * roughness);
    float3 halfWS = normalize(sunWS + viewWS);
    return pow(saturate(dot(microNormalWS, halfWS)), sharpness);
}

float3 SplatGlintColor(float glint, float amount, float materialGate, float luma, float3 tint, float r2)
{
    float gate = lerp(1.0, smoothstep(0.2, 0.6, luma) * 1.4, materialGate);
    float3 color = lerp(float3(1.0, 1.0, 1.0), min(tint, 2.5), 0.45);
    return color * (glint * amount * gate * exp(-r2 * 5.0));
}

#endif
