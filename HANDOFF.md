# HANDOFF — 물성 채집 MR 도구 (gather 브랜치, 보드 splatter)
updated: 2026-11-11 10:45 KST · by: claude-code

## 목표 (한 줄)
현실의 물성을 채집해서 손안에 갖고 형태에 입혀 쓰는 Quest MR 창작 도구. 손안의 느낌과 아름다움 > 한 바퀴가 걸림 없이 도는 것 > 기능 수. 마감 2026-11-19 05:00 KST. 원문: `/Users/rafaela/01_PROJECT_NOW/my-vr-colorpicker/1110HANDOFF_claude_code.md`

## 지금 카드
- id: `build-8ac0` (한 바퀴: 진열대 → 집기 → 형태에 대면 번짐 → 형태 바꾸기 → 고정 → 저장) · 상태: 증명 대기 · 필요한 증거: L3 + L4
- 성공 조건(사람 합격 문장): 설명 없이 혼자 3분 안에 진열대의 재료를 집어 형태에 대고, 형태를 바꾸고, 고정하고, 저장할 수 있다 (1110 인계 문서 6번)
- 조작: 핀치로 덩어리 집기 · 왼손 짧은 핀치 = 고정 · 왼손 엄지 쓸기 = 형태(A~Z·구·도넛) · 왼손 엄지 톡 = 저장 · 오른손 엄지 = 조율 다이얼 · 양손 핀치 = 옮기기·키우기
- stop: 실기 판정(`-verify-e2c3`)은 사람 몫
- 앞 카드 `build-4a39` (입자 모델·다이얼·Overlay) 는 사람 확인 끝, 커밋됨

## 다음 할 일 (최대 3)
1. 사람이 헤드셋에서 한 바퀴 판정 (빌드 `9d9e0b0-dirty-1111-103533` 설치됨, 실행 미확인). 저장한 PNG 는 `sh scripts/pull-stills.sh`
2. 판정 뒤 남은 일정 항목: 사용자용 패널(물성 다섯 값), 소리, 재료별 물성 프리셋, 채집 제스처
3. 윤슬 실사감: 해상도 올리기는 사람이 거절함. 다른 방법은 사람과 정함

## 정본 / 건드리지 말 것
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

첫 발화 한 줄: `HANDOFF.md 읽고 지금 카드 build-4a39. 윤슬 반짝임을 실사에 가깝게 하는 방법부터 사람과 정한다.`
