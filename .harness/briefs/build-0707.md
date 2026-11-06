너는 ORION 앱 레포(현재 디렉터리, 브랜치 orion)의 구현 담당이다. 라운드 시작 셀프 체크(AGENTS.md)를 하고 시작해: `git status`, HANDOFF.md, design/tokens/orion.tokens.json(정본·편집 금지), 작업대 ../../studio/shared/DECIDED.md 최상단 3개와 REJECTED.md 최상단 1개, docs/UNITY_GUARDRAILS.md(§4 셰이더·알파 쓰기, ColorMask RGB 금지).
설계 명세(사람 + Gemini Argon 확정): ../../studio/apps/orion/nodes/interface-design/README.md §2(병풍 폴리오), §3(칩), §5(예산), §8(토큰). 다른 값을 정하지 말고 토큰과 명세를 따라. 이미 있는 것은 읽기만: Assets/Scripts/Tokens(OrionTokens), Assets/Scripts/Portal(PalettePortal의 팔름업 판정·PortalController 의 PaintPicked/Washed 이벤트 계약), Assets/Scripts/HandState, Assets/Shaders/Cel(CelPaint 트라이플래너), Assets/Scripts/Catch(PaintStore·PaintRecord).

보드(작업대 루트에서 `npm run studio -- show build-0707 --app orion`)의 카드 둘을 순서대로 만들어. 휠·우물 방식은 폐기됐다(REJECTED 2026-11-06) — 코드를 지우지는 말고 새 코드가 쓰지 않게만 해.

### 카드 1: build-0707-build-adf7 — 3D 칩 메시 + 셰이더
- 새 폴더 Assets/Scripts/Folio/ (asmdef: MoodPicker.Folio, 참조 MoodPicker.Tokens·MoodPicker.Catch) 의 Core 에 ChipMeshBuilder(순수 C#, Unity Mesh 대신 정점·삼각형 배열을 돌려주고 Runtime 이 Mesh 로 감싼다). 칩 1개 = 카드 대지 5.4×6.8cm 두께 3mm(전면·단면 구분) + 그 위 4.2×4.2cm 타일 블록 +4mm 돌출. 삼각형 합 22 이하(토큰 geometry.chip.trisTotal 22) 단일 메시, 서브메시 금지(버텍스 컬러/UV2 로 구분).
- 셰이더 Assets/Shaders/Chip/OrionChip.shader (URP Unlit, 알파 1, ColorMask RGB 금지, 인스턴싱·스테레오 매크로): 카드 전면 #F0EEE6, 단면 #D2CEC3, 엣지 윤곽 #15317C, 타일 윗면과 옆면 4mm 는 트라이플래너로 칩 타일 PNG(_BaseMap), 모서리 ㄱ자 크롭 마크 4개(코발트 #2559B4)·번호 띠·루프 점(빨강 #D23A2C)은 UV 연산(추가 드로우콜 0, 텍스처 추가 금지). 번호는 UV 영역의 간단한 7세그먼트풍 기하 또는 마스크 한 장을 만들지 말고 셰이더 수식으로(01~12). 칩 번호·루프 여부는 머티리얼 프로퍼티 블록으로.
- Assets/Materials/Orion/Chip.mat (셰이더 GUID 직접 참조, Shader.Find 금지).
- 검증: scripts/verify-chip.sh (verify-orion-look.sh 패턴: 1) preflight 2) 셰이더 게이트 3) EditMode 4) 배치 렌더(진짜 그래픽, 광원 0, 앰비언트 검정): 칩 3개(윤슬·그라데이션·털 대용 샘플 tools/web-orion/samples/chips/*/tile.png)를 정면과 30° 기울여 렌더 → 판정: 마젠타 0, 객체 알파 1·배경 알파 0, 옆면 4mm 에 타일 색이 이어짐, ㄱ자 코발트 픽셀 4곳 이상, 윤곽 클라인 픽셀 존재). Unity 실행부는 샌드박스에서 못 돌 수 있으니 스크립트·렌더 진입점(Assets/Scripts/Dab/Editor 옆 새 ChipRender*.cs, GPU 테스트는 [Category("Gpu")])·EditMode 테스트(삼각형 수 ≤ 22, 치수 == 토큰 ±0.1mm, 단일 머티리얼·서브메시 1)까지 쓰고 Roslyn 컴파일·셰이더 정적 검사까지 돌려. 렌더는 오케스트레이터가 돌린다.

### 카드 2: build-0707-build-cf55 — 병풍 폴리오
- Core(순수 C#): FolioLayout(슬롯 4~5칸, 접지 ±15° 지그재그, 슬롯 간격 2mm, 마지막 칸 = CLEAR), FolioPlacement(평소 위치: 허공 ORION 왼쪽 X=-0.26m·Z=0.40m·Y=-0.14m — 토큰 zone.hero; 소환 위치: 왼손 위 7cm), FolioSummon(팔름업 히스테리시스 진입 dot 0.80·이탈 0.50, 열기 유지 0.27s·닫기 0.30s — PalettePortal 의 판정을 재사용하거나 같은 토큰으로 구현, 손바닥 위로 뒤집으면 소환·놓으면 복귀), FolioSlots(칩 채우기 순서, 첫 빈 슬롯, 꽉 차면 가장 오래된 것 교체), FlyToFolio 시간표(0.55s CubicInOut 포물선, 토큰 motion.chip.flyToFolio), ChipTouch(손끝이 칩 면에 3.0mm 이하로 진입 → 톡, 6.0mm 이상 벌어져야 재무장 — 토큰 hysteresis.touch.dab; 최소 히트박스 3.6cm).
- 이벤트 계약(기존 호환): 칩 톡 → PaintPicked(PaintRecord, Texture2D) 와 같은 의미의 이벤트 1회(Folio 쪽 이벤트 FolioChipPicked(PaintRecord, Texture2D) 를 내고, 얇은 Runtime 어댑터가 PortalController 대신 HandStateController 로 전달 가능하게 인터페이스 IPaintSource 를 만들어), CLEAR 톡 → Washed 1회.
- Runtime: 얇은 MonoBehaviour FolioController 하나(칩 인스턴스 생성은 ChipMeshBuilder 사용, 손 입력은 IHandInput). 씬 연결·실기 조정은 하지 않는다.
- 성공 조건(EditMode, Assets/Tests/EditMode/FolioTests.cs): 슬롯 채우기/순서/교체, palmUp 히스테리시스 픽스처(경계 근처 왕복 20회 → 열림/닫힘 깜빡임 0), flyToFolio 도착 슬롯 = 첫 빈 슬롯, 칩 톡 3mm 진입·6mm 이탈 재무장 → 이벤트 정확히 1회(손떨림 픽스처 10회 중 오발 ≤ 1), CLEAR 톡 → Washed 1회, 소환 위치 = 손바닥 법선 7cm ±1cm. 값은 전부 OrionTokens 참조(되돌리면 실패하는 토큰 테스트는 이미 있음).

- 규칙: .harness/touch.txt 밖은 고치지 마(asmdef 참조 추가가 필요하면 보고에 적고 패치를 /tmp 에 남겨 — 승인 요청). 설계 이유 주석 금지(주석 린트 있음). 새 .cs 를 Assets/**/Verification~ 에 두지 마. 예산: 칩 1개 22 tris·단일 머티리얼, 반투명 금지(Opaque Unlit).
- 커밋하지 마. 푸시하지 마. 다른 카드 시작하지 마.
- 끝나면 HANDOFF.md 의 '지금 카드'와 '최근 증거'를 갱신하고, 마지막 메시지로 보고: 카드 / 한 일 / 돌린 검증(명령) / 결과(핵심 줄) / 증거: L1 / 못 한 것 (카드별로).
