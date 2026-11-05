너는 ORION 앱 레포(현재 디렉터리, 브랜치 orion)의 구현 담당이다. 먼저 HANDOFF.md, AGENTS.md, docs/UNITY_GUARDRAILS.md, handoff/2026-11-05-orion-demo.md 의 §1 D6·D7·D8, §3(손 동작 지도 표), §4-7 을 읽어. Assets/Scripts/Portal(PortalController·PalettePortal·TipPaint), Assets/Scripts/Catch, Assets/Scripts/PullOut, Assets/Scripts/Hands 는 읽기만(수정 금지).

HANDOFF.md 의 '지금 카드' build-be9a-build-2714 하나만 해. 손 상태 기계:
- 새 폴더 Assets/Scripts/HandState/ (Core 순수 C# + 얇은 Runtime, asmdef; 필요한 asmdef 참조만 추가).
- 상태: Bare, Paint, Stamp, Dropper. 손은 좌/우가 따로 상태를 가질 수 있지만 이번엔 오른손(작업 손) 하나의 상태 기계. 왼손은 항상 포탈·펀치 조정 용도라 상태 없음(Bare 고정).
- 사건 enum: PortalDipPaint(칩 색 포함), Washed, GrabStamp, GrabDropper, ReleaseTool, GrabLetter, ReleaseLetter, TapScreen, LetterTouch(물감 손으로 글자에 닿음), StampFired, DropperPicked(색 포함). 사건 이름은 인수인계 §3 표의 동작과 1:1.
- 전이 표(§3): Bare + PortalDipPaint → Paint(색), Paint + Washed → Bare, Paint + GrabStamp/GrabDropper → 무시(먼저 씻어야 함은 아님: 도구를 집으면 물감은 사라지고 해당 도구 손, 이 규칙을 표에 명시), Bare + GrabStamp → Stamp, Bare + GrabDropper → Dropper, Stamp/Dropper + ReleaseTool → Bare, Dropper + DropperPicked → Dropper(마지막 색 갱신), Stamp + StampFired → Stamp. 인수인계 §3 표와 모순이 있으면 표를 따르고 어긋난 점을 보고에 적어.
- 허용 동작 표(AllowedActions): Bare{TapScreen, GrabLetter, TouchLetterPulse, ThrowWord, StirBench}, Paint{FillLetter}, Stamp{Capture}, Dropper{Pick, DropDrop}. 금지: Paint·Stamp·Dropper 에서 GrabLetter 등. 코드 테이블 하나(정적 읽기 전용 딕셔너리/배열)로. 동작 허용 여부 함수 IsAllowed(state, action) 와 Apply(state, event) → (새 상태, 무시됨 여부).
- 손 색: HandStyleSettings(ScriptableObject 아니어도 됨, 순수 클래스 + 기본값) 한 곳. Bare #F0EEE6, Stamp #2559B4, Paint = 칩의 lightTone, Dropper = 마지막으로 딴 색(없으면 #F0EEE6). 색값은 '디자인 토큰 자리'라 상수 한 곳에만 둬(다른 모델이 토큰을 받아 이 파일만 고친다).
- Runtime: 얇은 MonoBehaviour 하나. PortalController.PaintPicked / Washed 이벤트를 구독해 상태 기계에 전달하고, 상태 변경 이벤트 StateChanged(state, color) 를 낸다. 씬 연결·셰이더는 하지 않는다(다음 카드).
- 성공 조건(EditMode, Assets/Tests/EditMode/HandStateTests.cs): (1) 상태×사건 전수 순회 테스트 — 전이 표에 없는 조합은 모두 '무시됨'이고 상태가 변하지 않는다 (2) 금지 동작 시도 시 IsAllowed 가 false (3) 손 색 매핑: 4상태 각각 기대색, Paint 는 칩 색을 따름, Dropper 는 마지막 색 (4) 표를 한 줄 바꾸면(예: Paint 에 GrabLetter 허용) 테스트가 실패하도록 허용 표 스냅샷 테스트 하나. 기존 EditMode 전체가 그대로 통과해야 해.
- 규칙: .harness/touch.txt 밖은 고치지 마(테스트 asmdef 참조 추가만 최소 허용). 설계 이유 주석 금지(주석 린트 있음). Unity 실행은 샌드박스에서 못 돌 수 있음 → 테스트 코드와 컴파일 가능한 코드까지, 컴파일 확인은 PullOut/Verification~ 방식의 Roslyn 도구를 HandState 용으로 추가해 돌려. 못 한 건 보고에 적어.
- 커밋하지 마. 푸시하지 마. 다른 카드 시작하지 마.
- 끝나면 HANDOFF.md 의 '지금 카드'와 '최근 증거'를 갱신하고, 마지막 메시지로 보고: 카드 / 한 일 / 돌린 검증(명령) / 결과(핵심 줄) / 증거: L1 / 못 한 것.
