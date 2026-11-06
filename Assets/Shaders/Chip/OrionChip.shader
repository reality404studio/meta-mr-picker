Shader "MoodPicker/OrionChip"
{
    Properties
    {
        _BaseMap ("Tile", 2D) = "white" {}
        _PaperFace ("Paper Face", Color) = (0.94117647,0.93333333,0.90196078,1)
        _PaperEdge ("Paper Edge", Color) = (0.82352941,0.80784314,0.76470588,1)
        _Klein ("Klein", Color) = (0.08235294,0.19215686,0.48627451,1)
        _Cobalt ("Cobalt", Color) = (0.14509804,0.34901961,0.70588235,1)
        _Red ("Loop", Color) = (0.82352941,0.22745098,0.17254902,1)
        _Sky ("Clear", Color) = (0.49803922,0.65098039,0.85098039,1)
        _CardSize ("Card meters", Vector) = (0.054,0.068,0.003,0)
        _TileSize ("Tile meters", Vector) = (0.042,0.042,0.004,0.0065)
        _Stroke ("Stroke meters", Float) = 0.0018
        _Backing ("Backing meters", Float) = 0.0008
        [PerRendererData] _ChipNumber ("Number", Float) = 1
        [PerRendererData] _HasLoop ("Has Loop", Float) = 0
        [PerRendererData] _Clear ("Clear Slot", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ChipUnlit"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            ZWrite On
            Blend Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST, _CardSize, _TileSize;
                half4 _PaperFace, _PaperEdge, _Klein, _Cobalt, _Red, _Sky;
                float _Stroke, _Backing;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(Chip)
                UNITY_DEFINE_INSTANCED_PROP(float, _ChipNumber)
                UNITY_DEFINE_INSTANCED_PROP(float, _HasLoop)
                UNITY_DEFINE_INSTANCED_PROP(float, _Clear)
            UNITY_INSTANCING_BUFFER_END(Chip)
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 region : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalOS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float region : TEXCOORD3;
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
                o.positionOS = v.positionOS.xyz; o.normalOS = v.normalOS;
                o.uv = v.uv; o.region = v.region.x;
                return o;
            }
            float Box(float2 p, float2 center, float2 halfSize)
            {
                return step(abs(p.x-center.x), halfSize.x) * step(abs(p.y-center.y), halfSize.y);
            }
            float Segments(float2 p, uint bits)
            {
                float s = 0;
                s += ((bits >> 0) & 1u) * Box(p, float2(0.5,0.9), float2(0.31,0.1));
                s += ((bits >> 1) & 1u) * Box(p, float2(0.9,0.7), float2(0.1,0.2));
                s += ((bits >> 2) & 1u) * Box(p, float2(0.9,0.3), float2(0.1,0.2));
                s += ((bits >> 3) & 1u) * Box(p, float2(0.5,0.1), float2(0.31,0.1));
                s += ((bits >> 4) & 1u) * Box(p, float2(0.1,0.3), float2(0.1,0.2));
                s += ((bits >> 5) & 1u) * Box(p, float2(0.1,0.7), float2(0.1,0.2));
                s += ((bits >> 6) & 1u) * Box(p, float2(0.5,0.5), float2(0.31,0.1));
                return saturate(s);
            }
            uint DigitBits(uint d)
            {
                if (d == 0) return 63u; if (d == 1) return 6u;
                if (d == 2) return 91u; if (d == 3) return 79u;
                if (d == 4) return 102u; if (d == 5) return 109u;
                if (d == 6) return 125u; if (d == 7) return 7u;
                if (d == 8) return 127u; return 111u;
            }
            half3 Triplanar(float3 p, float3 n)
            {
                float3 q = (p - float3(0,_TileSize.w,_CardSize.z)) / float3(_TileSize.xy,_TileSize.x) + 0.5;
                float3 w = pow(abs(n), 6.0); w /= max(w.x+w.y+w.z, 0.0001);
                half3 x = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, q.zy * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb;
                half3 y = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, q.xz * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb;
                half3 z = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, q.xy * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb;
                return x*w.x + y*w.y + z*w.z;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 p = (i.uv-0.5) * _CardSize.xy;
                float clear = UNITY_ACCESS_INSTANCED_PROP(Chip, _Clear);
                half3 color = _PaperEdge.rgb;
                if (i.region > 1.5)
                {
                    color = Triplanar(i.positionOS, normalize(i.normalOS));
                    if (clear > 0.5)
                    {
                        float circle = step(length(p-float2(0,_TileSize.w)), _TileSize.x/4);
                        color = lerp(_Klein.rgb, _Sky.rgb, circle);
                    }
                }
                else if (i.region < 0.5)
                {
                    color = clear > 0.5 ? _Klein.rgb : _PaperFace.rgb;
                    float2 d = abs(p-float2(0,_TileSize.w)) - _TileSize.xy/2;
                    float reach = _Stroke*2;
                    float crop = max(Box(d, float2(reach, reach/2), float2(_Stroke/2,reach/2+_Stroke/2)),
                                     Box(d, float2(reach/2, reach), float2(reach/2+_Stroke/2,_Stroke/2)));
                    color = lerp(color, _Cobalt.rgb, crop);
                    float low = -_CardSize.y/2 + _Stroke*2;
                    float bandHeight = (_CardSize.y-_TileSize.y)/2 - _Stroke;
                    float2 label = (p-float2(-_CardSize.x/2+_Stroke*2,low)) / float2(bandHeight/2,bandHeight);
                    float band = Box(label, float2(1.25,0.5), float2(1.4,0.65));
                    color = lerp(color, _Cobalt.rgb, band * (1-step(0.5,clear)));
                    uint number = (uint)clamp(round(UNITY_ACCESS_INSTANCED_PROP(Chip, _ChipNumber)),1,12);
                    float digits = max(Segments(label,DigitBits(number/10)),Segments(label-float2(1.5,0),DigitBits(number%10)));
                    color = lerp(color, _PaperFace.rgb, digits * (1-step(0.5,clear)));
                    float loop = UNITY_ACCESS_INSTANCED_PROP(Chip, _HasLoop);
                    float dotMask = step(length(p-float2(_TileSize.x/2-_Stroke*2,low+bandHeight/2)),_Stroke);
                    color = lerp(color,_Red.rgb,dotMask*step(0.5,loop)*(1-step(0.5,clear)));
                    if (clear > 0.5)
                    {
                        float2 textUV = (p-float2(-_TileSize.x/2,low)) / float2(_TileSize.x/6,bandHeight);
                        float letters = max(Segments(textUV,57u), Segments(textUV-float2(1.2,0),56u));
                        letters = max(letters,Segments(textUV-float2(2.4,0),121u));
                        letters = max(letters,Segments(textUV-float2(3.6,0),119u));
                        letters = max(letters,Segments(textUV-float2(4.8,0),80u));
                        color = lerp(color,_Sky.rgb,letters);
                    }
                }
                if (i.region < 1.5)
                {
                    float2 edge = _CardSize.xy/2-abs(p);
                    float outline = step(min(edge.x,edge.y),_Backing);
                    if (i.region > 0.5)
                    {
                        float zEdge = min(i.positionOS.z,_CardSize.z-i.positionOS.z);
                        outline = step(zEdge,_Backing/2);
                    }
                    color = lerp(color,_Klein.rgb,outline);
                }
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
