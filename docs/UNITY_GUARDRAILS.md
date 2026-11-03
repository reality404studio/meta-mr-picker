# Unity 실기 가드레일 — 패스스루 안 보임 방지

> **에이전트에게:** Unity 작업에 들어가기 전에 끝까지 읽는다. 실기 루프를 돌 때마다 §6, 막히면 §7 증상표부터.
> 출처는 과거 실기에서 빌드를 태워 얻은 함정(08.MYGOLEM First Light 체크리스트, 06.Malrangee, MR_LABS 메모리)과
> Meta 공식 문서(2026-11-03 metavr `vr_docs_search` 로 대조)다. 일반론은 뺐다.
> SDK가 빨리 바뀐다. 여기 적힌 메뉴·버전이 화면과 다르면 **기억 말고 `vr_docs_search` 로 다시 확인**하고, 맨 아래 "실기에서 바뀐 것"에 덧붙인다.

---

## 0. 원칙 네 줄

1. **에디터는 거짓말한다.** 에디터는 패스스루를 그리지 않고, 카메라가 y=0(Quest는 바닥 기준)이고, 폴백 폰트·셰이더로 멀쩡해 보인다.
   → "된다"는 **기기 스크린샷에 방(현실)과 가상이 함께 보일 때만** 보고한다. 에디터 화면, 시뮬레이터는 근거가 아니다.
2. **Mac에서는 Link가 없다.** Passthrough over Link 불가. Meta XR Simulator에서는 **카메라 접근(PCA)이 안 돈다.**
   → 패스스루·PCA·손끝 정확도·72fps는 전부 **APK 실기**에서만 확인된다. 실기 루프는 하루 몇 번으로 묶는다.
3. **관찰은 MQDH 캐스팅이 기본.** 녹화는 사람이 요청했거나 시간 흐름이 필요한 버그일 때만 짧게.
4. **실기 시연 전의 수치·감각 결정은 전부 베이스라인이다.** (사람 결정 2026-11-03)
   선 굵기, 글자 크기, 거리·배치, 타이밍(기록 시간 등), 움직임, 소리, 손 판정 임계값처럼 헤드셋에서 봐야 아는 것은
   초안 값으로 **바로 구현하고 진행한다.** 사람에게 미리 확정을 묻지 않는다. 대신:
   - 값은 한곳(설정 에셋·상수 파일)에 모아 **실기에서 바로 바꿀 수 있게** 둔다. 코드 여기저기 하드코딩 금지
   - 문서·메모에는 `베이스라인` 으로 표시하고, 실기 확인 뒤 사람이 정한 값만 `확정` 으로 바꾼다
   - 확정은 실기 검증 하위 과업에서 사람이 한다 (빌드 ID 찍힌 증거와 함께)

---

## 1. 프로젝트 빌드 설정

- [H] Build Settings → **Android → Switch Platform**, **Texture Compression = ASTC**
- [M] `ProjectSettings/ProjectSettings.asset`: IL2CPP(`scriptingBackend Android: 1`), **ARM64**(`AndroidTargetArchitectures: 2`), 64비트 확인
- [M] `Packages/manifest.json` 에 URP
- [M] `packages-lock.json` 에 `com.meta.xr.sdk.core`, `com.meta.xr.mrutilitykit` (PCA 컴포넌트가 MRUK 안에 있다)

## 2. XR 플러그인 — 딱 하나만 ⚠️ 과거 메모와 반대가 됐다

| Unity | Meta SDK | XR Plug-in Management (Android 탭) |
| --- | --- | --- |
| **Unity 6 (6000.0.66f2+, 6.1+ 권장)** | v74 이상 | **OpenXR** (Meta 권장. Oculus 플러그인은 deprecated) |
| 2022.3 | v73 이하 | Oculus |

- **2026-06 MYGOLEM 메모의 "OpenXR 켜지 마라"는 2022 + 구 SDK 기준이다. Unity 6 + 최신 SDK에서는 반대로 OpenXR이 정답.**
  공통 결정 "Unity 버전"이 Unity 6이면 OpenXR, 2022면 이 표를 다시 문서로 확인.
- **둘을 동시에 켜지 않는다** (이중 초기화). 바꿀 땐 Package Manager에서 안 쓰는 쪽을 Remove (체크 해제만으로는 안 지워짐).
- [M] 확인: `Assets/XR/XRGeneralSettingsPerBuildTarget.asset` 에 로더가 **하나** 박혀 있는지. 0개면 `Vulkan ... does not support XR` 빌드 실패.
- [H] Project Setup Tool(`Window > Meta > Tools > Project Setup Tool`) → **빨강/노랑 0이 될 때까지 Fix All 반복**.
  Platform SDK 관련(DUC, App ID)은 사이드로드면 Mark as Fixed.

## 3. 패스스루가 보이려면 — 전부 다 맞아야 한다

하나라도 빠지면 **검은 배경 / 스카이박스**가 나온다. 에디터에선 절대 안 드러난다.

- [ ] **OVRManager → Quest Features → General → Passthrough Support = Required** (또는 Supported)
- [ ] **OVRManager → Insight Passthrough → Enable Passthrough 체크** (스크립트로 늦게 켤 거면 `OVRManager.isInsightPassthroughEnabled`)
- [ ] 씬에 **OVRPassthroughLayer 하나** (Building Block "Passthrough"). 씬이 여러 개면 한 인스턴스를 `DontDestroyOnLoad` 로 유지
- [ ] **Lighting → Environment → Skybox Material = None**
- [ ] CenterEyeAnchor 카메라: `clearFlags = SolidColor`, `backgroundColor = Color.clear` (**알파 0**).
      Setup Tool의 "Clear background of OVRCameraRig" Fix가 해 주지만, 의심되면 Start에서 코드로 강제하고 로그
- [ ] **기본 Main Camera 지우기** (OVRCameraRig 와 카메라 두 대 = 불투명 카메라가 덮음)
- [ ] 화면 전체를 덮는 불투명 오브젝트·UI 배경 없음 (Canvas 배경 이미지 알파 1, 큰 큐브 안에 들어간 카메라)
- [ ] 카메라 **Post Processing 끔** (72fps 규칙상 어차피 금지, 알파를 1로 덮을 수 있음)
- [ ] 나중에 패스스루를 켜는 경우: 켜자마자 수백 ms 검게 깜빡인다 → `passthroughLayerResumed` 이벤트 뒤에 연출 시작

### 카메라 접근 (Passthrough Camera Access, 이 앱의 핵심)

- [ ] **패스스루가 켜져 있어야 PCA가 동작한다.** §3 위 항목이 먼저
- [ ] Camera Rig → **OVRManager → Enable Passthrough Camera Access** 체크 + 씬에 **`PassthroughCameraAccess` 컴포넌트**(MRUK).
      옛 `WebCamTexture` 경로는 쓰지 않는다 (마이그레이션됨)
- [ ] 권한 **`horizonos.permission.HEADSET_CAMERA`**. Unity 6에서 매니페스트를 다시 만들면(**Update AndroidManifest.xml**) 이 권한이 **빠진다 → 손으로 다시 넣기**
- [ ] 권한 요청은 **한 곳에서만.** 샘플의 `RequestPermissionsOnce` 와 OVRManager "Permission Requests On Startup", OVRPermissionsRequester 를 같이 쓰지 않는다
- [ ] 해상도: **지원 목록에 있는 값(1280×960)을 요청**하고, 실제 값은 `CurrentResolution` 을 읽어 쓴다. 이미지 크기 하드코딩 금지 (v209부터 폴백 해상도가 바뀜)
- [ ] `GetTexture()` 는 권한 승인 + 컴포넌트 활성 뒤에만 유효. 그 전엔 null → 첫 프레임 null 처리
- [ ] CPU로 읽을 땐 **AsyncGPUReadback** (ReadPixels 동기 금지). `Camera.main` 은 Awake에서 캐싱
- [ ] 공식 샘플은 **컨트롤러 A/B 버튼**을 쓴다. 가져다 쓴 코드에 컨트롤러 입력이 남으면 핸즈 퍼스트 요구사항 위반

## 4. 가상 오브젝트가 안 보이거나 까맣거나 마젠타일 때

- **MR 씬엔 Directional Light가 없을 때가 많다** → URP/Lit 은 새까맣다. 상태색을 `_BaseColor` 에만 두면 안 보인다.
  → **Unlit**(또는 이 앱의 셀 셰이더처럼 광원 없이 도는 셰이더)을 기본으로. Lit을 꼭 쓰면 emission에 싣기 (0.12는 거의 검정, ~0.7이 흰색으로 읽힘)
- **`Shader.Find` 금지**, 런타임 렌더 파이프라인 폴백(`GraphicsSettings.currentRenderPipeline`)도 기기에서 null.
  → `[SerializeField] Material` 로 `.mat` 에셋을 Inspector에 연결 ([M]이 .mat 생성, [H]가 슬롯에 드래그). 빠지면 마젠타
- URP 에서 색은 `.color` 가 아니라 `SetColor("_BaseColor")`. LineRenderer 의 startColor/endColor 도 안 먹는다
- **커스텀 셰이더는 0이 아닌 알파를 써야 한다.** 합성기가 알파 0 프래그먼트를 버린다 → 패스스루 위에서만 사라짐.
  `ColorMask RGB` 금지
- 반투명은 **색과 알파 블렌드를 따로**: `Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha`. 같이 쓰면 알파가 제곱돼 그 자리로 현실이 비친다
- **인버티드 헐 외곽선 = Render Face: Back (`_Cull: 1`)**. Front로 하면 오브젝트 전체가 검은 덩어리. 모델이 이 이중 반전을 자주 틀린다 → `.mat` 의 `_Cull` 값으로 검증
- 패스스루엔 **오클루전이 없다.** 손 근처 이펙트는 손 위로·퍼지게. 손끝 아래로 떨어지는 파티클은 손을 뚫고 보인다
- 첫 오브젝트는 **사용자 정면**에 둔다 (yaw-only forward, `yield return null` 뒤 배치). 바닥 중심은 스크린샷 시야 밖

## 5. 손 (컨트롤러 없음)

- **손바닥이 얼굴 쪽 + 엄지-검지 핀치 = Quest 시스템 메뉴.** 손바닥이 위를 향한 UI(팔레트 포탈)에서는 핀치를 쓰지 않는다
- 두 손이 맞닿으면 추적이 흔들린다 → 왼손 위 UI는 손바닥에서 5~10cm 띄운다
- 핀치 순간 손가락이 움직인다 → 위치 확정은 핀치가 아니라 드웰/평균
- 월드 좌표 변환은 `cameraRig.trackingSpace.TransformPoint()`. 월드/카메라/로컬 기준을 먼저 적고 짠다
- 빌보드: `Quaternion.LookRotation(obj - cam)` 방향 주의. `rotation = cam.rotation` 은 뒷면이 보이는 경우가 있다
- 디버그 중 셸이 "컨트롤러 필요" 창을 띄우면 컨트롤러 하나를 켜도 되지만, **최종 핸즈 온리 테스트는 컨트롤러 전원을 끄고** 한다

## 6. 실기 루프 — 헛빌드 방지

- [H] **Development Build 체크.** 안 켜면 `Debug.Log` 가 logcat 에 안 뜬다
- [M] CLI로 고친 스크립트는 Unity가 재컴파일 안 할 수 있다 → [H] Unity 포커스 후 **Cmd+R**, Console 에러 0
- [M] 빌드 전: `Library/ScriptAssemblies/Assembly-CSharp.dll` 에 새 **메서드 이름**이 있는지 `strings | grep`
- [M] 빌드 후: APK **mtime 이 방금**인지. 옛날이면 헛빌드
- [M] **빌드 ID가 정본 확인이다.** 빌드 게이트(`unity/shared/BuildGate/`)가 git 짧은 해시 + 시각을 박고, 앱 첫 로그에 `[MP] build=…` 를 찍는다.
  logcat 의 ID가 기대한 값과 다르면 화면이 어떻든 헛빌드. 게이트 검사가 빌드를 막으면 우회하지 말고 원인을 고친다
- [M] 사람에게 빌드를 부탁하기 전에 **사전 점검 스크립트**(게이트와 같은 검사 + Roslyn 컴파일)를 먼저 돌린다
- [M] 확인 순서 (metavr MCP `device_logcat` / `device_screenshot` 이나 adb):
  ```
  adb shell am force-stop <PKG>        # 필수. am start 만 하면 resume 이라 Start() 가 다시 안 돈다
  adb logcat -c                        # force-stop 뒤에 지운다
  adb shell am start -n <PKG>/com.unity3d.player.UnityPlayerActivity
  adb logcat -d -s Unity | grep '\[MP\]'
  adb exec-out screencap -p > shot.png  # 방 + 가상이 같이 보이는지 눈으로
  ```
- [H] **헤드셋을 쓴 채로.** 벗으면 앱이 멈추고 `device being doffed` 가 쏟아진다
- [M] 첫 실행 로그에 남길 것 (태그 `[MP]`): 로더 이름, 패스스루 활성, 카메라 clearFlags·배경 알파, PCA 권한·`CurrentResolution`, 손 추적 상태

## 7. 증상 → 원인 → 처치

| 증상 | 원인 | 처치 |
| --- | --- | --- |
| 현실 대신 **검은 배경** | 카메라 배경 알파 1 / Enable Passthrough 꺼짐 / Passthrough Support 없음 / Main Camera 중복 | §3 체크리스트 위에서부터 |
| 현실 대신 **스카이박스** | Skybox Material 남아 있음 | Lighting → Skybox None, clearFlags SolidColor |
| 패스스루 켜는 순간 검게 깜빡 | 레이어 초기화 지연 | `passthroughLayerResumed` 뒤에 시작 |
| 가상 물체가 패스스루 위에서만 사라짐 | 셰이더가 알파 0을 씀 / `ColorMask RGB` | 알파 쓰기, ColorMask 제거 |
| 반투명 자리로 현실이 비침 | 알파 블렌드 공유 | 색·알파 블렌드 분리 |
| 오브젝트 **마젠타** | `Shader.Find` / 파이프라인 폴백 실패 | SerializeField 머티리얼 |
| 오브젝트 **새까맘** | 광원 없음 + Lit / 외곽선 Render Face Front | Unlit, 외곽선 `_Cull: 1` |
| `Vulkan ... does not support XR` | XR 로더 0개 | §2 플러그인 하나 켜기 |
| PCA 텍스처 null | 권한 미승인 / 매니페스트에서 권한 빠짐 / 패스스루 꺼짐 | §3 카메라 접근 |
| 이미지 크기 어긋남 | 해상도 하드코딩 | `CurrentResolution` 사용 |
| logcat 에 Unity 로그 0줄 | 릴리즈 빌드 | Development Build |
| 기기에서 옛 동작 | 헛빌드 / 미컴파일 | DLL·APK mtime, Cmd+R |
| Start() 로그 안 뜸 | resume 만 됨 | `am force-stop` 후 실행 |
| 시스템 메뉴가 갑자기 열림 | 손바닥 얼굴 쪽 + 핀치 | 그 자세에서 핀치 쓰지 않기 |
| `Loading prefab room` (MRUK 쓸 때) | MRUK Data Source 기본값 폴백 | Data Source = Device |
| 에디터에선 되는데 기기에서 안 보임 | y=0 기준 차이, 패스스루 미렌더 | 실기에서만 판단 |

## 참고

- [Passthrough 시작하기](https://developers.meta.com/vr/documentation/unity/unity-passthrough-gs) · [Passthrough 문제 해결](https://developers.meta.com/vr/documentation/unity/unity-pt-troubleshooting)
- [XR 플러그인 (OpenXR 권장)](https://developers.meta.com/vr/documentation/unity/unity-xr-plugin) · [PCA Unity](https://developers.meta.com/vr/documentation/unity/unity-pca-documentation)
- 원본 체크리스트: `05.MR_LABS/PROJECTS/PRIVATE/08.MYGOLEM/setting/PASSTHROUGH_UNITY_BOOTSTRAP_CHECKLIST.md` (§2 XR 플러그인 부분은 위 표로 대체)

## 실기에서 바뀐 것

_실기에서 다르게 나온 항목을 날짜와 함께 덧붙인다._

---
**Claude Code** · 2026-11-03 14:11

실기에서 바뀐 것 (2026-11-03, Unity 6000.3.27f2 + Core 207.0.1, 파일로 확인): 생성된 AndroidManifest 의 액티비티가 com.unity3d.player.UnityPlayerGameActivity 다 (GameActivity). §6 의 am start -n <PKG>/com.unity3d.player.UnityPlayerActivity 는 이 프로젝트에선 <PKG>/com.unity3d.player.UnityPlayerGameActivity 로. 실기에서 한 번 더 확인할 것.

---
**Claude Code** · 2026-11-03 14:16

실기에서 바뀐 것 (2026-11-03, Core 207.0.1, 파일로 확인): OVRProjectConfig 에 Passthrough Camera Access 를 켜 두면 OVRManifestPreprocessor.GenerateOrUpdateAndroidManifest 가 HEADSET_CAMERA 를 자동으로 넣는다 (§3 의 '다시 만들면 빠진다'는 이 버전에선 안 그랬음). 다만 손으로 매니페스트를 고쳤다면 매번 grep 으로 확인.

---
**Claude Code** · 2026-11-03 14:42

2026-11-03 사람 결정: §0 원칙 4 추가 — 실기 시연 전 수치·감각 결정은 베이스라인, 초안대로 구현하고 값은 한곳에 모아 실기에서 조정

---
**Claude Code** · 2026-11-03 15:14

실기에서 바뀐 것 (2026-11-04, 실기 확인): ① metavr_unity_setup 으로 부트스트랩한 프로젝트는 URP 패키지만 있고 **URP 파이프라인 에셋이 없다** (Graphics·Quality 모두 None) → 에디터에선 티가 덜 나고 기기에선 URP 머티리얼이 전부 마젠타. 빌드 게이트 render-pipeline 검사가 이제 막는다. ② 아무도 참조하지 않는 런타임 asmdef(예: [RuntimeInitializeOnLoadMethod] 만 있는 것)는 IL2CPP 스트리핑이 어셈블리째 지운다 → [assembly: AlwaysLinkAssembly]. 게이트 후처리가 BuildGate 런타임 어셈블리 누락을 막는다. ③ PCA 는 install -g 로 깔면 권한 창 없이 바로 1280x960 으로 돈다 (권한 창 흐름은 별도 확인).

---
**Claude Code** · 2026-11-03 16:30

실기에서 바뀐 것 (2026-11-04, 키보드 착시 실험 실기): ① 앱 시작 직후 OVRSkeleton 이 IsDataValid=true 인데 뼈가 전부 트래킹 원점(0,0,0)에 있는 프레임이 나온다 → 손 데이터는 OVRHand.IsTracked && IsDataHighConfidence 일 때만 쓴다 (OvrHandInput, 테스트 OvrHandInput_OnlyReportsTrackedHighConfidenceHands 가 강제). ② '손끝 0.6초 정지' 드웰은 공중에서 잠깐 멈춘 손도 잡는다 → 세 점 평면이 53° 기울어짐. 책상은 트래킹 공간 up(중력) 기준 수평으로 두고, 사람 결정대로 양손 사각형 높이로 잡는다 (RectCalibrator: 네 손끝 높이 차 2cm 이내, 손 간격 12~70cm, 1초 유지, 시작 1초 무시. 테스트가 기울어진 사각형·한 손을 거부). ③ Quest 오디오 출력 샘플레이트가 24000 으로 나옴 (DSP 버퍼 256).
