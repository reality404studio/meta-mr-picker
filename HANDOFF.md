# HANDOFF — 시간 채집 리추얼 (Time Ritual)
updated: 2026-11-16 02:35 KST · by: claude-code · branch `ritual` (앱 레포 `unity/mood-picker`) · 보드 `--app time-ritual`

## 목표 (한 줄)
흩어진 오늘을 손으로 알갱이로 모아 빛의 병에 담는 하루 마무리 의식 — 손 추적만, Quest 3S 72fps(60 아래 금지). 마감 **2026-11-19 05:00 KST**. 구현 기준은 명세 `/Users/rafaela/01_PROJECT_NOW/my-vr-colorpicker/Time Ritual 구현 명세 (Claude Code용).md` (7장면·9단계, 단계마다 멈추고 보고 형식으로 보고 → 사람 헤드셋 확인). 이 파일은 현황용.

## 지금 카드
- id: `build-e39f` 명세 8단계 붓기와 병 · 상태: 증명 대기 (구현·L1 끝, 헤드셋 확인 전) · 필요한 증거: L4
- 성공 조건(사람 합격 문장): 명세 5장면 완료 기준 「아주 조금만 꺼낸 날의 병도 초라해 보이지 않는다」 + 손바닥에 올려 기울여 붓기가 된다
- 바뀐 것: 손바닥을 펴 위로 → 조각이 실려 납작하게 내려앉음 → 병 위에서 기울이면 붓기(밖이면 미끄러짐), 찰수록 붓는 소리 음높이↑, 뚜껑 닫으면 나머지 하루의 옅은 고운 모래가 빈틈을 채워 항상 가득(기록엔 부은 층만), 층마다 모래알로 된 작은 숫자(무작위). 보고서 `evidence/time-ritual-spec-1115/stage8-report.md` (2절 헤드셋 질문 5개, 3절 조정값)
- stop: 헤드셋 라운드 뒤 사람 판정 전 9단계로 가지 않는다
- 첫 확인: 손바닥에 안 실리면 손바닥 방향 부호 반대일 수 있음 → `sh scripts/ritual.sh set palmNormalSign -1` (미확인)

## 다음 할 일 (최대 3)
1. 8단계 기기 라운드 (mr-round): 실행(이미 설치됨) → 보고서 2절 질문 → GPU/CPU 측정 → DECIDED 기록 → 보드 `build-e39f-verify-1a97` 닫기
2. 7단계에서 가볍게 바꾼 무게(`weightLag` 0.6, 제곱근 곡선)도 같은 라운드에서 확인 — 사람: 이전 값 "너무 무겁긴 함"
3. 9단계 가이드와 소리 마감 — **손 소리 포함**(사람 11-16: "큰 차이를 느끼지 못해서, 사운드 디자인을 하면 감각적으로 다르게 느껴질듯"): 알갱이 속 손 속도 사각거림·흩어짐·다리·끊어짐·합침, 화음, 녹화 모드(가이드 전체 끄기)

## 정본 / 건드리지 말 것
- 결정 `../../studio/shared/DECIDED.md`, 버린 안 `REJECTED.md` (11-16: 7단계 손의 물성 / 금선 없앰). 묻기 전에 먼저 본다
- 게이트(어기면 EditMode 실패): `Assets/Scripts/` 주석 0개 · `Ritual/` 의 `Ovr/` 밖 `OVR*` 금지 · 셰이더 `pow` 밑 클램프. 되돌리지 말고 통과하는 쪽으로만
- 카테고리 값은 `Assets/Editor/TimeRitualSceneSetup.Categories` 에서 (빌드가 `TimeRitual.unity`·카테고리 에셋을 다시 씀). 셰이더·렌더러 수정·에셋 삭제는 사람에게 묻는다
- 작업 트리의 Patisserie 5개 파일(`PatisserieSceneSetup.cs`, `PatisserieLab.unity`, `IcingCanvasController.cs`, `PipingBagInteractor.cs`, `SplatStrokeChunk.cs`)은 다른 작업 — 커밋에 섞지 않는다. `evidence/` 는 git 밖
- 코드 지도·옛 구현 설명은 `evidence/HANDOFF-history.md` 맨 위 블록(11-16 보관)

## 기기·환경
- Unity `/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity` (배치모드 `-quit` 금지). 테스트: `-batchmode -nographics -projectPath $PWD -runTests -testPlatform EditMode -testFilter "MoodPicker.Tests.RitualRuntimeTests;MoodPicker.Tests.RitualGestureTests;MoodPicker.Tests.RitualCoreTests;MoodPicker.Tests.BuildGateProjectTests;MoodPicker.Tests.HandInputTests;MoodPicker.Tests.ShaderPowBaseTests;MoodPicker.Tests.TimeUnitsTests" -testResults Logs/x.xml -logFile Logs/x.log` (~4분)
- APK: `-batchmode -nographics -projectPath $PWD -buildTarget Android -executeMethod MoodPickerBuild.BuildTimeRitualApkBatch -logFile Logs/build.log` → `Builds/time-ritual-dev.apk` (테스트 뒤엔 플랫폼 전환으로 ~10분)
- Quest 3S `340YC10G9707W0`, adb `/Applications/Unity/Hub/Editor/6000.3.27f2/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb`, 패키지 `com.DefaultCompany.timeritual`. `mr preflight`·`mr logcat` 오판·실패 → adb 직접. 빌드·배치모드가 adb 를 죽이니 logcat 스트림은 그 뒤 다시. 프레임은 logcat `VrApi` 줄(FPS·Stale·App ms)
- 라이브 튜닝 `sh scripts/ritual.sh set <키> <값>` → 기기 `ritual.json` (재실행 뒤에도 남음, 비교 끝나면 지움). 지금은 비어 있음(11-16 지움). 기기 `ritual-days.json` 에 실제 기록 있음
- 성능 여유 적음: 큰 조각 여럿일 때 GPU App 10.5~13ms (예산 13.9). 무거워지면 `shelfPastQuality` 0.5, `fieldReach`, `springSplats` 순

## 최근 증거
- 8 붓기와 병 `build-e39f` · (이 커밋) — L1: EditMode 102/102, APK `07b2239-dirty-1116-014731` 빌드·**기기에 설치됨**(실행 전) · 헤드셋 미확인
- 7 손의 물성 `build-4cb1` · `07b2239` — L4: 실수 흩뜨림 0, 금선 없앰, 무게 너무 무거움 → 가볍게(재확인 전), 72fps (`evidence/build-4cb1/198b0a4-dirty-1116-003936/logcat.txt`, 보고서 stage7-report.md)
- 6 선반 `build-fe7c` · `198b0a4` / 5 시계 `build-1eec` · `5052aa0` / 4 봉인 `build-452f` · `ebee6b4` / 3 꺼내기 `build-ef75`·`build-ed43` · `5656173` `b243977` / 2 빛과 정리 `build-c5b8` · `d62aab6` — 모두 L4. 단계 보고서 `evidence/time-ritual-spec-1115/stage{1..8}-report.md`

## 열린 질문 (사람에게)
- 8단계 헤드셋 질문 5개 (stage8-report.md 2절) — 특히 손바닥에 실리는지, 조금 꺼낸 날 병이 초라하지 않은지
- 헤드셋 메모 f5(양손 주무르기 힘듦) 해석 a17 확인 요청이 보드에 떠 있음 (확신 낮음)
