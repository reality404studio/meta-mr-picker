# 레시피: 웹 디자인 토큰 반영 (Fable · Cursor)

카드: `build-fd7e` 의 하위 과업 "[Fable] 웹 토큰" (`npm run studio -- show build-fd7e --app orion` 로 id 확인, 작업대 루트에서).
먼저 `.harness/briefs/web-orion-fable.md` 의 §1(건드려도 되는 곳)·§2(결정)·§5(일하는 순서)를 다시 읽는다. 아래 내용이 그와 다르면 **이 파일이 이긴다** (2026-11-06 Argon 확정 토큰).

## 바뀐 것
- 정본 토큰: `design/tokens/orion.tokens.json` (읽기 전용, canon). 사람 + Gemini Argon 이 확정한 값. 상세 설명: 작업대 `studio/apps/orion/nodes/interface-design/README.md` §8, §12 (경로는 이 레포 밖: `../../studio/apps/orion/nodes/interface-design/README.md`)
- 배경: 이전 `#16171b` → **`#0E1322`** (`color.web.stage`). 글자 `#F0EEE6`, 받침 `#15317C`, 코발트 `#2559B4`, 빨강 `#D23A2C`는 한 곳에만.
- 글자 크기: `type.spec.mono` (IBM Plex Mono 500) 만 쓰고 Thin/Light/Italic 금지. 최소 시야각 규칙은 맥북 화면 촬영용이라 본문 24px 유지.
- 에디터·히어로에 **[RAW TEXTURE / 2-TONE CEL] 토글**을 추가한다 (히어로 모드 우하단 근처). RAW = 지금의 재질 그대로, 2-TONE CEL = 같은 칩에서 `_ToneSteps=2` 처럼 2톤 램프로 뭉개는 웹 로더 스타일 옵션 (D13: 셀은 웹 로더 스타일 옵션).
- 히어로 하단에 스와치 배지 3개(윤슬·털·그라데이션 칩 번호 `01 02 03`, 큰 번호 `Unbounded 900`·코발트 사각 박스)와 `<orion-hero src="./orion-export.json" />` 한 줄 코드, `[DOWNLOAD .GLB]` 버튼(현재 `DOWNLOAD GLB`)로 맞춘다. 참고 시안: `../../studio/apps/orion/nodes/interface-design/assets/web_editor_loader_sheet.png`.

## 하는 일
1. `web/orion/src/tokens.js`: `design/tokens/orion.tokens.json` 을 **복사본** `web/orion/tokens.json` 으로 두고 import (빌드 없이, import map/fetch). `settings.js` 의 색·크기·시간 값이 이 토큰에서 나오게 한다. 값을 `settings.js` 에 하드코딩으로 남기지 않는다.
2. 복사본이 정본과 다르면 테스트가 실패해야 한다: `web/orion/test/tokens.test.mjs` — 키별 동일(또는 바이트 동일), 배경색 `#0E1322`, 토글 두 값이 화면에 존재(headless Chrome), `settings` 가 토큰을 참조.
3. `web/orion/test/shots/` 의 editor/hero PNG 를 새 토큰으로 다시 찍는다.

## 성공 조건
> node 테스트: web 토큰 == design/tokens/orion.tokens.json, 배경색 #0E1322, [RAW TEXTURE / 2-TONE CEL] 토글 두 값이 화면에 존재. 기존 47개 테스트는 그대로 통과.

검증: 작업대 루트에서 `npm run studio -- check <하위 과업 id> --app orion --as "Cursor Fable" -- "cd unity/mood-picker/web/orion && node --test test/*.test.mjs"` (글롭 형식. 폴더 인자 금지).
끝나면 note 를 남기되 done 은 하지 않는다 (오케스트레이터가 같은 명령을 다시 돌려 done). 커밋: 네 파일만, 메시지 `<하위 과업 카드 id>: <관찰 가능한 변화>`, 푸시 금지. HANDOFF 「최근 증거」 한 줄. 보고 형식은 이전 레시피와 같다.
