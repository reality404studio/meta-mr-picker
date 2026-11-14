Shader "MoodPicker/RitualGlowSplat"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 0.5
        _Reveal ("Bloom radius (m)", Float) = 0
        _Breath ("Breath amount", Float) = 0.12
        _Swirl ("Swirl (rad/s)", Float) = 0.025
        _AlphaShare ("Alpha written per unit of light", Float) = 0.18
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }

        Pass
        {
            Name "RitualGlow"
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
                float _Intensity;
                float _Reveal;
                float _Breath;
                float _Swirl;
                float _AlphaShare;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 quadUV : TEXCOORD0;
                float4 dataA : TEXCOORD1;
                float4 dataB : TEXCOORD2;
                float4 dataC : TEXCOORD3;
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

                float dist = v.dataC.x;
                float kind = v.dataC.y;
                float speed = v.dataC.z;
                float phase = v.dataB.w;
                float shown = smoothstep(dist, dist + 0.06, _Reveal);
                float breath = 1.0 + _Breath * sin(_Time.y * speed + phase);
                float twinkle = kind > 1.5 ? pow(saturate(0.5 + 0.5 * sin(_Time.y * speed * 3.0 + phase)), 6.0) * 2.2 : 1.0;
                float light = shown * breath * twinkle * _Intensity;

                float angle = _Time.y * _Swirl * v.dataC.w;
                float s = sin(angle), c = cos(angle);
                float3 center = v.positionOS.xyz;
                center.xz = float2(center.x * c - center.z * s, center.x * s + center.z * c);
                float2 axis = float2(v.dataA.x * c - v.dataA.y * s, v.dataA.x * s + v.dataA.y * c);
                float2 perp = float2(-axis.y, axis.x);
                float2 offset = axis * (v.quadUV.x * v.dataA.z) + perp * (v.quadUV.y * v.dataA.w);
                float3 posOS = center + float3(offset.x, 0.0, offset.y);

                o.quadUV = v.quadUV;
                o.color = (half3)(v.dataB.rgb * light);
                if (light < 0.004)
                {
                    o.positionCS = float4(0.0, 0.0, -2.0, 1.0);
                    return o;
                }
                o.positionCS = TransformObjectToHClip(posOS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half r2 = dot(i.quadUV, i.quadUV);
                half g = exp(-3.5h * r2) * saturate(1.0h - r2);
                half3 c = i.color * g;
                return half4(c, dot(c, half3(0.3333h, 0.3333h, 0.3333h)) * (half)_AlphaShare);
            }
            ENDHLSL
        }
    }
}
