# build-4a39 · 42ba43f-dirty-1110-204027 · L3 (사람 판정 L4 대기)
- 빌드: MoodPickerBuild.BuildSplatterApkBatch, gather 브랜치 42ba43f + 미커밋 변경 (셰이더·러너·손 입력·조율 패널·채집)
- 기기 Quest 3S 340YC10G9707W0, 착용 상태, 사람이 직접 만지는 중에 관찰
- 관찰: 기기 로그 build= 일치, 예외 0줄, 스크린샷 shot-play.png 에 방 + 글자 A + Immersive Debugger 패널(1 Spring ~ 5 Summon 범주)이 함께 보임
- 시뮬레이션: 손이 닿을 때 1.6~1.9ms (충돌체 12개), 손을 떼면 잠듦 (ms=0.00, awake=0.00), 다시 닿으면 깸. 왼손 핀치 토글 로그 확인
- VrApi: 72~73fps, CPU/GPU 레벨 2/2, App 6.5~7.5ms (패널 표시 중), Temp 22→28C (실행 6분, 기존 빌드와의 비교 측정은 아직 안 함)
- 앞선 두 빌드: 202921 은 패널용 오버레이 셰이더가 빠져 매 프레임 예외 → OVROverlayCanvasSettings 에셋을 씬 구성에서 만들어 해결. 203637 은 예외 없음, 로그 보강 전
- 미확인: 기존 빌드 대비 발열 비교, 사람 느낌 판정, 패널 손 조작 편의, 카메라 질감(8 Catch), 맥에서 tuning.json 밀어넣기
