# HANDOFF — Letter Pigment Splat (splatter 브랜치)
updated: 2026-11-09 17:12 KST · by: Gemini Argon

## 목표 (한 줄)
짧은 영상 3개를 이음매 없는 루프 텍스처(물감)로 만들고, 3D 입체 글자를 가우시안 스플랫 구름으로 샘플링해 매 프레임 GPU에서 트라이플래너 셰이더(2톤 셀·Glint·Chrome·Caustic·그대로)로 입히는 Quest 3 MR 앱.

## 지금 카드
- 에이전트 구현 카드 3건(`build-541d-build-a242`, `build-bb7f-build-cbae`, `build-d4b6-build-7c9f`) 모두 완료.
- 지금 차례(사람): `brief-confirm` (개요 확정), `build-d4b6-verify-2edf` (Quest 3 실기 확인: 스플랫 글자 2톤 셀·Glint 루프 재생 및 30fps 이상)

## 구현 완료 목록
1. `build-414a-build-28a1`: `splatter` 브랜치 분기, `PigmentCore` (`LoopFinder`, `PeriodicTile`, `FlipbookLayout`, `TwoToneExtractor`), `PigmentCommon.hlsl`
2. `build-541d-build-a242`: `MeshSplatSampler` (삼각형 면적 CDF 층화 샘플링, 접평면 스케일·노멀 쿼터니언, Welded 스무스 노멀 기반 2톤 셀 남색 외곽선 스플랫 층 `0.018*size`)
3. `build-bb7f-build-cbae`: `SplatCloudMeshBuilder`, `LetterSplatURP.shader` (5종 모드: 0 그대로 / 1 2톤 셀 + `#2E3350` 외곽선 / 2 Glint 불투명도 연동 / 3 Chrome 유한 차분 노멀 범프 / 4 Caustic 월드 트라이플래너), `SplatLetterRenderer`
4. `build-d4b6-build-7c9f`: `PigmentAtlasBuilder` (루프 탐색 → 크로스페이드 → FFT 주기화 → 2톤 추출 일괄 파이프라인 및 첫 실행 샘플 물결 슬롯 생성), `SplatWordState` (글자 선택·전체 슬롯 및 모드 지정), `SplatWordController` (슬롯별 독립 fps 프레임 진행 및 글자별 회전·둥실 애니메이션)

## 최근 증거
- 2026-11-09 `build-d4b6-build-7c9f`: 증거 L1. `python3 -B Assets/Scripts/Splatter/Verification~/verify_compile.py` exit 0: `files: PASS` (BuildGate 주석·파일 게이트 통과), `MoodPicker.Pigment` 4개 / `MoodPicker.Splatter` 5개 / `MoodPicker.Splatter.Runtime` 2개 / `LetterSplatURP.shader` 정적 검사 PASS, `.NET NUnit passed=10 failed=0 skipped=0`.
