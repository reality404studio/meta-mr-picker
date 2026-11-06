# 브리프: 소품 에셋 Blender 자동 생성 (Gemini Flash · Antigravity)

카드: 보드 `--app orion` build-a4ae "소품 4종 에셋" 의 하위 과업 "블렌더 파이썬: 소품 5종 glb + ORION_Props_Atlas.png 자동 생성" (`npm run studio -- list --app orion` 으로 id 확인).
먼저 `GEMINI.md`, `.harness/ROUTING.md`(§3 Blender 잠금, §4 프로토콜)을 읽고, `pgrep -fl Blender` 로 다른 Blender 가 없는지 확인한 뒤 `doing`.

## 명세 (사람 + Gemini Argon 확정, 다른 값을 정하지 마)
작업대 `../../studio/apps/orion/nodes/interface-design/README.md` **§9(소품 4종 조형·치수)**, **§11(1024×1024 아틀라스 UV 6분할, glb 파츠·피벗 규격)**, §8-4(geometry 토큰). 시안 이미지: `../../studio/apps/orion/nodes/interface-design/assets/props_design_sheet.png`, `blueprint_uv_atlas_sheet.png`. 색·치수 정본: `design/tokens/orion.tokens.json`(읽기 전용).
- 만들 glb 4종 + 아틀라스: `ORION_Console.glb`(96 tris: Console_Base + Console_BakeBtn + Marbling_Surface_Quad), `ORION_Stamp.glb`(44: Stamp_Frame_Base + Stamp_Plunger), `ORION_Pipette.glb`(48: Pipette_Body + Pipette_ColorTint, 컬러 창·방울 팁에 버텍스 컬러 R=1 마스크), `ORION_StageFrame.glb`(28: Top_SlotBar + Collapsible_Body). `ORION_Chip34.glb` 는 만들지 않는다(칩은 Unity 코드 `ChipMeshBuilder` 가 이미 있음).
- `ORION_Props_Atlas.png` 1024×1024 **단일 아틀라스**, UV 6분할(§11-1 좌표 그대로). 모든 파츠의 UV 는 자기 구역 안, **머티리얼 1개**. 텍스처 그림(제도선·크롭 마크·눈금·번호 박스·색 블록)은 Blender 에서 절차적으로 굽거나 파이썬 표준 라이브러리로 PNG 를 써도 된다. 폰트는 `Assets/Fonts/Unbounded/Unbounded-Black.ttf`·IBM Plex Mono(없으면 큰 번호·기호만; 잔글씨 금지 §8-3).
- 치수·피벗·애니메이션 기준 로컬 이동량(`Stamp_Plunger` -8mm, `Console_BakeBtn` -2mm 등)은 §9·§11-2 를 따른다. 단위 m, glTF Y-up.

## 파일 위치
- 스크립트: `tools/orion-props/make_props.py`, 설정 한 곳 `tools/orion-props/props_settings.json`(치수·tris 예산·구역 좌표), 검증 `tools/orion-props/verify_props.py`(+`verify.sh`, 표준 라이브러리만)
- 출력: `Assets/Models/Props/*.glb`, `Assets/Textures/Props/ORION_Props_Atlas.png`, 웹용 사본은 만들지 않는다.
- 렌더 확인 PNG: 카드 assets 에 `props-preview.png`(Blender 로 한 장에 5종).

## 실행 (사용자 환경에서 직접)
`TMPDIR=$PWD/.harness/state/tmp /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python tools/orion-props/make_props.py`

## 성공 조건 (카드)
verify: glb 4개의 **파츠 이름 일치**, **파일별 tris 가 예산 이하·합 190 이하**(Console 96 + Stamp 44 + Pipette 48, 오차 없이 이하), 머티리얼 1개(전 파일 동일 이름), 아틀라스 1024×1024, **각 파츠 UV 가 해당 구역 사각형 안**(§11-1), Pipette 버텍스 컬러 R 마스크 존재, 치수가 §9 ±1mm. 검증 스크립트는 **예산을 넘기거나 UV 를 구역 밖으로 보내면 실패**해야 한다(되돌리면 실패하는 게이트).
`npm run studio -- check <카드 id> --app orion --as "Gemini Flash" -- "sh unity/mood-picker/tools/orion-props/verify.sh"`

## 건드려도 되는 곳
`tools/orion-props/**`, `Assets/Models/Props/`, `Assets/Textures/Props/` (touch.txt 에도 추가됨). Unity 로 임포트 확인은 하지 않는다(오케스트레이터가 한다). `.meta` 는 Unity 가 만든다.

## 하지 말 것
커밋·푸시·HANDOFF·done, 정본(토큰 JSON·docs·board) 수정, 칩 glb, Unity 씬/스크립트 수정, 주석으로 설계 이유 쓰기(주석 린트는 .cs 에만 적용되지만 같은 원칙), 다른 카드. 막히면 note 에 적고 멈춘다.

## 보고 (마지막 메시지)
카드 / 한 일 / 돌린 검증(명령) / 결과(핵심 줄) / 증거: L1 / 못 한 것 / 만든 파일 목록
