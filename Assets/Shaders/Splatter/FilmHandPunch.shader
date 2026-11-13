Shader "MoodPicker/FilmHandPunch"
{
    Properties
    {
        _Inflate ("Push the hand surface out along normals (m)", Float) = 0.004
        _EdgeSoft ("Facing ratio where the cut-out reaches full strength", Float) = 0.45
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="AlphaTest+10" }

        Pass
        {
            Name "FilmHandPunch"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual
            Blend Zero OneMinusSrcAlpha, Zero OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Inflate;
                float _EdgeSoft;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 posWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 normalWS = normalize(TransformObjectToWorldNormal(v.normalOS));
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz) + normalWS * _Inflate;
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = normalWS;
                o.posWS = posWS;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float facing = abs(dot(normalize(i.normalWS), GetWorldSpaceNormalizeViewDir(i.posWS)));
                float cut = saturate(facing / max(_EdgeSoft, 1e-3));
                return half4(0.0, 0.0, 0.0, cut);
            }
            ENDHLSL
        }
    }
}
