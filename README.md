# Gaze · Catch · Dab

출퇴근길에 넋 놓고 바라본 빛과 재질을 손가락 사이로 물감처럼 담아 오고, 집에 와서 손끝으로 아무 오브제에나 "푝" 찍어 바르는 Quest 패스스루 앱.
Meta VR Start Developer Competition 2026 출품작 (Productivity · New Experience).

- 기기: Meta Quest 3 / 3S, 핸드트래킹만 (컨트롤러 없음), 72fps
- 엔진: Unity 6 (6000.0.66f2+), Meta XR SDK, MRUK Passthrough Camera Access, XR 플러그인 OpenXR

## 할 일 보드

**[board/BOARD.md](board/BOARD.md)** — 단계·과업·하위 과업, 지금 할 일, 흐름도. 작업대 보드를 내보낸 읽기 전용 사본이에요 (`scripts/sync-docs.sh` 가 갱신).

## 이 레포와 작업대

이 레포는 **앱 코드(Unity 프로젝트)** 를 담아요. 할 일 보드, 결정, 가드레일의 정본은 작업대 레포
[my-mr-studio](https://github.com/reality404studio/my-mr-studio) 의 `studio/apps/mood-picker/` 와 `studio/shared/` 에 있어요.
작업대 안에서는 `unity/mood-picker/` 위치에 이 레포를 둬요.

`docs/` 와 `board/` 는 읽기용 사본이에요. 고치지 말고 작업대에서 고친 뒤 `scripts/sync-docs.sh` 로 갱신해요.

| 사본 | 내용 |
| --- | --- |
| `docs/HANDOFF.md` | 컨셉, 대회 조건, 기술 계획 |
| `docs/BRIEF.md` | 앱 개요 한 장 |
| `docs/DECIDED.md` / `docs/REJECTED.md` | 사람이 정한 것 / 안 하기로 한 것 |
| `docs/UNITY_GUARDRAILS.md` | Unity 실기 함정 (패스스루 안 보임 등). **Unity 작업 전에 읽기** |

## 참고 구현

**[reference/texture-lab/](reference/texture-lab/README.md)** — 루프 자동 탐색, 이음매 없는 타일, 플립북 규칙, 셰이더 함수의 참고 구현 (웹 프로토타입 + 오프라인 도구 + 검증된 C#/HLSL 포팅). catch · loop · dab · export 과업 전에 읽기.

## 빌드

- Development Build 로 실기 확인. 빌드 게이트(`Assets/Editor/BuildGate`, 셋업 단계에서 추가)가 검사에 실패하면 빌드가 막혀요
- 앱 첫 로그의 `[MP] build=<해시> <시각>` 이 실기 확인의 기준이에요
