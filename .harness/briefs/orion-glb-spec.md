# 브리프: ORION.glb 를 Argon 확정 규격으로 다시 만들기 (Gemini Flash · Antigravity)

카드: 보드 `--app orion` 의 "ORION.glb 규격 정정 [Flash]" (제목으로 `npm run studio -- list --app orion` 에서 찾아 id 확인).
먼저 `GEMINI.md`, `.harness/ROUTING.md`(§3 Blender 잠금, §4 프로토콜)을 읽고, `pgrep -fl Blender` 로 다른 Blender 가 없는지 확인한 뒤 카드를 `doing` 으로.

## 왜
앱 레포 `ORION.glb` 는 확정 전에 만들었고 Argon 규격(작업대 `studio/apps/orion/nodes/interface-design/README.md` §8-3 type.hero.sculpture, §11-2)과 다르다.
| | 지금 | 확정 규격 |
|---|---|---|
| 노드 이름 | `O1 R I O2 N` | `Letter_O1 Letter_R Letter_I Letter_O2 Letter_N` |
| 크기 | 폭 0.615 × 높이 0.12 × 두께 0.021 m | 5글자 폭 **0.38 m** × 높이 **0.085 m** × 두께 **0.028 m** |
| 모서리 | bevel 1.5mm | **0.8mm 챔퍼** |
| 피벗 | 글자 바닥 중앙 | **각 글자 정중앙** (§11-2: "각 글자 정중앙 Pivot") |
| 삼각형 | 글자당 3천~6천 | **글자당 약 180~260 tris** (낮은 곡선 해상도) |
| 폰트 | Unbounded Black | Unbounded Black 900 (그대로) |

## 해야 할 것
1. 기존 생성기를 고친다: `tools/orion-glb/make_orion.py`, `tools/orion-glb/orion_settings.json`(수치는 이 한 곳), 검증기 `tools/orion-glb/verify_glb.py`·`verify_three.mjs`·`verify.sh`·`test_verify_glb.py`. 노드 이름·치수·피벗·삼각형 예산을 새 규격으로 바꾸고, 검증기가 **옛 규격이면 실패**하게 한다(예산 상한 글자당 ≤ 300, 이름·폭 38cm ±5%).
2. Blender 를 직접 실행한다 (사용자 환경): `TMPDIR=$PWD/.harness/state/tmp /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python tools/orion-glb/make_orion.py`. 출력 `Assets/Models/ORION.glb` 와 `web/orion/ORION.glb` 는 동일 파일.
3. 이 이름 변경에 걸린 곳을 찾아 같이 고친다: `Assets/Tests/EditMode/OrionGlbTests.cs`, `web/orion/src/loader.js`·`web/orion/test/*`·`tools/web-orion/samples/orion.export.json` 의 글자 이름, `.harness/briefs/*.md`·`design` 문서의 이름은 **고치지 말고** 목록으로 note 에 남긴다. (`design/tokens/orion.tokens.json`·`docs/`·`board/` 는 정본 — 수정 금지.) 웹 쪽 이름이 바뀌면 `orion.export.json` 스키마의 `letters[].name`(현재 `O1 R I O2 N`)도 바뀌므로, **스키마와 샘플을 바꾸기 전에 카드 note 로 오케스트레이터 확인을 받는다** (Unity 내보내기와 공유 계약). 확인 전에는 글자 이름 변경을 GLB 안에서만 하고, 웹 로더에 `Letter_` 접두어를 벗겨 매칭하는 최소 호환 한 줄을 두는 방식으로 진행해도 좋다 (오케스트레이터에게 note).
4. 검증을 돌린다: `sh tools/orion-glb/verify.sh --no-unity` (Unity 는 오케스트레이터가). 렌더 한 장(Blender 로 정면 PNG)을 카드 assets 에 `orion-v2-preview.png`.
5. `npm run studio -- check <카드 id> --app orion --as "Gemini Flash" -- "sh unity/mood-picker/tools/orion-glb/verify.sh --no-unity"`.

## 건드려도 되는 곳 (touch.txt 에도 추가됨)
`tools/orion-glb/**`(vendor 제외), `Assets/Models/ORION.glb(.meta)`, `web/orion/ORION.glb`, `Assets/Tests/EditMode/OrionGlbTests.cs`, `web/orion/src/loader.js`·`web/orion/test/**`(이름 호환 한 줄과 테스트 기대값만)

## 하지 말 것
커밋·푸시·HANDOFF·done, 정본(토큰 JSON·docs·board) 수정, 설계 이유 주석(주석 린트 있음), 다른 카드, 다른 도구에 Antigravity 로그인 연결.

## 보고 (마지막 메시지)
카드 / 한 일 / 돌린 검증(명령) / 결과(핵심 줄) / 증거: L1 / 못 한 것 / 고친 파일 목록 / 이름 변경이 닿는 다른 곳 목록
