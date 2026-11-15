# LESSONS
<!-- 한 항목 = 증상 / 원인 / 확인법 / 적용 범위. 두 프로젝트 이상에서 같은 교훈이 나오면 제목 끝에 `promote?` — 전역 승격은 사람이 정한다. -->

## <짧은 제목> (<YYYY-MM-DD>, <card-id>)
- 증상:
- 원인:
- 확인법: (다음에 1분 안에 확인하는 방법 — 명령이나 볼 로그)
- 적용 범위: 이 프로젝트 | Unity 전체 | WebXR 전체 | 모든 MR

## mr 도구가 에디터 닫힘을 "열림"으로 오탐 (2026-11-10, build-9c56)
- 증상: `mr preflight --unity` 가 "Unity 에디터가 이 프로젝트를 열고 있음"이라며 막는데 Unity 프로세스도 `Temp/UnityLockfile` 도 없음
- 원인: `mr` 가 `pgrep -fl Unity` 결과에서 `Unity.app` 과 프로젝트 폴더 이름이 같이 든 줄을 에디터로 본다. Claude Code 의 Bash 호출을 감싼 셸 프로세스 줄이 그 조건에 걸림
- 확인법: `pgrep -fl Unity | grep -v "Unity Hub"` 와 `ls Temp/UnityLockfile` 로 실제 에디터 유무 확인. 없으면 `mr` 를 건너뛰고 Unity 배치 명령을 직접 실행
- 적용 범위: 모든 MR (mr 도구)

## 배치 빌드 메서드가 한 프레임 뒤 시작하는데 `-quit` 를 붙이면 아무것도 안 함 (2026-11-10, build-9c56)
- 증상: Unity 가 "Batchmode quit successfully" 로 정상 종료하고 exit 0 인데 APK 와 프리뷰 PNG 시각이 그대로
- 원인: `MoodPickerBuild.BuildSplatterApkBatch` 가 `EditorApplication.update` 로 작업을 미루고 끝나면 스스로 `Exit` 한다. `-quit` 는 메서드가 반환되자마자 종료시켜서 작업이 시작되기 전에 끝남. 또 `mr build-unity` 는 이 프로젝트에 없는 `MRKit.Build.Android` 를 부름
- 확인법: 빌드 뒤 `ls -la Builds/*.apk` 시각과 로그의 `[MP] splatter scene setup done` 줄 수 (정상이면 2번)
- 적용 범위: 이 프로젝트 (미루는 배치 메서드 전반)

## IL2CPP 재빌드는 디스크가 수 GB 필요 (2026-11-10, build-9c56)
- 증상: 빌드가 `BuildDevApk` 에서 실패, 로그에 `No space left on device` / `IOException: Disk full ... Library/Bee`
- 원인: 셰이더·C# 를 바꾼 뒤 재빌드가 `Library/Bee` 에 많이 씀 (이때 8.2GB). 여유 12GB 라는 말을 믿고 시작했다가 가득 참
- 확인법: 빌드 전 `df -h /` 여유가 20GB 안팎인지, 빌드 뒤 `grep -n "No space\|Disk full" <build.log>`
- 적용 범위: Unity 전체

## Immersive Debugger 를 켜면 오버레이 셰이더가 빌드에서 빠져 매 프레임 예외 (2026-11-10, build-4a39)
- 증상: 기기 logcat 에 `ArgumentNullException: Parameter name: shader ... OVROverlayCanvas.InitializeRenderTexture` 와 `NullReferenceException ... OVROverlayCanvas.LateUpdate` 가 프레임마다 찍히고 패널이 안 뜸. 에디터에서는 안 드러남
- 원인: `Assets/Resources/OVROverlayCanvasSettings.asset` 이 없으면 기기에서 기본 인스턴스가 `Shader.Find("URP/UI/Prerendered")` 를 하는데, 아무도 참조하지 않아 빌드에서 빠져 있음. 설정 에셋은 에디터에서 `OVROverlayCanvasSettings.Instance` 를 한 번 읽어야 생김 (배치 빌드만 돌리면 안 생김)
- 확인법: `ls Assets/Resources/OVROverlayCanvasSettings.asset` 와 그 안의 `_transparentImposterShader` guid, 기기에서 `adb logcat -d -s Unity | grep -c Exception`
- 적용 범위: Unity 전체 (Meta XR Core + Immersive Debugger 또는 OVROverlayCanvas 를 배치 빌드로 쓸 때) promote?


## 뼈대 스프링을 k-최근접 한쪽 방향으로 이으면 놓은 뒤 발산 (2026-11-13, build-627f)
- 증상: 젤리를 눌렀다 놓으면 잦아들지 않고 뼈대 속도가 0.3→1.3m/s 로 커지며 모양이 터짐
- 원인: A 가 B 를 이웃으로 보지만 B 는 A 를 안 보는 비대칭 연결 → 힘이 짝을 이루지 않아 에너지가 새어 들어옴
- 확인법: `SplatSimSetup.BuildField` 의 `Edges` 가 (a<b) 중복 없는 간선 목록인지, 시뮬 테스트 `PuddingTopSways…` 의 마지막 `Activity < 1e-6`
- 적용 범위: Unity 전체 (스프링·질점 시뮬레이션)

## Unity Android 배치 빌드가 adb 서버를 죽여 logcat 스트림이 끊김 (2026-11-13, build-627f)
- 증상: 백그라운드 `adb logcat > file` 이 exit 255 로 끝나고, 다음 설치 때 "daemon not running; starting now"
- 원인: 빌드가 Android SDK 의 adb 를 재시작함
- 확인법: 설치·실행 뒤 스트림을 다시 켜고 3초 뒤 파일에 `[MP] build=` 줄이 있는지
- 적용 범위: Unity 전체 (Quest 실기 라운드)

## `adb logcat -d` 링버퍼는 몇 분이면 밀려 측정 구간이 사라짐 (2026-11-12, build-627f)
- 증상: 끝나고 덤프했더니 젤리 구간 fps 가 하나도 없음
- 원인: VrApi 가 1초마다 길게 찍어 버퍼가 금방 참
- 확인법: 측정하는 라운드는 실행 직후부터 `adb logcat -v time -s VrApi:I Unity:I > evidence/.../logcat.txt` 로 파일 스트리밍
- 적용 범위: 모든 MR

## 프리셋 값을 바꿔도 이미 만든 물성 에셋은 안 바뀜 (2026-11-13, build-627f)
- 증상: `SplatFeel.Slime` 에 `squish = 1` 을 넣었는데 기기에서 슬라임이 옛 방식으로 움직임
- 원인: `SplatterSceneSetup.EnsureFeel` 은 에셋이 있으면 그대로 돌려줌 (덮어쓰지 않음)
- 확인법: `grep -A11 "feel:" Assets/Feels/<이름>.asset` 값이 프리셋과 같은지. 다르면 에셋을 직접 고침
- 적용 범위: 이 프로젝트

## 1.2cm 넘게 밀린 점은 셰이더가 물방울로 그려 "입자가 보임" (2026-11-13, build-627f)
- 증상: 크게 변형되는 물성(슬라임)이 표면이 아니라 낱알 점으로 보임
- 원인: `LetterSplatURP` 의 detach = |변위| / `_DropParams.x`(기본 0.012) → 빌보드·물방울 모양으로 바뀜
- 확인법: `SplatFeelMap.Look(feel).detach` 값. 표면째 움직이는 물성은 0.5 로 둠
- 적용 범위: 이 프로젝트

## 스플랫을 키우면 겹침으로 GPU 가 먼저 무너짐 (2026-11-13, build-627f)
- 증상: 슬라임 손댈 때 26~42fps, 영상에서 붓자국 같은 큰 조각
- 원인: 늘어난 곳 스플랫 2.5배 + 속도 늘림 4배 → 화면 면적 수 배, 같은 픽셀을 여러 번 그림
- 확인법: VrApi `GPU%` 와 FPS 를 물성별로 나눠 보기 (evidence/feels 의 집계 스크립트 방식). 크기 상한 1.3배로 고친 뒤 중앙 72
- 적용 범위: Unity 전체 (가우시안 스플랫)

## 기능을 빼도 통과하는 테스트 (2026-11-13, build-627f)
- 증상: "빠른 찌름이 느린 누름보다 세게 민다" 테스트가 전분의 굳기를 꺼도 통과
- 원인: 빠른 손의 끌림(drag)만으로 차이가 났음. 비교 대상이 틀렸음
- 확인법: 핵심 줄을 잠시 바꿔(`Firmness` 를 1 로) 테스트가 실패하는지 본 뒤 되돌림. 굳기 없는 같은 물성과 비교하도록 고침
- 적용 범위: 모든 MR

## VrApi GPU% 는 현재 클럭 기준이고 "쉼" 구간은 사람이 벗은 시간과 섞임 (2026-11-13, build-16f1)
- 증상: 잠든 상태 GPU 0.26 을 기준으로 잡았다가 비교가 틀림
- 원인: 사람이 헤드셋을 벗거나 형태를 안 보는 시간이 섞임. GPU% 는 클럭 단계(`CPU4/GPU=x/y`)와 함께 봐야 함
- 확인법: 같은 라운드 안에서 "보는 중·손 닿음" 구간끼리 비교하고 클럭 단계도 같이 적기
- 적용 범위: 모든 MR (Quest)

## 포비에이티드가 안 들었다고 "정점 쪽"이라 단정하면 틀림 (2026-11-13, build-16f1)
- 증상: 고정 포비에이티드 High 를 켜도 GPU 사용률이 그대로라 정점(스플랫 수) 병목으로 봤는데, Perfetto 카운터는 셰이더 시간 픽셀 84%·정점 16%, 픽셀당 ALU 193
- 원인: 형태가 시야 가운데 있고 포비에이티드는 가장자리 해상도만 낮춘다. 가운데 물체의 픽셀 비용은 그대로
- 확인법: 짐작 말고 Perfetto `mode vr` 10초 → 카운터 `% Time Shading Fragments` / `% Time Shading Vertices`, `ALU / Fragment`. 앱 GPU 시간은 track `AppInfo` 의 `debug.gpuTime`
- 적용 범위: 모든 MR (Quest, 화면 가운데에 무거운 물체가 있는 앱)

## Quest 가 앱을 반 속도(36fps)로 돌림 — 앱 탓이 아니라 기기 상태 (2026-11-14, build-4bf1)
- 증상: VrApi `FPS=36/72`, `VSnc=2`, Stale 0, GPU% 0.2~0.4, App 4ms. 어제 72fps 였던 같은 APK 도 36fps
- 원인: 미확인. 하루 넘게 켜 둔 기기. 재부팅하자 `VSnc=1`·72fps
- 확인법: fps 가 낮으면 먼저 `grep -o "VSnc=[0-9]*"` 와 GPU%. VSnc=2 인데 GPU 가 한가하면 코드를 보기 전에 재부팅
- 적용 범위: Quest 실기 전반 promote?

## 프레임 간격만 보는 품질 거버너는 시스템 상한·로딩 버벅임을 "무겁다"로 읽음 (2026-11-14, build-4bf1)
- 증상: 36fps 상한에서 7초 만에 입자 품질 최저(0.5), 앱 시작 첫 프레임 18ms 에도 0.9 로 내림
- 원인: `Time.unscaledDeltaTime` = 일 + 기다림. 입자 수는 일하는 시간만 바꿈
- 확인법: `RitualGovernor.Judge` — fps 와 앱 GPU/CPU 시간이 둘 다 나쁠 때만 줄임, 시작 3초 무시. 기기 로그 `ritual perf frame= gpu= cpu=`
- 참고: `OVRPlugin.GetPerfMetricsFloat(App_GpuTime_Float)` 원값은 초 단위(0.00497). VrApi `App=` 와 같은 크기. `OVRPlugin` 이름은 `HandInputTests` 게이트가 `Assets/Scripts/Hands/Ovr/` 밖에서 막으므로 `OvrPerfMetrics` 를 거쳐 읽는다
- 적용 범위: 이 프로젝트 (입자 거버너가 있는 장면) promote?

## 설치 직후 실행이 가디언·OS 창 때문에 대기, 안 쓴 채 실행하면 자동 배치가 엉뚱한 곳 (2026-11-14, build-4bf1)
- 증상: `am start` 뒤 프로세스 없음, logcat `Launch is blocked because: a Guardian dialog is currently showing. Caching launch`. 또는 헤드셋을 책상에 둔 채 실행 → `ritual placed at (-0.77, -0.55, …)`
- 원인: Horizon OS 가 창이 닫힐 때까지 실행을 미룸. `autoPlace` 는 첫 30프레임의 머리 위치로 한 번만 놓음
- 확인법: 사람이 "준비"(착용) 한 뒤 `sh scripts/ritual.sh run`. 착용 전에 실행했으면 다시 실행. 단, 사람이 이미 쓰는 중이면 재시작이 그 판을 지움 → 재시작 전에 logcat 에서 `sealed=` 확인
- 적용 범위: Unity Quest 실기 라운드 promote?

## 패스스루를 "원래 밝기의 N%"로 어둡게 하려면 밝기 오프셋이 아니라 textureOpacity (2026-11-15, build-c5b8)
- 증상: `SetBrightnessContrastSaturation(brightness)` 는 오프셋이라 −0.36 을 줘도 "조금 어두운" 정도이고, 더 내리면 어두운 곳부터 뭉개져 "30~40%" 같은 배율 목표를 못 맞춘다
- 원인: brightness 는 휘도에 더하는 값. 배율로 어둡히는 건 `OVRPassthroughLayer.textureOpacity` (Meta 문서: "Use this to dim passthrough", 0~1)
- 확인법: `RitualOvrBridge.LateUpdate` 에서 `textureOpacity = ritual.PassthroughLight`. 실기 0.38 → 사람 "딱 좋았다" (손·책상 보이고 병 윤곽 또렷)
- 적용 범위: 모든 MR (Unity Meta XR Core 207)

## `mr logcat` 이 `-T '11-15'` 로 실패, 커밋은 보드에서 done 인 카드 id 가 첫 줄이어야 (2026-11-15, build-c5b8)
- 증상: `mr logcat <pkg>` 가 "-T '11-15' not in time format" 으로 끝남. `git commit` 이 "카드 "…" 가 어느 보드에도 없어요" 로 거절
- 원인: `mr launch` 가 실행 시각을 날짜만 기록. 커밋 훅(`my-mr-studio/scripts/githooks/rules.mjs`)은 첫 줄 `card-id:` 의 카드가 보드에 있고 status done 이어야 통과
- 확인법: logcat 은 `adb logcat -d -v time -s Unity:I VrApi:I > evidence/<card>/<buildId>/logcat.txt` 로 직접. 커밋 전 `npm run studio -- show <id> --app time-ritual` 에서 상태 done 확인 (보드에 없으면 add → sub → done 으로 카드부터)
- 적용 범위: 모든 MR (mr 도구 · 작업대 커밋 훅)
