# HANDOFF — 물성 채집 MR 도구 (gather 브랜치, 보드 splatter)
updated: 2026-11-10 20:50 KST · by: claude-code

## 목표 (한 줄)
현실의 물성을 채집해서 손안에 갖고 형태에 입혀 쓰는 Quest MR 창작 도구. 손안의 느낌과 아름다움 > 한 바퀴가 걸림 없이 도는 것 > 기능 수. 마감 2026-11-19 05:00 KST. 원문: `/Users/rafaela/01_PROJECT_NOW/my-vr-colorpicker/1110HANDOFF_claude_code.md`

## 지금 카드
- id: `build-4a39` (입자 모델 통합: 위치·속도를 기억하는 CPU 시뮬레이션) · 상태: 증명 대기 · 필요한 증거: L3 (기기 측정) + L4 (사람 느낌)
- 성공 조건(사람 합격 문장): 손이 지나가면 입자가 손을 따라가고 손가락 사이로 갈라졌다가 돌아온다. 발열·프레임이 기존 빌드보다 나쁘지 않다. (사람 말: "발열이 괜찮다면 가능합니다")
- stop: 발열 비교(`-verify-133b`)와 느낌 조율(`-verify-7df5`)은 사람 몫. 발열이 나쁘면 방향을 바꾸기 전에 멈추고 보고한다.
- 보드: 코어 `-f7a2` 완료·커밋(`42ba43f`). 셰이더 `-d0ce`, 손가락 충돌체 `-2681`, 조율 패널 `-b775`, 채집 실험 `build-9580-build-959a` 는 확인 대기(review)이고 **미커밋** (훅이 done 카드만 받음)

## 다음 할 일 (최대 3)
1. (끝남) 빌드 `42ba43f-dirty-1110-204027` 이 기기에 설치돼 돌고 있음
2. 발열 비교: `python3 scripts/thermal-compare.py evidence/build-d4b6-verify-2edf/8c63c15-dirty-1110-103808/8c63c15-dirty-1110-103808.apk old 10` → 새 APK 로 `new 10` → `--report`. 같은 동작(30초 쉬고 30초 휘젓기)
3. 사람이 조율 패널(오른손 핀치)로 물방울 느낌 찾기 → `SaveTuning` → `sh scripts/tuning.sh pull` → `SplatTuning` 기본값으로 옮김. 이어서 `8 Catch > liveCamera` 로 채집 실험 판정

## 정본 / 건드리지 말 것
- 되돌릴 기준점: 커밋 `65af4f8` (수식 소환·소멸이 든 옛 방식)과 그 APK. 지우지 않는다
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
- 디스크 여유 21GB (2026-11-10 20:40). 증분 빌드는 75초 안팎

## 최근 증거
- 2026-11-10 `build-4a39` 빌드 `42ba43f-dirty-1110-204027`: 증거 L3. 예외 0, 72~73fps (124개 중 70 미만 2개), 손 닿을 때 시뮬레이션 1.6~1.9ms·손 떼면 잠듦, 조율 패널이 기기에 뜸 (`evidence/build-4a39/42ba43f-dirty-1110-204027/summary.md`). 발열 비교·사람 판정·카메라 질감은 미확인
- 2026-11-10 `build-4a39-build-f7a2`: 증거 L1. `verify_sim.py` NUnit 10/10 (손 쓸기 속도 전달, 손가락 구멍, 뭉침, 묻어남, 소환 지나침 2cm 미만, 소멸, 최대 dt 안정)
- 2026-11-10 셰이더·러너·손 입력·패널·채집: 증거 L2. `verify_compile.py` exit 0, EditMode 297/298 (실패 1개는 손대지 않은 `FolioRuntimeTests`, 원인 미확인), 에디터 시트 `evidence/build-4a39/editor-sequence-1110.png`. 기기 실행·Burst AOT·정점 텍스처 읽기·패널 손 조작·카메라 질감은 전부 미확인
- 2026-11-10 `build-9c56` 빌드 `8c63c15-dirty-1110-103808`: 증거 L3 일부, 72fps 위주. 이 카드의 남은 조정은 접음 (사람 결정, 보드 a10)

## 열린 질문 (사람에게)
- 발열 비교 결과가 괜찮은가 (측정 뒤 판정)
- 대비: ORION 때 정한 "패스스루 78% 디밍"을 여기에도 쓸지. 패널 `6 Look > passthroughBrightness` 로 먼저 눈으로 볼 수 있음
- 채집 제스처: 폐기된 양손 사각형 대신 시선 앞 창틀(펀치)로 갈지. 지금은 패널 버튼으로만 찰칵

첫 발화 한 줄: `HANDOFF.md 읽고 지금 카드 build-4a39 의 빌드와 발열 비교부터. 발열 판정과 느낌 조율에서 stop.`
