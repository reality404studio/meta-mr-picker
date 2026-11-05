# HANDOFF — ORION (Meta VR Start 2026 출품작)
updated: 2026-11-06 01:14 KST · by: codex

## 목표 (한 줄)
현실에서 떠 온 재질로 3D 웹 타이틀을 만드는 공간 디자인 툴. 데모 흐름 v2 (handoff/2026-11-05-orion-demo.md §2) 를 Quest 3 실기로 찍을 수 있게 만든다. 제출 11-18 저녁 KST.

## 지금 카드
- 없음. build-7c8f-build-f55b 완료 (맥북 앵커 코어). 맥북 앞 실기 확인 카드(build-7c8f-verify-a375)는 사람 카드로 대기

## 다음 할 일 (최대 3)
1. 보드에 §4 나머지 P0 카드 깔기 후 다음 카드 선택 (풀아웃 → 손 상태 기계 → 글자 바르기 순)
3. 보드에 §4 나머지 P0 카드 깔기 (풀아웃, 손 상태 기계, 디밍, 재질 그대로 룩, 스포이드·작업대, 웹 로더, 던지기)

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
