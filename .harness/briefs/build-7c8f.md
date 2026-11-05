너는 ORION 앱 레포(현재 디렉터리, 브랜치 orion)의 구현 담당이야. 먼저 HANDOFF.md, AGENTS.md, docs/UNITY_GUARDRAILS.md, handoff/2026-11-05-orion-demo.md 의 §2(풀아웃)·§4-2·§4-3·§5 를 읽어.

HANDOFF.md 의 '지금 카드' build-7c8f-build-f55b 하나만 해. 맥북 화면 앵커:
- 새 폴더 Assets/Scripts/MacAnchor/ 에 Core(순수 C#, 유니티 의존 최소) + Runtime 을 asmdef 로 나눠. Assets/Scripts/Experiments/DeskKeyboard/Core 의 DeskCalibrator·DeskPlane 은 읽고 재사용만 해 (수정 금지, 참조 asmdef 추가).
- 입력: 양손 사각형(두 손 핀치 위치 2개 + 높이) 으로 맥북 화면의 모서리를 잡는 기존 방식 그대로. 화면 사각형 = 중심, 법선, 가로·세로(m), 위쪽 방향. 
- 저장/로드: Application.persistentDataPath 아래 json (경로는 주입 가능하게). 재로드하면 같은 값.
- MacAnchor.IsAvailable: 저장된 앵커가 있고 유효(크기 5~60cm, 평면이 수직 ±30°)면 true, 아니면 false → 호출 쪽이 '단독 패널 모드'로 내려가는 근거.
- 수치(유효 범위 등)는 MacAnchorSettings 한 곳에.
- 성공 조건(EditMode 테스트, Assets/Tests/EditMode/MacAnchorTests.cs): 손 궤적 픽스처(코드로 만든 4점)로 화면 사각형 → 평면 법선·크기 오차 ±1cm, 저장 후 재로드 동일, 앵커 없음/크기 범위 밖/기울어짐 → IsAvailable false. 기존 EditMode 테스트가 전부 통과해야 해.
- 규칙: .harness/touch.txt 밖은 고치지 마. 설계 이유 주석 금지(주석 린트 있음). Unity 실행·Blender 는 이 샌드박스에서 못 돌 수 있어. 못 돌면 테스트 코드와 컴파일 가능한 코드만 쓰고, 컴파일 확인은 scripts/ 의 사전 점검(Roslyn) 스크립트 같은 기존 도구로. 못 한 건 보고에 적어.
- 통과하면 `build-7c8f-build-f55b: 양손 사각형으로 잡은 맥북 화면 사각형이 저장·재로드되고 앵커 유효 여부가 판정된다` 로 커밋하되 네가 만든 파일만 스테이징. 푸시하지 마. 다른 카드 시작하지 마.
- 끝나면 HANDOFF.md 의 '지금 카드'와 '최근 증거'를 갱신하고, 마지막 메시지로 보고: 카드 / 한 일 / 돌린 검증(명령) / 결과(핵심 줄) / 증거: L1 / 못 한 것.
