# 라우팅 규칙 (누가 · 무엇을 · 어떻게 가져가는가)

이 파일은 **어떤 세션의 에이전트든**(Claude Code, Codex, Cursor, Gemini/Antigravity, 이후 새 세션) 일을 받거나 넘기기 전에 읽는다. 사람은 **결정만** 한다. 이 규칙이 모호하면 사람에게 묻지 말고 가장 안전한 쪽(범위를 좁히고, 잠금을 확인하고, 보고)으로 간다. 근거: 사람 결정 2026-11-06 (작업대 `studio/shared/DECIDED.md` 「라우팅」).

## 0. 원칙 5개
1. **파일이 계약이다.** 세션끼리 메시지로 일을 주고받지 않는다. 일감 = 보드 카드 + `.harness/briefs/<카드>.md`, 결과 = 커밋 + 카드 note + 마지막 보고.
2. **카드 상태 `doing` 이 잠금이다.** 가져가기 전에 `npm run studio -- list --app orion` 으로 `doing` 이 아닌 카드인지 본다. 이미 `doing` 이면 건드리지 않는다.
3. **검증은 만든 쪽이 돌리고(check), 완료는 오케스트레이터가 같은 명령을 다시 돌려 옮긴다.** 구현 에이전트는 `done` 으로 옮기지 않는다 (`proof` 까지).
4. **정본은 읽기만.** `.harness/canon.txt` 에 있는 것(토큰 JSON, 인수인계 원문, 폰트 라이선스, docs/·board/ 사본)은 편집 금지(훅이 막는다).
5. **로그인은 각 도구 안에서만.** Antigravity/Gemini 구독 로그인(OAuth)을 Claude Code·Codex·Cursor 등 다른 도구에 연결하지 않는다 (Google 약관 위반·계정 정지 사례). 다른 도구에서 Gemini 를 쓰려면 API 키(AI Studio/Vertex), 키는 Keychain + `envrun`, 파일·명령에 값 금지.

## 1. 역할 표 (기본 배정)
| 역할 | 에이전트 | 깊이 | 메모 |
|---|---|---|---|
| 오케스트레이터: 카드 쪼개기·브리핑·HANDOFF·DECIDED·검증 재실행·done·커밋 | Claude Code (Sonnet, 큰 갈림길만 Opus) | low~medium | HANDOFF.md 의 **유일한 작성자** |
| Unity C#·셰이더·손 상태·테스트 (복잡한 로직) | Codex CLI · Astra | high(셰이더·상태기계·투영) / medium | 샌드박스: Unity·Blender 실행 불가 → 코드·테스트까지 |
| 웹(three.js·Node 서버)·소규모 코드 | Cursor · Fable, 한도 시 Gemini Flash | medium | 파일 `web/orion/**`, `tools/web-orion/**` |
| Blender 스크립트 **실행 포함**·에셋 제작·소규모 C#/JS | Gemini Flash (Antigravity) | medium | 사용자 환경에서 도니 Blender·Unity CLI 가능. 아래 §3 잠금 |
| 디자인 토큰·시안·문서·영상/룩 판정 | Gemini Argon (Antigravity) | — | 결과는 작업대 interface-design 에 쓰고 DECIDED 는 사람 결정만 |
| 로그 요약 | logcat-reader (Haiku) | low | |
| 다른 눈 (큰 덩어리만) | 구현과 **다른 회사** 모델 | medium | Astra 구현 → Claude 리뷰, Claude 구현 → Astra/Flash 리뷰 |

## 2. 카드 → 에이전트 라우팅 (위에서부터 맞는 첫 줄)
1. 정본 토큰·결정·보드 구조를 바꿔야 한다 → **사람 결정**(Argon 은 제안·시안까지). 에이전트가 정하지 않는다.
2. Blender 를 **실행**해야 한다 / 에셋 파일(glb·png)을 만들어야 한다 → **Gemini Flash(Antigravity)**. Codex 는 스크립트 작성만, 실행은 오케스트레이터나 Flash.
3. 셰이더·손 상태 기계·투영·히스테리시스처럼 **되돌리면 조용히 깨지는 로직** → **Astra high** (+ 테스트로 인코딩).
4. 웹/JS, 설정·토큰 연결, 얇은 Runtime, 문서·스크립트 → **Fable → (한도) → Flash → (한도) → Astra medium**.
5. 헤드셋·실기 → **사람 카드**(`by: human`). 오케스트레이터가 설치·logcat·스크린샷을 준비하고 "준비" 신호를 기다린다.
6. 일감이 한 문장 성공 조건으로 안 적힌다 → 쪼개거나(sub) 오케스트레이터가 브리핑을 다시 쓴다. 에이전트는 추측으로 진행하지 않는다.
카드 제목 끝 `[P0 · Astra]` 같은 태그와 `npm run studio -- set <id> --owner "<이름>"` 로 담당을 적는다. 태그와 다른 에이전트가 가져가려면 카드 note 에 이유를 한 줄 남긴다(한도·부재 등).

## 3. 자원 잠금 (동시에 하나만)
| 자원 | 확인 | 규칙 |
|---|---|---|
| Unity 프로젝트 | `pgrep -f "projectPath .*unity/mood-picker"` | 나오면 기다린다(최대 30분). 다른 Unity 를 죽이지 않는다. GUI 에디터가 열려 있으면 실행 금지 |
| Blender | `pgrep -fl Blender` | 한 번에 하나. 쓰는 쪽이 카드 note 에 "Blender 사용 중" 적는다 |
| 헤드셋(adb) | 오케스트레이터만 설치·실행 | 구현 에이전트는 설치·실행 금지(요청은 note) |
| git push | 오케스트레이터만 | 지정 브랜치(`orion`)만 자동. main·force 는 사람 승인. 구현 에이전트는 커밋도 하지 않는다(게이트가 done 카드만 허용) |
| HANDOFF.md | 오케스트레이터만 쓴다 | 구현 에이전트는 마지막 보고(최종 메시지)와 카드 note 로 알린다. 예외: 오케스트레이터가 브리핑에서 "HANDOFF 갱신" 을 지시한 경우 |

## 4. 가져가기 프로토콜 (모든 에이전트 공통)
1. **셀프 체크**(사람에게 묻지 않는다): `git status`, `git fetch --all`, 작업대 `studio/shared/DECIDED.md`·`REJECTED.md` 최상단 3개, `npm run studio -- list --app orion`, `design/tokens/orion.tokens.json` 이 최신인지, 남은 프로세스. 결정이 바뀌었으면 브리핑이 낡았다는 뜻 → 작업 전에 note 로 알리고 멈춘다.
2. `npm run studio -- next --app orion --by agent` 에서 **자기 몫 태그** 카드를 고른다(또는 브리핑이 지정한 카드).
3. `.harness/briefs/<카드>.md` 를 끝까지 읽고 `.harness/touch.txt` 범위를 확인한다. 범위 밖 수정이 필요하면 하지 말고 패치를 `/tmp` 에 남기고 보고(승인 요청).
4. `npm run studio -- status <하위 과업 id> doing --app orion --as "<자기 이름>"`.
5. 구현 → 자기 환경에서 가능한 검증을 모두 돌린다 → `npm run studio -- check <id> --app orion --as "<자기 이름>" -- "<검증 명령>"`(`--app`·`--as` 는 `--` 앞에).
6. 카드 note(무엇을 했고 무엇이 남았는지), 마지막 보고(카드/한 일/돌린 검증/결과/증거 L0~L4/못 한 것). 한도·환경 제한으로 못 돌린 검증은 "미확인"으로 적는다.
7. `done`·HANDOFF·커밋은 하지 않는다(§3). 멈춘다.

## 5. 오케스트레이터 절차 (Claude Code 세션이 이어받을 때)
- 시작: 이 파일, `HANDOFF.md`, `AGENTS.md`, DECIDED/REJECTED 최상단, 보드 `list`, `git fetch`. 열린 `doing` 카드와 남은 프로세스 확인.
- 배정: §2 로 카드를 고르고 브리핑을 `.harness/briefs/` 에 쓴 뒤, `touch.txt` 에 경로 추가, 카드 owner 지정, HANDOFF 「지금 카드」 갱신.
- 실행: Codex 는 `codex exec -s workspace-write -C . -o .harness/state/codex-<카드>-last.md "$(cat .harness/briefs/<카드>.md)" < /dev/null` (stdin 닫기 필수). 장시간이면 백그라운드. 한도 오류가 나면 쓴 코드가 남아 있는지 보고 이어서 검증한다.
- 검증: Unity 는 `sh scripts/run-editmode-tests.sh`(그래픽 없는 실행, `[Category("Gpu")]` 제외), GPU 검사는 카드별 `scripts/verify-*.sh`. 환경 문제(EditMode 는 `enabled` 변경 시 OnDisable/OnEnable 을 부르지 않음, 생성자 단계 `MaterialPropertyBlock`, preflight 는 Assets 안 `~` 폴더의 .cs 도 컴파일)는 구현 에이전트가 Unity 를 못 돌려서 생기므로 오케스트레이터가 고친다.
- 마무리: `check` 재실행 → 통과하면 `done` → 커밋(`<카드 id>: <관찰 가능한 변화>`, 푸시는 `orion`) → HANDOFF → 사람에게 결정·실기 요청만 간단히.

## 6. 한도·실패 시 대체 경로
| 상황 | 하는 일 |
|---|---|
| Astra 한도(Codex `usage limit`) | 쓴 코드는 남는다. 오케스트레이터가 이어서 검증·수리. 새 코드가 필요하면 §2-4 에 해당하는 카드는 Flash, 복잡한 로직은 해제 시각까지 대기(그 사이 검증·문서·보드 정리) |
| Fable 한도 | 웹 카드 → Flash |
| Flash 가 못 푸는 카드 | 카드 note 에 막힌 곳을 적고 멈춘다. 오케스트레이터가 쪼개거나 Astra 로 |
| 오케스트레이터(Claude) 한도 | 사람에게 알리고, 구현 에이전트는 §4 를 계속하되 done·커밋 없이 proof 에서 멈춘다 |
| 서로 같은 파일 충돌 | 먼저 `doing` 한 쪽이 이긴다. 나중 쪽은 멈추고 note |

## 7. 사람을 부르는 경우 (이 외에는 부르지 않는다)
실기(헤드셋 착용·관찰), 디자인·경험 결정(선택지 2~5개 제시 후 멈춤), main 푸시·force-push·삭제·외부 패키지 추가 승인, 정본 토큰 변경, 약관·비용·계정 문제.
