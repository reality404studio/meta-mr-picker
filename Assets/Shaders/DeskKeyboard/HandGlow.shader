Shader "MoodPicker/HandGlow"
{
    Properties
    {
        _GlowColor ("Glow Color", Color) = (0.55, 0.45, 1.0, 1)
        _Intensity ("Intensity", Range(0, 2)) = 0.35
        _Power ("Fresnel Power", Range(0.5, 8)) = 2.5
        _Inflate ("Inflate (m)", Range(0, 0.01)) = 0.003
        _Alpha ("Alpha", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Glow"
            Tags { "LightMode"="UniversalForward" }
            Blend One One, One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                half _Intensity;
                half _Power;
                float _Inflate;
                half _Alpha;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
                float3 p = TransformObjectToWorld(v.positionOS.xyz) + n * _Inflate;
                o.positionCS = TransformWorldToHClip(p);
                o.normalWS = n;
                o.viewWS = GetWorldSpaceViewDir(p);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half f = pow(1.0h - saturate(abs(dot(normalize(i.normalWS), normalize(i.viewWS)))), _Power);
                half g = f * _Intensity;
                return half4(_GlowColor.rgb * g, g * _Alpha);
            }
            ENDHLSL
        }
    }
}
