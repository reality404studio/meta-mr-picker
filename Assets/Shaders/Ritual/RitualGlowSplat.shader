Shader "MoodPicker/RitualGlowSplat"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 0.5
        _Reveal ("Bloom radius (m)", Float) = 0
        _Breath ("Breath amount", Float) = 0.12
        _Swirl ("Swirl (rad/s)", Float) = 0.025
        _AlphaShare ("Alpha written per unit of light", Float) = 0.18
        _Arc0Start ("Arc0 Start", Vector) = (0, 0, 0, 0)
        _Arc0Mid ("Arc0 Mid", Vector) = (0, 0, 0, 0.01)
        _Arc0End ("Arc0 End", Vector) = (0, 0, 0, 0.5)
        _Arc0Color ("Arc0 Color", Vector) = (1, 0.9, 0.7, 1)
        _Arc1Start ("Arc1 Start", Vector) = (0, 0, 0, 0)
        _Arc1Mid ("Arc1 Mid", Vector) = (0, 0, 0, 0.01)
        _Arc1End ("Arc1 End", Vector) = (0, 0, 0, 0.5)
        _Arc1Color ("Arc1 Color", Vector) = (1, 0.9, 0.7, 1)
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
                float4 _Arc0Start;
                float4 _Arc0Mid;
                float4 _Arc0End;
                float4 _Arc0Color;
                float4 _Arc1Start;
                float4 _Arc1Mid;
                float4 _Arc1End;
                float4 _Arc1Color;
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
                if (kind > 2.5)
                {
                    bool secondArc = kind > 3.5;
                    float4 arcStart = secondArc ? _Arc1Start : _Arc0Start;
                    float4 arcMid = secondArc ? _Arc1Mid : _Arc0Mid;
                    float4 arcEnd = secondArc ? _Arc1End : _Arc0End;
                    float4 arcCol = secondArc ? _Arc1Color : _Arc0Color;

                    float alpha = arcStart.w * _Intensity;
                    if (alpha < 0.004)
                    {
                        o.quadUV = v.quadUV;
                        o.color = half3(0.0, 0.0, 0.0);
                        o.positionCS = float4(0.0, 0.0, -2.0, 1.0);
                        return o;
                    }

                    float u = frac(dist + _Time.y * arcEnd.w * speed);
                    float env = smoothstep(0.0, 0.16, u) * smoothstep(1.0, 0.80, u);
                    float omt = 1.0 - u;
                    float3 curvePos = omt * omt * arcStart.xyz + (2.0 * omt * u) * arcMid.xyz + u * u * arcEnd.xyz;
                    float3 deriv = 2.0 * omt * (arcMid.xyz - arcStart.xyz) + 2.0 * u * (arcEnd.xyz - arcMid.xyz);
                    float derivLen = length(deriv);
                    float3 tangOS = derivLen > 1e-5 ? (deriv / derivLen) : float3(0.0, 1.0, 0.0);

                    float3 refAxis = abs(tangOS.y) < 0.92 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
                    float3 sideOS = normalize(cross(refAxis, tangOS));
                    float3 upOS = cross(tangOS, sideOS);

                    float swirlDir = v.dataC.w >= 0.0 ? 1.0 : -1.0;
                    float swirlScale = abs(v.dataC.w);
                    float helixAng = phase + u * 6.2831853 * swirlDir + _Time.y * 1.4;
                    float helixR = arcMid.w * (0.28 + 0.72 * sin(u * 3.14159265)) * swirlScale;
                    float3 centerOS = curvePos + (sideOS * cos(helixAng) + upOS * sin(helixAng)) * helixR;

                    float3 posWS = TransformObjectToWorld(centerOS);
                    float3 tangRawWS = mul((float3x3)GetObjectToWorldMatrix(), tangOS);
                    float tangRawLen = length(tangRawWS);
                    float3 tangWS = tangRawLen > 1e-5 ? (tangRawWS / tangRawLen) : float3(0.0, 1.0, 0.0);
                    float3 viewDirWS = GetWorldSpaceNormalizeViewDir(posWS);
                    float3 sideCross = cross(tangWS, viewDirWS);
                    float sideLen = length(sideCross);
                    float3 sideWS = sideLen > 1e-4 ? (sideCross / sideLen) : normalize(cross(abs(viewDirWS.y) < 0.92 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0), viewDirWS));
                    float3 alongRaw = tangWS - viewDirWS * dot(tangWS, viewDirWS) * 0.45;
                    float alongLen = length(alongRaw);
                    float3 alongWS = alongLen > 1e-4 ? (alongRaw / alongLen) : cross(viewDirWS, sideWS);

                    float sizeScale = 0.68 + 0.52 * env;
                    posWS += alongWS * (v.quadUV.x * v.dataA.z * sizeScale) + sideWS * (v.quadUV.y * v.dataA.w * sizeScale);

                    float twinkle = 0.72 + 0.28 * sin(_Time.y * (2.6 + speed * 1.4) + phase);
                    float core = smoothstep(0.35, 0.85, env) * 0.28;
                    float3 moteCol = lerp(arcCol.rgb, float3(1.0, 0.98, 0.93), core);
                    float light = alpha * env * twinkle * v.dataB.r;

                    o.quadUV = v.quadUV;
                    o.color = (half3)(moteCol * light);
                    if (light < 0.004)
                    {
                        o.positionCS = float4(0.0, 0.0, -2.0, 1.0);
                        return o;
                    }
                    o.positionCS = TransformWorldToHClip(posWS);
                    return o;
                }
                float shown = smoothstep(dist, dist + 0.06, _Reveal);
                float breath = 1.0 + _Breath * sin(_Time.y * speed + phase);
                float twinkle = kind > 1.5 ? pow(saturate(0.5 + 0.5 * sin(_Time.y * speed * 3.0 + phase)), 6.0) * 2.2 : 1.0;
                float light = shown * breath * twinkle * _Intensity;

                float angle = _Time.y * _Swirl * v.dataC.w;
                float s = sin(angle), c = cos(angle);
                float3 center = v.positionOS.xyz;
                center.xz = float2(center.x * c - center.z * s, center.x * s + center.z * c);
                center.y += sin(_Time.y * speed * 0.85 + phase) * 0.003 * v.dataC.w;

                o.quadUV = v.quadUV;
                o.color = (half3)(v.dataB.rgb * light);
                if (light < 0.004)
                {
                    o.positionCS = float4(0.0, 0.0, -2.0, 1.0);
                    return o;
                }

                float3 centerWS = TransformObjectToWorld(center);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(centerWS);
                float3 objUpRaw = mul((float3x3)GetObjectToWorldMatrix(), float3(0.0, 1.0, 0.0));
                float objUpLen = length(objUpRaw);
                float3 objUpWS = objUpLen > 1e-5 ? (objUpRaw / objUpLen) : float3(0.0, 1.0, 0.0);
                float3 refUp = abs(dot(viewDirWS, objUpWS)) < 0.94 ? objUpWS : float3(1.0, 0.0, 0.0);
                float3 rightWS = normalize(cross(refUp, viewDirWS));
                float3 upWS = cross(viewDirWS, rightWS);
                float2 rotAxis = float2(v.dataA.x * c - v.dataA.y * s, v.dataA.x * s + v.dataA.y * c);
                float3 axisWS = rightWS * rotAxis.x + upWS * rotAxis.y;
                float3 perpWS = -rightWS * rotAxis.y + upWS * rotAxis.x;
                float3 posWS = centerWS + axisWS * (v.quadUV.x * v.dataA.z) + perpWS * (v.quadUV.y * v.dataA.w);

                o.positionCS = TransformWorldToHClip(posWS);
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
