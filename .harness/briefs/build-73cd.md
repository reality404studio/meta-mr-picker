너는 ORION 앱 레포(현재 디렉터리, 브랜치 orion)의 구현 담당이야. 먼저 HANDOFF.md, AGENTS.md, docs/UNITY_GUARDRAILS.md, handoff/2026-11-05-orion-demo.md 의 §2(10~16초 풀아웃)·§3·§4-4 를 읽어. Assets/Scripts/MacAnchor 와 Assets/Scripts/Experiments/DeskKeyboard/Core/TouchDetector.cs 는 읽고 재사용만 해(수정 금지).

HANDOFF.md 의 '지금 카드' build-73cd-build-d0e0 하나만 해. 풀아웃:
- 새 폴더 Assets/Scripts/PullOut/ (Core 순수 C# + Runtime, asmdef; MacAnchor·TouchDetector 참조 asmdef 추가).
- 입력: MacAnchor 의 화면 사각형(평면·크기). 손끝 위치 스트림. 검지 포인팅 손끝이 사각형 평면에 닿는 탭(깊이 임계·유지 시간)을 TouchDetector 방식으로 판정, 사각형 안일 때만.
- 출력: 글자 5개 O1 R I O2 N 에 대한 시간표. 탭 시각 t0 부터 글자 i 는 t0 + 0.15*i 에 시작, 화면 평면 위치에서 호버링 위치까지 이동하며 두께 스케일 0→1 (이징 하나). 호버링 위치는 화면 앞 법선 방향 오프셋·글자 간격·높이를 PullOutSettings 한 곳에.
- 'ORION.glb' 를 임포트해 인스턴스화하는 Runtime 은 얇게: 자식 5개를 찾아 시간표대로 transform 을 움직이는 MonoBehaviour 하나. 임포트는 glTFast 가 이미 있음.
- 성공 조건(EditMode, Assets/Tests/EditMode/PullOutTests.cs): 사각형 안 탭 1회 → 이벤트 순서 O1→R→I→O2→N, 사각형 밖 탭과 손떨림 10회(손끝이 사각형 근처에서 임계 아래로 요동) 중 오탐 ≤1, 애니메이션 종료 시각의 글자 위치 = 호버링 설정값 ±1cm, 두께 스케일 종료값 1.
- 규칙: .harness/touch.txt 밖은 고치지 마(테스트 asmdef 참조 추가는 허용, 최소로). 설계 이유 주석 금지(주석 린트 있음). Unity 실행은 못 돌 수 있음 → 테스트 코드와 컴파일 가능한 코드까지. 컴파일 확인은 기존 사전 점검(Roslyn) 도구(MacAnchor/Verification~ 의 verify_compile.py 방식을 참고해 PullOut 용도 추가 가능). 못 한 건 보고에 적어.
- 커밋하지 마(Unity 테스트를 오케스트레이터가 돌린 뒤 커밋한다). 푸시하지 마. 다른 카드 시작하지 마.
- 끝나면 HANDOFF.md 의 '지금 카드'와 '최근 증거'를 갱신하고, 마지막 메시지로 보고: 카드 / 한 일 / 돌린 검증(명령) / 결과(핵심 줄) / 증거: L1 / 못 한 것.
