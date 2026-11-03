# 텍스처 랩 — 루프 · 타일 · 셰이더 참고 구현

> 2026-11-03 claude.ai 대화에서 만든 프로토타입과 포팅 기준 코드. **앱 코드가 아니라 참고용**이에요.
> 테스트 영상(한강 물결 2초)으로 "영상 일부 → 루프 텍스처 → 오브제에 셰이더로 입히기"가 되는지 확인하고,
> 루프 자동 탐색 · 이음매 없는 타일 · 플립북 내보내기까지 웹에서 검증한 뒤 C#/HLSL 로 옮겨 놓았어요.

## 에이전트에게 먼저

- **결정된 것과 탐색한 것을 구분한다.**

  | 이 폴더의 것 | 상태 |
  | --- | --- |
  | 찰칵 / 루프 온 두 모드, 오브제 룩 = 2톤 셀 | 사람이 정함 (`docs/DECIDED.md` 2026-11-03) |
  | 루프 탐색, 크로스페이드, 주기화(이음매 제거), 플립북 규칙 | 보드 과업 `catch` · `loop` · `export` 를 구현하는 방법. 그대로 써도 됨 |
  | shader-lab 의 Glint · Liquid Chrome · Caustic · Hologram 등 7모드 | **탐색일 뿐**. 셀 룩 위에 얹을지는 사람이 정한다 (HANDOFF §9 방식으로 선택지 내고 멈춤) |
  | 특징값 → 셰이더 프롬프트 (`texlab.py stats`) | 대화에서 나온 아이디어. 보드 과업 아님. 앱에 넣지 않는다 |

- Unity 로 옮길 때 `Assets/` 에 넣는 건 `unity/PigmentCore/*.cs` 와 `unity/Shaders/PigmentCommon.hlsl` 만. `unity/tests/` 는 넣지 않는다.
- **원본 영상은 git 에 넣지 않는다** (catch 과업 메모). 여기 있는 샘플은 512px 크롭 파생물.
- `docs/`, `board/` 는 이 폴더 때문에 고치지 않는다.

## 폴더

```
reference/texture-lab/
├── web/
│   ├── shader-lab/      영상 텍스처 + 셰이더 7종 + 오브제 5종 + 투과형 글래스 미리보기 (three.js)
│   └── pigment-lab/     영상/사진 모드: 루프 자동 탐색 → 타일화 → 플립북 PNG·JSON 저장, 팔레트
├── tools/texlab.py      오프라인(맥) 도구: loop / tile / stats. C# 포팅의 기준 출력
└── unity/
    ├── PigmentCore/     LoopFinder.cs · PeriodicTile.cs · FlipbookLayout.cs  (UnityEngine 의존 없음)
    ├── Shaders/PigmentCommon.hlsl   플립북·트라이플래너·무작위 섞기·높이→노멀 (URP 매크로)
    └── tests/           파이썬 기준 출력과 비교하는 테스트 (mono), HLSL 문법 검사
```

## 보드 과업과 연결

| 과업 (board) | 쓰는 것 | 메모 |
| --- | --- | --- |
| catch · 테스트 영상으로 CelExtract·Loop 오프라인 검증 (Mac) | `tools/texlab.py loop`, `tile` | 테스트 영상(윤슬·나뭇잎·바닥 타일) 넣으면 시트·JSON·유사도 지도·미리보기 mp4 가 나온다 |
| catch · 찰칵: 무늬 보존 2톤 + 이음매 없는 타일 | `PeriodicTile.MakePeriodicRGBA` | **순서: 크롭 → 주기화 → 2톤.** 2톤으로 먼저 자르면 경계 점프가 계단이 돼서 주기화가 회색 중간값을 다시 만든다 |
| loop · 끝-시작 크로스페이드 + 플립북 | `LoopFinder` + `FlipbookLayout` | 고정 "끝-시작" 대신 가장 덜 티 나는 구간을 찾는다. 셀 셰이더 입력 = 아틀라스 + (cols, rows, fps) |
| loop · 응시 시간 → 진하기 · 루프 길이 | `LoopFinder.Find(..., minSec, maxSec, ...)` | 응시 시간을 `maxSec` 로 넘기면 "오래 볼수록 긴 루프"가 된다 |
| dab · 트라이플래너 셀 셰이더 + 플립북 재생 | `PigmentCommon.hlsl` | `SampleFlipbookTriplanar` 로 물감을 읽고, 셀 램프는 앱 셰이더에서 |
| export · 루프 스프라이트 시트 PNG + 셰이더 파라미터 JSON | 시트 규칙 + JSON 키 | JSON 은 HANDOFF §5 `loop` 필드(frames, frameCount, fps, cols)와 같은 키 + rows·진단값 |

## 측정한 것 (샘플: 한강 물결 2초, 512×524, 30fps)

| 항목 | 값 | 의미 |
| --- | --- | --- |
| 그냥 반복할 때 끝→처음 점프 | 평소 프레임 전환의 **2.4배** | 2초마다 툭 끊김이 보인다 |
| 크로스페이드 루프 이음매 | 평소 전환과 **같은 수준** | 0.47초 겹치면 안 보임 |
| 자동 탐색 최적 루프 점수 | **2.3** (1.17초) | 물은 0.2초면 무늬가 다 바뀌어 완벽히 닮은 쌍이 없다. 2~3 이어도 크로스페이드가 잔상 없이 녹인다. 4~6초 찍으면 후보가 늘어 좋아진다 |
| 타일 경계 (주기화 전 → 후) | 256px 루프 프레임 **3.5 → 0.70**, 512px 사진 **5.2 → 0.60** | 1 아래면 경계가 내부 이웃 차이보다 작아 안 보임 |
| C# 속도 (서버 mono, Quest 아님) | 루프 탐색 60프레임 **39ms**, 256² 주기화 **55ms/장** | 64장이면 약 3.5초 → 실기에선 메인 스레드 밖에서, 진행 표시와 함께. Quest 수치는 실기에서 다시 잰다 |
| C# ↔ 파이썬 | 루프 구간·점수 동일, 주기화 픽셀 합계 동일 | `unity/tests` |
| 셰이더 (shader-lab) | 아이폰 사파리 약 60fps | Liquid Chrome + 박스 화면 녹화로 확인 |
| 화면 기반 흔들림 보정 | 물에서는 **실패** | 무늬 자체가 흐르는 걸 손떨림으로 착각. 실기에선 PCA 가 주는 헤드셋 자세로 보정할 것 |
| 투과형 글래스 미리보기 | 어두운 몸통 + 밝은 반짝임이 잘 남음 | Quest 패스스루는 영상 합성이라 검정이 투명해지지 않음. 글래스 느낌을 보려면 `Blend One One` |

## 알고리즘

### 1. 루프 탐색 (`LoopFinder`)
- 프레임마다 64×64 흑백 `g_k`. 차이 행렬 `D[i,j] = mean|g_i − g_j|`, 기준값 `base = median(D[k,k+1])`.
- 후보 (i, j): 프레임 j−1 다음에 i 로 돌아간다. 길이 `L = j−i ∈ [minSec·fps, maxSec·fps]`, 그리고 `L ≥ max(F+2, 6)`.
- 비용: 크로스페이드로 겹칠 쌍들의 평균 `cost = Σ_{t=-1..F} w_t·D[i+t, j+t] / Σw` (양 끝 t=−1, t=F 는 가중 0.5).
- 선택: `cost/base − 0.02·(L/fps)` 최소 (같으면 긴 루프). 점수 `score = cost/base`.
- 크로스페이드: `t < F` 이면 `out_t = src[j+t]·(1−w) + src[i+t]·w`, `w = smoothstep((t+0.5)/F)`.
- HANDOFF 의 수집 계획은 10~15fps 예요. fps 는 인자라 그대로 되지만, 12fps 에서 F=8 은 0.67초라 길어요. **12fps 면 F=3~4** 를 권해요.
- 참고: Schödl et al., "Video Textures" (SIGGRAPH 2000) — 같은 아이디어의 원조.

### 2. 이음매 없는 타일 (`PeriodicTile`)
- Moisan, "Periodic plus smooth image decomposition" (2011). 주기 성분만 남긴다: `p = u − s`.
- 경계 이미지 `v`: 왼·오른쪽 열과 위·아래 행의 차이만 들어간 이미지.
- `ŝ(q,r) = v̂(q,r) / (2cos(2πq/M) + 2cos(2πr/N) − 4)`, `ŝ(0,0) = 0`.
- 무늬는 그대로 두고 가장자리 점프만 부드러운 저주파로 흡수한다. 물·풀·자갈 같은 불규칙 무늬에 특히 잘 맞는다.
- 구현 메모: R,G 두 채널을 복소 FFT 한 번에 실수부/허수부로 같이 넣는다 (분모가 실수·대칭이라 안 섞임).
- **행 순서와 무관하다** (위아래 뒤집혀도 결과 같음). 그래서 AsyncGPUReadback 의 아래→위 순서 그대로 넣어도 된다.

### 3. 플립북 규칙 (`FlipbookLayout`)
- 프레임이 `maxFrames`(기본 64) 를 넘으면 고르게 솎고 fps 를 그만큼 낮춘다.
- `cols = ceil(√n)`, `rows = ceil(n/cols)`, 256px 칸 → 64프레임이면 2048².
- **PNG 는 행 우선, 왼쪽 위부터.** Unity 텍스처는 왼쪽 아래 원점이라 `PixelRectUnity` 가 y 를 뒤집는다. `EncodeToPNG` 하면 PNG 규칙대로 나온다.
- 셰이더 쪽은 `FlipbookUV`: `uv = ((col + c.x)/cols, 1 − (row + 1 − c.y)/rows)`, 칸 경계는 반 텍셀 클램프.

### 4. 셰이더 (`PigmentCommon.hlsl`)
- `SampleFlipbookTriplanar`: 오브젝트 공간을 넘기면 물체에 붙고, 월드 공간을 넘기면 공간에 고정 (Caustic 이 월드).
- `SampleFlipbookStochastic`: 삼각 격자 + 무작위 오프셋 3번 읽기 + 분산 보존 블렌드로 반복 티를 숨긴다 (Heitz & Neyret 2018 의 간이판, 히스토그램 변환 생략). `mean` 은 아틀라스 평균색.
- `PerturbNormalFromHeight`: 밝기를 높이로 보고 화면 미분으로 노멀 흔들기 (Shader Graph Normal From Height 와 같은 원리).
- `GlintMask`, `CausticTerm`: shader-lab 탐색 모드의 핵심 항. 채택 여부는 사람이 정함.

### 5. 특징값 → 프롬프트 (`texlab.py stats`, 아이디어)
결 방향(스펙트럼 에너지 축), 띠 간격, 밝기 분위수, 하이라이트 크기·종횡비·수명, 무늬가 완전히 바뀌는 시간, 전체 흐름(위상 상관), 루프 주기를 재서 문장 틀에 넣는다. 샘플 결과는 `tools/examples/water_prompt.txt` 처럼 "가로로 누운 띠, 에너지 95% 세로 주파수, 하이라이트 6:1, 수명 0.1초, 제자리에서 일렁임" 으로 나온다. 앱 안 AI 없이 계산만으로 만든다.

## Unity 포팅

### 데이터 흐름 (루프 온)

```
PCA 카메라 텍스처 ──(크롭 rect: WorldToViewportPoint)──▶ Blit → RT 256 (RGBA, 크롭만)
                                                       └─▶ Blit → RT 64 (분석용)
   AsyncGPUReadback ─▶ byte[] 256² 링버퍼 (메모리 상한)  ·  float[] 64² 흑백
응시 끝 ─▶ LoopFinder.DiffMatrix / Find(fps, minSec, maxSec=응시시간, fade)
       ─▶ FlipbookLayout.For → 프레임마다 크로스페이드 → PeriodicTile → 아틀라스 바이트에 복사
       ─▶ Texture2D(RGBA32, mip 없음, sRGB, Clamp, Bilinear).SetPixelData → Apply(false)
       ─▶ 셀 셰이더: _Atlas, _Layout = (cols, rows, 0.5/256, 0), _Fps, _Frames
       ─▶ 내보내기: EncodeToPNG + JSON (위 키)
```

찰칵 모드는 같은 흐름에서 프레임 하나만: 크롭 → `PeriodicTile` → 2톤 → 저장.

### 붙이는 코드 스케치 (**미검증 — Unity 에서 컴파일해 보지 않았다**)

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Pigment;

public class LoopCaptureSketch : MonoBehaviour
{
    const int Tile = 256, An = 64;
    public int maxBufferedFrames = 180;               // 256²×4 = 256KB/장 → 180장 ≈ 46MB
    RenderTexture rtTile, rtSmall;
    readonly List<byte[]> frames = new List<byte[]>();
    readonly List<float[]> gray = new List<float[]>();

    void Awake()
    {
        rtTile  = new RenderTexture(Tile, Tile, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rtSmall = new RenderTexture(An, An, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
    }

    // src: PCA 텍스처. crop: 0~1 뷰포트 (x, y, w, h). 원본 전체 프레임은 어디에도 남기지 않는다.
    public void PushFrame(Texture src, Rect crop)
    {
        if (frames.Count >= maxBufferedFrames) return;
        Graphics.Blit(src, rtTile, crop.size, crop.position);
        Graphics.Blit(rtTile, rtSmall);
        AsyncGPUReadback.Request(rtTile, 0, TextureFormat.RGBA32, r => { if (!r.hasError) frames.Add(r.GetData<byte>().ToArray()); });
        AsyncGPUReadback.Request(rtSmall, 0, TextureFormat.RGBA32, r =>
        {
            if (r.hasError) return;
            var d = r.GetData<byte>(); var g = new float[An * An];
            for (int i = 0; i < g.Length; i++)
                g[i] = (d[i * 4] * 0.2126f + d[i * 4 + 1] * 0.7152f + d[i * 4 + 2] * 0.0722f) / 255f;
            gray.Add(g);
        });
    }

    // 무거우니 Task/Job 으로 메인 스레드 밖에서. Texture2D 생성·Apply 만 메인 스레드.
    public byte[] BuildAtlasBytes(float fps, float minSec, float maxSec, int fade, int maxFrames,
                                  out FlipbookLayout lay, out LoopResult loop)
    {
        int n = Mathf.Min(frames.Count, gray.Count);
        float[] d = LoopFinder.DiffMatrix(gray.GetRange(0, n));
        loop = LoopFinder.Find(d, n, fps, minSec, maxSec, fade);
        int fEff = Mathf.Min(fade, n - loop.End);
        int[] pick;
        lay = FlipbookLayout.For(loop.Length, maxFrames, Tile, fps, out pick);
        var atlas = new byte[lay.Width * lay.Height * 4];
        var buf = new byte[Tile * Tile * 4];
        for (int k = 0; k < lay.Frames; k++)
        {
            int t = pick[k];
            byte[] a = frames[loop.Start + t];
            if (t < fEff)
            {
                float w = LoopFinder.CrossfadeWeight(t, fEff);
                byte[] b = frames[loop.End + t];
                for (int p = 0; p < buf.Length; p++) buf[p] = (byte)Mathf.RoundToInt(b[p] * (1 - w) + a[p] * w);
            }
            else System.Buffer.BlockCopy(a, 0, buf, 0, buf.Length);
            PeriodicTile.MakePeriodicRGBA(buf, Tile);
            int x, y; lay.PixelRectUnity(k, out x, out y);
            for (int row = 0; row < Tile; row++)   // readback 도 Unity 도 아래→위 행 순서
                System.Buffer.BlockCopy(buf, row * Tile * 4, atlas, ((y + row) * lay.Width + x) * 4, Tile * 4);
        }
        return atlas;
    }

    public static Texture2D ToTexture(byte[] atlas, FlipbookLayout lay)
    {
        var tex = new Texture2D(lay.Width, lay.Height, TextureFormat.RGBA32, false, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        tex.SetPixelData(atlas, 0);
        tex.Apply(false);
        return tex;
    }
}
```

### 걸리는 것

- **`fmod`:** HLSL `fmod` 는 음수에서 GLSL `mod` 와 다르다 → `PigmentMod` 사용.
- **색 공간:** 웹 버전은 영상 텍스처를 셰이더에서 `pow(2.2)` 로 직접 풀었다. Unity Linear 프로젝트에서는 아틀라스를 sRGB 텍스처로 만들고(`linear: false`) 수동 변환은 뺀다. 안 그러면 이중 변환.
- **밉맵 끄기:** 아틀라스에 밉맵이 있으면 칸끼리 섞인다. 반복은 셰이더의 `frac` 이 한다 (Wrap 은 Clamp).
- **메모리:** 런타임 아틀라스는 빌드 때처럼 ASTC 로 압축되지 않는다. 2048² RGBA32 = 16MB. 팔레트 개수 상한이나 1024²(128px 칸) 옵션을 둔다.
- **72fps:** 주기화 64장 ≈ 수 초. 응시가 끝난 뒤 비동기로, 끝나기 전엔 첫 프레임(정지)으로 먼저 보여 주면 대기가 안 느껴진다.
- **흔들림:** 화면 분석 보정은 물에서 실패했다. PCA 가 프레임마다 주는 헤드셋 자세로 크롭 rect 를 안정시키는 쪽이 맞다.
- **프라이버시:** 링버퍼에는 크롭만. 원본 전체 프레임 저장 금지 (BRIEF 규칙).

## 실행

```sh
# 웹 (three.js 를 CDN 에서 받으므로 인터넷 필요). 로컬에선 저장 버튼이 일반 다운로드로 동작
cd reference/texture-lab/web/pigment-lab && python3 -m http.server 8000   # http://localhost:8000
cd reference/texture-lab/web/shader-lab  && python3 -m http.server 8001

# 오프라인 도구 (python3 + numpy + ffmpeg)
python3 reference/texture-lab/tools/texlab.py loop  clip.mov  -o out/ --min 1 --max 3 --fade 8
python3 reference/texture-lab/tools/texlab.py loop  clip.mov  -o out/ --fps 12 --fade 4   # HANDOFF 수집 fps 로 미리 보기
python3 reference/texture-lab/tools/texlab.py tile  photo.jpg -o out/                      # *_check2x2.png 로 경계 확인
python3 reference/texture-lab/tools/texlab.py stats clip.mov  -o out/

# C# 포팅 검증 (Unity 없이, mono)
cd reference/texture-lab/unity/tests
python3 make_fixtures.py clip.mov photo.jpg fixtures/
mcs -out:fixtures/run.exe ../PigmentCore/*.cs RunTests.cs && mono fixtures/run.exe fixtures/
glslangValidator -D -V -S frag -e PSMain -I../Shaders hlsl_check.hlsl -o /dev/null      # HLSL 문법
```

웹 pigment-lab 은 브라우저에서 영상을 다시 디코딩하므로 루프 구간이 `texlab.py` 와 한두 프레임 다를 수 있다. 기준은 `texlab.py` 이고, C# 은 그것과 정확히 같다.

## 출처

- L. Moisan, "Periodic plus Smooth Image Decomposition", *J. Math. Imaging and Vision*, 2011
- E. Heitz, F. Neyret, "High-Performance By-Example Noise using a Histogram-Preserving Blending Operator", HPG 2018
- A. Schödl, R. Szeliski, D. Salesin, I. Essa, "Video Textures", SIGGRAPH 2000
- Meta Passthrough Camera API (Unity): https://developers.meta.com/horizon/documentation/unity/unity-pca-overview/
