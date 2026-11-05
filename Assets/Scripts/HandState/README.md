# HandState — build-be9a-build-2714

`Core`는 Unity 참조가 없는 순수 C#이다 (`noEngineReferences: true`).
`HandSnapshot`은 모드, 칩 색, 마지막 스포이드 색을 가진 불변 값이다.
`HandStateMachine.Apply(snapshot, event)`는 `(State, Ignored)`를 반환한다.
`IsAllowed(mode, action)`의 유일한 허용 표는 `AllowedActions`이며 내부 행까지 읽기 전용이다.

## 전이 표

| 현재 | 사건 | 다음 | 처리 |
| --- | --- | --- | --- |
| Bare | PortalDipPaint(color) | Paint | 칩 lightTone 저장 |
| Bare | GrabStamp | Stamp | 도구 모드 |
| Bare | GrabDropper | Dropper | 마지막으로 딴 색 사용 |
| Bare | GrabLetter / ReleaseLetter / TapScreen | Bare | 허용된 동작, 모드 유지 |
| Paint | Washed | Bare | 물감 제거 |
| Paint | GrabStamp | Stamp | 씻기 없이 물감 제거 후 도구 모드 |
| Paint | GrabDropper | Dropper | 씻기 없이 물감 제거 후 도구 모드 |
| Paint | LetterTouch | Paint | 버킷 바르기 후에도 물감 유지 |
| Stamp | ReleaseTool | Bare | 도구 해제 |
| Stamp | StampFired | Stamp | 채집 후 모드 유지 |
| Dropper | ReleaseTool | Bare | 도구 해제, 마지막 색 보존 |
| Dropper | DropperPicked(color) | Dropper | 마지막 색 갱신 |

나머지 조합은 모두 `Ignored=true`, 전체 snapshot 유지다. 색이 필요한 사건에
색이 없으면 무시한다. Paint에서 다시 dip하거나 도구 손에서 씻기는 명시된 전이에
없으므로 무시한다. GrabLetter/ReleaseLetter는 글자 쥠 상태를 추가하지 않는다.
LetterTouch는 이 계약에서 물감으로 글자에 닿는 사건이며, 맨손 파동은
`TouchLetterPulse` 허용 여부로 구분한다. 실제 동작 실행은 다음 카드의 소비자 몫이다.

요청의 `Paint + GrabStamp/GrabDropper → 무시` 문구는 같은 문장의
“도구를 집으면 물감은 사라지고 해당 도구 손”, 인수인계 D8/§3와 충돌한다.
후자의 규칙을 적용했다. §3의 허용 동작은 모드가 변하지 않아도 수락한다.
정본 인수인계 파일은 수정하지 않았다.

## 색과 Runtime 연결 계약

`HandStyleSettings.cs` 한곳이 디자인 토큰 편집 지점이다. Bare/Stamp 기본 색은
베이스라인이며, Paint는 칩 lightTone, Dropper는 마지막 색(없으면 Bare 색)을 쓴다.
스포이드 색은 도구를 내려놓거나 다른 모드를 거쳐도 유지된다. 물감 색은 씻거나
도구를 집을 때 지워지며 도구를 놓아도 되살아나지 않는다.

`HandStateController` 하나가 오른손 snapshot을 소유하고, 왼손은 Bare 고정이다.
Inspector의 `portal` 또는 `BindPortal(source)`로 이벤트 원천을 지정한다.
활성 동안 PaintPicked/Washed를 구독하고 비활성·재연결·파괴 시 해제한다.
기존 Portal의 물감/표시를 수정하거나 제어하지 않으며 이 카드의 상태는 독립적이다.
활성 전에 지나간 Portal 사건은 재생하지 않는다.

도구 등 다른 사건은 오른손 전용 `Submit(new HandEvent(...))`로 넣는다.
비활성이면 입력을 무시한다. snapshot이 실제 바뀔 때만
`StateChanged(HandState, UnityEngine.Color)`를 발생시키며 같은 모드의 색 변경도 포함한다.
초기 상태·색은 `RightHand`/`CurrentColor`로 읽는다. 손 동작 실행 전에는
`IsAllowed`를 호출해야 한다. 씬 연결, 손 표시·셰이더, 기존 상호작용 차단은 다음 카드다.

## 검증

`python3 -B Assets/Scripts/HandState/Verification~/verify_compile.py`

기존 GateFileChecks를 임시 디렉터리에서 실행한다. HandState와 의존 asmdef는
선언한 참조로 다시 컴파일하고, 전체 EditMode 소스도 컴파일한다.
Core는 Unity DLL 없이 컴파일한다. 동일한 `HandStateTests.cs`를 로컬 NUnit으로
.NET에서 실행한다. 이어 임시 복사본에 Paint의 GrabLetter 허용을 추가하고
허용 표 스냅샷 테스트가 실패하는지 검사한다. 프로젝트 원본은 변형하지 않는다.
로그·rsp·DLL·NUnit XML은 출력된 임시 증거 경로에 저장한다.

`HandStateTests.cs`는 전이 44조합, 허용 동작 36조합, 표 스냅샷·불변성,
모드별 색·색 기억·물감 제거·잘못된 사건을 검사한다.
`HandStateRuntimeTests.cs`는 Portal 이벤트 전달, 왼손 고정, 색 변경 알림,
구독 해제·재연결·파괴를 검사한다. Runtime 테스트와 기존 전체 EditMode의
Unity 실행은 이 명령에 포함되지 않으며 미확인이다.
