# HANDOFF — ORION (Meta VR Start 2026 출품작)
updated: 2026-11-06 01:14 KST · by: codex

## 목표 (한 줄)
현실에서 떠 온 재질로 3D 웹 타이틀을 만드는 공간 디자인 툴. 데모 흐름 v2 (handoff/2026-11-05-orion-demo.md §2) 를 Quest 3 실기로 찍을 수 있게 만든다. 제출 11-18 저녁 KST.

## 지금 카드
- id: build-73cd-build-d0e0 (보드 `--app orion`) · 상태: 대기 → Codex(Astra) · 필요한 증거: L1
- 할 일: 풀아웃. MacAnchor 화면 사각형 안을 검지로 탭하면 글자 5개(O1 R I O2 N)가 시차 0.15s 로 화면 평면에서 앞으로 나와 두께 0→1 로 부풀며 호버링 위치에 선다. 새 폴더 Assets/Scripts/PullOut/ (Core 순수 C# + Runtime, asmdef)
- 성공 조건: EditMode — 사각형 안 탭 1회 → 이벤트 순서 O1→R→I→O2→N, 사각형 밖 탭·손떨림 10회 중 오탐 ≤1, 애니메이션 완료 시 위치 = 호버링 설정값 ±1cm
- 건드려도 되는 곳: .harness/touch.txt
- stop: 테스트 코드와 컴파일 통과 후 커밋 보류하고 보고 (Unity 테스트 실행은 이 세션이 한다)

## 다음 할 일
1. build-be9a 손 상태 기계 → build-3e76 글자 룩 셰이더 → build-e463 글자별 버킷 (11-10 게이트: 풀아웃→채집→바르기 한 번에)
2. build-8a30 스탬프 채집, build-5e10 스포이드·작업대, build-1b00 단독 패널·디밍, build-a3a1 던지기·내보내기·웹
3. 사람 실기 카드: build-7c8f-verify-a375 (맥북 앵커), build-73cd 실기

## 정본 / 건드리지 말 것
- 결정: 작업대 `studio/shared/DECIDED.md` `[orion]` D1~D17 (사본 docs/DECIDED.md). 버린 안: REJECTED.md
- 원천 문서: handoff/2026-11-05-orion-demo.md
- docs/, board/ 는 사본 (편집 차단: .harness/canon.txt)
- 컬러피커는 main 브랜치에 보존. orion 에서 컬러피커 코드(Catch, Portal, Dab)는 재사용하되 지우지 않는다
- Unity 는 이 폴더에서만 연다 (git worktree 금지: Library 11GB, 디스크 여유 약 7GB)

## 기기·환경
- Quest 3 (panther) · USB · 패키지/경로는 .harness/device.env
- Unity 6000.3.27f2, Unity CLI `~/.unity/bin/unity` (test/build/recompile), Blender.app 배치모드
- 동시에 Unity 를 여는 프로세스는 하나만: `pgrep -f "projectPath .*unity/mood-picker"` 확인 후

## 최근 증거
- 2026-11-06 build-7c8f-build-f55b: 증거 L1 (파일 게이트·컴파일 한정). `python3 -B Assets/Scripts/MacAnchor/Verification~/verify_compile.py` exit 0: `files: PASS`, Hands/DeskKeyboard/Core/Runtime 및 전체 EditMode 15개 소스 Roslyn PASS. 신규 NUnit 29케이스를 포함한 테스트 실행은 미확인
- 위 검증 로그·rsp·DLL: `/var/folders/n_/8p5lyp65519c7k__7vz83jgm0000gn/T/orion-mac-anchor-check-r1uxozci/`. `git diff --check` 통과, touch/canon 경로 검사 통과. 검증 스크립트는 기존 GateFileChecks를 그대로 임시 디렉터리에서 컴파일·실행하며 Unity 테스트 통과를 주장하지 않음
- 제한: `mr status`는 ps 권한 오류, `pgrep -f 'projectPath .*unity/mood-picker'`는 exit 3. `sh ../shared/BuildGate/Preflight~/preflight.sh .`는 공용 Checker/obj 쓰기 권한에서 실패(Assembly-CSharp 1개·Editor 9개 컴파일은 PASS); 위 임시 폴더 검증으로 파일 게이트·새 asmdef 컴파일 확인
- 2026-11-06 build-402f-make-098a: 증거 L1. `sh tools/orion-glb/verify.sh` 통과 (GLB 파싱·글자 5개·구멍 O1/R/O2=1·크기·three.js 로드·Unity EditMode OrionGlbTests 1/1). 로그 studio/apps/orion/nodes/build-402f-make-098a/assets/
- 2026-11-06 build-dce1: 패스스루는 metacam 스크린샷에 담김, adb screenrecord 는 검은 영상 → 영상은 MQDH 캐스팅 녹화로 (사람 확정). 맥북 화면은 큰 글자·슬라이더만 읽힘
- glTFast 6.20.0 추가 (사람 승인 2026-11-06). Blender 는 Codex 샌드박스 밖(Claude 쪽)에서 돌림: `TMPDIR=.harness/state/tmp /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python tools/orion-glb/make_orion.py`
- 컬러피커 마지막 빌드 mvp-dev2 6d25a43-1104-151623 (미실기, main)

## 열린 질문 (사람에게)
- 취향 앵커 3줄(AGENTS.md)은 DECIDED D6·D13·D14 에서 옮겨 적음 — 사람이 고쳐도 됨
