# ⬜ 루프 온 모드: 물결 루프

[← 보드로](../BOARD.md)

- **목표:** 11-08~09 · 토글을 켜면 움직이는 재질을 짧은 반복 루프로 담는다. 응시 시간 = 진하기·루프 길이 (MVP 다음 순위)
- **산출물:** LoopCapture 스크립트 + 스프라이트 시트
- **기다림:** Catch: 채집 원 · 크롭 · 2톤

## 하위 과업

- [ ] ▶ **약 2초, 10~15fps 크롭 프레임 수집** · 에이전트 · 구현
  <br><sub>MaxFramerate 낮춰 요청, AsyncGPUReadback, 메모리 상한</sub>
- [ ] **끝-시작 크로스페이드 + 플립북** · 에이전트 · 구현
  <br><sub>텍스처 배열 또는 시트. 셀 셰이더 플립북 입력 형식과 맞춘다</sub>
- [ ] **응시 시간 → 진하기 · 루프 길이** · 에이전트 · 구현
  <br><sub>3초 옅게, 20초 깊게. 수치는 실기에서 조정</sub>
- [ ] **실기: 물결 루프가 이음매 없이 도는지** · 사람 · 검증
  <br><sub>증거: 빌드 ID가 찍힌 logcat 발췌 + 스크린샷을 첨부해야 done</sub>

## 메모

HANDOFF §4-1, §5 (LoopCapture). 차별점의 핵심이지만 MVP 다음 순위. 셀 단순화가 흔들림과 저화질을 덮는다.
- 프레임 읽기는 AsyncGPUReadback, 루프 메모리 상한을 둔다 (72fps 성능 규칙)

---
**Claude Code** · 2026-11-03 13:56

2026-11-03: three.js 데모의 포팅 기준이 meta-mr-picker PR #1 (`reference/texture-lab/`, 미머지)에 있음. 연결표는 그 README '보드 과업과 연결'. Assets 에 넣는 건 PigmentCore/*.cs + PigmentCommon.hlsl 만. shader-lab 7모드는 탐색일 뿐 (셀 룩 위에 얹을지는 사람 결정).
