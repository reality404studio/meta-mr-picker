Shader "MoodPicker/RitualGlassSplat"
{
    Properties
    {
        _Tint ("Glass tint", Color) = (0.86, 0.94, 1.0, 1)
        _Inner ("Light picked up from inside", Color) = (0, 0, 0, 0)
        _Rim ("Rim gain", Float) = 0.3
        _RimPower ("Rim power", Float) = 5
        _Spec ("Highlight gain", Float) = 0.4
        _SpecPower ("Highlight power", Float) = 220
        _Body ("Face-on body", Float) = 0
        _Radius ("Splat radius (m)", Float) = 0.0018
        _AlphaShare ("Alpha written per unit of light", Float) = 0.2
        _Pulse ("Seal pulse height (0..1)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }

        Pass
        {
            Name "RitualGlass"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One One, One One

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float4 _Inner;
                float _Rim;
                float _RimPower;
                float _Spec;
                float _SpecPower;
                float _Body;
                float _Radius;
                float _AlphaShare;
                float _Pulse;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 quadUV : TEXCOORD0;
                float4 dataA : TEXCOORD1;
                float4 dataB : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 quadUV : TEXCOORD0;
                half3 color : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 centerWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 N = normalize(TransformObjectToWorldNormal(v.normalOS));
                float3 V = GetWorldSpaceNormalizeViewDir(centerWS);
                float facing = dot(N, V);
                float3 Nf = facing < 0.0 ? -N : N;
                float rimAngle = 1.0 - abs(facing);
                float rimMask = smoothstep(0.18, 0.72, rimAngle);
                float rim = pow(saturate(rimAngle), max(5.5, _RimPower + 1.2)) * rimMask;
                float2 nh = normalize(Nf.xz + float2(1e-5, 0.0));
                float2 h1 = normalize(normalize(float2(-0.58, -0.52)) + normalize(V.xz + float2(1e-5, 0.0)));
                float2 h2 = normalize(normalize(float2(0.82, -0.24)) + normalize(V.xz + float2(1e-5, 0.0)));
                float upright = saturate(1.0 - abs(Nf.y) * 1.45);
                float spec1 = pow(saturate(dot(nh, h1)), _SpecPower);
                float spec2 = 0.42 * pow(saturate(dot(nh, h2)), _SpecPower * 0.75);
                float spec = (spec1 + spec2) * upright * step(0.0, facing);
                float weight = v.dataB.y;
                float bevel = max(0.6, v.dataB.z);
                float dw = (v.dataB.w - _Pulse) * 5.5;
                float wave = _Pulse > 0.001 ? exp(-(dw * dw)) * (0.65 + 0.9 * rimMask) : 0.0;
                float twinkle = 0.94 + 0.06 * sin(_Time.y * 0.8 + v.dataA.w * 40.0);
                float baseMask = saturate((0.038 - v.dataB.w) / 0.038);
                float baseSheen = baseMask * (0.14 + 0.55 * pow(saturate(rimAngle), 2.0)) * _Rim * weight;
                float light = (rim * _Rim * weight * bevel + baseSheen + spec * _Spec * (0.72 + 0.28 * v.dataA.w) + _Body + wave * 0.45) * twinkle;

                o.quadUV = v.quadUV;
                float innerMix = rim * weight * 1.15 + baseMask * 0.42 + spec * 0.35 + wave * 0.55;
                o.color = (half3)(_Tint.rgb * light + _Inner.rgb * innerMix);
                if (light < 0.018 && dot(_Inner.rgb, float3(1.0, 1.0, 1.0)) * innerMix < 0.008)
                {
                    o.positionCS = float4(0.0, 0.0, -2.0, 1.0);
                    return o;
                }
                float3 upRef = abs(V.y) < 0.98 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
                float3 right = normalize(cross(upRef, V));
                float3 up = cross(V, right);
                float radius = _Radius * v.dataB.x;
                float stretch = 1.0 + 0.95 * saturate(rim + spec * 1.1) * upright;
                float3 posWS = centerWS + (right * (v.quadUV.x * 0.82) + up * (v.quadUV.y * stretch)) * radius;
                o.positionCS = TransformWorldToHClip(posWS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half r2 = dot(i.quadUV, i.quadUV);
                half g = exp(-3.2h * r2) * saturate(1.0h - r2);
                half3 c = i.color * g;
                return half4(c, dot(c, half3(0.3333h, 0.3333h, 0.3333h)) * (half)_AlphaShare);
            }
            ENDHLSL
        }
    }
}
