# 에이전트 안내

- 이 레포는 작업대 `my-mr-studio` 안의 `unity/mood-picker/` 에 놓인 앱 레포예요. 할 일은 작업대 보드에서: `npm run studio -- next --app mood-picker --by agent` (작업대 루트에서)
- Unity 작업 전에 `docs/UNITY_GUARDRAILS.md` (정본: 작업대 `studio/shared/nodes/unity-guardrails/README.md`)
- 사람에게 묻기 전에 `docs/DECIDED.md`, `docs/REJECTED.md`
- `docs/`, `board/` 는 직접 고치지 않는다. 작업대에서 고치고 `scripts/sync-docs.sh` (보드 내보내기 포함) → 커밋·푸시
- 카드는 poteto 스타일 증명 게이트를 따른다 (작업대 AGENTS.md 「증명 게이트」): `doing → check <id> -- <검증 명령> (테스트 / 배치모드 / 재생 체크) → proof → done`. 검증은 이 세션이 돌린다. 작업대가 통과 로그 없는 done 을 거절한다
- 설계 이유는 주석으로 남기지 않는다. 흔하지 않은 결정은 에디터 검증(`IPreprocessBuildWithReport`, `OnValidate`), EditMode 테스트, 타입으로 만들어 되돌리면 실패하게 한다. Why 는 카드 메모·PR 본문에
- 그런 게이트가 있으면 되돌리지 말고 통과하는 쪽으로만 고친다
