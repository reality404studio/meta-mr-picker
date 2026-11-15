# HANDOFF — 시간 채집 리추얼 (Time Ritual)
updated: 2026-11-16 01:50 KST · branch: `ritual` (앱 레포 `unity/mood-picker`) · 보드: `studio/apps/time-ritual/` (`--app time-ritual`)

## 1. 프로젝트 한 줄 정의 & 목표
- **작품 한 줄**: 형태 없이 흘러가 버린 오늘을, 그 시간을 보낸 책상 위에 빛의 병으로 남기는 하루 마무리 의식.
- **배경/타깃**: Meta VR Start Developer Competition 2026 생산성 트랙(마감 `2026-11-19 05:00 KST`). 컨트롤러 없이 **핸드트래킹만**으로 처음부터 끝까지 조작. **구현 기준(명세): `/Users/rafaela/01_PROJECT_NOW/my-vr-colorpicker/Time Ritual 구현 명세 (Claude Code용).md` (2026-11-15, 7장면·작업 순서 9단계, 단계마다 멈추고 보고 → 사람 헤드셋 확인).** 이 HANDOFF 는 현황 파악용. 옛 `../studio/apps/time-ritual/source/ritual-guide-1112.md` 는 11-12 실험 빌드 가이드
- **핵심 원칙**: 압박/스트릭/점수/알림 없음("성취가 아니라 안도감"). 아름다움 + Quest 3S 72fps/발열 안정.
- **다음 메인 작업**: 명세 **8단계 붓기와 병** (아래 0절). 사람 선택(11-16): 손 소리는 9단계에서 한꺼번에

---

## 0. 지금 카드 (2026-11-15 22:20)
- 구현 기준: `/Users/rafaela/01_PROJECT_NOW/my-vr-colorpicker/Time Ritual 구현 명세 (Claude Code용).md` (7장면·9단계, 단계마다 멈추고 보고 → 사람 헤드셋 확인). 단계 보고서 `evidence/time-ritual-spec-1115/stage{1..6}-report.md`
- **끝난 단계** (보드 card · 커밋)
  - 2 빛과 정리 `build-c5b8` · `d62aab6` — 방 38%(textureOpacity), 후광·손 빛, 책상 웅덩이, 라벨 왼손 마이크로제스처 온오프(기본 끔)
  - 3 꺼내기 `build-ef75` `build-ed43` · `5656173` `b243977` — 샘 = 알갱이 무리(움직임으로 구분), 사슬 처짐, 놓으면 모임(A), Enjoyed 깃털, 떠다님 끔
  - 4 봉인 `build-452f` — 뚜껑 → 샘이 연기처럼 밀랍으로 → 엄지 누르고 떼면 Bloom(병 발광·빛 알갱이·구름안개 2m)·작은 싱잉볼
  - 5 시계 `build-1eec` · `5052aa0` — 글리프로 된 HH:MM:SS + MM.DD(13.5cm), 닿으면 멈춤 → 무너짐(A) → 다섯 샘. BGM Before the Morning Wakes
  - 6 선반 `build-fe7c` · `198b0a4` — 만드는 동안 1/4 미니어처(왼쪽 구석) → 봉인 뒤 커지며 메인으로. 떠 있는 받침 다섯(B), 지난 병 = 완성된 입자 병(데모 2일), 빈자리 고리+날짜. 자석 안착 → 물결·Logged·지난 병 함께 밝아짐 → 톡 소용돌이·화음·기록 → 달빛 구슬 소등 → 페이드 → 앱 종료
  - 7 손의 물성 `build-4cb1` · (이 커밋) — 손가락 다섯+손바닥이 알갱이를 밀어 천천히 = 갈라짐, 손바닥 0.6m/s↑ = 흩어졌다 2.2초에 돌아옴, 핀치 모으는 중·놓은 직후 흩뜨림 없음(실기 실수 0). 쥔 조각은 크기의 제곱근만큼 늦게(`weightLag` 0.6, 사람 "너무 무겁긴 함" 뒤 1.5→0.6, 재확인 전). 같은 종류 1.6cm 안 → 알갱이 다리 0.7초 → 합침(4.5cm 벌리면 끊어짐). 금선은 사람이 뺌(`seam` false). 라벨 개수 표기 삭제. 보고서 `evidence/time-ritual-spec-1115/stage7-report.md`. 실기 72fps, 조각 여럿일 때 GPU 10.5~13ms(여유 거의 없음). 사람: "큰 차이를 못 느낌, 사운드 디자인을 하면 다르게 느껴질 듯" → 손 소리(사각거림·흩어짐·다리·끊어짐·합침)는 9단계에
- **다음: 8단계 붓기와 병** — 손바닥 붓기, 층, 고운 모래(항상 가득), 병 속 숫자 글리프. 그다음 9 가이드·소리(손 소리 포함). 마감 2026-11-19 05:00 KST. 기기 `ritual.json` 에 `{"seam": false}` 남아 있음(새 빌드 기본값과 같음, 지워도 됨)
- 사람 결정 (DECIDED 11-15 여러 항목): 카테고리 이름·굵기 지금 값, 라벨 온오프, 형상 = Enjoyed 만 깃털, 떠다님 끔, 놓을 때 모임, 봉인 Bloom, 시계 무너짐 A, 선반 B·미니어처. **60fps 아래로 떨어지면 안 됨**
- 남은 리스크: GPU 9~10ms (지난 병 두 개를 입자 병으로 그린 뒤, 72fps 유지·여유 줄어듦 → 무거워지면 `shelfPastQuality` 0.5). 시작 직후 1초 33 stale(어둠 중). 시계 표시 중 간헐 stale(원인 미확정). 붓기 시작 3프레임. MRUK 벽 찾기 미구현(명세 대체 경로만)
- 도구 주의: `mr preflight` 오판·`mr logcat` 실패 → adb 직접(빌드가 adb 를 죽이니 빌드 뒤 스트림 재시작), PACKAGE `com.DefaultCompany.timeritual` 명시, 커밋은 보드 done 카드 id, 빌드가 `TimeRitual.unity`와 카테고리 에셋을 다시 씀 → 카테고리 값은 `TimeRitualSceneSetup.Categories` 에서. 기기 라이브 설정 `ritual.json` 은 재실행 뒤에도 남음 → 비교 끝나면 지움(지금은 지움). 기기 `ritual-days.json` 에 오늘 실제 기록이 있음(데모 켜져 있어 안 보임). 작업 트리의 Patisserie 파일 변경은 다른 작업 — 커밋하지 않음

## 2. 현재 구현 완료 상태 (2026-11-15 00:45 기준)
직전 커밋(`e2b1685`) 이후 이번 세션까지 구현·검증·Quest 3S 배포된 전체 루프:

1. **카테고리 & 구슬 배치 (`TimeRitualSceneSetup.cs`, `TimeCategoryAsset.cs`)**
   - 배치 순서(`PlacedCategories`): `sleep`(여백 / `Rested` / 하늘) · `move`(환기 / `Moved` / 윤슬) · `work`(몰입 / `Focused` / 노을 — 중앙) · `drift`(탐색 / `Explored` / 은행나무 — 중우측) · `play`(유희 / `Enjoyed` / 단풍).
   - 10분 단위(`TimeUnits`, 기본 `unitLength = 0.020m`, 히스테리시스 `15%`), 30·60분 마디 강조. `PieceFeel` 젤리 통일 및 물방울 파티클·오르골/풍경 효과음(`RitualAudio`).

2. **뽑기 · 뭉치기 · 꾸깃 · 붓기 (`TimePullController`, `TimeHeap`, `TimePieceView`, `RitualJar`)**
   - 빛 더미에서 핀치로 뽑아(실타래 회전 + 휘는 실) 공중에 띄움.
   - 같은 카테고리 조각끼리 근접 합치기 / 손바닥 쓸기(`StepSweep`) / 양손 꾸깃(`GripSqueeze`) & 덩어리 주무르기 / 다른 카테고리끼리 반발.
   - 병 입구(`InPourZone`)에 가져가면 모래로 분해되어 바닥부터 울퉁불퉁한 층(`Strata`)으로 적층(20cm 약병, 24시간 = 몸통 가득, 알갱이 상한 `maxGrains = 20000` 예산 배분). 용량 초과 시 입구 튕김 + "남은 시간" 표시.

3. **병 라벨 (`BottleLabel`, `TimeRitual.cs`)**
   - 스플랫 입자 없는 매끈한 팔각 챔퍼 종이 카드(`LabelCard`) + 연속 금박 띠(`LabelBorder`) + `Fraunces_72pt-Black.ttf` 2분할 글리프 메시(`LabelText`).
   - 평소 병 옆에 떠 있다가 봉인(`_sealed`) 시 병 몸통 원통 곡면을 따라 휘어지며(`DeformLabelTextVertices`) 자동 부착.

4. **3D 스플랫 빛 궤적 가이드 & 오른손 마이크로제스처 토글 (`RitualGuideTrails.cs`, `TimeRitual.cs`, `RitualGlowSplat.shader`)**
   - 구슬 호버 → 뽑기 → 병 입구로 붓기 / 같은 카테고리 조각 병합 → 밀랍 유도까지 실시간 3D 베지어 입자 궤적 표시(`Arc0`, `Arc1`).
   - 오른손 마이크로제스처(`OvrMicrogestureInput`, 쿨다운 0.35s)로 `config.guideTrails` 온/오프 토글(`StepMicrogesture`). 왼손 마이크로제스처는 의도적으로 제외.

5. **봉인 & 봉인 후 3D 회전 · 3D 입체 오라 (`RitualWax`, `RitualDeskGlow`, `RitualJar`, `TimeRitual.StepJarPose`)**
   - 책상 위 밀랍 막대에서 핀치로 방울을 당겨 입구 위에 떨구고 1.2초 눌러 봉인(`SealPress`, `SealEmblem`, 다수 시간 빛 색상).
   - 봉인 후 `y=0` 책상 평면 빛 대신 **병 몸통·어깨를 감싸는 3D 타원체 오라 + 회전 별가루 헤일로**(`RitualDeskGlow.TrackVessel`) 적용.
   - 봉인된 병을 **양손 핀치(`TryTwoHandFrame`)**로 **크기 변화 없이(`Vector3.one`) 3D 회전** 가능, 손을 놓으면 `BodyCenter` 기준으로 부드럽게 수직 복귀(`UprightYaw`, 오뚝이).
   - **방금 수정(Quest 3S 배포됨)**:
     - `RitualJar._cap`(`SandTop`)의 높이를 월드 오프셋 `disp` 대신 병 로컬 `home.y` (`_cap.SetHome`)에 기록해 기울여도 윗면 모래 뚜껑이 병 밖으로 분리되지 않도록 수정.
     - `RitualJar.BuildFloor()`(`SandBottom`)로 첫 층을 부을 때 바닥 모래 원반을 채우고, `BuildGlass()`(`baseArea * 1.6f`, 외곽 받침 링 + 오목 유리 펀트) 및 `RitualGlassSplat.shader` `baseSheen`으로 바닥을 올려다봐도 텅 빈 원통처럼 보이지 않도록 마감.

---

## 3. 코드 & 에셋 구조 (기억보관대 작업 시 바로 읽을 핵심 파일)
- **도메인/순수 C# (`MoodPicker.Ritual.Core`)**:
  - `Assets/Scripts/Ritual/Core/RitualCore.cs` (`JarShape`, `RitualFill`, `Strata`, `SealPress`, `RitualTally`, `RitualGovernor`, `SealEmblem`)
  - `Assets/Scripts/Ritual/Core/TimeUnits.cs` (`TimePiece` 구조체, 단위/라벨 포맷)
- **런타임 오케스트레이션 (`MoodPicker.Ritual.Runtime`)**:
  - `Assets/Scripts/Ritual/Runtime/TimeRitual.cs` (`Init`, `BuildJar`, `Seal`, `ResetDay`, `StepGrips`, `StepJarPose`, `StepPanel`, `StepGuideTrails`, `StepMicrogesture`, `StepGovernor`, `PollLive`)
  - `Assets/Scripts/Ritual/Runtime/RitualJar.cs` (`Layer` 리스트, `_layers` / `_cap` / `_floor` / `_glass`, `TotalUnits`, `Layers`, `SurfaceMean`)
  - `Assets/Scripts/Ritual/Runtime/RitualWax.cs`, `RitualDeskGlow.cs`, `RitualGuideTrails.cs`, `RitualSplatCloud.cs`, `RitualAudio.cs`
  - `Assets/Scripts/Ritual/Runtime/TimeCategoryAsset.cs`, `TimeRitualConfig.cs`, `RitualHands.cs`, `TimeRitualLook.cs`, `TimeHeap.cs`, `TimePieceView.cs`, `TimePullController.cs`, `TimeRitualPlacer.cs`
- **OVR 브리지 (`MoodPicker.Ritual.Ovr`, `MoodPicker.Hands.Ovr`)**:
  - `Assets/Scripts/Ritual/Ovr/RitualOvrBridge.cs`
  - `Assets/Scripts/Hands/Ovr/{OvrHandInput,OvrMicrogestureInput,OvrPerfMetrics}.cs`
- **셰이더 / 씬 / 에셋**:
  - `Assets/Shaders/Ritual/{RitualGlassSplat,RitualWaxSplat,RitualGlowSplat}.shader`
  - `Assets/Shaders/Splatter/LetterSplatURP.shader`
  - `Assets/Editor/{TimeRitualSceneSetup,TimeRitualPreviewRender,MoodPickerBuild}.cs`
  - `Assets/Scenes/TimeRitual.unity`, `Assets/Ritual/TimeRitualConfig.asset`, `Assets/Ritual/Category_*.asset`
- **테스트 & 게이트 (`Assets/Tests/EditMode/`)**:
  - `RitualCoreTests.cs`, `RitualGestureTests.cs`, `RitualRuntimeTests.cs`, `TimeUnitsTests.cs`
  - `BuildGateProjectTests.cs` (주석 금지 게이트 포함), `HandInputTests.cs` (`OVR*` 격리 게이트), `ShaderPowBaseTests.cs` (`pow` 음수 방지 게이트)

---

## 4. (대체됨 2026-11-15 → 명세 6단계 「선반」) 기억보관대 착수 가이드 — 참고용
원천 명세(`ritual-guide-1112.md`)에서 다음 단계로 보류되어 있던 **"선반, 일주일 보기, 날짜별 저장(기억보관대)"**을 이제 시작한다.

### 착수 전 사용자와 확인할/결정할 사항 (필요 시 `ask_question` 또는 간단 확인)
1. **공간 배치 & 형태**: 책상 위/뒤편 공중 선반(고정 오프셋 vs MRUK/Spatial Anchor 벽·책상 배치), 1주일(7슬롯) vs N일 가로 레일/다단 선반, 스플랫/유리/목재 톤 중 어떤 물성으로 보관대를 그릴지.
2. **상호작용 흐름**:
   - 봉인 완료된 병을 한 손/양손 핀치(`GripJar`)로 집어 보관대 슬롯에 갖다 대면 스냅·안착하고, 놓은 뒤 "새 병 시작 / 오늘 다시 만들기" 버튼·동작을 어떻게 둘지(자동 새 병 생성 vs 보관대 빈 슬롯/버튼 트리거).
   - 보관대에 꽂힌 과거 병을 다시 집어 눈앞으로 가져와 `BottleLabel`을 읽고 양손 3D 회전(`StepJarPose`)으로 들여다본 뒤 제자리에 돌려놓기.
   - "나중에 채운 날의 인장 문양 변형"(명세에 언급됨)을 이번 기억보관대에 포함할지 여부.

### 구현 시 기술 핵심 & 주의점
1. **데이터 영속 저장 (JSON in `Application.persistentDataPath`)**:
   - 현재 `RitualJar`는 런타임 메모리(`_layers`)만 가진다. `SavedDayRecord` (`id`, `dateIso`, `sealedAtUtc`, `dominantCategoryId`, `layers`: `{ categoryId, units }[]`, `shelfSlotIndex`, `emblemVariant`)를 JSON으로 저장/로드한다.
   - `RitualJar`에 비행/붓기 애니메이션 없이 즉시 층·상단 캡(`_cap`)·하단 바닥(`_floor`)·밀랍 봉인(`RitualWax` sealed 상태)·곡면 라벨(`BottleLabel`)을 복원하는 `LoadSealed(...)` 경로를 추가하면 재구성이 깔끔하다.
2. **Quest 3S 72fps 성능 / 알갱이 예산 (중요)**:
   - 현재 활성 병 1개는 최대 모래 `20,000` + 캡/바닥 `2,200×2` + 유리 `5,600` + 밀랍/커버 `~10,600` 스플랫을 쓴다.
   - 보관대에 5~7개 병을 동시에 띄울 때 풀 해상도 그대로 두면 픽셀 오버드로우로 72fps가 깨진다.
   - `RitualJar.SetQuality(float)`는 이미 알갱이 크기 보정(`grainScale = 1 / sqrt(quality)`)과 `SetDrawCount`를 지원하므로, **보관대에 거치된 비활성 병은 저밀도 모드(예: 모래/캡 품질 `0.25~0.35`, 유리 경량, 3D 오라 `RitualDeskGlow` 비활성)**로 유지하고, 손으로 집어 눈앞으로 꺼낸 병만 풀 디테일/오라로 전환하는 구조를 권장한다.
3. **기존 봉인 회전/오뚝이 (`StepJarPose`)와의 공존**:
   - 보관대 슬롯 근처(`InShelfSlotZone`)에 내려놓을 때는 오뚝이 복귀 후 슬롯 위치로 스냅·고정하고, 슬롯 밖 작업대 영역에서 놓을 때는 현재처럼 그 자리 높이/위치에 서도록 분기한다.

---

## 5. 빌드 · 테스트 · 기기 규율 (반드시 준수)
- **프로젝트 게이트 (하나라도 어기면 EditMode 실패)**:
  1. `Assets/Scripts/` 하위 모든 `.cs` 파일에 `//` 및 `/* */` 주석 **0개** (`BuildGateProjectTests`).
  2. `Assets/Scripts/Ritual/` 내 `Ovr/` 폴더 밖에 `OVR*` 토큰 금지 (`HandInputTests`).
  3. 모든 `.shader`/`.hlsl`의 `pow(base, exp)` 밑을 `saturate`/`max`/`abs`로 클램프 (`ShaderPowBaseTests`).
- **Unity 배치 실행 규칙 (`-quit` 금지, `-buildTarget Android` 필수)**:
  - **EditMode 테스트**:
    `/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath /Users/rafaela/01_PROJECT_NOW/my-vr-colorpicker/my-mr-studio/unity/mood-picker -runTests -testPlatform EditMode -testResults Logs/editmode-results.xml -logFile Logs/editmode.log`
    *(참고: `-nographics` 실행 시 GPU가 필요한 기존 `ChipRuntimeTests` 1건·`OrionLookTests` 6건만 실패하며, Ritual/Gate/HandInput/ShaderPow 전체는 정상 통과해야 함.)*
  - **씬 재생성**: `... -buildTarget Android -executeMethod TimeRitualSceneSetup.RunBatch -logFile Logs/scene.log`
  - **APK 빌드**: `... -buildTarget Android -executeMethod MoodPickerBuild.BuildTimeRitualApkBatch -logFile Logs/build-apk.log` → `Builds/time-ritual-dev.apk`
  - **Quest 3S (`340YC10G9707W0`)**: `adb` 경로 `/Applications/Unity/Hub/Editor/6000.3.27f2/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb`, 패키지 `com.DefaultCompany.timeritual`. 라이브 튜닝/재시작: `sh scripts/ritual.sh ...`
- **직전 검증 기록 (2026-11-15 00:30~00:33 KST)**:
  - EditMode: 396 중 389 통과 (`RitualCoreTests`, `RitualGestureTests`, `RitualRuntimeTests`, `TimeUnitsTests`, `BuildGateProjectTests`, `HandInputTests`, `ShaderPowBaseTests` **전부 통과**; 실패 7개는 `-nographics` GPU 미지원 기존 Dab 테스트).
  - APK 빌드: `BuildTimeRitualApkBatch` `Succeeded errors=0` (`Builds/time-ritual-dev.apk`, 114MB). Quest 3S 설치·실행 완료(PID `23451`, 기울임 뚜껑·바닥 마감 수정본 실기 확인 대기).
- **작업 트리 분리 주의사항**:
  - `unity/mood-picker` 워킹 트리의 Patisserie 5개 파일(`PatisserieSceneSetup.cs`, `PatisserieLab.unity`, `IcingCanvasController.cs`, `PipingBagInteractor.cs`, `SplatStrokeChunk.cs`)은 11-12 작업분이며, 대용량 `evidence/` 디렉토리는 Git에서 제외되어 있으므로 커밋 시 섞지 않는다.
  - 바깥 작업대 레포 `my-mr-studio/`는 다수 앱 변경분과 함께 미커밋 상태이며(`studio/apps/time-ritual/` 보드 메모는 최신 반영 완료).

---

## 6. 다음 세션 첫 발화 추천
`HANDOFF 0절 확인. 「Time Ritual 구현 명세 (Claude Code용).md」 5단계 시계부터 이어 갑니다(먼저 봉인 직후 정지 계측). 단계 끝에 멈추고 보고 형식으로 보고합니다.`
