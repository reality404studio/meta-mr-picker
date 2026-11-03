# 에이전트 안내

- 이 레포는 작업대 `my-mr-studio` 안의 `unity/mood-picker/` 에 놓인 앱 레포예요. 할 일은 작업대 보드에서: `npm run studio -- next --app mood-picker --by agent` (작업대 루트에서)
- Unity 작업 전에 `docs/UNITY_GUARDRAILS.md` (정본: 작업대 `studio/shared/nodes/unity-guardrails/README.md`)
- 사람에게 묻기 전에 `docs/DECIDED.md`, `docs/REJECTED.md`
- 루프 · 이음매 없는 타일 · 플립북 · 물감 셰이더는 `reference/texture-lab/README.md` 의 참고 구현과 측정값부터 (결정된 것 / 탐색한 것 구분 표 포함)
- `docs/`, `board/` 는 직접 고치지 않는다. 작업대에서 고치고 `scripts/sync-docs.sh` (보드 내보내기 포함) → 커밋·푸시
