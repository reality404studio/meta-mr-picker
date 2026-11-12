# HANDOFF — 물성 채집 MR 도구 (gather 브랜치, 보드 splatter)
updated: 2026-11-12 22:40 KST · by: claude-code

## 목표 (한 줄)
현실의 물성을 채집해서 손안에 갖고 형태에 입혀 쓰는 Quest MR 창작 도구. 손안의 느낌과 아름다움 > 한 바퀴가 걸림 없이 도는 것 > 기능 수. 마감 2026-11-19 05:00 KST. 원문: `/Users/rafaela/01_PROJECT_NOW/my-vr-colorpicker/1110HANDOFF_claude_code.md`

## 지금 카드
- `build-8ac0` (한 바퀴) 는 제작자 확인으로 끝남. 커밋 `8ea1792` `94e604a` `4e8f69d` `2ba1f9a`. 다음 카드는 사람과 정한다
- 조작: 핀치로 구슬 집어 형태에 대기 (윗줄 색 구슬 = 색만, 아랫줄 물성 구슬 = 움직임만) · 왼손 엄지 좌우 쓸기 = 형태(A~Z·구·도넛) · 앞으로 쓸기 = 고정, 고정 중 앞 = 저장 · 뒤로 쓸기 = 재개 · 오른손 엄지 = 조율 다이얼 · 양손 핀치 = 옮기기·키우기
- 남은 모름: 제작자가 아닌 사람이 설명 없이 3분 안에 도는지 (u7, 17일 시험)
- 푸시됨: 앱 `gather` (origin/gather), 작업대 `mood-picker-setup` `9097b6f`
- 미커밋: 기기 저장 이미지·실기 증거 폴더(사람이 커밋 불필요하다고 함), 작업대의 이번 세션 이전 변경(웹 앱 src/·server/, ORION, brief.zip·cakeiceing.md 등 출처 모름)

## 다음 할 일 (최대 3)
1. 사람과 다음 순서 정하기. 인계 문서 일정의 남은 것: 사용자용 패널(물성 값), 소리, 채집 제스처(지금은 다이얼 버튼), 다른 사람 시험(17일)
2. 설명 없이 손이 먼저 나가게 하는 단서 (제스처가 보이지 않음)
3. 노을(IMG_4398) 촬영 시각 받으면 이름표 교체

## 정본 / 건드리지 말 것
- 단풍·하늘은 `LightLayers.BuildAdaptive`(웹 랩 Letter Pigment 빛 굽기 이식) + `_LightGlint` 1. 은행은 옛 `Build` + 0 그대로. 다시 구우려면 `MAPLE_FRAMES_DIR`/`SKY_FRAMES_DIR` (기본 `/tmp/vidref_frames/4566`·`4565`, 원본 작업대 `videref/`)
- 물성 구슬 값은 사람이 찾음: 물방울 0.2/0.2/0.1/0.2/1/0 · 모래 0.7/0.3/0.7/0.8/1/0 · 복실털 0.1/0.7/0.2/0.7/1/1 (무게/뭉침/끈기/탄성/알갱이/털), 테스트 고정. 은행 빛 모드 확정
- 고정·저장·재개는 엄지 앞/뒤 쓸기 (엄지 톡은 오인식이라 버림, 서랍 t6)
- 물방울 물성 = 무게 0.2·뭉침 0.2·끈기 0.1·탄성 0.2·알갱이 1.0 (사람이 찾음, `SplatFeel.Water`, 테스트 고정). 움직임은 형태 변화 × 물성 두 층 (`docs/DECIDED.md` 2026-11-11)
- 은행나무 재료를 다시 구우려면 `GINKGO_FRAMES_DIR` 에 `videref/IMG_4543.mov` 를 256px 로 뽑은 프레임 (`../shared/VideoSplat/extract_video_frames.sh`)
- 되돌릴 기준점: 커밋 `65af4f8` (수식 소환·소멸이 든 옛 방식)과 그 APK. 지우지 않는다
- 눈별 반짝임은 윤슬에 쓰지 않는다. 모듈 `Assets/Shaders/Splatter/SplatGlint.hlsl` 은 다른 재료용 보류 (서랍 h2), 기본 0
- 입자 힘 계산 정본 = `Assets/Scripts/Splatter/Sim/SplatSimKernel.cs`. 값 정본 = `SplatTuning` (전부 베이스라인). 셰이더에 수치를 새로 박지 않는다
- 렌더는 불투명 + AlphaToMask 1패스 유지 (`unity/shared/VideoSplat/README.md` §3)
- 손 뼈대는 `MoodPicker.Hands.IHandInput` 으로만 읽는다 (EditMode 규칙 테스트가 막음)
- `docs/`, `board/` 는 직접 고치지 않는다. 폰트 Fraunces 72pt Black 확정. 털뭉치 질감은 서랍 보류 `h1`

## 기기·환경
- Quest 3S `340YC10G9707W0`, 패키지 `com.DefaultCompany.moodpicker`, 에디터 6000.3.27f2
- 빌드: `Unity -batchmode -projectPath . -buildTarget Android -executeMethod MoodPickerBuild.BuildSplatterApkBatch` **`-quit` 없이**. `mr build-unity`·`mr preflight`·`mr logcat` 은 이 프로젝트에서 안 맞음 (LESSONS.md), adb 는 Unity SDK platform-tools
- 헤드셋 없이 보기: `-executeMethod SplatterPreviewRender.RenderSequenceBatch -splatOut <png>` (쓸기·소환·소멸 18장 시트)
- 검증: `python3 Assets/Scripts/Splatter/Verification~/verify_compile.py`, `sh scripts/run-editmode-tests.sh`
- 맥에서 값 바꾸기: `sh scripts/tuning.sh push <json>` (기기가 0.5초마다 읽음, 실기 미확인)
- 디스크 여유 13GB (2026-11-11 01:25). 증분 빌드는 75~100초, 이 여유에서도 성공

## 최근 증거
- 2026-11-12 `build-b8af` 단풍(IMG_4566)·하늘(IMG_4565) 색 구슬: 사람 확인(채팅, 빌드 `5428c62-dirty-1112-223138` "좋아요 단풍이랑 sky 이걸로 커밋"), 커밋 `3a519f0`. 하늘 그림자 희게 올리기는 "너무 하얘졌네요" → 롤백. 5구슬 fps 미확인 (u8)
- 2026-11-11 `build-8ac0` 사람 확인(채팅): 진열대 집기·번짐, 제자리 변형, 구슬 조합, 은행 빛, 쓸기 고정·저장 "베리굿". 기기 저장 PNG `evidence/build-8ac0/stills/` (2048px 투명)
- 2026-11-11 `build-8ac0` 편집기 한 바퀴: 증거 L2 (`evidence/build-4a39/9d9e0b0-dirty-1111-103533/editor-loop.png`), 투명 PNG 2048px 저장 확인(편집기), 코어 16/16·시뮬레이션 10/10·EditMode 304/304
- 2026-11-11 Overlay 사람 판정(실기 `70922fa-dirty-1111-095150`): "오버레이는 있는게 낫네요. 오버레이 1" → 기본 1 유지
- 2026-11-10 사람 판정(채팅): 발열은 기존 대비 괜찮은 듯, 손 느낌 잘 바뀜, 패널 드래그는 힘듦 → 마이크로제스처 다이얼로 교체, "잘 인식됨". 커밋 `26d07fa`, `94bf813`
- 2026-11-11 눈별 반짝임 사람 판정: "너무 후처리 느낌, 생각보다 별로" → 롤백. 빌드 `94bf813-dirty-1111-094559` 설치됨 (반짝임 꺼짐, 실행 미확인)
- 2026-11-11 눈별 반짝임 구현: 증거 L2 (에디터 두 눈 렌더 `evidence/build-4a39/editor-stereo-glint-1111.png`). 빌드 `94bf813-dirty-1111-012234` 설치됨, 실행·체감 미확인
- 2026-11-10 `build-4a39` 빌드 `42ba43f-dirty-1110-204027`: 증거 L3. 예외 0, 72~73fps (124개 중 70 미만 2개), 손 닿을 때 시뮬레이션 1.6~1.9ms·손 떼면 잠듦, 조율 패널이 기기에 뜸 (`evidence/build-4a39/42ba43f-dirty-1110-204027/summary.md`). 발열 비교·사람 판정·카메라 질감은 미확인
- 2026-11-10 `build-4a39-build-f7a2`: 증거 L1. `verify_sim.py` NUnit 10/10 (손 쓸기 속도 전달, 손가락 구멍, 뭉침, 묻어남, 소환 지나침 2cm 미만, 소멸, 최대 dt 안정)
- 2026-11-10 셰이더·러너·손 입력·패널·채집: 증거 L2. `verify_compile.py` exit 0, EditMode 297/298 (실패 1개는 손대지 않은 `FolioRuntimeTests`, 원인 미확인), 에디터 시트 `evidence/build-4a39/editor-sequence-1110.png`. 기기 실행·Burst AOT·정점 텍스처 읽기·패널 손 조작·카메라 질감은 전부 미확인
- 2026-11-10 `build-9c56` 빌드 `8c63c15-dirty-1110-103808`: 증거 L3 일부, 72fps 위주. 이 카드의 남은 조정은 접음 (사람 결정, 보드 a10)

## 열린 질문 (사람에게)
- 발열 비교 결과가 괜찮은가 (측정 뒤 판정)
- 대비: ORION 때 정한 "패스스루 78% 디밍"을 여기에도 쓸지. 패널 `6 Look > passthroughBrightness` 로 먼저 눈으로 볼 수 있음
- 채집 제스처: 폐기된 양손 사각형 대신 시선 앞 창틀(펀치)로 갈지. 지금은 패널 버튼으로만 찰칵

첫 발화 한 줄: `HANDOFF.md 읽고 한 바퀴(build-8ac0) 완료 상태에서 다음 카드를 사람과 정한다. 후보 1순위: 설명 없이 손이 먼저 나가게 하는 단서.`
