# ⬜ Catch: 채집 원 · 크롭 · 2톤

[← 보드로](../BOARD.md)

- **목표:** 11-05~07 · 머리 레이 + 핀치 간격으로 영역을 담는다. 찰칵(정지 텍스처, 무늬 보존 2톤) 모드가 MVP
- **산출물:** Capture, CelExtract, PaintStore 스크립트 + 물감 JSON 샘플
- **기다림:** Unity 셋업 + First Light

## 하위 과업

- [ ] ▶ **테스트 영상 촬영: 윤슬 · 나뭇잎 · 바닥 타일 (+ 밤 불빛)** · 사람 · 수집
  <br><sub>폰으로. 과목당 고정 10~15초 + 손에 든 10초, 노출·초점 잠금. 원본은 my-vr-colorpicker/footage/ (git 밖), 메모만 여기에</sub>
- [ ] **테스트 영상으로 CelExtract·Loop 오프라인 검증 (Mac)** · 에이전트 · 분석
  <br><sub>실기 전에 2톤 추출·빛 방향·루프 이음매를 영상 크롭으로 미리 맞춘다. 결과 이미지를 catch 파일 칸에</sub>
- [ ] **머리 방향 레이로 채집 중심** · 에이전트 · 구현
  <br><sub>Quest 3엔 눈 추적 없음. 응시를 클릭 대용으로 쓰지 않는다 (HANDOFF §4-1)</sub>
- [ ] **양손 핀치 → 정사각형 채집 영역 크기, 기록 시간이 지나면 자동으로 담김** · 에이전트 · 구현
  <br><sub>DECIDED 2026-11-03. 원 대신 정사각형(타일과 같은 모양). 손 떼고도 영역 유지되는지, 자동 담기 타이머는 HUD 타임코드로 보임</sub>
- [ ] **월드 → 카메라 이미지 투영, 영역만 크롭** · 에이전트 · 구현
  <br><sub>PassthroughCameraAccess.WorldToViewportPoint, CurrentResolution 기준. AsyncGPUReadback. 원본 전체 프레임 저장 금지</sub>
- [ ] **CelExtract: 2~3톤 + 빛 방향 근사** · 에이전트 · 구현
  <br><sub>k-means, 디라이팅 없음. 밝은 쪽 방향 = lightDir</sub>
- [ ] **모드 토글: 한 손 핀치 1회 = 찰칵 / 루프 온 전환** · 에이전트 · 구현
  <br><sub>양손 핀치(크기)와 구분: 한 손만 핀치 = 토글. 손바닥이 얼굴 쪽일 땐 무시(Quest 시스템 메뉴)</sub>
- [ ] **찰칵: 무늬 보존 2톤 + 이음매 없는 타일** · 에이전트 · 구현
  <br><sub>옷감·벽·타일처럼 무늬가 재료인 것. 크롭을 픽셀마다 2톤으로(톤만 뽑으면 무늬가 사라짐) + 가장자리 이음매 제거해서 트라이플래너로 반복해도 티 안 나게. 테스트 영상의 타일로 먼저 비교</sub>
- [ ] **PaintStore: 물감 JSON 저장·로드** · 에이전트 · 구현
  <br><sub>HANDOFF §5 데이터 구조 + mode: snap \| loop. snap 은 loop 없음. Application.persistentDataPath</sub>
- [ ] **실기: 실내·창밖에서 담아 보기** · 사람 · 검증
  <br><sub>흔들려도 원이 따라오는지, 톤이 그럴듯한지. MQDH 캐스팅으로 같이 본다 · 증거: 빌드 ID가 찍힌 logcat 발췌 + 스크린샷을 첨부해야 done</sub>

## 메모

HANDOFF §4-1, §4-2, §5 (Capture, CelExtract, PaintStore). 실기 전 공통 "Unity 실기 가드레일" §3 카메라 접근, §5 손.
- 원본 전체 프레임 저장 금지. 크롭한 영역만
- 응시는 클릭 대용이 아니다. 응시 시간은 진하기·루프 길이로만 쓴다

---
**Claude Code** · 2026-11-03 13:56

2026-11-03: three.js 데모의 포팅 기준이 meta-mr-picker PR #1 (`reference/texture-lab/`, 미머지)에 있음. 연결표는 그 README '보드 과업과 연결'. Assets 에 넣는 건 PigmentCore/*.cs + PigmentCommon.hlsl 만. shader-lab 7모드는 탐색일 뿐 (셀 룩 위에 얹을지는 사람 결정).
