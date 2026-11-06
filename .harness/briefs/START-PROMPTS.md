# 시작 프롬프트 (사람이 복붙 · 에이전트별)

공통: 각 도구는 **자기 로그인으로 자기 앱 안에서** 연다. 작업 폴더는 이 앱 레포 `unity/mood-picker`(브랜치 `orion`).

## Gemini Flash (Antigravity) — 지정 카드
```
GEMINI.md 와 .harness/ROUTING.md 를 읽고 §4 가져가기 프로토콜대로 셀프 체크부터 해. 그다음 .harness/briefs/<브리핑 파일>.md 를 끝까지 읽고 그 카드 하나만 진행해. 커밋·푸시·HANDOFF·done 은 하지 말고 proof 까지 하고 마지막 보고 형식으로 끝내.
```
현재 Flash 몫 브리핑: `web-tokens-fable.md`(웹 토큰), `build-a4ae-props.md`(소품 Blender), `orion-glb-spec.md`(ORION.glb 규격 정정). 한 번에 하나씩, **Blender 는 한 번에 한 세션만**.

## Gemini Flash — 보드에서 알아서 가져가기
```
GEMINI.md 와 .harness/ROUTING.md 를 읽어. §4 셀프 체크 후, 작업대 루트에서 `npm run studio -- next --app orion --by agent` 를 보고 카드 제목 태그가 [Flash] 또는 owner 가 Gemini Flash 인 카드 중 doing 이 아닌 첫 카드를 가져가. 브리핑이 없으면 만들지 말고 멈춰서 알려. 한 카드 끝나면 멈춰.
```

## Codex (Astra) — 오케스트레이터가 실행
`codex exec -s workspace-write -C . -c model_reasoning_effort=<high|medium> -o .harness/state/codex-<카드>-last.md "$(cat .harness/briefs/<카드>.md)" < /dev/null`

## Cursor (Fable)
```
GEMINI.md 대신 AGENTS.md 와 .harness/ROUTING.md 를 읽고 §4 대로 진행. .harness/briefs/<브리핑>.md 를 그대로 수행. 커밋·푸시·done 금지.
```
