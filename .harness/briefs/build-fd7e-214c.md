너는 ORION 앱 레포(현재 디렉터리, 브랜치 orion)의 구현 담당이다. 먼저 HANDOFF.md, AGENTS.md(「라운드 시작 셀프 체크」 포함), docs/UNITY_GUARDRAILS.md 를 읽고, 정본 토큰 design/tokens/orion.tokens.json (읽기 전용·canon, 절대 수정 금지)과 그 설명 ../../studio/apps/orion/nodes/interface-design/README.md §8 을 읽어. 이 토큰은 사람 + Gemini Argon 이 확정한 값이다. 다른 값을 정하지 마.

HANDOFF.md 의 '지금 카드' build-fd7e-build-214c 하나만 해. Unity 쪽 토큰 → 코드:
1. 생성기: tools/orion-tokens/gen_tokens.py (표준 라이브러리만) 가 design/tokens/orion.tokens.json 을 읽어 Assets/Scripts/Tokens/OrionTokens.g.cs (순수 C#, 상수·정적 읽기 전용, 파일 머리에 '자동 생성' 한 줄 외 주석 금지, 주석 린트 있음) 와 Assets/Scripts/Tokens/MoodPicker.Tokens.asmdef 를 만든다. 생성 결과는 결정적이어야 한다(같은 JSON → 같은 바이트). 색은 hex 문자열과 Color 변환 헬퍼, 시간·길이는 float(미터·초·도 단위로 통일, mm/cm 키는 이름에 단위를 남기고 미터 상수도 함께).
2. 동기 검증 scripts/verify-tokens.sh: (a) gen_tokens.py 를 임시 경로에 다시 돌려 커밋된 OrionTokens.g.cs 와 diff — 다르면 실패 (JSON 을 고치고 재생성 안 한 경우를 잡는다) (b) Roslyn 컴파일(기존 verify_compile 방식).
3. 기존 설정이 토큰을 따르게 연결 (값을 코드에 남기지 말고 OrionTokens 참조):
   - Assets/Scripts/PullOut/Core/PullOutSettings.cs: LetterDelaySeconds = motion.pullout.staggerSeconds(0.08), 전체 이동·부풀기 시간 0.65, 이징 EaseOutBack, 두께 0.02→1.0. (HoverHeightMeters 같은 위치 값은 z-zone 에 있는 값으로 맞출 수 있으면 맞추고, 모호하면 건드리지 말고 보고.)
   - Assets/Scripts/Portal/Core/PortalSettings.cs·PalettePortal.cs: 손바닥 위 판정 진입 dot 0.80 / 이탈 0.50, 열기 유지 0.27s, 닫기 0.30s (hysteresis.palmUp). 높이 heightAbovePalm 은 hand.folioAboveCm(7cm). 휠·우물 관련 필드는 지우지 말고 그대로 둔다(다음 카드가 폴리오로 교체).
   - Assets/Scripts/HandState/Core/HandStyleSettings.cs: 손 색은 color.paper.face(#F0EEE6, 맨손), color.print.cobalt(#2559B4, 스탬프) 를 토큰에서, 림 파라미터(rimPower 6.0, inflateMm 1.5)와 디밍(env.passthrough.*) 상수도 노출.
4. EditMode 테스트 Assets/Tests/EditMode/OrionTokensTests.cs (테스트가 JSON 을 직접 읽는 방식: 파일 경로는 프로젝트 루트 기준 design/tokens/orion.tokens.json, 테스트 안에서 최소 파서로 읽거나 System.Text.Json 대신 간단한 정규식, 새 패키지 추가 금지): 대표 값들(색 7개, passthrough 3개, stagger/시간, palmUp 4개, hit.minSize, hand.rimPower)이 코드 상수와 같다. JSON 값을 바꾸면 실패해야 하므로 '코드 상수 == JSON 에서 읽은 값' 형태로. 기존 PullOut·Portal·HandState 테스트가 바뀐 값(예: 0.15 → 0.08)에 맞게 깨지면 기대값을 토큰 참조로 고쳐라 — 단, 테스트의 검증 의미(순서·오탐 한계 등)는 유지.
- 규칙: .harness/touch.txt 밖은 고치지 마. 설계 이유 주석 금지. Unity 는 샌드박스에서 못 돌 수 있음 → 코드·테스트·컴파일까지, 컴파일 확인은 기존 Roslyn 도구(PullOut/Verification~ 방식, tools/ 아래 스크립트)로. 새 .cs 를 Assets/**/Verification~ 에 두지 마(preflight 가 컴파일함, tools/ 에 둬).
- 커밋하지 마. 푸시하지 마. 다른 카드 시작하지 마.
- 끝나면 HANDOFF.md 의 '지금 카드'와 '최근 증거'를 갱신하고, 마지막 메시지로 보고: 카드 / 한 일 / 돌린 검증(명령) / 결과(핵심 줄) / 증거: L1 / 못 한 것.
