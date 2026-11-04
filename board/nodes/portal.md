# ✅ 팔레트 포탈 (왼손바닥)

[← 보드로](../BOARD.md)

- **목표:** 11-10~11 · 왼손바닥 포탈에 손가락을 넣었다 빼면 손끝에 물감이 묻는다
- **산출물:** PalettePortal 스크립트

## 하위 과업

- [x] **왼손바닥 위로 뒤집기 감지 → 포탈 열기** · 에이전트 · 구현
  <br><sub>손바닥 위 5~10cm 에 띄움. 공통 hand-patterns 의 "손바닥 위로"(얼굴 향함)와 다르다: 여기선 하늘을 향함</sub>
- [x] **오른손가락 넣었다 빼기 → 손끝에 물감** · 에이전트 · 구현
  <br><sub>포탈 열린 동안 핀치 금지 (시스템 제스처 충돌)</sub>
- [x] **실기: 두 손이 가까워도 추적이 안 흔들리고 시스템 메뉴가 안 뜨는지** · 사람 · 검증
  <br><sub>증거: 빌드 ID가 찍힌 logcat 발췌 + 스크린샷을 첨부해야 done</sub>
- [x] **포탈 높이 2배: 손바닥 위 약 14cm** · 에이전트 · 구현
  <br><sub>PalettePortal.heightAbovePalm 7cm → 14cm (Range 도 넓힘). DECIDED 2026-11-04 포탈 실기 후</sub>
- [x] **회전판 휠: 물감이 둘레에, 테두리 쓸기로 회전, 맨 앞 칸만 찍힘** · 에이전트 · 구현
  <br><sub>DECIDED 2026-11-04 W1. 기존 dip 판정 재사용. 시스템 제스처 피해서 집기 없음</sub>
- [x] **가운데 우물: 가운데 넣었다 빼면 손끝 물감 씻김** · 에이전트 · 구현
  <br><sub>DECIDED 2026-11-04 C3. PortalController 에 Washed 이벤트, CurrentPaint = null. dab 쪽은 손끝 물감 없으면 안 칠함</sub>
- [x] **실기: 높이 14cm · 회전판 · 우물 씻기** · 사람 · 검증
  <br><sub>빌드 ID logcat + 스크린샷 (portal-device 스크린샷도 이때)</sub>
- [x] **포탈 높이 7cm 로 되돌림** · 에이전트 · 구현
  <br><sub>DECIDED 2026-11-04 스켈레톤 실기 후 4). PortalSettings.asset + 기본값 0.07, 에셋 값 테스트 갱신</sub>
- [x] **휠 키우기: 칩·앞 칸·우물 크게, 찍기 쉽게** · 에이전트 · 구현
  <br><sub>DECIDED 같은 날 3). 반지름·앞 칸 판정원 키우고 우물과 간격도 넓힘. 실기에서 '찍기 잘 안 됨'의 원인 후보(앞 칸 1.3cm 판정, 쓸기 오작동)를 픽스처로 재현</sub>
- [x] **펀치 조종 중 포탈 깜빡임 막기** · 에이전트 · 구현
  <br><sub>사람 실기 보고 2026-11-04: "왼손을 이동하다 보면 왼손 안쪽에 포탈이 자꾸 생길락말락함". 왼손 펀치 드래그 중엔 포탈 판정 끔 + 손바닥 위 판정에 히스테리시스·유지 시간</sub>

## 메모

HANDOFF §4-3. 공통 "Unity 실기 가드레일" §5 손.
- 포탈은 손바닥 위 5~10cm. 두 손이 맞닿으면 추적이 흔들린다
- 포탈이 열린 동안 핀치 금지 (손바닥이 얼굴 쪽 + 핀치 = 시스템 메뉴). "넣었다 빼기"만

---
**Claude Code** · 2026-11-03 04:20

사람 제안 (2026-11-03): 손을 뒤집으면 열리고, 포탈은 그 자리에 남는다. 닫기는 왼손을 빠르게 털기. → 왼손이 떠나니 두 손 겹침 문제가 사라짐. 검토 중 (확정 아님):
- 남는 위치: 월드 고정은 달리는 전철에서 미끄러질 수 있음 → 몸(무릎 높이, 머리 yaw 기준)에 붙이는 안
- 털기: 아주 빠른 털기는 손 추적이 끊기는 속도 + 전철 흔들림 오인식 위험 → '좌우 2번 흔들기'(방향 반전 2회, 폭 8cm 이상, 0.8초 안) 정도로, 포탈 위가 아니라 아무 데서나
- 손바닥 아래로 뒤집기로 닫기는 안 됨: 열고 나서 손을 무릎에 내려놓는 자연스러운 자세가 곧 닫기가 됨
- 안전망: 포탈이 시야 밖 몇 초면 자동으로 접힘

---
**Claude Code** · 2026-11-04 05:03

2026-11-04 portal-palm·portal-dip 구현 (Assets/Scripts/Portal/Core/PalettePortal.cs + Runtime/PortalController.cs, Hands/Core/PinchGate.cs). Catch 씬에 같이 들어감. 테스트 8개 + 전체 67/67, preflight PASS.
- 열기: 왼손바닥 법선·하늘 dot ≥0.8 이 0.12s 유지 + 손바닥 속도 ≤0.6m/s → 열림. 닫기: dot <0.5 가 0.3s 또는 추적 끊김 0.5s. 포탈 = 손바닥 + 법선×7cm (PortalSettings, 베이스라인). 손바닥 법선 방향은 실기에서 확인 → invertPalmNormal
- 남는 위치·털어서 닫기(사람 제안, 검토 중)는 넣지 않음. 지금은 손바닥을 따라다님
- dip: 오른 검지가 표면 위에서 원판 안으로 1cm 이상 내려갔다 0.5cm 위로 나오면, 들어간 자리의 칩 = 손끝 물감 (PortalController.CurrentPaint / CurrentTile = PaintRecord.tile, PaintPicked 이벤트 → dab 쪽이 받음). 손끝에 타일 쿼드 표시
- 핀치 게이트: 포탈이 열렸거나 왼손바닥이 하늘 쪽(dot>0.5)이거나 닫힌 뒤 0.5s 동안 CatchSquare 에 손을 '추적 안 됨'으로 넘김 → 채집·토글로 안 샘 (Gate_ 테스트가 강제)
- 칩은 최근 물감 최대 9개, 3열. 물감 0개면 원판만 (기본 물감 번들은 polish)

---
**Claude Code** · 2026-11-04 05:43

portal-build-4130 (2026-11-04): 포탈 중심 = 손바닥 + 법선 x 0.14m (PortalSettings.heightAbovePalm 기본·에셋 0.14, Range 0.05~0.20, 베이스라인). 발견: PortalSettings 클래스가 PalettePortal.cs 안에 있어 Assets/Settings/PortalSettings.asset 의 m_Script 가 비어 있었음 → 씬이 에셋을 못 읽고 Defaults 로 돌았음 (portal-device 실기도 Defaults). 클래스를 PortalSettings.cs 로 옮기고 에셋 스크립트 참조 복구, 테스트 Portal_ShippedSettingsAssetUses14cm 가 에셋 로드·값을 강제. 테스트: 기울인 손바닥(20°) 법선 방향 0.14m ±1cm, 기존 dip/게이트 테스트 통과 (verify-portal.sh 10/10)

---
**Claude Code** · 2026-11-04 05:46

portal-build-88f6 (2026-11-04): 회전판 W1. 물감 칩이 반지름 4cm 둘레에 균등(칸 수 = max(물감 수, 8)), 최신 칩이 맨 앞(손목 쪽, frontAngle -90° = -forward). 오른 검지가 테두리 띠(반지름 3~7.5cm, 표면 -0.5~+3cm)에서 6° 넘게 쓸면 휠이 손끝을 따라 돌고, 띠를 벗어나거나 아래로 찍으면 가까운 칸으로 스냅 (Rotated 이벤트, 로그 'portal wheel front='). 찍기는 맨 앞 칸 원(1.3cm) 안에서 들어갔을 때만 (다른 칸 dip 무시). 핀치·집기 없음. 앞 칸 표시 = 흰 사각 테두리. 칩 크기 = min(2.6cm, 둘레/칸 x0.8). 값은 PortalSettings 'Wheel (baseline)'. MaxChips 9→12. 테스트 17/17 (쓸기 ±1칸 스냅, 작은 쓸기 복귀, 앞 칸만 찍힘, 0·1·12개 배치). 남은 것: 실기에서 쓸기 띠 높이·앞 칸 방향(손목 쪽이 맞는지)

---
**Claude Code** · 2026-11-04 05:49

portal-build-e776 (2026-11-04): 가운데 우물 C3. 휠 가운데 반지름 1.8cm 원(흰 원 표시) 안으로 들어갔다 빼면 PalettePortal.Washed → PortalController.Washed (새 이벤트, 인자 없음), CurrentPaint/CurrentTile = null, 손끝 칩 숨김, 로그 'portal washed'. 가장자리(맨 앞 칸) dip 은 그대로 PaintPicked(PaintRecord, Texture2D) — 시그니처 안 바꿈. 손끝 물감 상태는 Core/TipPaint<TPaint,TTile> (컨트롤러와 테스트가 같은 배선). 우물(≤1.8cm)·앞 칸(4cm±1.3)·쓸기 띠(≥3cm) 사이 빈 띠를 테스트가 강제. 테스트: 가운데 dip → Washed 1회 + Paint null, 가장자리 → Picked, 세 방향 반지름 0.1~3.7cm 2mm 간격 dip 에서 오탐 0, 물감 0개에서도 씻김. verify-portal.sh 21/21, 전체 EditMode 111/111. 남은 것: 실기 (portal-verify-c868)

---
**Claude Code** · 2026-11-04 06:10

2026-11-04 스켈레톤 실기 후속 (Claude Code). 전체 117/117.
- cfff: 높이 7cm (기본값 + PortalSettings.asset, Range 5~10cm)
- b57d: 왼손 엄지-검지 3cm 이내(핀치 중)와 놓은 뒤 0.3s 동안 열림 판정 끔, 이때 핀치 게이트도 안 걸어서 펀치 드래그가 안 끊김. 열기 유지 0.12→0.27s (0.3s 유지에만 열리고 A2 0.3s 안 반응 둘 다 맞춤). 경계 각도 왕복 깜빡임 ≤1
- 9c6b: 재현 = 기존 값에서 흔들린 앞 칸 dip 10회 중 0회 (내려오며 생긴 옆 흔들림이 쓸기로 잡혀 휠이 돌아감). 수정: 쓸기는 손끝 수직 속도 >0.15m/s 면 각도 누적 안 함, 엔게이지 6→10°, 표면 아래로 내려가면(=dip) 쓸기 취소(회전 없음). 크기: 포탈 반지름 6→9cm, 휠 4→5.5cm, 앞 칸 판정 1.3→2.5cm, 우물 1.8→2cm, 쓸기 띠 3.5~10cm, 칩 2.6→3.6cm. 결과 10/10, 회전 0, 우물 혼동 0
