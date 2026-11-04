# 🔶 Dab: 푝 + 셀 셰이더

[← 보드로](../BOARD.md)

- **목표:** 11-10~12 · 오브제를 손끝으로 찌르면 닿은 지점부터 셀 재질이 번진다 (여기까지 MVP)
- **산출물:** DabPaint, CelShader, HandJudge, 페트병

## 하위 과업

- [x] **결정: 집 무대(CRT 세계 유지?), 오브제 목록, 손 판단 방식** · 사람 · 결정
  <br><sub>HANDOFF §8-2, 5, 6. 에이전트는 선택지만 메모로 정리하고 멈춘다</sub>
- [x] **CelShader: 트라이플래너 + 2톤 램프 + lightDir** · 에이전트 · 구현
  <br><sub>가드레일 §5 (알파 쓰기, ColorMask RGB 금지). 광원 없이 동작. 담은 원본에 물감 JSON 의 램프·lightDir 로 2톤을 입힌다 (DECIDED 2026-11-04 ①)</sub>
- [x] **CelShader 외곽선: 클라인 #15317C, 뒷면** · 에이전트 · 구현
  <br><sub>가드레일 §5 외곽선 Render Face Back. 색은 DECIDED 인터페이스 B안</sub>
- [ ] ▶ **CelShader 플립북 (루프 물감)** · 에이전트 · 구현
  <br><sub>loop-seam 시트 형식과 같은 타입을 입력으로</sub>
- [x] **DabPaint: 접촉점 → 페인트 마스크 번짐** · 에이전트 · 구현
  <br><sub>거리 + 노이즈. 오브제별 렌더 텍스처는 작게 (72fps)</sub>
- [x] **HandJudge 휴리스틱: 톡 / 꾹** · 에이전트 · 구현
  <br><sub>접근 속도, 관통 깊이, 손가락 굽힘. 휴리스틱만 (ML 분류기 안 함, 2026-11-03 결정)</sub>
- [x] **오브제: 페트병 하나 먼저 (나머지는 시간 남으면)** · 에이전트 · 생성
  <br><sub>폴리 예산 지키기. 의자·머그는 MVP 이후</sub>
- [ ] **실기: 페트병에 "푝" → 셀 물결이 흐르고 60fps 합격선** · 사람 · 검증
  <br><sub>증거: 빌드 ID가 찍힌 logcat 발췌 + 스크린샷을 첨부해야 done</sub>
- [x] **버킷 채우기: 물감 묻힌 손을 대면 오브제 전체가 한 번에 채워짐** · 에이전트 · 구현
  <br><sub>DECIDED 같은 날 6). 번짐 마스크 대신 오브제 전체 물감 교체(짧은 채움 연출 허용). 손끝 물감 없으면 반응 없음</sub>

## 메모

HANDOFF §4-5, §5 (DabPaint, CelShader, HandJudge). 셀 셰이더는 공통 "Unity 실기 가드레일" §4 를 지킨다:
알파 0이 아닌 값 쓰기, ColorMask RGB 금지, 외곽선 Render Face Back(_Cull 1), 광원 없이 동작.

## 결정 (2026-11-03, dab-decide)

- 집 무대: **셀 애니 스타일만, CRT 데스크 세계관은 뺀다.** 오브제는 그냥 책상 위에
- 손 판단: **휴리스틱만** (ML 분류기 안 함)
- 오브제: **페트병 하나 먼저.** 의자·머그는 시간 남으면

---
**Claude Code** · 2026-11-03 13:56

2026-11-03: three.js 데모의 포팅 기준이 meta-mr-picker PR #1 (`reference/texture-lab/`, 미머지)에 있음. 연결표는 그 README '보드 과업과 연결'. Assets 에 넣는 건 PigmentCore/*.cs + PigmentCommon.hlsl 만. shader-lab 7모드는 탐색일 뿐 (셀 룩 위에 얹을지는 사람 결정).

---
**Claude Code** · 2026-11-04 05:00

dab-shader: MoodPicker/CelPaint (URP 언릿, 트라이플래너 오브젝트 공간, N·lightDir 스텝으로 shadowTone/lightTone 섞기 _ToneMix 0.65 베이스라인, 알파 1 고정). 바인딩 CelPaint.Apply(mat, PaintRecord, tex) — lightDir 없으면 lightDirView, 텍스처는 TexturePath(store, r) = tile 우선. 게이트 shader-colormask 추가 (알파 빠지는 ColorMask 거절). 검증 verify-dab-shader.sh: 광원 0 배치렌더에서 알파·2톤·텍스처 디테일 통과. 남은 것: midTone 안 씀, 실기 72fps/트라이플래너 3샘플 비용 확인, .mat 에셋은 페트병 카드에서

---
**Claude Code** · 2026-11-04 05:36

2026-11-04 dab-outline·mask·judge·objects (Claude Code). EditMode 90/90 + dab 렌더 검사 PASS + 배치 빌드 통과.
- outline: Shaders/Cel/CelOutline.shader (인버티드 헐, #15317C, _Cull 기본 1, 폭 4mm 베이스라인) + Dab_Outline.mat 을 두 번째 머티리얼로. 게이트에 outline-shader 검사 추가 (Cull Back/Off/없음, _Cull 기본값≠1 이면 실패). 렌더 검사에 외곽선 픽셀 수 추가
- mask: CelPaint 에 _PaintMask(메시 UV, R) + 안 칠한 곳 _BareColor/#F0EEE6·_BareShadow. PaintMask = CPU 128px(상한 256, DabSettings.OnValidate·테스트) R8, 도장별로 반경이 0.8s 동안 커지고 거리+노이즈로 줄어듦, U 이어짐. 손끝 물감 = PortalController.PaintPicked, CurrentPaint 가 null(씻김)이면 안 칠함
- 한계(MVP): 오브제당 물감 한 층. 다른 물감을 찍으면 칠한 곳 전체가 새 물감으로 바뀜
- judge: 접촉 깊이 3mm·접근 속도 ≥0.08m/s 로 접촉, 깊이 ≥12mm 또는 0.25s 유지 = 꾹(반경 0.18), 그 전에 떼면 톡(0.08). 0.25s 안에 판정. 손가락 굽힘은 HandPose 에 관절이 없어 안 씀. ML 패키지 금지 테스트
- objects: 회전체 페트병 메시(32분할, 1500 삼각형 예산 안, UV u=둘레 v=높이), 볼록 MeshCollider(접촉 깊이) + 정확 MeshCollider(UV 레이캐스트). 시작 시 정면 45cm·눈 아래 35cm. 머티리얼은 AssetDatabase 로 셰이더 로드(Shader.Find 없음)

---
**Claude Code** · 2026-11-04 06:10

2026-11-04 dab-build-cc8f: 버킷 채우기 (REJECTED 번짐 덧바르기). PaintMask·번짐 테스트 지움. DabBucket: 손끝이 표면 3mm 안으로 들어오는 순간(속도 조건 없음, 천천히 가져다 대도 됨) 손끝 물감이 있으면 오브제 전체 물감 교체 + 0.2s 채움(_PaintMask 1x1 값 0→1). 대고 있는 동안 재채움 없음, 6mm 떼야 다시 무장. 손끝 비었거나 씻겼으면 변화 없음. HandJudge(톡/꾹)는 런타임에서 안 씀 (코드·테스트는 남김)
