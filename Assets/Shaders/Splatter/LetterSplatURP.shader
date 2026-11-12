Shader "MoodPicker/LetterSplatURP"
{
    Properties
    {
        _Atlas ("Flipbook Atlas", 2D) = "white" {}
        _Layout ("Layout (cols, rows, halfTexel, frames)", Vector) = (1, 1, 0.001953125, 1)
        _Frame ("Current Frame", Float) = 0
        _HasSlot ("Has Slot", Float) = 0
        _Mode ("Shader Mode (0..5, 5 = light)", Float) = 1
        _Calm ("Light mode calm colour tile", 2D) = "grey" {}
        _Spark ("Light mode spark flipbook (r spark, g halo)", 2D) = "black" {}
        _Calm2 ("Previous calm", 2D) = "grey" {}
        _Spark2 ("Previous spark", 2D) = "black" {}
        _Scale ("Triplanar Scale", Float) = 1.2
        _Intensity ("Intensity", Float) = 1.0
        _LightTone ("Light Tone (Linear)", Color) = (0.85, 0.90, 0.98, 1)
        _ShadowTone ("Shadow Tone (Linear)", Color) = (0.18, 0.25, 0.42, 1)
        _Threshold ("Two-Tone Threshold (sRGB luma)", Float) = 0.5
        _Outline ("Enable Outline Layer", Float) = 1.0
        _OutlineColor ("Outline Color (#2E3350)", Color) = (0.180, 0.200, 0.314, 1)
        _AtlasCrop ("Crop (scale xy, offset zw)", Vector) = (1, 1, 0, 0)
        _CropOn ("Mirror-Tiled Crop Instead Of Flipbook", Float) = 0
        _Atlas2 ("Previous Atlas (spreading wave)", 2D) = "white" {}
        _Layout2 ("Previous Layout", Vector) = (1, 1, 0.001953125, 1)
        _Slot2 ("Previous (frame, has slot, mirror tiled, gamma)", Vector) = (0, 0, 0, 1)
        _AtlasCrop2 ("Previous Crop", Vector) = (1, 1, 0, 0)
        _Slot2b ("Previous (mode, glint tint, -, -)", Vector) = (0, 0, 0, 0)
        _LightGlint ("Light mode: glint takes the local colour 0..1", Float) = 0
        _Apply ("Apply wave (origin OS xyz, radius)", Vector) = (0, 0, 0, 1000)
        _ApplyShape ("Apply wave (band, bulge m, lift, -)", Vector) = (0.03, 0.004, 0.15, 0)
        _SplatTime ("Splat time, stops when frozen", Float) = 0
        _SimTex ("Sim State (disp+scale, vel+glow)", 2D) = "black" {}
        _SimOn ("Sim Drives Vertices", Float) = 0
        _FlowParams ("Flow (amplitude m, speed, contrast, crop gamma)", Vector) = (0.0008, 1.6, 1, 1)
        _DropParams ("Drop (detach m, stretch s, stretch max, grain)", Vector) = (0.012, 0.008, 2.5, 0.85)
        _GlowParams ("Glow (gain, sparkle, painted overlay, -)", Vector) = (0.35, 0.6, 1, 0)
        _FurParams ("Fur (fluff 0..1, hair length x radius, sway, comb 0..1)", Vector) = (0, 3, 0.35, 0)
        _GlintParams ("Glint module, off at 0 (amount, speed, wave scale 1/m, roughness)", Vector) = (0, 1.2, 120, 0.45)
        _GlintShape ("Glint (sharpness, size, material gate, -)", Vector) = (160, 0.15, 0.6, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "../Pigment/PigmentCommon.hlsl"
        #include "SplatGlint.hlsl"

        TEXTURE2D(_Atlas);
        SAMPLER(sampler_Atlas);
        TEXTURE2D(_Atlas2);
        SAMPLER(sampler_Atlas2);
        TEXTURE2D(_Calm);
        SAMPLER(sampler_Calm);
        TEXTURE2D(_Spark);
        SAMPLER(sampler_Spark);
        TEXTURE2D(_Calm2);
        SAMPLER(sampler_Calm2);
        TEXTURE2D(_Spark2);
        SAMPLER(sampler_Spark2);
        TEXTURE2D(_SimTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _Layout;
            float4 _LightTone;
            float4 _ShadowTone;
            float4 _OutlineColor;
            float4 _FlowParams;
            float4 _AtlasCrop;
            float4 _DropParams;
            float4 _GlowParams;
            float4 _GlintParams;
            float4 _GlintShape;
            float4 _FurParams;
            float4 _Layout2;
            float4 _Slot2;
            float4 _AtlasCrop2;
            float4 _Slot2b;
            float4 _Apply;
            float4 _ApplyShape;
            float _SplatTime;
            float _SimOn;
            float _CropOn;
            float _Frame;
            float _HasSlot;
            float _Mode;
            float _Scale;
            float _Intensity;
            float _Threshold;
            float _Outline;
            float _LightGlint;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 quadUV : TEXCOORD0;
            float4 centerAndFlags : TEXCOORD1;
            float2 simTexel : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 quadUV : TEXCOORD0;
            float3 posOS : TEXCOORD1;
            float3 centerOS : TEXCOORD2;
            float3 normalOS : TEXCOORD3;
            float3 posWS : TEXCOORD4;
            float3 normalWS : TEXCOORD5;
            half4 modeOutlineSplashRadius : TEXCOORD6;
            half glint : TEXCOORD7;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        float3 ProceduralChromeEnv(float3 R)
        {
            float y = clamp(R.y, -1.0, 1.0);
            float3 sky = lerp(float3(0.08, 0.11, 0.18), float3(0.55, 0.72, 0.95), smoothstep(-0.2, 0.85, y));
            float horizon = exp(-abs(y) * 18.0) * 0.65;
            float stripes = smoothstep(0.65, 0.95, sin(R.x * 12.0 + R.z * 6.0)) * smoothstep(-0.1, 0.6, y) * 0.35;
            float3 sunDir = normalize(float3(0.45, 0.8, 0.55));
            float sun = pow(max(dot(R, sunDir), 0.0), 48.0) * 1.4;
            return sky + float3(0.75, 0.85, 1.0) * (horizon + stripes + sun);
        }

        float3 SampleCropPlane(TEXTURE2D_PARAM(atlas, smp), float4 crop, float2 tileUV)
        {
            float2 t = PigmentMod(tileUV, float2(2.0, 2.0));
            t = 1.0 - abs(1.0 - t);
            return SAMPLE_TEXTURE2D_LOD(atlas, smp, t * crop.xy + crop.zw, 0).rgb;
        }

        float3 SampleCropTriplanar(TEXTURE2D_PARAM(atlas, smp), float4 crop, float3 p, float3 n, float scale)
        {
            float3 w = TriplanarWeights(n, 4.0);
            float3 col = float3(0.0, 0.0, 0.0);
            float wSum = 0.0;
            if (w.x > 0.05)
            {
                col += SampleCropPlane(TEXTURE2D_ARGS(atlas, smp), crop, p.zy * scale + 0.13) * w.x;
                wSum += w.x;
            }
            if (w.y > 0.05)
            {
                col += SampleCropPlane(TEXTURE2D_ARGS(atlas, smp), crop, p.xz * scale + 0.37) * w.y;
                wSum += w.y;
            }
            if (w.z > 0.05)
            {
                col += SampleCropPlane(TEXTURE2D_ARGS(atlas, smp), crop, p.xy * scale + 0.71) * w.z;
                wSum += w.z;
            }
            return col / max(wSum, 1e-5);
        }

        float3 SampleSurfaceSRGB(TEXTURE2D_PARAM(atlas, smp), float4 layout, float frame, float mirror, float4 crop, float3 p, float3 n)
        {
            if (mirror > 0.5) return SampleCropTriplanar(TEXTURE2D_ARGS(atlas, smp), crop, p, n, _Scale);
            return SampleFlipbookTriplanar(TEXTURE2D_ARGS(atlas, smp), p, n, _Scale, frame, layout);
        }

        float3 SurfaceLinear(float3 srgb, float mirror, float gamma)
        {
            return pow(max(srgb, 0.0), (mirror > 0.5 ? gamma : 2.2) * _FlowParams.z);
        }

        float3 SampleCalmTriplanar(TEXTURE2D_PARAM(calm, smp), float3 p, float3 n)
        {
            float3 w = TriplanarWeights(n, 4.0);
            return SAMPLE_TEXTURE2D_LOD(calm, smp, p.zy * _Scale + 0.13, 0).rgb * w.x
                 + SAMPLE_TEXTURE2D_LOD(calm, smp, p.xz * _Scale + 0.37, 0).rgb * w.y
                 + SAMPLE_TEXTURE2D_LOD(calm, smp, p.xy * _Scale + 0.71, 0).rgb * w.z;
        }

        float3 ShadeLight(TEXTURE2D_PARAM(calmTex, calmSmp), TEXTURE2D_PARAM(sparkTex, sparkSmp), float4 layout, float frame, float3 p, float3 n, float3 N, float3 V, float3 texSRGB, float tint)
        {
            float3 calm = pow(max(SampleCalmTriplanar(TEXTURE2D_ARGS(calmTex, calmSmp), p, n), 0.0), 2.2);
            float2 sh = SampleFlipbookTriplanar(TEXTURE2D_ARGS(sparkTex, sparkSmp), p, n, _Scale, frame, layout).rg;
            float3 Ld = normalize(float3(0.45, 0.8, 0.55));
            float wrapL = saturate((dot(N, Ld) + 0.35) / 1.35);
            float3 warm = float3(1.0, 0.72, 0.38);
            float3 base = calm * (0.25 + 0.85 * wrapL) * (warm + (1.0 - warm) * wrapL);
            float fres = pow(1.0 - saturate(dot(N, V)), 3.0);
            // tint 0 = warm white glint (ginkgo, confirmed). tint 1 = glint in the colour of that spot (web lab "빛", maple/sky)
            float3 lift = texSRGB / max(max(texSRGB.r, max(texSRGB.g, texSRGB.b)), 1e-3);
            float whit = saturate((min(texSRGB.r, min(texSRGB.g, texSRGB.b)) - 0.55) / 0.35);
            float3 coloured = pow(lerp(lift, float3(1.0, 1.0, 1.0), 0.35 + 0.65 * whit), 2.2);
            float3 glint = lerp(float3(1.0, 0.97, 0.86), coloured, tint);
            float3 c = base + glint * (sh.r * 3.0 + sh.g * 1.4) * _Intensity + calm * fres * 0.6;
            return c / (1.0 + 0.6 * c) * 1.25;
        }

        float3 ShadeRaw(float3 texLinear, float3 N, float3 L, float3 V, float h, float d)
        {
            float3 R = reflect(-L, N);
            float spec = pow(max(dot(R, V), 0.0), 38.0) * 0.48;
            float causticCrest = CausticTerm(h, 2.5) * 0.38;
            return texLinear * (0.48 + 0.86 * d) * _Intensity
                + (float3(0.48, 0.86, 1.0) * causticCrest + float3(0.88, 0.96, 1.0) * spec) * _GlowParams.z;
        }

        float3 randDir3(float3 seed)
        {
            return normalize(seed * 2.0 - 1.0 + 1e-4);
        }

        float ApplyFront(float3 centerOS)
        {
            float x = (distance(centerOS, _Apply.xyz) - _Apply.w) / max(_ApplyShape.x, 1e-4);
            return exp(-x * x);
        }

        Varyings EvaluateSplatVertex(Attributes v, float passKind)
        {
            Varyings o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

            int mode = (int)round(_Mode);
            bool isOutline = v.centerAndFlags.w > 0.5;
            o.glint = 0.0;

            o.posOS = v.positionOS.xyz;
            o.centerOS = v.centerAndFlags.xyz;
            o.normalOS = normalize(v.normalOS);

            if (isOutline)
            {
                if (passKind > 0.5 || mode != 1 || _Outline < 0.5 || _SimOn > 0.5)
                {
                    o.positionCS = float4(0.0, 0.0, -2.0, 1.0);
                    o.quadUV = float2(2.0, 2.0);
                    o.posWS = float3(0.0, 0.0, 0.0);
                    o.normalWS = float3(0.0, 0.0, 1.0);
                    o.modeOutlineSplashRadius = half4((half)mode, 2.0, 0.0, 0.0);
                    return o;
                }
                float3 centerWS = TransformObjectToWorld(v.centerAndFlags.xyz);
                float3 viewDir = GetWorldSpaceNormalizeViewDir(centerWS);
                float3 upRef = abs(viewDir.y) < 0.98 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
                float3 rightWS = normalize(cross(upRef, viewDir));
                float3 upWS = cross(viewDir, rightWS);
                float outlineRadius = 0.0038;
                float3 posWS = centerWS - viewDir * 0.022 + (rightWS * v.quadUV.x + upWS * v.quadUV.y) * outlineRadius;
                o.positionCS = TransformWorldToHClip(posWS);
                o.quadUV = v.quadUV;
                o.posWS = posWS;
                o.normalWS = viewDir;
                o.modeOutlineSplashRadius = half4((half)mode, 1.0, 0.0, 0.0);
                return o;
            }

            float3 centerWSActual = TransformObjectToWorld(o.centerOS);
            float3 posWSActual = TransformObjectToWorld(v.positionOS.xyz);
            float3 cornerOffsetWS = posWSActual - centerWSActual;
            float3 normalWS = normalize(TransformObjectToWorldNormal(o.normalOS));
            float3 V = GetWorldSpaceNormalizeViewDir(centerWSActual);

            float3 seed3 = frac(sin(o.centerOS * float3(127.1, 311.7, 74.7)) * 43758.5453);
            float splatHash = frac(seed3.x + seed3.y + seed3.z);

            float3 simDisp = float3(0.0, 0.0, 0.0);
            float3 simVel = float3(0.0, 0.0, 0.0);
            float simScale = 1.0;
            float simGlow = 0.0;
            if (_SimOn > 0.5)
            {
                uint2 texel = uint2(v.simTexel + 0.5);
                float4 stateA = LOAD_TEXTURE2D_LOD(_SimTex, texel, 0);
                float4 stateB = LOAD_TEXTURE2D_LOD(_SimTex, texel + uint2(1, 0), 0);
                simDisp = stateA.xyz;
                simScale = stateA.w;
                simVel = stateB.xyz;
                simGlow = stateB.w;
            }

            float detach = saturate(length(simDisp) / _DropParams.x);
            if (simScale < 0.01 || (mode != 2 && detach < 0.05 && dot(normalWS, V) < -0.15))
            {
                o.positionCS = float4(0.0, 0.0, -2.0, 1.0);
                o.quadUV = float2(2.0, 2.0);
                o.posWS = float3(0.0, 0.0, 0.0);
                o.normalWS = float3(0.0, 0.0, 1.0);
                o.modeOutlineSplashRadius = half4((half)mode, 2.0, 0.0, 0.0);
                return o;
            }

            float flowPhase = dot(o.centerOS, float3(61.0, 47.0, 53.0)) + splatHash * 6.28318 + _SplatTime * _FlowParams.y;
            float3 flowTangent = normalize(cross(normalWS, float3(0.31, 0.87, 0.38)) + 1e-4);
            float3 flowWS = (normalWS * sin(flowPhase) + flowTangent * (0.6 * cos(flowPhase * 0.73))) * (_FlowParams.x * (1.0 - detach));

            float3 upRef = abs(V.y) < 0.98 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
            float3 billRightWS = normalize(cross(upRef, V));
            float3 billUpWS = cross(V, billRightWS);
            float splatRadiusWS = length(cornerOffsetWS) * 0.70710678;
            float3 billboardCornerWS = (billRightWS * v.quadUV.x + billUpWS * v.quadUV.y) * splatRadiusWS;

            float3 velView = simVel - V * dot(simVel, V);
            float velViewLen = length(velView);
            float stretch = min(velViewLen * _DropParams.y / max(splatRadiusWS, 1e-4), _DropParams.z);
            if (velViewLen > 1e-4 && stretch > 0.01)
            {
                float3 ax = velView / velViewLen;
                float3 ay = cross(V, ax);
                billboardCornerWS = (ax * v.quadUV.x * (1.0 + stretch) + ay * v.quadUV.y) * splatRadiusWS;
            }

            float glint = _GlintParams.x > 0.001
                ? SplatGlint(o.centerOS, seed3, normalWS, V, _GlintParams, _GlintShape.x, simGlow, detach) * simScale
                : 0.0;

            float fluff = _FurParams.x * (1.0 - detach);
            float3 furOffsetWS = float3(0.0, 0.0, 0.0);
            if (fluff > 0.001)
            {
                float sway = sin(_SplatTime * 1.7 + splatHash * 6.28318) * _FurParams.z;
                float3 comb = (simVel - normalWS * dot(simVel, normalWS)) * _FurParams.w;
                float tidy = saturate(length(comb));
                float3 hairWS = normalize(normalWS + (randDir3(seed3) * (0.55 * (1.0 - 0.8 * tidy)) + flowTangent * (sway * (1.0 - tidy))) * fluff + simVel * (0.15 * (1.0 - _FurParams.w)) + comb);
                float hairLen = 1.0 + (_FurParams.y - 1.0) * fluff * (0.6 + 0.8 * frac(splatHash * 23.0));
                float3 hairView = hairWS - V * dot(hairWS, V);
                float hairViewLen = length(hairView);
                float3 ax = hairViewLen > 1e-3 ? hairView / hairViewLen : billUpWS;
                float3 ay = cross(V, ax);
                float3 furCorner = (ax * v.quadUV.y * hairLen + ay * v.quadUV.x * lerp(1.0, 0.45, fluff)) * splatRadiusWS;
                billboardCornerWS = lerp(billboardCornerWS, furCorner, fluff);
                furOffsetWS = hairWS * (splatRadiusWS * hairLen * fluff);
            }

            float billboardBlend = max(max(max(detach, saturate(stretch * 2.0)), saturate(glint * 2.0)), fluff);
            float grain = lerp(1.0, _DropParams.w * (0.65 + 0.7 * frac(splatHash * 19.0)), detach) * (1.0 + glint * _GlintShape.y);
            float3 morphedCornerWS = lerp(cornerOffsetWS, billboardCornerWS, billboardBlend) * (grain * simScale);

            float3 applyWS = normalWS * (_ApplyShape.y * ApplyFront(o.centerOS));
            float3 finalPosWS = centerWSActual + simDisp + flowWS + applyWS + furOffsetWS + morphedCornerWS + V * (splatRadiusWS * 1.5 * saturate(glint * 2.0));
            float3 finalNormalWS = normalize(lerp(normalWS, V, detach * 0.6));

            o.positionCS = TransformWorldToHClip(finalPosWS);
            o.quadUV = v.quadUV;
            o.posWS = finalPosWS;
            o.normalWS = finalNormalWS;
            o.modeOutlineSplashRadius = half4((half)mode, 0.0, (half)detach, (half)simGlow);
            o.glint = (half)glint;
            return o;
        }

        half4 EvaluateSplatShade(Varyings i)
        {
            if (i.modeOutlineSplashRadius.y > 0.5)
            {
                return half4(0.027, 0.033, 0.080, 1.0);
            }

            int mode = (int)round(i.modeOutlineSplashRadius.x);
            float detach = saturate((float)i.modeOutlineSplashRadius.z);
            float glow = saturate((float)i.modeOutlineSplashRadius.w);
            float3 normalOS = normalize(i.normalOS);
            float3 surfaceNormalWS = normalize(i.normalWS);
            float3 V = GetWorldSpaceNormalizeViewDir(i.posWS);
            float3 L = normalize(V * 0.50 + float3(-0.42, 0.74, 0.0));

            float r2 = saturate(dot(i.quadUV, i.quadUV));
            float sphereZ = sqrt(max(0.0, 1.0 - r2));
            float3 upRef = abs(V.y) < 0.98 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
            float3 rightWS = normalize(cross(upRef, V));
            float3 upWS = cross(V, rightWS);
            float3 crystalNormalWS = normalize(rightWS * (i.quadUV.x * 1.1) + upWS * (i.quadUV.y * 1.1) + V * (0.25 + 0.75 * sphereZ));
            float3 normalWS = normalize(lerp(surfaceNormalWS, crystalNormalWS, detach * 0.5));

            float dropletLock = (detach > 0.35) ? saturate((detach - 0.35) * 1.35) : 0.0;
            float3 samplePosOS = lerp(i.posOS, i.centerOS, dropletLock * 0.75);
            float3 centerWS = TransformObjectToWorld(i.centerOS);
            float3 samplePosWS = lerp(i.posWS, centerWS, dropletLock * 0.75);

            float3 samplePos = (mode == 4) ? samplePosWS : samplePosOS;
            float3 sampleNrm = (mode == 4) ? normalWS : normalOS;

            float3 texSRGB = float3(0.65, 0.65, 0.65);
            float3 texLinear = float3(0.42, 0.42, 0.42);
            if (_HasSlot > 0.5)
            {
                texSRGB = SampleSurfaceSRGB(TEXTURE2D_ARGS(_Atlas, sampler_Atlas), _Layout, _Frame, _CropOn, _AtlasCrop, samplePos, sampleNrm);
                texLinear = SurfaceLinear(texSRGB, _CropOn, _FlowParams.w);
            }

            float h = PigmentLuma(texLinear);
            float d = max(dot(normalWS, L), 0.0);
            float3 col = texLinear;
            float opacity = 1.0;

            if (mode == 0)
            {
                col = ShadeRaw(texLinear, normalWS, L, V, h, d);
            }
            else if (mode == 5)
            {
                col = ShadeLight(TEXTURE2D_ARGS(_Calm, sampler_Calm), TEXTURE2D_ARGS(_Spark, sampler_Spark), _Layout, _Frame, samplePos, sampleNrm, normalWS, V, texSRGB, _HasSlot > 0.5 ? _LightGlint : 0.0);
            }
            else if (mode == 1)
            {
                float lumaSRGB = PigmentLuma(texSRGB);
                float3 tone = (_HasSlot > 0.5)
                    ? lerp(_ShadowTone.rgb, _LightTone.rgb, step(_Threshold, lumaSRGB))
                    : float3(0.42, 0.42, 0.42);
                col = tone * lerp(0.42, 1.0, step(0.35, d)) * _Intensity;
            }
            else if (mode == 2)
            {
                float g = smoothstep(0.6, 0.68, h);
                float core = smoothstep(0.68, 1.0, h);
                float fresnel = 0.12 * pow(1.0 - saturate(dot(normalWS, V)), 4.0);
                col = float3(0.45, 0.78, 1.0) * (1.6 * g + 2.5 * core) * _Intensity + float3(0.02, 0.025, 0.04) + fresnel;
                opacity = saturate(smoothstep(0.55, 0.64, h) * 0.92 + 0.08);
            }
            else if (mode == 3)
            {
                float uvEps = 0.006 / max(_Scale, 0.1);
                float3 nPerturbedOS = (_HasSlot > 0.5)
                    ? PerturbNormalFromHeightFiniteDiff(TEXTURE2D_ARGS(_Atlas, sampler_Atlas), samplePosOS, normalOS, _Scale, _Frame, _Layout, uvEps, 2.2)
                    : normalOS;
                float3 nPerturbedWS = normalize(TransformObjectToWorldNormal(nPerturbedOS));
                float3 R = reflect(-V, nPerturbedWS);
                float fresnel = 0.55 + 0.45 * pow(1.0 - saturate(dot(nPerturbedWS, V)), 5.0);
                col = ProceduralChromeEnv(R) * fresnel + pow(h, 4.0) * 1.2 * _Intensity;
            }
            else
            {
                col = float3(0.05, 0.06, 0.09) * (0.35 + 0.65 * d)
                    + float3(0.45, 0.82, 1.0) * (CausticTerm(h, 3.0) * (0.35 + 0.65 * d)) * _Intensity;
            }

            if (_Slot2.y > 0.5)
            {
                float newness = 1.0 - smoothstep(_Apply.w - _ApplyShape.x, _Apply.w, distance(i.centerOS, _Apply.xyz));
                if (newness < 0.999)
                {
                    float3 oldCol;
                    if (_Slot2b.x > 4.5)
                    {
                        float3 oldTex = float3(1.0, 1.0, 1.0);
                        if (_Slot2b.y > 0.0) oldTex = SampleSurfaceSRGB(TEXTURE2D_ARGS(_Atlas2, sampler_Atlas2), _Layout2, _Slot2.x, _Slot2.z, _AtlasCrop2, samplePos, sampleNrm);
                        oldCol = ShadeLight(TEXTURE2D_ARGS(_Calm2, sampler_Calm2), TEXTURE2D_ARGS(_Spark2, sampler_Spark2), _Layout2, _Slot2.x, samplePos, sampleNrm, normalWS, V, oldTex, _Slot2b.y);
                    }
                    else
                    {
                        float3 oldSRGB = SampleSurfaceSRGB(TEXTURE2D_ARGS(_Atlas2, sampler_Atlas2), _Layout2, _Slot2.x, _Slot2.z, _AtlasCrop2, samplePos, sampleNrm);
                        float3 oldLinear = SurfaceLinear(oldSRGB, _Slot2.z, _Slot2.w);
                        oldCol = ShadeRaw(oldLinear, normalWS, L, V, PigmentLuma(oldLinear), d);
                    }
                    col = lerp(oldCol, col, newness);
                }
            }

            float3 tint = texLinear / max(PigmentLuma(texLinear), 0.04);
            float sparkleSpec = pow(max(dot(reflect(-L, crystalNormalWS), V), 0.0), 45.0);
            col += min(tint, 3.0) * (sparkleSpec * detach * _GlowParams.y);
            col *= 1.0 + glow * _GlowParams.x;
            if (_Slot2.y > 0.5) col *= 1.0 + _ApplyShape.z * ApplyFront(i.centerOS);

            col += SplatGlintColor((float)i.glint, _GlintParams.x, _GlintShape.z, h, tint, r2);

            return half4(col, opacity);
        }
        ENDHLSL

        Pass
        {
            Name "LetterSplatCore"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            AlphaToMask On
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertCore
            #pragma fragment FragCore
            #pragma multi_compile_instancing

            Varyings VertCore(Attributes v)
            {
                return EvaluateSplatVertex(v, 0.0);
            }

            half4 FragCore(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float r2 = dot(i.quadUV, i.quadUV);
                if (r2 > 1.0 || i.modeOutlineSplashRadius.y > 1.5) clip(-1.0);

                half4 shaded = EvaluateSplatShade(i);
                int mode = (int)round(i.modeOutlineSplashRadius.x);
                if (i.modeOutlineSplashRadius.y > 0.5)
                {
                    return half4(shaded.rgb, 1.0);
                }
                if (mode == 2)
                {
                    float alpha = exp(-2.2 * r2) * shaded.a;
                    if (alpha < 0.22) clip(-1.0);
                    return half4(shaded.rgb, saturate(alpha * 1.35));
                }
                float detach = saturate((float)i.modeOutlineSplashRadius.z);
                float dropletEdge = lerp(1.0, saturate((1.0 - r2) * 3.5), detach);
                dropletEdge = lerp(dropletEdge, saturate((1.0 - r2) * 1.6), saturate((float)i.glint * 3.0));
                return half4(shaded.rgb, dropletEdge);
            }
            ENDHLSL
        }
    }
}
