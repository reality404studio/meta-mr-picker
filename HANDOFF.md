# HANDOFF — 시간 채집 리추얼 (Time Ritual)
updated: 2026-11-16 09:45 KST · by: claude-code · branch `ritual` (앱 레포 `unity/mood-picker`) · 보드 `--app time-ritual`

## 목표 (한 줄)
흩어진 오늘을 손으로 알갱이로 모아 빛의 병에 담는 하루 마무리 의식 — 손 추적만, Quest 3S 72fps(60 아래 금지). 마감 **2026-11-19 05:00 KST**. 구현 기준은 명세 `/Users/rafaela/01_PROJECT_NOW/my-vr-colorpicker/Time Ritual 구현 명세 (Claude Code용).md` (7장면·9단계, 단계마다 멈추고 보고 형식으로 보고 → 사람 헤드셋 확인). 이 파일은 현황용.

## 지금 카드
- id: `build-e39f` 명세 8단계 붓기와 병 · 상태: 사람 판정 대기 (헤드셋 1차 L4 일부 + 다듬기 L1, 새 APK 설치됨) · 필요한 증거: L4
- 성공 조건(사람 합격 문장): 명세 5장면 완료 기준 「아주 조금만 꺼낸 날의 병도 초라해 보이지 않는다」 + 손바닥에 올려 기울여 붓기가 된다
- 11-16 헤드셋(사람): 손바닥에 실림 예 · 납작하게 내려앉음 예 · 병 밖 미끄러짐 예 · **병 위 기울여 붓기 답 없음(미확인)** · 고운 모래 "뿌연 작은 모래가 더 별로" → 뺌 · 병 속 숫자 "핵심 경험이 아니니 삭제" · 무게 0.6 "지금이 좋음" 확정. 원문 `evidence/build-e39f/07b2239-dirty-1116-014731/human-answers-1116.md`, DECIDED·REJECTED 11-16
- 다듬기 (보드 `build-e39f-build-abc7` done): `restSand` 기본 false(설정으로 남김), 병 속 숫자 코드 삭제, 게이트 갱신. 고운 모래가 빠져 「조금 꺼낸 날 초라하지 않다」는 수단 없이 사람 판정에 남음 (보드 u12)
- stop: 사람 판정 전 9단계로 가지 않는다. 남은 판정 = 병 위 기울여 붓기 + 8단계 통과 여부 (보드 `build-e39f-verify-1a97`)

## 다음 할 일 (최대 3)
1. 사람 판정 받기: 새 빌드 `f83df4d-dirty-1116-093646`(설치됨)에서 병 위 손바닥 기울여 붓기, 고운 모래 없는 조금 꺼낸 날 병 → 통과면 DECIDED 갱신·`verify-1a97` 닫기 (다듬기는 커밋됨)
2. 9단계 가이드와 소리 마감 — **손 소리 포함**(사람 11-16: "큰 차이를 느끼지 못해서, 사운드 디자인을 하면 감각적으로 다르게 느껴질듯"): 알갱이 속 손 속도 사각거림·흩어짐·다리·끊어짐·합침, 화음, 녹화 모드(가이드 전체 끄기)
3. 9단계 소리는 **`/Users/rafaela/01_PROJECT_NOW/my-vr-colorpicker/sound/` (interaction_sounds·seal_sounds) 파일만** — 먼저 파일별 태그·라이선스 표를 만들고, 출처 표기가 필요한 것은 쓸 때 기록 (사람 11-16, DECIDED)

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
- 8 다듬기 `build-e39f-build-abc7` · (이 커밋) — L1: EditMode 103/103, APK `f83df4d-dirty-1116-093646` 빌드·**기기에 설치됨**(실행 전) · 헤드셋 미확인 (`evidence/build-e39f/f83df4d-dirty-1116-093646/`)
- 8 붓기와 병 `build-e39f` · `f83df4d` — L4 일부(사람 11-16: 실림·내려앉음·미끄러짐 예), 병 위 붓기 미확인. logcat 버퍼 VrApi FPS 72~73 (실행 구간 여부 미확인, `evidence/build-e39f/07b2239-dirty-1116-014731/logcat.txt`)
- 7 손의 물성 `build-4cb1` · `07b2239` — L4: 실수 흩뜨림 0, 금선 없앰, 무게 너무 무거움 → 가볍게(재확인 전), 72fps (`evidence/build-4cb1/198b0a4-dirty-1116-003936/logcat.txt`, 보고서 stage7-report.md)
- 6 선반 `build-fe7c` · `198b0a4` / 5 시계 `build-1eec` · `5052aa0` / 4 봉인 `build-452f` · `ebee6b4` / 3 꺼내기 `build-ef75`·`build-ed43` · `5656173` `b243977` / 2 빛과 정리 `build-c5b8` · `d62aab6` — 모두 L4. 단계 보고서 `evidence/time-ritual-spec-1115/stage{1..8}-report.md`

## 열린 질문 (사람에게)
- 8단계 남은 판정: 병 위에서 손바닥을 기울이면 흘러 들어가는지, 고운 모래 없이 조금 꺼낸 날 병이 괜찮은지 → 통과면 9단계 (stage8-report.md 6절)
- 헤드셋 메모 f5(양손 주무르기 힘듦) 해석 a17 확인 요청이 보드에 떠 있음 (확신 낮음)
