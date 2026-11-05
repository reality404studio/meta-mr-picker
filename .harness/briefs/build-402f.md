너는 ORION 앱 레포(현재 디렉터리, 브랜치 orion)의 구현 담당이야. 먼저 HANDOFF.md, AGENTS.md, docs/UNITY_GUARDRAILS.md, handoff/2026-11-05-orion-demo.md 의 D13~D15 와 §4-1 을 읽어.

HANDOFF.md 의 '지금 카드' build-402f-make-098a 하나만 해:
- Assets/Fonts/Unbounded/Unbounded-Black.ttf (OFL, 이미 있음) 로 ORION 을 만들어. Blender 배치모드: /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python tools/orion-glb/make_orion.py. 블렌더 설정·캐시가 홈에 못 쓰면 환경변수 BLENDER_USER_CONFIG / BLENDER_USER_SCRIPTS / TMPDIR 를 .harness/state/blender 아래로.
- 글자 5개 별도 오브제 O1 R I O2 N (텍스트 오브젝트를 글자별로 메시로 변환, 두께 extrude, 약한 bevel), O 구멍 유지, 원점 = 각 글자 바닥 중앙, 단위 m, 글자 높이 0.12m, 자간은 Unbounded 기본. 수치는 tools/orion-glb/orion_settings.json 한 곳에. 재질은 무광 연회색 하나 (D14: 블렌더 기본 베이스 컬러 0.8).
- 출력: Assets/Models/ORION.glb 와 web/orion/ORION.glb (같은 파일).
- 검증 스크립트 tools/orion-glb/verify.sh 하나로 다 돌게:
  (1) python3 표준 라이브러리로 glb 파싱 → 노드 이름 5개 일치, 각 메시 오일러 특성으로 O1·O2 구멍 1개·나머지 0개, 전체 폭·높이가 설정 기준 ±10%
  (2) node 로 tools/orion-glb/vendor/three 의 GLTFLoader 를 써서 glb 를 parse 해서 Mesh 5개 확인. GLTFLoader 의 bare 'three' import 는 --import 로더 훅이나 상대 경로로 바꾼 사본을 tools/orion-glb 아래에 만들어 처리. vendor 원본은 고치지 마.
  (3) Unity EditMode 테스트 Assets/Tests/EditMode/OrionGlbTests.cs: ORION.glb 가 임포트돼 자식 5개 이름이 맞는지. Unity 실행은 `~/.unity/bin/unity test . --platform editmode` 또는 기존 scripts/ 의 테스트 스크립트. 먼저 `pgrep -f "projectPath .*unity/mood-picker"` 로 다른 Unity 가 없는지 확인. Unity 가 샌드박스 때문에 못 돌면 (3) 은 테스트 코드만 쓰고 verify.sh 에 --no-unity 옵션을 둬서 (1)(2) 만 돌게 하고, 그 사실을 보고에 적어. glb 임포트에는 Unity 에 glTFast 패키지가 있어야 하니 Packages/manifest.json 을 확인만 하고, 없으면 추가하지 말고 보고해.
- 규칙: .harness/touch.txt 밖은 고치지 마. 설계 이유 주석 금지(주석 린트 있음). 네트워크 없음(필요한 건 다 받아 둠).
- 통과하면 `build-402f-make-098a: ORION 글자 5개가 별도 오브제로 든 ORION.glb 가 Unity·웹에서 열린다` 로 커밋하되 네가 만든 파일만 스테이징. 푸시하지 마. 다른 카드 시작하지 마.
- 끝나면 HANDOFF.md 의 '지금 카드'와 '최근 증거'를 갱신하고, 마지막 메시지로 보고: 카드 / 한 일 / 돌린 검증(명령) / 결과(핵심 줄) / 증거: L1 / 못 한 것.
