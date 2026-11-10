Shader "MoodPicker/LetterSplatURP"
{
    Properties
    {
        _Atlas ("Flipbook Atlas", 2D) = "white" {}
        _Layout ("Layout (cols, rows, halfTexel, frames)", Vector) = (1, 1, 0.001953125, 1)
        _Frame ("Current Frame", Float) = 0
        _HasSlot ("Has Slot", Float) = 0
        _Mode ("Shader Mode (0..4)", Float) = 1
        _Scale ("Triplanar Scale", Float) = 1.2
        _Intensity ("Intensity", Float) = 1.0
        _LightTone ("Light Tone (Linear)", Color) = (0.85, 0.90, 0.98, 1)
        _ShadowTone ("Shadow Tone (Linear)", Color) = (0.18, 0.25, 0.42, 1)
        _Threshold ("Two-Tone Threshold (sRGB luma)", Float) = 0.5
        _Outline ("Enable Outline Layer", Float) = 1.0
        _OutlineColor ("Outline Color (#2E3350)", Color) = (0.180, 0.200, 0.314, 1)
        _MagicParams ("Magic Params (progress, state, flash, elapsed seconds)", Vector) = (1, 0, 0, 0)
        _SummonOriginWS ("Summon Origin WS", Vector) = (0, 0, 0, 0)
        _HandLeftWS ("Left Hand WS (xyz, active)", Vector) = (0, 0, 0, 0)
        _HandRightWS ("Right Hand WS (xyz, active)", Vector) = (0, 0, 0, 0)
        _HandLeftVelWS ("Left Hand Vel WS (xyz, radius)", Vector) = (0, 0, 0, 0.075)
        _HandRightVelWS ("Right Hand Vel WS (xyz, radius)", Vector) = (0, 0, 0, 0.075)
        _Ripple0 ("Ripple 0 (posOS.xyz, age)", Vector) = (0, 0, 0, -1)
        _Ripple1 ("Ripple 1 (posOS.xyz, age)", Vector) = (0, 0, 0, -1)
        _Ripple2 ("Ripple 2 (posOS.xyz, age)", Vector) = (0, 0, 0, -1)
        _Ripple3 ("Ripple 3 (posOS.xyz, age)", Vector) = (0, 0, 0, -1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "../Pigment/PigmentCommon.hlsl"

        TEXTURE2D(_Atlas);
        SAMPLER(sampler_Atlas);

        CBUFFER_START(UnityPerMaterial)
            float4 _Layout;
            float4 _LightTone;
            float4 _ShadowTone;
            float4 _OutlineColor;
            float4 _HandLeftWS;
            float4 _HandRightWS;
            float4 _HandLeftVelWS;
            float4 _HandRightVelWS;
            float4 _Ripple0;
            float4 _Ripple1;
            float4 _Ripple2;
            float4 _Ripple3;
            float4 _MagicParams;
            float4 _SummonOriginWS;
            float _Frame;
            float _HasSlot;
            float _Mode;
            float _Scale;
            float _Intensity;
            float _Threshold;
            float _Outline;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 quadUV : TEXCOORD0;
            float4 centerAndFlags : TEXCOORD1;
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

        void AccumulateHandSplash(
            float3 centerOS,
            float3 centerWS,
            float3 normalWS,
            float3 randDir,
            float splatHash,
            float4 handPosWS,
            float4 handVelWS,
            inout float3 dispWS,
            inout float splashEnergy)
        {
            if (handPosWS.w < 0.5) return;
            float3 toSplat = centerWS - handPosWS.xyz;
            float dist = length(toSplat);
            float radius = max(0.035, handVelWS.w);
            if (dist >= radius) return;

            float prox = 1.0 - saturate(dist / radius);
            float prox2 = prox * prox;
            float speed = clamp(length(handVelWS.xyz), 0.0, 2.5);
            float scaleFactor = radius / 0.075;
            bool isDroplet = splatHash > 0.60;

            float3 streamOS = sin(centerOS * 42.0 + centerOS.yzx * 27.0 + _Time.y * 4.0);
            float3 streamDirWS = normalize(TransformObjectToWorldDir(streamOS) + randDir * 0.35 + 1e-5);

            if (isDroplet)
            {
                float dropletRank = saturate((splatHash - 0.60) * 2.5);
                float3 pushDir = normalize(toSplat + normalWS * 0.62 + streamDirWS * 0.45 + 1e-5);
                float dropletSpread = (0.018 + 0.034 * dropletRank) * prox2 * scaleFactor;
                float wakeCarry = (0.020 + 0.034 * dropletRank) * prox;
                float flutter = sin(_Time.y * 28.0 + splatHash * 6.28318) * 0.006 * prox * scaleFactor;

                dispWS += pushDir * (dropletSpread + flutter)
                        + handVelWS.xyz * wakeCarry
                        + float3(0.0, 0.014 * prox2 * (0.35 + dropletRank) * scaleFactor, 0.0);
                splashEnergy = max(splashEnergy, saturate(prox * (0.98 + speed * 0.70)));
            }
            else
            {
                float wavePhase = dist * 115.0 - _Time.y * 16.0;
                float surfaceWave = (0.0038 + sin(wavePhase) * 0.0042) * prox * scaleFactor;
                dispWS += normalWS * surfaceWave + handVelWS.xyz * (0.006 * prox2);
                splashEnergy = max(splashEnergy, saturate(prox * (0.65 + speed * 0.40)));
            }
        }

        void AccumulateRippleRings(
            float3 centerOS,
            float3 normalOS,
            float3 normalWS,
            float3 randDir,
            float splatHash,
            float wordScale,
            inout float3 dispWS,
            inout float splashEnergy)
        {
            float4 ripples[4] = { _Ripple0, _Ripple1, _Ripple2, _Ripple3 };
            bool isDroplet = splatHash > 0.60;
            float3 streamOS = sin(centerOS * 42.0 + centerOS.yzx * 27.0);
            float3 streamDirWS = normalize(TransformObjectToWorldDir(streamOS) + randDir * 0.35 + 1e-5);

            for (int r = 0; r < 4; r++)
            {
                float age = ripples[r].w;
                if (age >= 0.0 && age < 1.8)
                {
                    float3 diffOS = centerOS - ripples[r].xyz;
                    float distOS = length(diffOS);
                    float waveFront = age * 0.14;
                    float ringDist = distOS - waveFront;
                    float env = exp(-abs(ringDist) * 26.0) * exp(-age * 1.95) * smoothstep(0.0, 0.03, age);
                    if (env > 0.005)
                    {
                        float phase = ringDist * 96.0;
                        float wave = sin(phase) * env;
                        float3 radialWS = normalize(TransformObjectToWorldDir(diffOS + normalOS * 0.01) + streamDirWS * 0.25);
                        if (isDroplet)
                        {
                            float dropletJump = max(0.0, sin(phase + splatHash * 3.14159)) * env;
                            dispWS += (normalWS * (wave * 0.008 + dropletJump * 0.018)
                                    + radialWS * (wave * 0.006 + dropletJump * 0.010)) * wordScale;
                            splashEnergy = max(splashEnergy, saturate(env * 1.25));
                        }
                        else
                        {
                            dispWS += (normalWS * (wave * 0.0048) + radialWS * (wave * 0.0022)) * wordScale;
                            splashEnergy = max(splashEnergy, saturate(env * 0.72));
                        }
                    }
                }
            }
        }

        Varyings EvaluateSplatVertex(Attributes v, float passKind)
        {
            Varyings o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

            int mode = (int)round(_Mode);
            bool isOutline = v.centerAndFlags.w > 0.5;

            o.posOS = v.positionOS.xyz;
            o.centerOS = v.centerAndFlags.xyz;
            o.normalOS = normalize(v.normalOS);

            if (_MagicParams.y > 2.5)
            {
                o.positionCS = float4(0.0, 0.0, -2.0, 1.0);
                o.quadUV = float2(2.0, 2.0);
                o.posWS = float3(0.0, 0.0, 0.0);
                o.normalWS = float3(0.0, 0.0, 1.0);
                o.modeOutlineSplashRadius = half4((half)mode, 2.0, 0.0, 0.002);
                return o;
            }

            if (isOutline)
            {
                if (passKind > 0.5 || mode != 1 || _Outline < 0.5 || _MagicParams.y > 1.5)
                {
                    o.positionCS = float4(0.0, 0.0, -2.0, 1.0);
                    o.quadUV = float2(2.0, 2.0);
                    o.posWS = float3(0.0, 0.0, 0.0);
                    o.normalWS = float3(0.0, 0.0, 1.0);
                    o.modeOutlineSplashRadius = half4((half)mode, 2.0, 0.0, 0.002);
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
                o.modeOutlineSplashRadius = half4((half)mode, 1.0, 0.0, (half)outlineRadius);
                return o;
            }

            float3 centerWSActual = TransformObjectToWorld(o.centerOS);
            float3 posWSActual = TransformObjectToWorld(v.positionOS.xyz);
            float3 cornerOffsetWS = posWSActual - centerWSActual;
            float3 normalWS = normalize(TransformObjectToWorldNormal(o.normalOS));
            float3 V = GetWorldSpaceNormalizeViewDir(centerWSActual);

            float3 seed3 = frac(sin(o.centerOS * float3(127.1, 311.7, 74.7)) * 43758.5453);
            float splatHash = frac(seed3.x + seed3.y + seed3.z);
            float3 randDir = normalize(seed3 * 2.0 - 1.0 + 1e-4);
            float wordScale = max(0.25, _HandLeftVelWS.w / 0.075);

            float3 dispWS = float3(0.0, 0.0, 0.0);
            float splashEnergy = 0.0;
            AccumulateHandSplash(o.centerOS, centerWSActual, normalWS, randDir, splatHash, _HandLeftWS, _HandLeftVelWS, dispWS, splashEnergy);
            AccumulateHandSplash(o.centerOS, centerWSActual, normalWS, randDir, splatHash, _HandRightWS, _HandRightVelWS, dispWS, splashEnergy);
            AccumulateRippleRings(o.centerOS, o.normalOS, normalWS, randDir, splatHash, wordScale, dispWS, splashEnergy);

            float magicScale = 1.0;
            float landGlint = 0.0;
            float summonBillboard = 0.0;
            float3 streakDispWS = float3(0.0, 0.0, 0.0);
            if (_MagicParams.y > 0.5 && _MagicParams.y < 1.5)
            {
                float t = _MagicParams.w;
                float3 originWS = _SummonOriginWS.xyz;
                float3 rootWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float dn = saturate((distance(originWS, centerWSActual) - (distance(originWS, rootWS) - 0.14)) / 0.28);
                float order = saturate(0.7 * dn + 0.3 * splatHash);
                float departT = 0.5 + order * 1.2;
                float travel = 1.2;
                float e = 1.0 - pow(1.0 - saturate((t - departT) / travel), 2.2);
                float ePrev = 1.0 - pow(1.0 - saturate((t - 0.04 - departT) / travel), 2.2);

                float3 jit = randDir * (0.018 + 0.014 * splatHash) * (1.0 + 0.25 * sin(t * 13.0 + splatHash * 6.28318));
                float3 P0 = originWS + jit;
                float3 chord = centerWSActual - P0;
                float chordLen = length(chord);
                float3 side = normalize(cross(chord, float3(0.0, 1.0, 0.0)) + 1e-4);
                float3 P1 = P0 + chord * 0.45 + side * ((splatHash - 0.5) * 0.7 * chordLen) + float3(0.0, 0.25 * chordLen * splatHash, 0.0);

                float3 pos = (1.0 - e) * (1.0 - e) * P0 + 2.0 * (1.0 - e) * e * P1 + e * e * centerWSActual;
                float3 posPrev = (1.0 - ePrev) * (1.0 - ePrev) * P0 + 2.0 * (1.0 - ePrev) * ePrev * P1 + ePrev * ePrev * centerWSActual;
                float3 tang = normalize(cross(normalWS, randDir) + 1e-4);
                pos += tang * 0.035 * (1.0 - e) * (1.0 - e) * sin(e * 10.0 + splatHash * 6.28318);

                dispWS += pos - centerWSActual;
                streakDispWS = pos - posPrev;

                float appear = smoothstep(0.0, 0.35, t);
                magicScale = lerp(0.55, 1.0, smoothstep(0.45, 1.0, e)) * appear;
                summonBillboard = 1.0 - smoothstep(0.80, 1.0, e);

                float arriveT = departT + travel;
                landGlint = 0.6 * smoothstep(arriveT - 0.12, arriveT, t) * (1.0 - smoothstep(arriveT, arriveT + 0.30, t));
            }
            else if (_MagicParams.y > 1.5 && _MagicParams.y < 2.5)
            {
                float t = _MagicParams.w;
                float releaseDelay = splatHash * 0.22 + saturate((o.centerOS.y + 0.09) * 5.5) * 0.14;
                float fallTime = max(0.0, t - releaseDelay);

                float sag = 0.03 * saturate(t / 0.25);
                float fall = 0.5 * 2.4 * pow(max(0.0, fallTime - 0.2), 2.0);
                float drift = fallTime * 0.03;
                float3 fallDispOS = float3(randDir.x * drift, -(sag + fall), randDir.z * drift);
                dispWS += TransformObjectToWorldDir(fallDispOS);

                float particleLife = 1.0 - smoothstep(0.35, 0.65, sag + fall);
                if (particleLife <= 0.02)
                {
                    o.positionCS = float4(0.0, 0.0, -2.0, 1.0);
                    o.quadUV = float2(2.0, 2.0);
                    o.posWS = float3(0.0, 0.0, 0.0);
                    o.normalWS = float3(0.0, 0.0, 1.0);
                    o.modeOutlineSplashRadius = half4((half)mode, 2.0, 0.0, 0.002);
                    return o;
                }
                magicScale = particleLife;
            }

            if (mode != 2 && splashEnergy < 0.05 && dot(normalWS, V) < -0.15 && _MagicParams.y < 0.5)
            {
                o.positionCS = float4(0.0, 0.0, -2.0, 1.0);
                o.quadUV = float2(2.0, 2.0);
                o.posWS = float3(0.0, 0.0, 0.0);
                o.normalWS = float3(0.0, 0.0, 1.0);
                o.modeOutlineSplashRadius = half4((half)mode, 2.0, 0.0, 0.002);
                return o;
            }

            bool isDroplet = splatHash > 0.60;
            float dropletScale = (isDroplet
                ? lerp(1.0, 0.48 + 0.38 * frac(splatHash * 19.0), saturate(splashEnergy * 1.25))
                : lerp(1.0, 1.05, saturate(splashEnergy))) * magicScale;

            float3 upRef = abs(V.y) < 0.98 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
            float3 billRightWS = normalize(cross(upRef, V));
            float3 billUpWS = cross(V, billRightWS);
            float splatRadiusWS = length(cornerOffsetWS) * 0.70710678;
            float3 billboardCornerWS = (billRightWS * v.quadUV.x + billUpWS * v.quadUV.y) * splatRadiusWS;
            float streakLen = length(streakDispWS);
            if (summonBillboard > 0.001 && streakLen > 1e-5)
            {
                float3 vdir = streakDispWS - V * dot(streakDispWS, V);
                float vl = length(vdir);
                if (vl > 1e-6)
                {
                    float3 ax = vdir / vl;
                    float3 ay = cross(V, ax);
                    float stretch = 1.0 + min(streakLen / max(splatRadiusWS, 1e-4) * 0.9, 6.0);
                    billboardCornerWS = (ax * v.quadUV.x * stretch + ay * v.quadUV.y) * splatRadiusWS;
                }
            }
            float billboardBlend = max(isDroplet ? saturate(splashEnergy * 1.2) : 0.0, summonBillboard);
            float3 morphedCornerWS = lerp(cornerOffsetWS, billboardCornerWS, billboardBlend) * dropletScale;

            float3 finalPosWS = centerWSActual + dispWS + morphedCornerWS;
            float3 finalNormalWS = normalize(lerp(normalWS, normalize(normalWS + dispWS * 18.0 + V * 0.35), saturate(splashEnergy)));

            o.positionCS = TransformWorldToHClip(finalPosWS);
            o.quadUV = v.quadUV;
            o.posWS = finalPosWS;
            o.normalWS = finalNormalWS;
            o.modeOutlineSplashRadius = half4((half)mode, 0.0, (half)saturate(splashEnergy + landGlint), (half)(splatRadiusWS * dropletScale));
            return o;
        }

        half4 EvaluateSplatShade(Varyings i)
        {
            if (i.modeOutlineSplashRadius.y > 0.5)
            {
                return half4(0.027, 0.033, 0.080, 1.0);
            }

            int mode = (int)round(i.modeOutlineSplashRadius.x);
            float splash = saturate((float)i.modeOutlineSplashRadius.z);
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
            float3 normalWS = normalize(lerp(surfaceNormalWS, crystalNormalWS, saturate(splash * 1.25)));

            float dropletLock = (splash > 0.35) ? saturate((splash - 0.35) * 1.35) : 0.0;
            float3 samplePosOS = lerp(i.posOS, i.centerOS, dropletLock * 0.75);
            float3 centerWS = TransformObjectToWorld(i.centerOS);
            float3 samplePosWS = lerp(i.posWS, centerWS, dropletLock * 0.75);

            float3 samplePos = (mode == 4) ? samplePosWS : samplePosOS;
            float3 sampleNrm = (mode == 4) ? normalWS : normalOS;

            float3 texSRGB = float3(0.65, 0.65, 0.65);
            float3 texLinear = float3(0.42, 0.42, 0.42);
            if (_HasSlot > 0.5)
            {
                texSRGB = SampleFlipbookTriplanar(TEXTURE2D_ARGS(_Atlas, sampler_Atlas), samplePos, sampleNrm, _Scale, _Frame, _Layout);
                texLinear = pow(max(texSRGB, 0.0), 2.2);
            }

            float h = PigmentLuma(texLinear);
            float d = max(dot(normalWS, L), 0.0);
            float3 col = texLinear;
            float opacity = 1.0;

            if (mode == 0)
            {
                float3 R = reflect(-L, normalWS);
                float spec = pow(max(dot(R, V), 0.0), 38.0) * 0.48;
                float causticCrest = CausticTerm(h, 2.5) * 0.38;
                col = texLinear * (0.48 + 0.86 * d) * _Intensity
                    + float3(0.48, 0.86, 1.0) * causticCrest
                    + float3(0.88, 0.96, 1.0) * spec;
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

            if (splash > 0.01)
            {
                float3 R3D = reflect(-L, crystalNormalWS);
                float glintSpec = pow(max(dot(R3D, V), 0.0), 45.0) * 3.8;
                float glintRing = pow(r2, 3.2) * (0.45 + 0.55 * sphereZ) * 2.2;
                float starFlare = pow(max(0.0, 1.0 - abs(i.quadUV.x * i.quadUV.y) * 20.0), 6.0) * pow(h, 1.6) * 2.2;
                float causticFlash = CausticTerm(h, 2.8) * 1.8;
                float crystalFresnel = pow(1.0 - saturate(dot(crystalNormalWS, V)), 3.2) * 1.6;

                float3 crystalGlint = float3(0.55, 0.90, 1.0) * causticFlash
                                    + float3(0.85, 0.98, 1.0) * (glintRing + starFlare)
                                    + float3(1.0, 1.0, 1.0) * glintSpec
                                    + float3(0.48, 0.86, 1.0) * crystalFresnel;

                col = col * (1.0 + splash * 0.45) + crystalGlint * (splash * 1.15);
            }

            if (_MagicParams.z > 0.01)
            {
                float flash = _MagicParams.z;
                float3 seed3 = frac(sin(i.centerOS * float3(127.1, 311.7, 74.7)) * 43758.5453);
                float splatHash = frac(seed3.x + seed3.y + seed3.z);
                float flashShimmer = 1.0 + 0.45 * sin(_Time.y * 42.0 + splatHash * 6.28);
                float starFlare = pow(max(0.0, 1.0 - abs(i.quadUV.x * i.quadUV.y) * 16.0), 5.0) * 3.0;
                float causticBurst = CausticTerm(h, 3.2) * 2.2;
                float3 flashCol = float3(0.92, 0.98, 1.0) * (1.5 + starFlare + causticBurst);
                col += flashCol * (flash * flashShimmer);
            }

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
                float splash = saturate((float)i.modeOutlineSplashRadius.z);
                float dropletEdge = lerp(1.0, saturate((1.0 - r2) * 3.5), saturate(splash * 1.25));
                return half4(shaded.rgb, dropletEdge);
            }
            ENDHLSL
        }
    }
}
