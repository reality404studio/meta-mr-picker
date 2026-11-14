Shader "MoodPicker/RitualWaxSplat"
{
    Properties
    {
        _Color ("Lit colour", Color) = (0.62, 0.16, 0.10, 1)
        _Deep ("Shadow colour", Color) = (0.28, 0.05, 0.04, 1)
        _Light ("Sheen colour", Color) = (1.0, 0.92, 0.82, 1)
        _Glow ("Inner glow", Float) = 0
        _Sheen ("Sheen gain", Float) = 0.45
        _Radius ("Splat radius (m)", Float) = 0.001
        _Press ("Drop 0 .. seal 1", Float) = 0
        _Stagger ("How much later the outer wax follows", Float) = 0.6
        _Stamp ("Stamp relief 0..1", Float) = 0
        _StampDepth ("Stamp depth (m)", Float) = 0.0011
        _Appear ("Appear 0..1", Float) = 1
        _Origin ("Where it rises from (object space)", Vector) = (0, 0, 0, 0)
        _Bob ("Hover bob (m)", Float) = 0.0025
        _GrowWithPress ("Splat size follows press", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }

        Pass
        {
            Name "RitualWax"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            AlphaToMask On
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _Deep;
                float4 _Light;
                float4 _Origin;
                float _Glow;
                float _Sheen;
                float _Radius;
                float _Press;
                float _Stagger;
                float _Stamp;
                float _StampDepth;
                float _Appear;
                float _Bob;
                float _GrowWithPress;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
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
                float3 normalWS : TEXCOORD1;
                float3 viewWS : TEXCOORD2;
                float3 rightWS : TEXCOORD3;
                float3 upWS : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float delay = v.dataA.w;
                float p = saturate(_Press * (1.0 + _Stagger) - delay * _Stagger);
                p = p * p * (3.0 - 2.0 * p);
                float rnd = v.dataC.w;
                float3 a = v.positionOS.xyz;
                a.y += sin(_Time.y * 1.3 + 1.7) * _Bob;
                a += v.normalOS * (sin(_Time.y * 2.1 + rnd * 6.2831853) * 0.0003);
                float runs = step(v.dataB.w, -0.5);
                float3 c = lerp(a, v.dataA.xyz, p);
                float3 viaPath = p < 0.5 ? lerp(v.positionOS.xyz, v.dataC.xyz, p * 2.0) : lerp(v.dataC.xyz, v.dataA.xyz, p * 2.0 - 1.0);
                c = lerp(c, viaPath, runs);
                float3 n = normalize(lerp(v.normalOS, v.dataB.xyz, p));
                c.y -= max(v.dataB.w, 0.0) * _StampDepth * _Stamp;
                n = normalize(lerp(n, lerp(v.dataC.xyz, n, runs), _Stamp));
                float appear = saturate(_Appear);
                float grow = appear * appear * (3.0 - 2.0 * appear);
                c = lerp(_Origin.xyz + (c - _Origin.xyz) * 0.15, c, grow);
                grow *= lerp(1.0, smoothstep(0.0, 0.15, p), runs);

                float3 centerWS = TransformObjectToWorld(c);
                float3 V = GetWorldSpaceNormalizeViewDir(centerWS);
                float3 upRef = abs(V.y) < 0.98 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
                float3 right = normalize(cross(upRef, V));
                float3 up = cross(V, right);
                float size = _Radius * grow * lerp(1.0, p, saturate(_GrowWithPress));
                o.quadUV = v.quadUV;
                o.normalWS = normalize(TransformObjectToWorldNormal(n));
                o.viewWS = V;
                o.rightWS = right;
                o.upWS = up;
                if (size < 1e-5)
                {
                    o.positionCS = float4(0.0, 0.0, -2.0, 1.0);
                    return o;
                }
                float3 posWS = centerWS + (right * v.quadUV.x + up * v.quadUV.y) * size;
                o.positionCS = TransformWorldToHClip(posWS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float r2 = dot(i.quadUV, i.quadUV);
                if (r2 > 1.0) clip(-1.0);
                float3 N = normalize(i.normalWS + (i.rightWS * i.quadUV.x + i.upWS * i.quadUV.y) * 0.16);
                float3 V = normalize(i.viewWS);
                float3 L = normalize(float3(-0.4, 0.8, -0.35));
                float wrap = saturate((dot(N, L) + 0.6) / 1.6);
                float spec = pow(saturate(dot(N, normalize(L + V))), 28.0) * _Sheen;
                float fres = pow(1.0 - saturate(dot(N, V)), 3.0);
                float3 col = lerp(_Deep.rgb, _Color.rgb, wrap) * (0.4 + 0.75 * wrap)
                    + _Light.rgb * (spec + fres * 0.18)
                    + _Color.rgb * (_Glow * (0.65 + 0.5 * fres));
                return half4(col, saturate((1.0 - r2) * 4.0));
            }
            ENDHLSL
        }
    }
}
