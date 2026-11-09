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
        _OutlineColor ("Outline Color (#2E3350)", Color) = (0.027, 0.033, 0.079, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Name "LetterSplatForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Pigment/PigmentCommon.hlsl"

            TEXTURE2D(_Atlas);
            SAMPLER(sampler_Atlas);

            CBUFFER_START(UnityPerMaterial)
                float4 _Layout;
                float4 _LightTone;
                float4 _ShadowTone;
                float4 _OutlineColor;
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
                half4 splatColor : TEXCOORD1;
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

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                int mode = (int)round(_Mode);
                bool isOutline = v.centerAndFlags.w > 0.5;

                if (isOutline)
                {
                    if (mode != 1 || _Outline < 0.5)
                    {
                        o.positionCS = float4(0.0, 0.0, 0.0, 0.0);
                        o.quadUV = float2(2.0, 2.0);
                        o.splatColor = half4(0.0, 0.0, 0.0, 0.0);
                        return o;
                    }
                    float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                    float3 viewDir = GetWorldSpaceNormalizeViewDir(posWS);
                    posWS -= viewDir * 0.003;
                    o.positionCS = TransformWorldToHClip(posWS);
                    o.quadUV = v.quadUV;
                    o.splatColor = half4(_OutlineColor.rgb, 1.0);
                    return o;
                }

                float3 posWSActual = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWSActual);
                o.quadUV = v.quadUV;

                float3 centerOS = v.centerAndFlags.xyz;
                float3 normalOS = normalize(v.normalOS);
                float3 centerWS = TransformObjectToWorld(centerOS);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                float3 V = GetWorldSpaceNormalizeViewDir(centerWS);
                float3 L = normalize(float3(0.45, 0.8, 0.55));

                float3 samplePos = (mode == 4) ? centerWS : centerOS;
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
                    float spec = pow(max(dot(R, V), 0.0), 40.0) * 0.2;
                    col = texLinear * (0.32 + 0.85 * d) * _Intensity + spec;
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
                    float3 nPerturbedOS = (_HasSlot > 0.5)
                        ? PerturbNormalFromHeightFiniteDiff(TEXTURE2D_ARGS(_Atlas, sampler_Atlas), centerOS, normalOS, _Scale, _Frame, _Layout, 0.004, 2.2)
                        : normalOS;
                    float3 nPerturbedWS = TransformObjectToWorldNormal(nPerturbedOS);
                    float3 R = reflect(-V, nPerturbedWS);
                    float fresnel = 0.55 + 0.45 * pow(1.0 - saturate(dot(nPerturbedWS, V)), 5.0);
                    col = ProceduralChromeEnv(R) * fresnel + pow(h, 4.0) * 1.2 * _Intensity;
                }
                else
                {
                    col = float3(0.05, 0.06, 0.09) * (0.35 + 0.65 * d)
                        + float3(0.45, 0.82, 1.0) * (CausticTerm(h, 3.0) * (0.35 + 0.65 * d)) * _Intensity;
                }

                o.splatColor = half4(col, opacity);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float r2 = dot(i.quadUV, i.quadUV);
                if (r2 > 1.0) clip(-1.0);
                float gauss = exp(-2.4 * r2);
                float alpha = gauss * i.splatColor.a;
                if (alpha < 0.18) clip(-1.0);
                return half4(i.splatColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
