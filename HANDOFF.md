# HANDOFF — Letter Pigment Splat (splatter 브랜치)
updated: 2026-11-09 16:43 KST · by: Gemini Argon

## 목표 (한 줄)
짧은 영상 3개를 이음매 없는 루프 텍스처(물감)로 만들고, 3D 입체 글자를 가우시안 스플랫 구름으로 샘플링해 매 프레임 GPU에서 트라이플래너 셰이더(2톤 셀·Glint·Chrome·Caustic·그대로)로 입히는 Quest 3 MR 앱.

## 지금 카드
- 없음 (`brief-draft`, `build-414a-build-28a1` 완료)

## 다음 할 일
1. `build-541d-build-a242`: 메시 삼각형 면적 가중 샘플링, 접평면 스케일·노멀 쿼터니언 및 외곽선 스플랫 층 생성 (`Assets/Scripts/Splatter/Core/MeshSplatSampler.cs`)
2. `build-bb7f-build-cbae`: `LetterSplatURP.shader` (5종 모드·트라이플래너 플립북·유한차분 범프) 및 `SplatLetterRenderer` 구현
3. `build-d4b6-build-7c9f`: 물감 3슬롯 로더, 글자별 슬롯·모드 할당 및 회전·둥실 애니메이션 연결 (`Splatter.unity`)

## 정본 / 건드리지 말 것
- 결정: 작업대 `studio/shared/DECIDED.md` `[splatter]` (사본 `docs/DECIDED.md`). 버린 안: `REJECTED.md`
- 원천 브리프: 작업대 `splatter/letter-pigment-splat-brief.md` 및 `studio/apps/splatter/nodes/brief/README.md`
- `docs/`, `board/` 는 사본 (`scripts/sync-docs.sh` 로만 갱신)
- 브랜치 보존: `main` = 컬러피커, `orion` = ORION (직전 미커밋 작업은 `git stash` `orion-wip-build-00ab` 에 보존됨). `Library/` 중복 방지를 위해 이 폴더에서만 작업한다.

## 최근 증거
- 2026-11-09 `build-414a-build-28a1`: 증거 L1. `python3 -B Assets/Scripts/Splatter/Verification~/verify_compile.py` exit 0: `files: PASS` (BuildGate 주석·파일 게이트 통과), `MoodPicker.Pigment` 4개 / `MoodPicker.Splatter` 1개 / `Splatter.CoreTests` Roslyn 컴파일 PASS, `.NET NUnit passed=5 failed=0 skipped=0` (`BaselineExcludesZeroDiffDuplicateFrames`, `FlipbookLayoutCapsFramesAt48AndScalesFps`, `PeriodicTileReducesBoundarySeamErrorBelowOne`, `TwoToneExtractorSeparatesShadowAndLightAndMidpointThreshold`, `LetterPoseMathFollowsBriefEquations`).
