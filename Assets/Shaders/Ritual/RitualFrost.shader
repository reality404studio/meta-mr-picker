Shader "MoodPicker/RitualFrost"
{
    Properties
    {
        _Color ("Body (rgb) and opacity (a)", Color) = (0.86, 0.92, 1, 0.1)
        _EdgeColor ("Edge light", Color) = (1, 0.93, 0.82, 1)
        _Edge ("Edge strength", Float) = 0.7
        _EdgeWidth ("Edge width (uv)", Float) = 0.12
        _Fade ("Fade", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+5" }

        Pass
        {
            Name "RitualFrost"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest [_ZTest]
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _EdgeColor;
                float _Edge;
                float _EdgeWidth;
                float _Fade;
                float _ZTest;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half w = (half)max(1e-3, _EdgeWidth);
                half front = smoothstep(1.0h - w, 1.0h, (half)i.uv.y);
                half back = smoothstep(w, 0.0h, (half)i.uv.y) * 0.45h;
                half edge = (front + back) * (half)_Edge;
                half mask = i.color.a * (half)_Fade;
                half3 rgb = _Color.rgb * i.color.rgb * (1.0h - saturate(edge)) + _EdgeColor.rgb * saturate(edge);
                half a = saturate(_Color.a + edge * 0.45h) * mask;
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
