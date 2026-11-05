# PullOut — build-73cd-build-d0e0

`Core`는 MonoBehaviour·씬·프레임 루프 없이 호출하는 C# 객체다. 기존
MacAnchor/DeskKeyboard와 같은 Unity 벡터 값 타입을 사용한다.

## 연결

1. `Assets/Models/ORION.glb`를 기존 glTFast 임포터로 임포트한다.
2. `PullOutView`의 `orionPrefab`에 임포트된 GameObject를 연결하고,
   `anchorSpace`에 MacAnchor를 캘리브레이션한 좌표계(OVR 입력이면 trackingSpace)를 연결한다.
   시작 시 저장된 MacAnchor를 읽는다. 저장된 앵커가 없으면 초기화하지 않는다.
   캘리브레이션 직후에는 `Initialize(rectangle, importedPrefab, trackingSpace, settings)`로 연결할 수 있다.
3. 선택한 검지 하나의 스트림을 매 프레임
   `SubmitTip(Time.time, tipInAnchorSpace, tracked, isPointing, isBareHand)`에 전달한다.
   손끝과 사각형은 같은 좌표계, 시간은 `Time.time` 기준 초 단위다.
   추적을 잃거나 포인팅/맨손 모드를 벗어난 프레임도 false로 전달한다.
   선택한 손이 바뀔 때는 `Sequence.CancelContact()`를 호출한다.
4. `Sequence.Tapped`는 유지 시간이 충족된 확정 시각 t0를 전달한다.
   `LetterStarted`는 O1/R/I/O2/N의 예약 시각을 전달하며,
   프레임을 건너뛰면 그 프레임에 도래한 이벤트를 순서대로 전달한다.
   한 Sequence는 한 번만 풀아웃한다.

Runtime은 MonoBehaviour 하나이며, glTFast가 임포트한 프리팹의 메시·재질을
유지한 채 자식 5개의 위치·회전·로컬 Z 두께를 적용한다. 임포트된 메시의 X축
변환에 맞춰 글자 정면을 화면의 앞 법선에 정렬한다.

## 베이스라인

수치는 `PullOutSettings` 한곳에서 조정한다. 접촉 깊이 5mm, 재접근 30mm,
접촉 유지 60ms, 허용 관통 15mm, 최대 샘플 간격 100ms다.
화면 앞 오프셋 20cm, 글자 중심 간격 8cm, 화면 중심에서 위로 15cm,
글자별 이동 시간 0.45초, 원본 메시 배율 0.5다. 시차는 요구사항대로 0.15초다.
이동·두께 모두 smoothstep 하나를 사용하며 설정은 초기화 시 복사한다.

## 검증과 범위

`python3 -B Assets/Scripts/PullOut/Verification~/verify_compile.py`

기존 GateFileChecks를 수정 없이 임시 폴더에서 컴파일·실행한다. 새 Core/Runtime은
각 asmdef에 선언한 참조로 컴파일하고 전체 EditMode 소스도 컴파일한다.
이 명령은 Unity 테스트를 실행하지 않는다.

`Assets/Tests/EditMode/PullOutTests.cs`는 탭 순서·시차, 사각형 밖 10회,
손떨림 10회, 유지·재접근, 추적/포인팅/맨손 조건, 접촉 취소, 종료 위치·두께,
프레임 건너뛰기, 실제 GLB 임포트와 Runtime transform 적용을 검사한다.

이 카드에서는 씬·손 상태 기계를 연결하지 않았다. 포인팅/맨손 판정은 입력 계약이며,
웹 화면의 2D 글자 숨김·빛 연출·다음 손 상태 카드도 포함하지 않는다.
Unity EditMode 실행과 기기 관찰은 오케스트레이터가 이어서 확인한다.
