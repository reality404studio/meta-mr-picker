# LESSONS
<!-- 한 항목 = 증상 / 원인 / 확인법 / 적용 범위. 두 프로젝트 이상에서 같은 교훈이 나오면 제목 끝에 `promote?` — 전역 승격은 사람이 정한다. -->

## <짧은 제목> (<YYYY-MM-DD>, <card-id>)
- 증상:
- 원인:
- 확인법: (다음에 1분 안에 확인하는 방법 — 명령이나 볼 로그)
- 적용 범위: 이 프로젝트 | Unity 전체 | WebXR 전체 | 모든 MR

## mr 도구가 에디터 닫힘을 "열림"으로 오탐 (2026-11-10, build-9c56)
- 증상: `mr preflight --unity` 가 "Unity 에디터가 이 프로젝트를 열고 있음"이라며 막는데 Unity 프로세스도 `Temp/UnityLockfile` 도 없음
- 원인: `mr` 가 `pgrep -fl Unity` 결과에서 `Unity.app` 과 프로젝트 폴더 이름이 같이 든 줄을 에디터로 본다. Claude Code 의 Bash 호출을 감싼 셸 프로세스 줄이 그 조건에 걸림
- 확인법: `pgrep -fl Unity | grep -v "Unity Hub"` 와 `ls Temp/UnityLockfile` 로 실제 에디터 유무 확인. 없으면 `mr` 를 건너뛰고 Unity 배치 명령을 직접 실행
- 적용 범위: 모든 MR (mr 도구)

## 배치 빌드 메서드가 한 프레임 뒤 시작하는데 `-quit` 를 붙이면 아무것도 안 함 (2026-11-10, build-9c56)
- 증상: Unity 가 "Batchmode quit successfully" 로 정상 종료하고 exit 0 인데 APK 와 프리뷰 PNG 시각이 그대로
- 원인: `MoodPickerBuild.BuildSplatterApkBatch` 가 `EditorApplication.update` 로 작업을 미루고 끝나면 스스로 `Exit` 한다. `-quit` 는 메서드가 반환되자마자 종료시켜서 작업이 시작되기 전에 끝남. 또 `mr build-unity` 는 이 프로젝트에 없는 `MRKit.Build.Android` 를 부름
- 확인법: 빌드 뒤 `ls -la Builds/*.apk` 시각과 로그의 `[MP] splatter scene setup done` 줄 수 (정상이면 2번)
- 적용 범위: 이 프로젝트 (미루는 배치 메서드 전반)

## IL2CPP 재빌드는 디스크가 수 GB 필요 (2026-11-10, build-9c56)
- 증상: 빌드가 `BuildDevApk` 에서 실패, 로그에 `No space left on device` / `IOException: Disk full ... Library/Bee`
- 원인: 셰이더·C# 를 바꾼 뒤 재빌드가 `Library/Bee` 에 많이 씀 (이때 8.2GB). 여유 12GB 라는 말을 믿고 시작했다가 가득 참
- 확인법: 빌드 전 `df -h /` 여유가 20GB 안팎인지, 빌드 뒤 `grep -n "No space\|Disk full" <build.log>`
- 적용 범위: Unity 전체

