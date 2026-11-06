@AGENTS.md

# Gemini / Antigravity 에게 (Argon · Flash 공통)

- 이 레포의 출발점은 `AGENTS.md` 와 `.harness/ROUTING.md`. 먼저 둘을 읽는다. 일감은 보드 카드 + `.harness/briefs/<카드>.md` 로 받는다.
- 로그인은 **Antigravity 앱 안에서만** 쓴다. 다른 도구(Claude Code·Codex·Cursor 등)에 이 로그인을 연결하지 않는다.
- 작업 시작 한 줄 프롬프트는 `.harness/briefs/START-PROMPTS.md`.
- Flash 의 기본 몫: Blender 를 **실행하는** 에셋 카드, 웹/JS, 소규모 코드. 셰이더·손 상태 기계 같은 복잡한 로직 카드는 Astra 몫이니 가져가지 않는다.
- 커밋·푸시·HANDOFF·`done` 은 하지 않는다 (`proof` 까지). 마지막 보고 형식과 한도 규칙은 ROUTING.md §4.
- 보드 CLI 는 작업대 루트(`../..`, `my-mr-studio`)에서: `npm run studio -- next --app orion --by agent`.
