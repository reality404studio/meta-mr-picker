# 👀 Unity 셋업 + First Light

[← 보드로](../BOARD.md)

- **목표:** Quest 3에서 패스스루, 손, 카메라 텍스처가 한 화면에 보인다
- **산출물:** unity/mood-picker/ 프로젝트, First Light 스크린샷 (setup 파일 칸)

## 하위 과업

- [x] **가드레일 읽고 이번 셋업 체크리스트 뽑기** · 에이전트 · 분석
  <br><sub>show unity-guardrails --app shared. 정해진 Unity 버전 기준으로 §1~§4를 체크리스트로 setup 메모에 덧붙인다</sub>
- [x] **Unity 프로젝트 생성 + Meta SDK·MRUK 임포트 + XR 플러그인** · 사람 · 구현
  <br><sub>unity/mood-picker/ 는 이미 git 레포(reality404studio/meta-mr-picker)라 비어 있지 않음 → Hub에서 URP 프로젝트를 unity/_new/mood-picker 에 만들고, 에이전트가 Assets·Packages·ProjectSettings 를 unity/mood-picker/ 로 옮긴다. Android 전환, ASTC. XR 플러그인은 OpenXR 하나 (가드레일 §2). Project Setup Tool Fix All</sub>
- [ ] ▶ **설정 파일 점검** · 에이전트 · 검증 👀 확인 필요
  <br><sub>가드레일 §1~§3 의 [M] 항목: ProjectSettings, packages-lock, XR 로더 asset, AndroidManifest 권한. 결과를 setup 메모에</sub>
- [ ] **씬: Camera Rig + Passthrough + 손 추적 + 카메라 접근 켜기** · 사람 · 구현
  <br><sub>가드레일 §3. OVRManager 의 Passthrough Support / Enable Passthrough / Passthrough Camera Access, Skybox None</sub>
- [ ] **빌드 게이트 + 빌드 ID (unity/shared/BuildGate/)** · 에이전트 · 구현
  <br><sub>IPreprocessBuildWithReport. 실패: XR 로더 1개, HEADSET_CAMERA 권한, Skybox None, 카메라 1대, 외곽선 _Cull 1, 금지 API(Shader.Find·ReadPixels·WebCamTexture·OVRInput). 경고만: Development Build 꺼짐. 빌드 ID(git 짧은 해시+시각)를 박고 앱 첫 로그에 [MP] build=… 출력</sub>
- [ ] **사전 점검 스크립트 (에이전트용)** · 에이전트 · 구현
  <br><sub>게이트와 같은 파일 검사 + Roslyn 컴파일 체크. 사람에게 빌드를 부탁하기 전에 매번 돌리고 결과를 note 로</sub>
- [ ] **First Light 스크립트: 정면 Unlit 큐브 + 카메라 텍스처 쿼드 + 손끝 점** · 에이전트 · 구현
  <br><sub>가드레일 §4·§5. SerializeField 머티리얼, PassthroughCameraAccess 1280×960 요청 후 CurrentResolution 로그, 권한 요청은 한 곳</sub>
- [ ] **실기 First Light: 방 + 가상 + 카메라 텍스처 + 손이 한 화면에** · 사람 · 검증
  <br><sub>Development Build 설치. 에이전트가 logcat·screencap 으로 같이 확인 (가드레일 §6) · 증거: 빌드 ID가 찍힌 logcat 발췌 + 스크린샷을 첨부해야 done</sub>
- [ ] **전철 실기: Travel Mode 에서 패스스루 · 손 · 카메라 · 공중에 둔 물체** · 사람 · 검증
  <br><sub>First Light 빌드를 전철에 들고 가서. 설정 > 실험 기능 > Travel Mode (기차 지원, v71+). 공중에 띄운 물체가 차량 가감속에 미끄러지는지, 손 추적이 흔들림에 버티는지, PCA 가 도는지. 컨셉 전체가 여기에 걸려 있어 최대한 일찍</sub>

## 메모

**시작 전에 반드시:** `npm run studio -- show unity-guardrails --app shared` (공통의 "Unity 실기 가드레일").
특히 §2 XR 플러그인은 과거 프로젝트 메모와 정반대가 됐으니 그 표를 따른다.

- 위치: `unity/mood-picker/`
- 공통 "Unity 버전" 결정이 나야 시작 (PCA 샘플 기준 최소 6000.0.38f1, 최신 Meta SDK는 6000.0.66f2+)
- **First Light 통과 기준** (기기 스크린샷 한 장에 모두):
  1. 방(현실)이 보인다 — 검은 배경·스카이박스가 아님
  2. 정면 약 60cm 에 Unlit 큐브
  3. PCA 카메라 텍스처를 붙인 작은 쿼드 (`CurrentResolution` 이 logcat 에 찍힘)
  4. 손끝 점이 손을 따라감 (컨트롤러 꺼진 상태)
- 공식 PCA 샘플(CameraViewer, CameraToWorld)은 참고만. 컨트롤러 A/B 입력은 가져오지 않는다

## 가드레일에서 뽑은 이번 체크리스트

_setup-guardrails 하위 과업에서 에이전트가 덧붙인다._

---
**Claude Code** · 2026-11-03 13:56

### 이번 셋업 체크리스트 (2026-11-03, Unity 6000.3.27f2 / Apple Silicon 기준, setup-guardrails)

설치 상태: Unity 6000.3.27f2 + Android(SDK 34~37·NDK·OpenJDK) 설치됨. 2022.3 에디터는 삭제. 이 버전이면 가드레일 §2 표의 "Unity 6 → **OpenXR**" 행.

**setup-project (프로젝트·패키지)**
- [ ] 6000.3.27f2 로 URP 프로젝트 → `unity/mood-picker/` 로 옮기기 (레포라 비어 있지 않음)
- [ ] Android Switch Platform, Texture Compression **ASTC**
- [ ] IL2CPP + **ARM64 만**
- [ ] 패키지: `com.meta.xr.sdk.core` + `com.meta.xr.mrutilitykit` (PCA 가 MRUK 안). 최신으로. Operator 쓰려면 Core **v207+**
- [ ] XR Plug-in Management Android: **OpenXR 하나만** (Meta Quest feature group). Oculus 패키지가 깔려 있으면 Remove
- [ ] Operator 쓸 거면 OpenXR Plugin **1.17.0+** (Meta 자동 셋업 스크립트는 1.16.1 로 고정 → 셋업 뒤 올리기)
- [ ] Project Setup Tool → 빨강·노랑 0 될 때까지 Fix All (DUC·App ID 는 Mark as Fixed)

**setup-files (에이전트가 파일로 확인)**
- [ ] `ProjectSettings.asset`: `scriptingBackend Android: 1`, `AndroidTargetArchitectures: 2`
- [ ] `manifest.json` 에 URP, `packages-lock.json` 에 core·MRUK
- [ ] `Assets/XR/XRGeneralSettingsPerBuildTarget.asset` 의 Android 로더 **정확히 1개 (OpenXR)**
- [ ] AndroidManifest 에 `horizonos.permission.HEADSET_CAMERA` (Update AndroidManifest 하면 빠짐 → 다시 넣기)

**setup-scene (패스스루 + PCA)**
- [ ] OVRCameraRig 하나, 기본 Main Camera 삭제
- [ ] OVRManager: Passthrough Support = Required, Enable Passthrough, **Enable Passthrough Camera Access**
- [ ] OVRPassthroughLayer 하나, Skybox = None, CenterEye `SolidColor` + `Color.clear`(알파 0), Post Processing 끔
- [ ] `PassthroughCameraAccess` 컴포넌트, 1280×960 요청 + `CurrentResolution` 로그. 권한 요청은 한 곳만
- [ ] 손 추적 켬, 컨트롤러 입력 코드 없음

**First Light 스크립트·빌드 (gate / preflight / firstlight)**
- [ ] 머티리얼은 SerializeField `.mat` (Shader.Find 금지), 큐브 **Unlit**, 정면 60cm (yaw-only, `yield return null` 뒤)
- [ ] Development Build, `[MP] build=<해시+시각>` 첫 로그, 로더·패스스루·clearFlags·알파·PCA 해상도·손 상태 로그
- [ ] 실기 확인: force-stop → logcat -c → am start → `[MP]` grep → 스크린샷 (방+큐브+카메라 쿼드+손끝 점)

**Mac 에서 안 되는 것:** Link 패스스루 불가, XR Simulator 에서 PCA 안 됨 → 패스스루·PCA·손·72fps 는 APK 실기에서만.

**새 도구 (2026-11-03 조사):** Meta XR Unity MCP Extension(Unity 6000.0.66f2+, SDK v78+, Unity MCP 패키지 위에), Meta XR Operator(Core v207+, OpenXR 1.17+, Mac 은 XR Simulator, 실험). setup-project 는 metavr `metavr_unity_setup`(apply)으로 에이전트가 대신 돌릴 수 있음 — 사람 하위 과업이라 사람 승인 후.
