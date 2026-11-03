// PigmentCommon.hlsl — 웹 프로토타입(web/shader-lab, web/pigment-lab) 셰이더의 HLSL 포팅.
// URP 셰이더나 Shader Graph Custom Function(File 모드)에서 #include 해서 쓴다.
// 셀 램프(2톤, 빛 방향, 외곽선)는 앱의 CelShader 몫이라 여기 없다. 여기는 "물감을 어떻게 읽느냐"만.
//
// 텍스처 인자는 URP Core 의 TEXTURE2D_PARAM / TEXTURE2D_ARGS 매크로를 쓴다
// (Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl 이 먼저 include 돼 있어야 함).

#ifndef PIGMENT_COMMON_INCLUDED
#define PIGMENT_COMMON_INCLUDED

// ---------------------------------------------------------------- GLSL 호환

// HLSL fmod 는 음수에서 GLSL mod 와 다르다. 거울 반복·루프 계산은 반드시 이걸 쓴다.
float  PigmentMod(float  x, float  y) { return x - y * floor(x / y); }
float2 PigmentMod(float2 x, float2 y) { return x - y * floor(x / y); }

float PigmentLuma(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

// 거울 반복: 0→1→0→1… 이음매 없이 반복 (주기화 전 텍스처를 임시로 깔 때)
float2 MirrorRepeat(float2 t) { t = PigmentMod(t, 2.0); return 1.0 - abs(1.0 - t); }

// ---------------------------------------------------------------- 플립북 (FlipbookLayout.cs 와 같은 규칙)

// 현재 프레임 번호. time 은 _Time.y, 루프 시작을 맞추려면 time - startTime.
float PigmentFrame(float time, float fps, float frameCount)
{
    return floor(PigmentMod(time * fps, frameCount));
}

// 아틀라스 PNG 는 "행 우선, 왼쪽 위부터". Unity UV 는 왼쪽 아래 원점이라 v 를 뒤집는다.
// tileUV 는 타일 좌표(반복 가능), halfTexel = 0.5 / tileSize (칸 경계에서 옆 프레임이 번지는 것 방지).
float2 FlipbookUV(float2 tileUV, float frame, float cols, float rows, float halfTexel)
{
    float2 c   = clamp(frac(tileUV), halfTexel, 1.0 - halfTexel);
    float  col = PigmentMod(frame, cols);
    float  row = floor(frame / cols);
    return float2((col + c.x) / cols, 1.0 - (row + 1.0 - c.y) / rows);
}

// 아틀라스는 밉맵을 끄고 (칸끼리 섞임) Wrap 은 Clamp. 반복은 FlipbookUV 의 frac 이 한다.
float3 SampleFlipbook(TEXTURE2D_PARAM(atlas, smp), float2 tileUV, float frame, float4 layout /* cols, rows, halfTexel, _ */)
{
    return SAMPLE_TEXTURE2D_LOD(atlas, smp, FlipbookUV(tileUV, frame, layout.x, layout.y, layout.z), 0).rgb;
}

// ---------------------------------------------------------------- 트라이플래너 (UV 없는 오브제)

float3 TriplanarWeights(float3 n, float sharpness /* 4 */)
{
    float3 w = pow(abs(n), sharpness);
    return w / (w.x + w.y + w.z);
}

// 오브젝트 공간 위치/노멀을 넘기면 물체에 붙고, 월드 공간을 넘기면 공간에 고정된다 (Caustic 은 월드).
float3 SampleFlipbookTriplanar(TEXTURE2D_PARAM(atlas, smp), float3 p, float3 n, float scale, float frame, float4 layout)
{
    float3 w = TriplanarWeights(n, 4.0);
    // 축마다 조금씩 밀어 세 면의 무늬가 같은 자리에서 겹치지 않게 한다 (웹과 같은 오프셋)
    return SampleFlipbook(TEXTURE2D_ARGS(atlas, smp), p.zy * scale + 0.13, frame, layout) * w.x
         + SampleFlipbook(TEXTURE2D_ARGS(atlas, smp), p.xz * scale + 0.37, frame, layout) * w.y
         + SampleFlipbook(TEXTURE2D_ARGS(atlas, smp), p.xy * scale + 0.71, frame, layout) * w.z;
}

// ---------------------------------------------------------------- 반복 티 지우기 (무작위 섞기)
// 삼각 격자 꼭짓점마다 무작위 오프셋으로 세 번 읽고 분산 보존 블렌드.
// Heitz & Neyret 2018 의 히스토그램 변환은 생략한 간이판. mean 은 아틀라스 평균색 (CPU 에서 계산해 넘김).

float2 PigmentHash2(float2 p)
{
    p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
    return frac(sin(p) * 43758.5453);
}

float3 SampleFlipbookStochastic(TEXTURE2D_PARAM(atlas, smp), float2 uv, float frame, float4 layout, float3 mean, float sharp /* 3 */)
{
    float2 g  = uv * 1.6;
    float2 sk = float2(g.x, -0.57735027 * g.x + 1.15470054 * g.y);
    float2 b  = floor(sk);
    float3 t  = float3(frac(sk), 0.0); t.z = 1.0 - t.x - t.y;
    float3 w; float2 v1, v2, v3;
    if (t.z > 0.0) { w = float3(t.z, t.y, t.x);              v1 = b;                v2 = b + float2(0, 1); v3 = b + float2(1, 0); }
    else           { w = float3(-t.z, 1.0 - t.y, 1.0 - t.x); v1 = b + float2(1, 1); v2 = b + float2(1, 0); v3 = b + float2(0, 1); }
    w = pow(max(w, 0.0), sharp); w /= (w.x + w.y + w.z);
    float3 c1 = SampleFlipbook(TEXTURE2D_ARGS(atlas, smp), uv + PigmentHash2(v1) * 7.0, frame, layout);
    float3 c2 = SampleFlipbook(TEXTURE2D_ARGS(atlas, smp), uv + PigmentHash2(v2) * 7.0, frame, layout);
    float3 c3 = SampleFlipbook(TEXTURE2D_ARGS(atlas, smp), uv + PigmentHash2(v3) * 7.0, frame, layout);
    float3 o  = w.x * (c1 - mean) + w.y * (c2 - mean) + w.z * (c3 - mean);
    return mean + o / sqrt(dot(w, w));
}

// ---------------------------------------------------------------- 밝기 = 높이 → 노멀 (Liquid Chrome 의 범프)
// 화면 미분으로 노멀을 흔든다. Shader Graph 의 Normal From Height 와 같은 원리.
// h 는 분기 밖에서 계산해야 ddx/ddy 가 유효하다.
float3 PerturbNormalFromHeight(float3 posWS, float3 nWS, float h, float strength)
{
    float3 dpx = ddx(posWS), dpy = ddy(posWS);
    float dhx = ddx(h) * strength, dhy = ddy(h) * strength;
    float3 r1 = cross(dpy, nWS), r2 = cross(nWS, dpx);
    float det = dot(dpx, r1);
    float3 g = sign(det) * (dhx * r1 + dhy * r2);
    return normalize(abs(det) * nWS - g);
}

// ---------------------------------------------------------------- 탐색용 재질 항 (shader-lab 의 모드)
// 아래는 탐색 결과일 뿐 결정된 룩이 아니다. 앱의 룩은 2톤 셀 (docs/DECIDED.md 2026-11-03).
// 셀 위에 얹는 하이라이트 층으로 쓸지 여부는 사람이 정한다.

// Glint: 밝기 상위 부분만 발광. threshold 0.6 쯤 (선형 밝기 기준, 웹 샘플의 상위 ~3%)
float GlintMask(float hLinear, float threshold)
{
    float g    = smoothstep(threshold, threshold + 0.08, hLinear);
    float core = smoothstep(threshold + 0.08, 1.0, hLinear);
    return g * 1.6 + core * 2.5;
}

// Caustic: 영상을 월드 트라이플래너로 투영해 빛 무늬로 더함. contrast 1.5~5
float CausticTerm(float hLinear, float contrast) { return pow(hLinear, contrast) * 4.0; }

#endif // PIGMENT_COMMON_INCLUDED
