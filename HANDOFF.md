# HANDOFF — 시간 채집 리추얼 (ritual 브랜치, 보드 time-ritual) · 엔진 = 물성 채집 플레이그라운드
updated: 2026-11-14 14:31 KST · by: claude-code

## 목표 (한 줄)
형태 없이 흘러가 버린 오늘을 그 시간을 보낸 책상 위에 빛으로 남기는 하루 마무리 의식 (제출작, 작업대 `studio/shared/DECIDED.md` 2026-11-13 [time-ritual]). 아름다움(계속 만지고 바라보고 싶은) > Quest 3S 10분 발열·프레임 안정. 가우시안 스플랫으로 먼저 푼다(면·버텍스 메시 금지). 마감 2026-11-19 05:00 KST. 명세: 작업대 `studio/apps/time-ritual/source/ritual-guide-1112.md`

## 지금 카드
- id: `build-4bf1` 뽑기 · `build-24db` 뭉치기 · `build-d285` 병·붓기 · `build-5fae` 봉인 (에이전트 하위 과업 4개 doing) · 상태: 하는중 · 필요한 증거: L1 테스트 → L3 기기 → L4 사람
- 성공 조건(사람 합격 문장): 하위 과업 `*-verify-*` 4개 = 가이드 「검증 방법」 표. 예) 소리를 듣기 전에 눈금에 걸리는 느낌을 말한다 · 층만 보고 어느 빛인지 맞힌다 · 누르고 난 뒤 "오늘은 끝났다"
- stop: verify 4개는 사람. 기존 셰이더·파티클·질감 동작을 바꿔야 하면 먼저 묻는다 (가이드 작업 원칙)
- 11-14 실기 3회로 사람이 답한 것 (카드 note 에 원문): 눈금 걸림 있음 · 손끝 숫자 매우 좋음 · 층만 보고 빛 맞히기 "당연" · 붓기 72~73fps · 밀랍 엄지 꾸욱 괜찮음 · 봉인 결과 예쁨. 아직 안 물은 것: 마디가 보이나/놓으면 매끈해지나, 떼었다 붙였다 반복하나/다른 종류 튕김, 1시간 층 눈에 띄나/붓고 다시 들여다보나, "오늘은 끝났다"·실수로 안 닫히나 → verify 4개 아직 done 아님
- 11-14 반영(DECIDED 2026-11-14 두 항목): 실타래 회전·휘는 실, 꾸깃하기(막대 양 끝 두 손 핀치 → 울퉁불퉁한 덩어리), 덩어리 주무르기(15%씩, 바닥 70%), 넘치면 튕김+"남은 시간", 밀랍 A(책상 위 막대 → 당겨 방울 → 입구에 떨굼 → 누름, 떠 있던 구 없앰), 거버너 = fps+앱 GPU/CPU 시간(`Hands/Ovr/OvrPerfMetrics`), 굵기 2.2cm, 병 20cm·24시간=몸통 가득(용량 163u)
- 사람 마지막 말: 주무르기 "양쪽 핀치한 상태에서 꿀렁꿀렁하는거라 약간 힘들다" → 보드 모름 `u1` (손짓 후보: 손바닥 사이에 끼워 누르기 / 한 손 쥐었다 펴기 / 그대로)
- 미커밋 (카드 done 전): 앱 레포 리추얼 전부 — `Assets/Scripts/Ritual/**`, 새 `Assets/Scripts/Hands/Ovr/OvrPerfMetrics.cs`, `Assets/Shaders/Ritual/`, `Assets/Editor/TimeRitual*.cs`·`MoodPickerBuild.cs`, `Assets/Ritual/*.asset`, 테스트 `{TimeUnits,RitualCore,RitualRuntime,RitualGesture,ShaderPowBase}Tests.cs`, `LESSONS.md`, evidence. 작업대: DECIDED 11-14 두 항목, time-ritual 보드. 패티스리(`PatisserieSceneSetup`·`PatisserieLab.unity`·`IcingCanvasController`·`PipingBagInteractor`, 11-12, 출처 미확인)는 커밋 때 분리
- 플레이그라운드(splatter): 단서·소리 카드 보류(DECIDED 2026-11-13). `build-16f1` 남은 것: Perfetto 비교(`-verify-1666`), 30분 발열(`-verify-5187`, 사람). 옛 내용은 `evidence/HANDOFF-history.md`

## 다음 할 일 (최대 3)
1. 주무르기 손짓을 사람이 고르면(`u1`) 바꾸기. 고르기 전엔 지금(양쪽 핀치) 유지. 합쳐진 구체 주무르기는 실기 로그에 아직 없음 → 한 번 확인
2. verify 4개의 남은 문항을 실기에서 묻고(위 「아직 안 물은 것」), 성능 체크리스트 남은 것: 10~15분 연속 발열·온도, 봉인 뒤 책상 빛 fps, 병을 눈앞에 둘 때. 착용 "준비" 뒤에 `sh scripts/ritual.sh run` (LESSONS: 착용 전 실행은 배치가 엉뚱함, 재시작 전 `sealed=` 확인)
3. 통과한 카드부터 done → 커밋(`card-id: 관찰 가능한 변화`, 패티스리 분리). 디스크 여유 13GB(교훈은 20GB) — 빌드 더 하기 전에 사람과 정리 상의(`~/.codex/sessions` 는 지우지 않음)

## 정본 / 건드리지 말 것
- 물성 구슬 6개: 물방울·모래·털(빗질) / 젤리(푸딩 뼈대, 바닥 고정)·슬라임(뼈대+표면 접촉만, 끈적 0.4초, 영구 변형 1.5cm 상한, 천천히 회복)·전분(빠르면 판 갈라짐, 느리면 스며듦). 모두 사람이 확인한 값, 테스트로 고정 (`SplatSimTests`). 뼈대 이웃은 반드시 양방향 간선 (한쪽 방향이면 발산)
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
- Quest 3S `340YC10G9707W0`, 에디터 6000.3.27f2. 패키지: 리추얼 `com.DefaultCompany.timeritual`(APK `Builds/time-ritual-dev.apk`), 플레이그라운드 `com.DefaultCompany.moodpicker`
- 빌드: `Unity -batchmode -projectPath . -buildTarget Android -executeMethod MoodPickerBuild.BuildTimeRitualApkBatch`(플레이그라운드는 `BuildSplatterApkBatch`) **`-quit` 없이**. `mr build-unity`·`mr preflight`·`mr logcat` 은 이 프로젝트에서 안 맞음 (LESSONS.md), adb 는 Unity SDK platform-tools
- 헤드셋 없이 보기: `-executeMethod TimeRitualPreviewRender.RenderBatch -ritualOut <폴더>` (27장 + sheet.png, 로그에 `ritual-preview PASS`). 플레이그라운드는 `SplatterPreviewRender.RenderSequenceBatch -splatOut <png>`
- 검증: `python3 Assets/Scripts/Splatter/Verification~/verify_compile.py`, `sh scripts/run-editmode-tests.sh`
- 맥에서 값 바꾸기: 리추얼 `sh scripts/ritual.sh unit|shape|nodes|labels|set|push|reset`, 플레이그라운드 `sh scripts/tuning.sh push <json>` (둘 다 실기 미확인)

## 최근 증거
- 2026-11-14 14:25 빌드 `cf24a34-dirty-1114-141553` (2.2cm·큰 병·주무르기): L3 + L4 일부. 144u 에서 21u 거절 → 구김 → 주무르기 2번 → 162u(27시간) 5층 → 밀랍 → 봉인, 72~73fps, 품질 1.00, 예외 0. EditMode 381/381, 프리뷰 run6 PASS. `evidence/build-4bf1/cf24a34-dirty-1114-141553/summary.md`
- 2026-11-14 14:00 빌드 `cf24a34-dirty-1114-135704` (실타래·꾸깃·거절·밀랍 A·거버너): L3. 두 바퀴, 꾸깃 4번·거절 2번·봉인 2번, 72~73fps, 앱 GPU 최대 10ms, 거버너 감소 0회. `evidence/build-4bf1/cf24a34-dirty-1114-135704/summary.md`
- 2026-11-14 13:03 첫 실기 `cf24a34-dirty-1114-121724`: L3. 기기가 36fps(VSnc=2, 재부팅으로 풀림) + 거버너가 품질 0.5 로 내린 상태로 한 바퀴. `evidence/build-4bf1/cf24a34-dirty-1114-121724/round1-summary.md`
- 2026-11-14 12:22 리추얼 첫 APK `cf24a34-dirty-1114-121724`: 증거 L1 (EditMode 369 중 368, 리추얼 23/23, 실패 1개 기존 Folio) + 빌드 Succeeded errors=0 238s. `evidence/build-4bf1/cf24a34-dirty-1114-121724/summary.md`. 설치·기기 미확인
- 2026-11-14 11:44 리추얼 프리뷰 run3 + EditMode: 증거 L1·L2. 흰 점 픽셀 255→12, 게이트 뮤테이션(셰이더 되돌림) 실패 확인, 리추얼 21/21, 알갱이 8508 그대로. `evidence/build-4bf1/editor-preview-1114/run3-1144/`. 기기·사람 판정 미확인
- 2026-11-14 00:09 리추얼 에디터 프리뷰 run2: 증거 L2. 27장, 예외 0, 53u·5층·입자 8508, 봉인 dominant=sleep, 시뮬 틱 평균 0.59ms·최악 10ms(맥). `evidence/build-4bf1/editor-preview-1114/`. 기기·소리·사람 판정 미확인
- 2026-11-13 12:36 빌드 `8341e03-dirty-1113-123454` (A 셰이더 다이어트 + 촬영 모드): 사람 "네 다 괜찮은 것 같아요". logcat 12:39 film vr on, 구슬 4번, 예외 0, 손 닿는 50초 fps 중앙 72·stale 2.0/초·GPU% 0.85 (GPU 2단계). EditMode 346/346, 뮤테이션 2개 실패 확인. 증거 `evidence/build-16f1/8341e03-dirty-1113-123454/`
- 2026-11-13 12:18 `build-16f1` Perfetto 1회 (L3, 빌드 `1fae59f-dirty-1113-003009`): 픽셀 84%·정점 16%, ALU/픽셀 193, fps 63~73·stale 6.6/초. 요약 `evidence/build-16f1/1fae59f-dirty-1113-003009/summary.md`. 발열 체감 미확인
- 2026-11-13 `build-627f` 젤리·슬라임·전분·털 사람 확인(채팅). 증거 `evidence/feels/`, `evidence/heat/`. EditMode 341/342 (Folio 1개, 기존과 같음)
- 2026-11-13 발열: 사람이 "발열 너무 심한데요"로 헤드셋 끔. 직전 배터리 40℃ 열 상태 1, 약 3시간 연속 + 충전 중. 식힌 뒤 4분 30초 37.0→37.0℃

## 열린 질문 (사람에게)
- 주무르기 손짓 (`u1`): 양쪽 핀치는 "꿀렁꿀렁 약간 힘들다". 손바닥 사이에 끼워 누르기 / 한 손 쥐었다 펴기 / 그대로
- 병 유리 테두리: 병을 키우며 유리 점도 커져 테두리가 굵어 보임(`glassSplatRadius` 0.0025). 거슬리면 점 수를 늘리는 쪽(성능 비용)
- 인간공부 01 (부동소수점 오차와 셰이더 원리): 공통 보드 `reference-8b57` assets, 읽고 질문은 카드 메모로
- 리추얼 책상 빛: 번짐(새 기본) vs 빛줄기(옛) — `compare-run2-run3.png` 로 먼저 보고, 실기에서 `ritual.sh glow` 로 바꿔 가며 판정
- 발열(플레이그라운드): A 로 모자라면 B 점 하나에 색 하나 / C 스플랫 수 / D 해상도 0.9 중 무엇

첫 발화 한 줄: `HANDOFF.md 읽고 시간 채집 리추얼 이어서. 열린 질문 u1(주무르기 손짓)부터.`
