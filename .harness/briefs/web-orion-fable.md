# 레시피: ORION 웹 쪽 (Fable · Cursor)

카드: `build-a3a1-build-8c28` (보드 `--app orion`, 작업대 루트 `my-mr-studio`에서 `npm run studio -- ...`).
역할: 맥 브라우저에서 도는 **three.js 에디터 + 로더**와 **Node 로컬 서버**. Unity(Quest) 쪽은 Astra가 맡고, 여기서는 건드리지 않는다.
깊이: medium. 파일이 Unity와 완전히 분리돼 있어서 지금 바로 시작해도 된다.

## 0. 시작 전에 읽을 것 (순서대로)
1. `AGENTS.md`, `HANDOFF.md` (이 레포 루트)
2. `handoff/2026-11-05-orion-demo.md` 의 §0, §2(Before·Throw Back 장면), §5(두 모드), §6-4(두 렌더러의 룩 차이)
3. `docs/DECIDED.md` 에서 `[orion]` D1·D13·D14·D15·D16·D17
4. 작업대 `AGENTS.md` 「증명 게이트」 — 카드는 `check`가 통과 로그를 남겨야 done 이 된다

## 1. 건드려도 되는 곳 / 안 되는 곳
- **된다:** `web/orion/**` (ORION.glb 는 읽기 전용 복사본), `tools/web-orion/**`(테스트·샘플 데이터), `.harness/briefs/` (이 파일은 고치지 말고 메모는 카드 note 에)
- **안 된다:** `Assets/**`, `Packages/**`, `docs/**`, `board/**`, `tools/orion-glb/**`(three 벤더는 읽기·복사만), `.harness/canon.txt` 의 모든 경로
- 새 폴더가 필요하면 `web/orion/` 아래에 만든다. 루트 `package.json`을 새로 만들지 말고 `web/orion/package.json` 하나만 쓴다.

## 2. 이미 결정된 것 (바꾸지 않는다)
- 순수 three.js. **Vite·React·react-three-fiber 쓰지 않는다** (D16). import map + 벤더 복사.
- three 는 `tools/orion-glb/vendor/three/` 의 **0.170.0** 을 `web/orion/vendor/three/` 로 복사해서 쓴다 (CDN 금지, 오프라인에서 열려야 함). GLTFLoader 의 `from 'three'` 는 import map 이 해결한다.
- npm 의존성은 **서버 WebSocket 하나**만 허용: `ws` (exact 버전 핀, 출시 2주 이상·주간 다운로드 확인 후 선택). 설치는 반드시 `npm install --ignore-scripts`. 그 외 의존성 추가 금지. `ws` 가 꺼려지면 `node:http` upgrade + 손수 프레임 인코딩도 허용(테스트로 증명).
- 글자 룩 = **재질 그대로** (D13): 담은 칩 타일을 트라이플래너로 입히고 2톤으로 뭉개지 않는다. 안 칠한 글자 = 블렌더 기본 클레이 `#E7E7E7` 무광 (D14).
- 글자 메시는 `ORION.glb` 안의 노드 `O1 R I O2 N` 5개 (이름 바꾸지 않는다).
- UI 글자: IBM Plex Mono(라벨), 제목에 쓰는 글자는 이미 GLB 안. 색: 흰 제도선 `#F0EEE6`, 배경 어두운 단색 (`#16171b` 근처), 빨강 `#D23A2C` 한 점. 장식 금지. 큰 글자·고대비 (맥북 화면을 헤드셋 패스스루로 촬영하므로 **작은 글자는 안 읽힌다**: 시험 결과 `build-dce1`). 본문 글자 최소 24px, 슬라이더 라벨은 더 크게.
- 수치(색·크기·속도)는 베이스라인이다. 한곳(`web/orion/src/settings.js`)에 모은다.

## 3. 만들 것

### 3-1. 데이터 계약 `orion.export.json` v1 (Unity 내보내기 카드와 공유)
이 스키마가 Unity ↔ 웹의 계약이다. 필드를 바꾸려면 카드 note 에 이유를 적고 `web/orion/schema/orion.export.schema.json` 과 테스트를 같이 바꾼다.
```json
{
  "version": 1,
  "exportedAt": "2026-11-08T21:14:00+09:00",
  "word": { "tracking": 0.5, "thickness": 1.0, "rotationYDeg": 0 },
  "letters": [
    { "name": "O1", "offset": [0, 0, 0],
      "paint": { "kind": "chip", "id": "paint_20261104_140211_002",
                 "lightTone": "#F5EDE0", "midTone": "#A69989", "shadowTone": "#5C5449",
                 "lightDir": [0.6, 0.65, -0.45],
                 "tile": "chips/paint_20261104_140211_002/tile.png",
                 "loop": null } },
    { "name": "R",  "offset": [0, 0, 0], "paint": { "kind": "clay" } }
  ]
}
```
- `letters` 는 정확히 5개, 이름은 `O1 R I O2 N`. `offset` 은 글자 원점 기준 미터 단위 [x,y,z] (글자별 간격·높낮이 조절용, D5).
- `paint.kind`: `chip`(담은 칩, 타일 PNG 있음) | `gradient`(마블링 작업대에서 구운 PNG, 타일 필드 동일) | `clay`(안 칠함). `clay` 는 색·타일 필드를 갖지 않는다.
- `paint.loop` 는 지금은 `null` 만 허용 (루프 칩은 보류). 값이 있으면 경고만 하고 정지 타일로 그린다.
- 타일 PNG 경로는 JSON 파일 위치 기준 상대 경로. `lightDir` 는 월드 방향(3 float).
- 샘플: `tools/web-orion/samples/` 에 `orion.export.json` 과 `chips/` 를 만든다. 칩은 실기에서 담은 진짜 파일을 쓴다 — `studio/apps/mood-picker/nodes/catch-verify-5047/assets/paints/*/tile.png` + `paint.json` (작업대 레포, 이 레포 밖 경로이므로 필요하면 복사만 한다. 레포 루트의 두 단계 위: `../../studio/...`). 윤슬 칩은 `reference/webxr-reaction/assets/yunseul_tile.png` (앱 레포 `claude/hopeful-pasteur-9n9kuz` 브랜치에 있음, 필요하면 `git show origin/claude/hopeful-pasteur-9n9kuz:reference/webxr-reaction/assets/yunseul_tile.png > ...`).

### 3-2. 로더 `web/orion/src/` (브라우저와 Node 양쪽에서 테스트 가능하게 나눈다)
- `schema.js` — 위 JSON 검증 (순수 JS, 오류 메시지는 필드 경로 포함)
- `material.js` — 칩 하나 → 셰이더 uniform 객체 (순수 함수: 색 문자열 파싱, lightDir 정규화). 트라이플래너 `ShaderMaterial`(GLSL ES 3.0): 오브젝트 공간 triplanar 샘플 → lightTone/midTone/shadowTone 3단 부드러운 램프 + lightDir (단계 폭은 settings), 외곽선은 켜고 끄기 옵션(뒷면 확대 메시, 색 `#15317C`). 클레이는 `MeshStandardMaterial` 무광 `#E7E7E7`.
- `loader.js` — GLB 로드, 노드 5개 찾기, JSON 적용, `word` 값(트래킹=글자 간격 스케일, 두께=Z 스케일, 회전=Y)과 `letters[].offset` 적용. 순수 로직은 `material.js`·`layout.js`(글자 위치 계산) 로 분리해 Node 테스트.
- `index.html` — 에디터 화면: 가운데 3D(ORION), 아래 큰 슬라이더 3개(TRACKING · THICKNESS · ROTATION), 재질 프리셋 버튼(샘플 칩 3개 + CLAY), 우상단 연결 상태 점(서버·Quest). 슬라이더 값은 서버로 `{type:"slider", key, value}` 전송(맥 → Quest).
- **Before 장면용 모드** `?mode=editor`: 풀아웃 전의 평범한 글자(재질 없음 = 클레이) + 슬라이더. 영상에서 2배속으로 만질 화면이다. **Throw Back 모드** `?mode=hero`: 던진 결과를 받아 같은 재질의 ORION 이 천천히 회전, 구석에 로더 코드 한 줄, `Download GLB` 버튼(GLTFExporter 로 현재 상태 내보내기. GLTFExporter 는 같은 벤더 버전의 addons 에서 복사).

### 3-3. 서버 `web/orion/server/server.mjs`
- `node web/orion/server/server.mjs [--port 5174] [--dir ./inbox]`
- 정적 파일 제공(`web/orion/` 전체) + WebSocket 중계(`/ws`): 접속자는 `hello {role:"mac"|"quest"}` 를 먼저 보낸다. 한쪽 메시지를 반대 role 에 전달. 마지막 `slider` 값은 저장해서 새 접속자에게 재전송.
- 메시지: `{type:"slider",key,value}` (맥→Quest) · `{type:"pullout",t}` (Quest→맥: 풀아웃 시작) · `{type:"export",path}` (Quest→맥: 던지기 끝, `path` 는 inbox 안 파일).
- `POST /upload?name=...` — Quest 가 `orion.export.json` 과 칩 PNG 들을 올리는 곳. 파일명은 화이트리스트(`[A-Za-z0-9._-]`, 슬래시 금지), 크기 상한(PNG 5MB, JSON 200KB), `--dir` 밖으로 못 나가게. 업로드 끝에 `export` 이벤트를 맥에 중계. `inbox/` 는 `.gitignore`.
- 비밀값·인증 없음(로컬망 전용). 기본 바인딩 `0.0.0.0` 이므로 시작할 때 쓰는 주소(LAN IP)를 콘솔에 출력하고 "이 서버는 로컬 개발용" 한 줄을 출력한다.
- `ws://`·`http://` 는 Quest 쪽 Android cleartext 설정이 필요하다는 점은 Unity 카드(`build-a3a1-build-c0c8`)가 처리한다. 여기서는 `README.md` 에 한 줄로 적어 둔다.

## 4. 성공 조건 (카드 `build-a3a1-build-8c28`)
> node 테스트: JSON+PNG → 재질 적용 headless 로드 OK, 서버 중계 에코. 같은 칩 PNG 로 Unity·웹 룩 나란히 비교 PNG (리스크 §6-4)

쪼개서 만든다. 아래 둘은 이 카드 완료에 **필수**, 셋째는 Unity 쪽 셰이더 카드(`build-3e76`)가 끝난 뒤에 이어서 한다.
1. **node 테스트** (`web/orion/test/*.test.mjs`, 실행은 `node --test web/orion/test/`)
   - `schema`: 샘플 통과, 이름 5개 아님·kind 불일치·타일 경로 없음·`clay` 에 색 필드 있음 → 각각 오류 메시지에 필드 경로가 들어 실패
   - `material`: 색 파싱·lightDir 정규화·클레이/칩 분기 (경계값: 대소문자 hex, 잘못된 hex, 영벡터 lightDir)
   - `layout`: 트래킹·두께·오프셋 → 글자 위치 (순수 수식, 글자 5개 폭 합 = 0.615m×트래킹 기준)
   - `loader` (headless): `three` + GLTFLoader 로 `web/orion/ORION.glb` 를 파싱해 Mesh 5개, 샘플 JSON 적용 뒤 각 글자 material 종류가 기대와 같다
   - `server`: 서버를 임시 포트로 띄우고 Node 내장 `WebSocket` 클라이언트 두 개(mac·quest)로 `slider` 중계, 새 접속자 재전송, 잘못된 업로드 이름(`../x`) 거절, 크기 초과 거절, 업로드 → `export` 이벤트 중계
2. **화면 증거 (L2)** headless Chrome 으로 `?mode=editor` 와 `?mode=hero` 를 각각 1280×720 PNG 로 저장 (`web/orion/test/shots/`). 큰 글자가 읽히는지 눈으로 확인하고 카드 note 에 한 줄.
3. **룩 비교 PNG** (나중): 같은 칩 PNG·같은 lightDir 로 Unity 배치 렌더(`unity/mood-picker/Library/CelRender/` 등 Astra 쪽 결과) 와 웹 렌더를 나란히 놓은 한 장. Unity 쪽 결과가 나오면 오케스트레이터가 알려 준다. 그때까지는 웹 쪽 렌더만 `web/orion/test/shots/look-web.png` 로 남기고, 비교는 열린 항목으로 note 에 적는다.

## 5. 일하는 순서 (카드 한 장 = 한 번에 하나씩, 매번 `check`)
1. `npm run studio -- show build-a3a1-build-8c28 --app orion` 로 카드 확인 → `status build-a3a1-build-8c28 doing --app orion --as "Cursor Fable"`
2. 아래 순서로 만들되, **각 단계가 끝날 때마다** 해당 `node --test` 를 돌려 통과를 확인:
   스키마·샘플 → material/layout 순수 로직 → 로더 → 서버 → index.html·모드 두 개 → 화면 증거
3. 전부 통과하면 작업대 루트에서:
   `npm run studio -- check build-a3a1-build-8c28 --app orion --as "Cursor Fable" -- "node --test unity/mood-picker/web/orion/test/"`
   (명령 인용에 주의: `--app …` 는 `--` 앞에 둔다)
4. proof 가 되면 카드 note(`npm run studio -- note build-a3a1 "무엇을 했고 무엇이 남았는지" --app orion --as "Cursor Fable"`)를 남기고 `status … done` 은 **하지 않는다** — 증명 → done 은 오케스트레이터가 같은 명령을 다시 돌려서 한다(이 레포 규칙: 검증은 이 세션이 돌린다, 완료는 다른 눈이 확인).
5. 커밋: `unity/mood-picker/` 안에서 네가 만든 파일만 스테이징. 메시지 `build-a3a1-build-8c28: <관찰 가능한 변화>`. 푸시하지 않는다.
6. `HANDOFF.md` 의 「최근 증거」에 한 줄만 덧붙이고(지금 카드 칸은 건드리지 않음), 마지막 보고를 이 형식으로:
```
카드: build-a3a1-build-8c28
한 일: <바뀐 관찰 가능한 동작>
돌린 검증: <명령>
결과: <통과/실패와 핵심 줄>
증거: L1 | L2 (경로)
못 한 것: <없으면 '없음'>
```

## 6. 하지 말 것
- Unity 파일·씬·셰이더 수정, 스키마를 Unity 쪽과 말 없이 바꾸기
- 의존성 추가(`ws` 외), `npm install` 에서 `--ignore-scripts` 빼기, CDN 스크립트 로딩
- 설계 이유를 길게 주석으로 적기 (주석은 거의 쓰지 않는다. 이유는 카드 note 에)
- 작은 글자 UI, 장식(그라데이션 배경·그림자 남발), 한글 UI (UI 는 영어, D 결정)
- 다른 카드 시작

## 7. 막히면
- 스키마·계약이 모자라면: 만들지 말고 카드 note 에 질문을 적고 멈춘다 (오케스트레이터 = Claude Code 가 HANDOFF 를 보고 답한다).
- 사람에게 직접 물을 것: 없음. 취향(색·글자 크기)은 베이스라인으로 두고 settings.js 한 곳에 모은다.
