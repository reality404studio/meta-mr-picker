// PigmentCommon.hlsl 문법·타입 검사용 (Unity 없이):
//   glslangValidator -D -V -S frag -e PSMain -I../Shaders hlsl_check.hlsl -o /dev/null
// URP 매크로 최소 셰임
#define TEXTURE2D_PARAM(t, s) Texture2D t, SamplerState s
#define TEXTURE2D_ARGS(t, s) t, s
#define SAMPLE_TEXTURE2D_LOD(t, s, uv, l) t.SampleLevel(s, uv, l)
#include "PigmentCommon.hlsl"
Texture2D _Atlas; SamplerState sampler_Atlas;
cbuffer C { float4 _Layout; float3 _Mean; float _T; };
struct V { float4 pos : SV_Position; float3 ws : TEXCOORD0; float3 n : TEXCOORD1; float2 uv : TEXCOORD2; };
float4 PSMain(V i) : SV_Target {
  float f = PigmentFrame(_T, 30, 35);
  float3 a = SampleFlipbookTriplanar(_Atlas, sampler_Atlas, i.ws, i.n, 0.9, f, _Layout);
  float3 b = SampleFlipbookStochastic(_Atlas, sampler_Atlas, i.uv, f, _Layout, _Mean, 3);
  float h = PigmentLuma(a);
  float3 nb = PerturbNormalFromHeight(i.ws, normalize(i.n), h, 2);
  float2 m = MirrorRepeat(i.uv * 3);
  return float4(a * GlintMask(h, .6) + b * CausticTerm(h, 2) + nb + m.xyx, 1);
}
