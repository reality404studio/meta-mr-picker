# ⬜ Unity 셋업 + First Light

[← 보드로](../BOARD.md)

- **목표:** Quest 3에서 패스스루, 손, 카메라 텍스처가 한 화면에 보인다
- **산출물:** unity/mood-picker/ 프로젝트, First Light 스크린샷 (setup 파일 칸)

## 하위 과업

- [ ] ▶ **가드레일 읽고 이번 셋업 체크리스트 뽑기** · 에이전트 · 분석
  <br><sub>show unity-guardrails --app shared. 정해진 Unity 버전 기준으로 §1~§4를 체크리스트로 setup 메모에 덧붙인다</sub>
- [ ] **Unity 프로젝트 생성 + Meta SDK·MRUK 임포트 + XR 플러그인** · 사람 · 구현
  <br><sub>unity/mood-picker/ 는 이미 git 레포(reality404studio/meta-mr-picker)라 비어 있지 않음 → Hub에서 URP 프로젝트를 unity/_new/mood-picker 에 만들고, 에이전트가 Assets·Packages·ProjectSettings 를 unity/mood-picker/ 로 옮긴다. Android 전환, ASTC. XR 플러그인은 OpenXR 하나 (가드레일 §2). Project Setup Tool Fix All</sub>
- [ ] **설정 파일 점검** · 에이전트 · 검증
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
