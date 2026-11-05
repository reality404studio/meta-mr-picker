# 에이전트 안내

- 이 레포는 작업대 `my-mr-studio` 안의 `unity/mood-picker/` 에 놓인 앱 레포예요. 브랜치 `orion` = ORION 앱(보드 `--app orion`), `main` = 컬러피커 보존본(보드 `--app mood-picker`, 보류). 할 일은 작업대 루트에서 `npm run studio -- next --app orion --by agent`
- Unity 작업 전에 `docs/UNITY_GUARDRAILS.md` (정본: 작업대 `studio/shared/nodes/unity-guardrails/README.md`)
- 사람에게 묻기 전에 `docs/DECIDED.md`, `docs/REJECTED.md`
- `docs/`, `board/` 는 직접 고치지 않는다. 작업대에서 고치고 `scripts/sync-docs.sh` (보드 내보내기 포함) → 커밋·푸시
- 카드는 poteto 스타일 증명 게이트를 따른다 (작업대 AGENTS.md 「증명 게이트」): `doing → check <id> -- <검증 명령> (테스트 / 배치모드 / 재생 체크) → proof → done`. 검증은 이 세션이 돌린다. 작업대가 통과 로그 없는 done 을 거절한다
- 설계 이유는 주석으로 남기지 않는다. 흔하지 않은 결정은 에디터 검증(`IPreprocessBuildWithReport`, `OnValidate`), EditMode 테스트, 타입으로 만들어 되돌리면 실패하게 한다. Why 는 카드 메모·PR 본문에
- 그런 게이트가 있으면 되돌리지 말고 통과하는 쪽으로만 고친다

## 누가 무엇을 (2026-11-06 사람 결정)
| 역할 | 모델 | 깊이 |
|---|---|---|
| 구현 (Unity C#·셰이더·손 상태 기계·Blender) | Codex CLI · Astra | 셰이더·상태 기계·투영 high, 스크립트·에셋 medium |
| 웹 구현 (three.js 로더·맥 에디터·로컬 서버) | Cursor · Fable | medium. 파일은 web/ · server/ 만 |
| 오케스트레이터 (카드·HANDOFF·DECIDED·사람 질문) | Claude Code · Sonnet (큰 갈림길만 Opus) | low~medium |
| 기기 라운드 (`mr` 명령) | Claude Code Haiku/Sonnet 또는 Codex | low |
| 로그 요약 | logcat-reader (Haiku) | low |
| 다른 눈 (큰 덩어리만) | 구현과 다른 회사: Astra 구현 → Claude 리뷰 | medium |
| 영상·룩 판정 | Gemini Argon | medium |
| 구상·콘티 | 채팅 (claude.ai / Gemini) → handoff/ 로 들여옴 | high, 드물게 |
- 일감은 HANDOFF.md 「지금 카드」로 넘긴다. 세션끼리 메시지 안 씀. 결과는 커밋 + HANDOFF 갱신 + 마지막 보고

<!-- mrkit:project:begin -->
## 이 프로젝트의 작업 규칙 (Claude Code·Codex 공통 — CLAUDE.md는 이 파일을 가져온다)
- 출발점은 HANDOFF.md. 결정은 DECIDED.md, 버린 안은 REJECTED.md — 사람에게 묻기 전에 두 파일부터 본다.
- 카드 하나 = 검증 가능한 한 단위. 성공 조건의 증거 수준(L0~L4)에 닿아야 증명 → Done. 추측으로 Done 금지.
- 설계상 특이한 결정(A가 아니라 C)은 주석이 아니라 되돌리면 실패하는 테스트·게이트로 남긴다.
- 커밋은 게이트를 통과한 카드만, 메시지 `card-id: 관찰 가능한 변화`. main 직접 푸시·force-push는 묻는다.
- 실기 전 수치는 베이스라인이다. 확정을 묻지 말고 한곳(설정 파일)에 모은다.
- 취향 앵커 (3줄 이내로 사람이 채움):
  - 글자 룩은 재질 그대로 (2톤 셀 단순화 안 함, D13). 안 칠한 글자 = 블렌더 기본 클레이 연회색 (D14)
  - 손은 항상 가상 오브제 위 (손 마스크), 손 색 = 모드 (D6)
  - 장식 금지, 에디토리얼 (흰 제도선 #F0EEE6, Plex Mono)
- 라운드 시작 셀프 체크 (사람에게 묻지 않고 먼저 한다): `git fetch --all` 로 원격 브랜치·최근 커밋, 작업대 `studio/shared/DECIDED.md`·`REJECTED.md` 최상단과 보드 변경(다른 에이전트가 만든 카드·메모, 예: Gemini Argon 의 interface-design), 디자인 토큰 확정 여부(`design/tokens/orion.tokens.json` ↔ 작업대 interface-design §8), 남은 프로세스. 결정이 바뀌었으면 진행 중 카드 브리핑과 보드를 먼저 맞춘다. 사람은 결정만 한다.
- 기기 라운드: mr-round 스킬. 마무리: wrap 스킬.
<!-- mrkit:project:end -->
