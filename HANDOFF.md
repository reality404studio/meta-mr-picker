# HANDOFF — Letter Pigment Splat (splatter 브랜치)
updated: 2026-11-10 14:26 KST · by: claude-code

## 목표 (한 줄)
짧은 영상 3개를 이음매 없는 루프 텍스처(물감)로 만들고, 3D 입체 글자를 가우시안 스플랫 구름으로 샘플링해 매 프레임 GPU에서 트라이플래너 셰이더(2톤 셀·Glint·Chrome·Caustic·그대로)로 입히는 Quest 3 MR 앱.

## 지금 카드
- id: `build-9c56` (글자 폰트 Fraunces + 소환·소멸 연출 재설계) · 상태: 하는중 · 필요한 증거: L4 (사람이 헤드셋에서 확인, 화면 녹화는 `/Users/rafaela/01_PROJECT_NOW/my-vr-colorpicker/videref/`)
- 성공 조건(사람 합격 문장): 소환 때 내가 부른 손끝에서 파티클이 서서히 글자로 흘러들어 글자가 만들어지는 게 보이고, 소멸 때 힘 빠지듯 아래로 떨어지는 과정이 보인다. (사람 말: "서서히 파티클들이 촥 와서 글자를 만들고", "슉 하고 아래로 떨어졌음")
- stop: 글자 위치 결정(`build-9c56-decide-634f`)과 실기 확인(`build-9c56-verify-1340`)은 사람 몫. 거기서 멈추고 보고한다.
- 보드: 폰트 `build-9c56-build-8c8f` 완료, 소환 `-9286`·소멸 `-2d8e` 확인 대기(review), 수치 조정 `-08c4` 대기

## 다음 할 일 (최대 3)
1. `-08c4` 수치 조정 적용: 손끝 덩어리 반경 절반, 늘이기 상한 6배→2.5배, 비행 크기 0.55→0.4, 경로 갈라짐 ±35%→±20%, 출발 0.5→0.25초 → 빌드·설치
2. 사람에게 글자 위치 고정 여부(`-634f`) 묻기: 소환 순간 시선 정면 팔 길이 고정 / 지금처럼 머리 방향 추종
3. 새 녹화를 프레임으로 뽑아 이전과 비교(`evidence/build-d4b6-verify-2edf/<빌드>/videref-*.png`). 소멸은 아직 사람이 평가하지 않음

## 정본 / 건드리지 말 것
- 폰트 = Fraunces 72pt Black (사람: "폰트는 좋아요", 보드 a9 확정). 재생성은 `tools/splatter-glyph/make_glyph.py` (Blender)
- 털뭉치 질감은 서랍 보류 `h1`. 작업대 `studio/apps/splatter/nodes/build-9c56/assets/` 의 shader·cs·png 사본을 지우지 않는다 (수치 조정하면 현재 코드에서는 사라짐)
- `docs/`, `board/` 는 직접 고치지 않는다. 이번 세션 변경은 전부 미커밋 (커밋은 게이트 통과 카드만)

## 기기·환경
- Quest 3S `340YC10G9707W0`, 패키지 `com.DefaultCompany.moodpicker`, 에디터 6000.3.27f2
- 빌드: `Unity -batchmode -projectPath . -buildTarget Android -executeMethod MoodPickerBuild.BuildSplatterApkBatch` **`-quit` 없이**. 장면 재생성·프리뷰 렌더 뒤 `Builds/splatter-dev.apk` 생성. 끝난 뒤 APK 시각이 새것인지, 로그에 `No space`가 없는지 확인
- `mr build-unity` 는 이 프로젝트에 없는 `MRKit.Build.Android` 를 부른다. `mr preflight`는 에디터가 닫혀 있어도 "열림"으로 오탐한다. `mr logcat` 은 시각 형식 오류. 대신 adb 직접 사용 (Unity SDK의 platform-tools)
- IL2CPP 재빌드는 디스크가 수 GB 필요 (`Library/Bee` 8.2GB). 한 번 디스크 가득 차서 실패함

## 최근 증거
- 2026-11-10 `build-9c56` 빌드 `8c63c15-dirty-1110-103808`: 증거 L3 일부. 설치·실행·프로세스 생존·크래시 없음, 녹화 `videref/화면 기록 2026-11-10 오전 10.46.50.mov` 프레임 검토. 손→흐름→글자 인과는 생김. 손끝 덩어리가 10cm 털뭉치로 1.2초 정지하고 흐름이 두꺼운 털 원뿔이라 개별 입자가 안 보임. 사람 판정(L4) 미확인
- 2026-11-10 빌드 `8c63c15-dirty-1110-004047`: 증거 L3. 대문자 A Fraunces 렌더 확인(스크린샷), VrApi FPS 72/72 위주(샘플 62개 중 73/72 32, 72/72 27, 70/72 2, 69/72 1). 직전 소환(반경 0.22 구름)은 녹화에서 구름과 글자가 따로 보여 폐기
- 2026-11-10 `verify_compile.py` exit 0, NUnit 10/10 (L1, 셰이더·컨트롤러 변경 후)
- 2026-11-09 `build-d4b6-build-7c9f`: 증거 L1. `files: PASS`, Roslyn 4/5/2 PASS, NUnit 10/10

## 구현 완료 목록
1. `build-414a-build-28a1`: `PigmentCore` (`LoopFinder`, `PeriodicTile`, `FlipbookLayout`, `TwoToneExtractor`), `PigmentCommon.hlsl`
2. `build-541d-build-a242`: `MeshSplatSampler` (면적 CDF 층화 샘플링, Welded 스무스 노멀 외곽선 층 `0.018*size`)
3. `build-bb7f-build-cbae`: `SplatCloudMeshBuilder`, `LetterSplatURP.shader` (5종 모드), `SplatLetterRenderer`
4. `build-d4b6-build-7c9f`: `PigmentAtlasBuilder`, `SplatWordState`, `SplatWordController`

## 열린 질문 (사람에게)
- 소환할 때 글자를 시선 정면 팔 길이에 고정할지, 머리 방향을 따라가게 둘지 (`u3`, `-634f`)
- 소멸 연출(처짐 후 가속 낙하)이 의도와 맞는지: 아직 평가 없음

첫 발화 한 줄: `HANDOFF.md 읽고 지금 카드 build-9c56 의 수치 조정(-08c4)부터. 글자 위치 결정에서 stop.`
