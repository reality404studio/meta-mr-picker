# HANDOFF — 시간 채집 리추얼 (Time Ritual)
updated: 2026-11-16 10:20 KST · by: claude-code · branch `ritual` (앱 레포 `unity/mood-picker`) · 보드 `--app time-ritual`

## 목표 (한 줄)
흩어진 오늘을 손으로 알갱이로 모아 빛의 병에 담는 하루 마무리 의식 — 손 추적만, Quest 3S 72fps(60 아래 금지). 마감 **2026-11-19 05:00 KST**. 구현 기준은 명세 `/Users/rafaela/01_PROJECT_NOW/my-vr-colorpicker/Time Ritual 구현 명세 (Claude Code용).md` (7장면·9단계, 단계마다 멈추고 보고 형식으로 보고 → 사람 헤드셋 확인). 이 파일은 현황용.

## 지금 카드 — 성능 측정과 GPU 여유 확보 (사람 11-16: 다음 세션은 성능 최적화)
- id: 미발행 — 시작할 때 `build` 과업으로 발행. 상태: 시작 전 · 필요한 증거: L3(측정) + L4(룩이 그대로인지 사람 눈)
- 왜: 큰 조각 여럿일 때 GPU App 10.5~13ms, 예산 13.9ms (72fps). 9단계 소리·가이드가 더 얹힌다
- 성공 조건(베이스라인, 사람 확정 전): 무거운 세 장면의 최악 GPU App ≤ 11.5ms, 룩 변화 없음
- 순서
  1. 준비: 기기 오래 켜 뒀으면 재부팅(VSnc=1 확인, LESSONS 11-14), 기기 `ritual.json` 비어 있는지, 설치 빌드 `f83df4d-dirty-1116-093646`
  2. 측정 (사람이 헤드셋을 쓰고 장면을 재현, 장면마다 10초): (a) 큰 조각 여럿 + 손이 알갱이 속 (b) 봉인 절정 Bloom (c) 선반 — 오늘 병 + 지난 병. Perfetto `mode vr` (Meta VR MCP `perf_trace_capture` 또는 `meta-vr:hz-perfetto-debug` 스킬) + logcat `VrApi`·`ritual perf frame= gpu= cpu=` 줄 → `evidence/perf-<MMDD>/`
  3. 판정: `% Time Shading Fragments` 대 `Vertices`, `ALU / Fragment`, 앱 GPU 시간(`AppInfo` 의 `debug.gpuTime`), 렌더 패스·머티리얼(Splat·Glass·Wax·Glow·Frost)별 비중. 짐작 금지 (LESSONS 11-13: 포비에이티드가 안 들었다고 정점 쪽이라 단정해서 틀림, 실제는 픽셀 84%)
  4. 처방 후보 표 (비용·룩 영향·되돌리는 법) → **stop: 사람이 고른다** (셰이더 수정은 묻는 규칙). 룩이 안 바뀌는 것부터 (splatter 11-13 결정과 같은 순서): 셰이더 연산 줄이기(half 정밀도·exp 근사·낮은 알파 조기 discard), 화면에서 커진 스플랫 크기 상한(겹침), 안 보이는 것 안 그리기(미니어처 선반·시야 밖). 그다음 설정 레버 `shelfPastQuality` 0.5 · `fieldReach` · `springSplats` · 동적 해상도 하한
  5. 고른 것 구현 → 같은 세 장면 재측정·비교(`perf_trace_compare`) → 사람이 룩 확인
- 함정: VrApi `GPU%` 는 클럭 기준이고 벗은 시간이 섞임 / 36fps(VSnc=2)면 코드보다 재부팅 먼저 / `RitualGovernor` 가 품질을 낮춰 측정을 가릴 수 있음 → 측정 때 거버너 품질 값을 같이 기록

## 그다음 카드 — 명세 9단계 가이드와 소리 마감
- id: 미발행 — 시작할 때 보드에 `build` 과업으로 발행 (`npm run studio -- add build "명세 9단계 가이드와 소리 마감: …" --stage build --app time-ritual --as "Claude Code"`). 상태: 시작 전 · 필요한 증거: L4
- 성공 조건(기준): 명세 「전체에 걸친 규칙 — 가이드」「소리」 + 사람 11-16 「사운드 디자인을 하면 감각적으로 다르게 느껴질듯」(7단계 손의 물성이 소리로 구분돼야 함). 사람 합격 문장은 카드 발행 때 이 두 줄로 적는다
- 할 일 (순서대로)
  0. **소리 파일 표 먼저 (사람 11-16, 구현 전).** `/Users/rafaela/01_PROJECT_NOW/my-vr-colorpicker/sound/` 의 파일**만** 쓴다. 파일마다 태그(용도)·라이선스·원작자·원본 링크를 확인한 표를 `evidence/time-ritual-spec-1115/sound-licenses.md` 에 만든다
     - `interaction_sounds/` 는 `manifest.json`(sources·tests·method)·`README_KO.md` 가 있다 — 여기서 시작. 파일: `Unity_WAV/` 8개(PULL·SLICE·MERGE·POUR), `Originals/` 3개, `Quick_Test/` 5개. README: "라이선스는 확인했으나 직접 청음한 선별은 아님, 물기·배경 소음 없음 보장 안 함"
     - `seal_sounds/` 7개는 manifest 없음 → 파일명만으로 확정하지 말고 원본을 확인한다
     - 파일명 기준 **CC-BY-4.0 (출처 표기 필요, 미확인)**: `07_MERGE_Two_Marbles__D43thsilence`, `Quick_Test/Merge_Marbles_Test`, `seal_sounds/05_Glass_Harp_C__InspectorJ`. 쓰는 것은 앱 안 크레딧 파일(예: `Assets/Ritual/Audio/CREDITS.md`)과 보고서에 원작자·제목·링크·라이선스를 적는다. 나머지는 파일명상 CC0
  1. **손 소리**: 알갱이 속에서 손 속도에 따라 사각거림, 흩어짐, 다리, 끊어짐, 합침 (7단계 동작에 붙는 소리. 후보 PULL_Sand·SLICE·MERGE·POUR 계열)
  2. **화음**: 카테고리마다 음과 음색 → 병에 쌓이면 화음 → 봉인에서 완성. 붓는 소리 음높이(8단계 `pourPitchLow/High`)와 이어진다
  3. **가이드 규칙**: 힌트는 한 번에 하나, 다음 행동이 일어날 자리에만, 4~5초 동안 행동이 없을 때만, 한 번 성공하면 다시 안 나옴, 글이 아니라 사물 반응 (명세 예시: 꺼내기 샘 숨쉬기 · 합치기 서로 기울기 · 붓기 병 입구 빛 — 지금은 손바닥에 실었을 때 · 봉인 밀랍 호흡 · 선반 오늘 자리 깜빡임)
  4. **녹화 모드**: 가이드 전체를 끄는 설정 하나
  5. 전체 소리 점검: 째깍으로 열고 유리 닿는 소리로 닫기, 오브제 위치에서 소리 남·긴 잔향, 정적과 절정의 대비 (봉인 싱잉볼은 4단계 결정 「작고 신비롭게」를 지킨다)
- 지키는 것: 글자·숫자·패널·버튼을 더하지 않는다. 새 패키지·에셋 삭제는 묻는다. 72fps (오디오 소스 수 늘리면 확인)
- stop: 9단계 구현 뒤 헤드셋 라운드에서 사람 판정 전에는 마감용 빌드·녹화로 넘어가지 않는다

## 다음 할 일 (최대 3)
1. 성능 카드: 세 장면 측정 → 병목 판정 → 처방 표에서 stop (사람이 고름) → 구현·재측정
2. 9단계: 소리 파일 태그·라이선스 표 → 손 소리 → 화음 → 가이드 → 녹화 모드 → 헤드셋 라운드 (mr-round), 보고서 `stage9-report.md`. 소리를 넣은 뒤 성능 재측정
3. 마감(11-19 05:00) 전 녹화 모드로 제출 빌드·영상

## 정본 / 건드리지 말 것
- 결정 `../../studio/shared/DECIDED.md`, 버린 안 `REJECTED.md` (11-16: 8단계 붓기와 병 / 고운 모래·병 속 숫자 뺌, 7단계 손의 물성 / 금선 없앰). 묻기 전에 먼저 본다
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
- 기기에 설치된 빌드: `f83df4d-dirty-1116-093646` (8단계 통과본)

## 최근 증거
- 8 붓기와 병 `build-e39f` · `f83df4d` + 다듬기 `a49b088` — **L4 통과** (사람 11-16): 손바닥에 실림·내려앉음·병 밖 미끄러짐·병 위 기울여 붓기 예, 고운 모래 없이 조금 꺼낸 날 병 괜찮음 예. 고운 모래(`restSand` false)·병 속 숫자(코드 삭제)는 사람이 뺌. 무게 0.6 확정. EditMode 103/103, APK `f83df4d-dirty-1116-093646`. logcat 버퍼 VrApi FPS 72~73 (실행 구간 여부 미확인). 원문 `evidence/build-e39f/07b2239-dirty-1116-014731/human-answers-1116.md`, 보고서 stage8-report.md 6절
- 7 손의 물성 `build-4cb1` · `07b2239` — L4: 실수 흩뜨림 0, 금선 없앰, 무게 → 가볍게 (8단계 라운드에서 확정), 72fps (`evidence/build-4cb1/198b0a4-dirty-1116-003936/logcat.txt`, 보고서 stage7-report.md)
- 6 선반 `build-fe7c` · `198b0a4` / 5 시계 `build-1eec` · `5052aa0` / 4 봉인 `build-452f` · `ebee6b4` / 3 꺼내기 `build-ef75`·`build-ed43` · `5656173` `b243977` / 2 빛과 정리 `build-c5b8` · `d62aab6` — 모두 L4. 단계 보고서 `evidence/time-ritual-spec-1115/stage{1..8}-report.md`

## 열린 질문 (사람에게)
- 헤드셋 메모 f5(양손 주무르기 힘듦) 해석 a17 확인 요청이 보드에 떠 있음 (확신 낮음)
