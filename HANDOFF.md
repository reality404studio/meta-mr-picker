# HANDOFF — 시간 채집 리추얼 (Time Ritual)
updated: 2026-11-17 23:35 KST · by: claude-code · branch `ritual` (앱 레포 `unity/mood-picker`) · 보드 `--app time-ritual`

## 목표 (한 줄)
흩어진 오늘을 손으로 알갱이로 모아 빛의 병에 담는 하루 마무리 의식 — 손 추적만, Quest 3S 72fps. Meta VR Start Developer Competition 2026 생산성 트랙, **마감 2026-11-19 05:00 KST**. 구현 기준 명세 `/Users/rafaela/01_PROJECT_NOW/my-vr-colorpicker/Time Ritual 구현 명세 (Claude Code용).md` (9단계 전부 구현됨). 이 파일은 현황용.

## 지금 카드 — 9단계 헤드셋 판정 반영 → 제출 APK versionCode 2 (11-17 22:0x)
- 사람 판정 (DECIDED 11-17 「9단계 헤드셋 판정」, 제출 APK 1.0.1 로 한 바퀴): ① 합치기 끝소리 빨강·노랑 예, 물방울 계열 셋 아니오 → `bubbles (2).wav` 편집해 쓰기 ② 붓기 소리가 다 부은 뒤에도 남음 ③ 카드 괜찮음(크기·각도 유지), 가장자리 얇은 선 테두리
- 고친 것 (작업 트리): ① `merge_cool_bubbles.wav` (원본 0.98~1.39초, `scripts/sound_stage9.py` EXCERPT, 크레딧 CC BY 4.0 `CREDITS.md`) — `RitualAudio.Chimes` 차가운 쪽만, 설정 `mergeCoolVolume`·`mergeCoolPitch` ② `RitualJar.StreamEnded` (마지막 알갱이가 모래 면에 닿는 순간, `SurfaceTau` 0.62) 에서 `PourStop`, 알갱이 음도 그때 멈춤, 붓기 꼬리 `pourTailVolume` 0 (시계 무너짐은 `clockSandTailVolume` 0.8 그대로) ③ `RitualTutorial` 테두리 선 (안쪽 4mm, 굵기 0.8mm, 아랫줄 글자색 55%, `tutorialFrame*`) — 금박·장식은 넣지 않음 (형용사 "고급스러운 초대장"은 선 한 줄로만 해석, 사람 확인 전)
- 봉인: 사람 "b가 훨씬 나음 … 현재의 60퍼 길이로" → `sealVoice` 기본 1, 여운 10.8초 → 6.5초 (`afterglowLengthScale` 0.6, 앞 30% 유지 뒤 잦아듦, `RitualAudio.HarpLength`·`HarpFade`). 튜토리얼 4·5: 꺼진 적 없음, 과하지 않음 → 그대로 (DECIDED 11-17)
- 검증: EditMode 120/120 (`Logs/round-fix-1117-editmode3.xml`), 테스트 추가 `WaterKindsMergeWithBubbles…`·`PouringStopsWithoutATail…`(순수 함수 `PourTail`)·`ThePourStreamEnds…`·카드 테두리·봉인 B 60% 단언
- **v2 APK** `Builds/time-ritual-release.apk` 1.0.2 (versionCode 2, sha256 `cca85f4e…`) 서명 verify 통과, 11-17 22:04 헤드셋에 설치(업데이트, 기록 유지), 기기 `ritual.json` 지움. 헤드셋 확인 전 (L1). v1 보존 `Builds/time-ritual-release-v1-1.0.1.apk`
- v2 헤드셋 라운드 (22:05, `evidence/build-fd69/release-v2-1117-2203/`): 네 가지 **L4 통과** ("사운드는 완벽"), 72fps(최저 71), 붓기 소리가 층 가라앉기 약 1초 전에 끝남
- 그 뒤 사람 결정 (DECIDED 11-17): 구름 만질 때 무음 · 병 상한 24시간 고정 (`RitualFill.DayUnits` 144, 테스트 2개 갱신) · 선반에 놓으면 라벨 무조건 정면 (`RitualShelf.TodayFacing`, 셸빙 테스트에 140° 돌려 든 병 단언)
- v3 APK 1.0.3 (`Builds/time-ritual-release-v3-1.0.3.apk`, sha `e59f7976…`) = 24시간 상한 (테스트 120/120). 라벨 정면 코드는 빌드 도중에 고쳐 들어갔는지 불확실 → **올리지 말 것**, v4 로
- 모래 A안 (DECIDED 11-17 "a로 갑니다"): `RitualJar.StepSlosh`/`ApplySlosh` — 봉인 뒤·셸빙 전 `SloshEnabled`, 맨 위 표면 1.5cm 띠+덮개만 disp 로 움직임(셰이더 그대로), 기울기 20°+ 낮은 쪽으로 흐르고 쌓임(최대 9mm), 흔들면 스프링(1.6Hz, 감쇠 0.35), 가만하면 연산 0. 소리 `RitualAudio.SandShift` (붓기 루프, 0.16, 피치 0.8, 움직일 때만). 설정 `sandSlosh`·`slosh*`·`sandShift*` 라이브 조정 가능
- **v4 APK 1.0.4** (`Builds/time-ritual-release-v4-1.0.4.apk`, sha `1bede163…`) = 24시간 상한 + 라벨 정면 + 모래 A. EditMode 122/122 (`Logs/v4-editmode.xml`), 서명 verify 통과, 11-17 22:24 헤드셋 설치. 헤드셋 확인 전
- **v4 헤드셋 L4 통과 (11-17 22:30, "네 좋아요 아주좋아요")** — 라운드 `evidence/build-fd69/release-v4-1117-2224/`. 보드 `build-4cce-verify-6613`·`build-7d6a-verify-0249` done (모름 u19·u20 남김)
- **v6 APK 1.0.6** (`Builds/time-ritual-release-v6-1.0.6.apk`, sha `f611cfb6…`) = v4 + 선반 뒤 따뜻한 어둠 (카드 build-01ac, DECIDED 11-17 「따뜻한 어둠 값 컨펌」: 밝기 0.34, 전구색 (1,0.78,0.5), 채도 0.45, 감마 1.05, 패스스루 컬러 LUT). EditMode 123/123, 서명 verify, 11-17 23:3x 헤드셋 설치, 기기 ritual.json 지움. 값은 v5 라이브로 사람 컨펌, v6 자체로 선반까지는 미확인
- **v7 APK 1.0.7** (`Builds/time-ritual-release-v7-1.0.7.apk`, sha `84f8c111…`) = v6 + Ourglass 앱 아이콘 (카드 build-df55, 사람이 준 `my-mr-studio/icon-ourglass.PNG` → `Assets/Ritual/Icon/`, 제출 빌드 동안만 기본·둥근·적응형에 넣고 되돌림). EditMode 124/124, APK 에서 아이콘 꺼내 확인, 11-17 헤드셋 설치. 헤드셋 앱 목록에서 아이콘은 사람 확인 전 (`build-df55-verify-0bbe`)
- **업로드할 파일은 v7** (`Builds/time-ritual-release-v7-1.0.7.apk`)
- 디스크 여유 5.6GB (11-17 23:5x). `Library/Bee` 10GB — 마감 전엔 지우지 않음. 빌드 끝난 뒤 v3·v5 사본 정리 제안 중
- 다음: ① 사람이 Competition 채널에 v7 업로드 → 초대 URL → Devpost (`build-fd69-verify-90c3`, 한국 계정은 설치 확인 불가) ② 녹화 (튜토리얼 끔 = 녹화 모드, 끈 뒤 1분 기다림) ③ 제출물에 CC BY 크레딧 3건 (`Assets/Ritual/Audio/CREDITS.md`) ④ 커밋 끝 (11-17, 브랜치 ritual → origin/ritual 푸시함, 사람이 push-branches.json 에 ritual 추가): `48b66bb` build-2c40-build-2b35 성능 셰이더 변형·솎기 · `87480ff` build-fd69-build-67bf 제출 빌드·서명 · (이 HANDOFF 가 든 커밋) build-9775-build-c6f2 9단계 소리·튜토리얼·실기 다듬기(섞인 파일이라 한 커밋, 본문에 카드 목록). 커밋 안 한 것: Patisserie 5개(다른 작업), DevAgentSettings.asset(로컬 IP), env.keys, evidence/. 보드: build-9775 새 카드 done, build-2c40 은 verify-40f8(재측정)만 남음
- 라운드 2(B안) 원본 logcat 은 에이전트 실수로 유실, 요약만 `evidence/build-fd69/release-v1-1117-1910/round2-sealB-summary.md`

## 이전 카드 — 제출 포장 `build-fd69` · 서명된 APK 준비 끝, 사람이 업로드·초대 URL 하는 중
- 제출 APK `Builds/time-ritual-release.apk` (11-17 19:10): 앱 이름 **Ourglass**(라벨), 패키지 `com.reality404.timeritual`, versionCode 1 (1.0.1), 개발 빌드 아님, 정식 키 v2 서명 — `sh scripts/release_sign.sh verify` 통과. 헤드셋 실행 미확인
- 다시 만들기: `MoodPickerBuild.BuildTimeRitualReleaseApkBatch -ritualVersionCode <n>` (첫 빌드는 8GB RAM 에서 IL2CPP 하위 프로세스 Killed: 9 로 실패 → 같은 명령 재시도로 성공) → `envrun sh scripts/release_sign.sh sign` (레포 루트, `env.keys`). 다음 업로드는 versionCode 2 이상
- 서명 키 `~/.keystores/timeritual-release.keystore` + Keychain `TIMERITUAL_KEYSTORE_PASS` — 이 앱의 영구 키, 사람이 백업. 레포에 넣지 않는다
- 사람이 할 것 (`build-fd69-build-e66c`): 대시보드에 앱 Ourglass → Private 채널 `Competition` → Developer Hub App Distribution 으로 업로드 → Channel Settings 「Grant access to users by URL」 → 초대 URL 을 Devpost 에. 한국 계정은 GRAC 비성인 등급이 있어야 비공개 채널 가입 가능(Meta 문서) — 본인 시험 설치가 막히면 adb
- 결정: DECIDED 11-17 「제출」「제출 앱 이름 = Ourglass」
- 11-17 20:49 사람 보고: 초대 URL 이 "South Korea 에서는 테스트할 수 없다"로 막힘(위 GRAC 예상대로) → 제출 APK 를 adb 로 설치함 (`com.reality404.timeritual` 1.0.1, sha256 `abf44fbe…`). 헤드셋 앱 목록에서는 「알 수 없는 출처」의 Ourglass. dev 앱(`com.DefaultCompany.timeritual`, 실제 기록 있음)과 따로 깔려 있고, 제출 앱은 기록이 비어 있음. 초대 URL 설치 확인(`build-fd69-verify-90c3`)은 한국 밖 계정이 필요해 미확인
- `ritual.sh` 가 `PKG` 환경 변수를 받게 함: `PKG=com.reality404.timeritual sh scripts/ritual.sh run|set …` (제출 빌드가 `ritual.json` 을 읽는지는 미확인)

## 그다음 카드 — 9단계 헤드셋 라운드: 소리 `build-4cce` + 튜토리얼 `build-7d6a` (+ 성능 재측정 한 번 더, 사람 11-17)
- 상태: 둘 다 구현·빌드 끝 → 사람 헤드셋 대기 (`build-4cce-verify-6613`, `build-7d6a-verify-0249`). 필요한 증거: L4. 사람은 기기까지 이동 약 1시간 (11-17 18:50)
- APK `Builds/time-ritual-dev.apk` (11-17 18:41) = 커밋 `77c982c` + 작업 트리 전부(성능 1·2번 + 소리 + 튜토리얼). 설치 `sh scripts/ritual.sh install` → 사람 "준비" → `sh scripts/ritual.sh run`, logcat `VrApi` 로 72fps
- 성공 조건(사람 합격 문장, 확정 전): 소리 — "소리마다 맞는 자리에서 나고, 합치기가 소리로 구분되며, 봉인 절정과 정적이 대비된다" / 튜토리얼 — "처음 쓰는 사람이 카드만 보고 한 바퀴를 돈다"
- 묻는 것: 보고서 2절 예/아니오 질문 각 5개 — `evidence/time-ritual-spec-1115/stage9-sound-report.md`, `stage9-tutorial-report.md`. 조정값은 라이브로 (`sh scripts/ritual.sh set <키> <값>`, 끝나면 지움). 봉인 소리 A/B = `sealVoice` 0/1
- 사람 결정 11-17 (DECIDED): 튜토리얼 = 병 왼쪽 흰 카드, 영어 한 문장, 오른손 엄지 톡으로 켜고 끔, **튜토리얼 끔 = 녹화 모드**. 앱은 켤 때마다 튜토리얼 켬으로 시작 (끈 상태 기억 안 함 — 녹화 뒤 심사위원에게 건넬 때 안전, `tutorialRemember`)
- 녹화 요령: 앱 켬 → 오른손 톡 → 1분 기다린 뒤 녹화 (끈 뒤 60초 안에만 손끝 빛 힌트가 한 번 나옴)
- 맥에서 미리 들어 볼 것: `evidence/build-4cce/previews/*.wav` (오프라인 믹스, 잔향·BGM 없음). 카드 캡처: `evidence/build-7d6a/cards/*.png`
- stop: 사람 헤드셋 판정 전에는 제출 빌드·녹화로 넘어가지 않는다. 결과가 오면 DECIDED 에 원문으로 쓰고 보드 verify done

## 다음 할 일 (최대 3)
1. 사람이 Competition 업로드·초대 URL → 초대 URL 로 설치 확인 (`build-fd69-verify-90c3`) → Devpost
2. 기기 라운드: **제출 APK**(`Builds/time-ritual-release.apk`, 패키지 `com.reality404.timeritual` — `ritual.sh` 는 dev 패키지라 adb 직접)로 한 바퀴 + 성능 재측정 + 9단계 소리·튜토리얼 질문. 고칠 게 나오면 versionCode 2 로 재빌드·서명·같은 채널 업로드
3. 커밋: 성능 `build-2c40` · 소리 `build-4cce` · 튜토리얼 `build-7d6a` · 제출 `build-fd69` 가 같은 파일(`TimeRitual.cs`·`TimeRitualConfig.cs`·`RitualRuntimeTests.cs`·`MoodPickerBuild.cs`)에 섞여 있다. 카드가 done 이 된 뒤 카드별로 나눠 `card-id: 관찰 가능한 변화` 로 (커밋 훅이 보드 done 을 요구). Patisserie 5개 파일은 다른 작업

## 정본 / 건드리지 말 것
- 결정 `../../studio/shared/DECIDED.md`, 버린 안 `REJECTED.md`. 묻기 전에 먼저 본다 (11-17 튜토리얼, 11-16 8단계·7단계·성능 처방)
- 게이트(어기면 EditMode 실패): `Assets/Scripts/` 주석 0개 · `Ritual/` 의 `Ovr/` 밖 `OVR*` 금지 · 셰이더 `pow` 밑 클램프. 되돌리지 말고 통과하는 쪽으로만
- 소리 음원은 `Assets/Ritual/Audio/Resources/RitualSfx/` (사람이 고른 25개 + `sfx_gains.json`, 테스트가 폴더 구성을 고정). 다시 만들기 `python3 -I scripts/sound_stage9.py /Users/rafaela/01_PROJECT_NOW/my-vr-colorpicker/sound/prepared_1116 Assets/Ritual/Audio/Resources/RitualSfx`. 크레딧 `Assets/Ritual/Audio/CREDITS.md` (CC BY 4.0 두 건 — 제출물에 표기 필요)
- 카테고리 값은 `Assets/Editor/TimeRitualSceneSetup.Categories` (빌드가 씬·카테고리 에셋을 다시 씀). 셰이더·렌더러 수정·에셋 삭제는 사람에게 묻는다. `evidence/` 는 git 밖
- 옛 HANDOFF 전문(성능 카드 상세·재측정 방법·9단계 원래 할 일)은 `evidence/HANDOFF-history.md` 맨 위 블록(11-17 보관)

## 기기·환경
- Unity `/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity` (배치모드 `-quit` 금지). 테스트 `-batchmode -nographics -projectPath $PWD -runTests -testPlatform EditMode -testFilter "MoodPicker.Tests.RitualRuntimeTests;MoodPicker.Tests.RitualGestureTests;MoodPicker.Tests.RitualCoreTests;MoodPicker.Tests.BuildGateProjectTests;MoodPicker.Tests.HandInputTests;MoodPicker.Tests.ShaderPowBaseTests;MoodPicker.Tests.TimeUnitsTests;MoodPicker.Tests.SplatShaderCostTests"` (~4분, 117개)
- APK `-batchmode -nographics -projectPath $PWD -buildTarget Android -executeMethod MoodPickerBuild.BuildTimeRitualApkBatch` → `Builds/time-ritual-dev.apk` (~2~5분). 카드 캡처 `-batchmode -projectPath $PWD -buildTarget Android -executeMethod TimeRitualTutorialPreview.RenderBatch -tutorialOut <폴더>` (그래픽 필요, `-nographics` 빼기). 옛 `TimeRitualPreviewRender` 는 11-15 시계 추가 뒤로 깨져 있음
- Quest 3S `340YC10G9707W0`, adb `/Applications/Unity/Hub/Editor/6000.3.27f2/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb`, 패키지 `com.DefaultCompany.timeritual`. `mr preflight`·`mr logcat` 오판 → adb 직접. 기기 `ritual-days.json` 에 실제 기록 있음, `ritual.json` 비어 있음
- 기기에 설치된 빌드 `77c982c-dirty-1116-102101` (성능 1번). 보존 APK `Builds/time-ritual-perf2-77c982c-dirty-1116-154323.apk` (성능 1+2번만, 재측정용)
- 디스크 여유 약 9.7GB (11-17 18:45) — 빌드 여러 번이면 정리 필요 (diskcheck)

## 최근 증거
- 9 튜토리얼 `build-7d6a` — **L1** EditMode 117/117 (`Logs/tutorial-editmode.xml`), **L2** 에디터 캡처 15장, APK 빌드 성공 (`Logs/tutorial-build.log`). 헤드셋 미확인
- 9 소리 `build-4cce` — **L1** EditMode 114/114 (`Logs/stage9-editmode.xml`), APK 빌드 성공. 사람·에이전트 모두 청음 전
- 성능 `build-2c40` — 1번 L4 룩 통과 + L3 재측정 ((a) 최악 12.57ms, 목표 11.5 미달), 2번 L1 (실기 전). `evidence/perf-1116/verdict.md`
- 8 붓기와 병 `build-e39f` 까지 2~8단계 모두 L4 (11-15~16). 단계 보고서 `evidence/time-ritual-spec-1115/stage{1..8}-report.md`

## 열린 질문 (사람에게)
- 성능 재측정을 보존 APK(성능 2번만)로도 할지, 제출 APK 한 번으로 갈음할지 (사람 11-17 「한번 더」)
- 제출 빌드 전 설정 `Dev` 절(`showMinutesLabel`·`showTotals` = 1)이 화면에 무엇을 띄우는지 미확인 — 제출 전 확인
- 헤드셋 메모 f5(양손 주무르기 힘듦) 해석 a17 확인 요청이 보드에 떠 있음 (확신 낮음)
