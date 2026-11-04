Shader "MoodPicker/CelPaint"
{
    Properties
    {
        _BaseMap ("Caught Texture", 2D) = "white" {}
        _LightTone ("Light Tone", Color) = (0.95, 0.66, 0.31, 1)
        _ShadowTone ("Shadow Tone", Color) = (0.18, 0.2, 0.31, 1)
        _LightDir ("Light Dir (world, toward light)", Vector) = (0.42, 0.31, -0.85, 0)
        _ToneMix ("Tone Mix", Range(0, 1)) = 0.65
        _Threshold ("Ramp Threshold", Range(-1, 1)) = 0
        _Softness ("Ramp Softness", Range(0.001, 0.5)) = 0.02
        _TriScale ("Triplanar Scale (tiles per unit)", Float) = 2
        _TriSharpness ("Triplanar Sharpness", Range(1, 16)) = 6
        _PaintMask ("Paint Mask (R, mesh UV)", 2D) = "white" {}
        _BareColor ("Bare Light", Color) = (0.9411765, 0.93333334, 0.9019608, 1)
        _BareShadow ("Bare Shadow", Color) = (0.6, 0.62, 0.68, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _LightTone;
            half4 _ShadowTone;
            float4 _LightDir;
            half _ToneMix;
            half _Threshold;
            half _Softness;
            float _TriScale;
            half _TriSharpness;
            float4 _PaintMask_ST;
            half4 _BareColor;
            half4 _BareShadow;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "CelPaint"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_PaintMask);
            SAMPLER(sampler_PaintMask);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD3;
                float3 positionOS : TEXCOORD0;
                float3 normalOS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.positionOS = v.positionOS.xyz;
                o.uv = v.uv;
                o.normalOS = v.normalOS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half3 SampleTriplanar(float3 p, float3 n)
            {
                float3 w = pow(abs(n), _TriSharpness);
                w /= max(w.x + w.y + w.z, 1e-4);
                float3 q = p * _TriScale;
                half3 x = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, q.zy * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb;
                half3 y = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, q.xz * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb;
                half3 z = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, q.xy * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb;
                return x * w.x + y * w.y + z * w.z;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 baseCol = SampleTriplanar(i.positionOS, normalize(i.normalOS));
                float3 l = normalize(_LightDir.xyz + float3(0, 1e-5, 0));
                half ndl = dot(normalize(i.normalWS), l);
                half band = smoothstep(_Threshold - _Softness, _Threshold + _Softness, ndl);
                half3 tone = lerp(_ShadowTone.rgb, _LightTone.rgb, band);
                half3 painted = lerp(baseCol, tone, _ToneMix);
                half3 bare = lerp(_BareShadow.rgb, _BareColor.rgb, band);
                half mask = SAMPLE_TEXTURE2D(_PaintMask, sampler_PaintMask, i.uv * _PaintMask_ST.xy + _PaintMask_ST.zw).r;
                return half4(lerp(bare, painted, mask), 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
