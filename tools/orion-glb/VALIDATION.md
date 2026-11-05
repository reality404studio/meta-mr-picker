# build-402f-make-098a — 2026-11-06 00:40 KST

상태: 막힘. GLB 미생성, 실제 에셋 게이트 미통과. 커밋·푸시·보드 완료 처리 없음.

## 작성한 파일

- `make_orion.py`: Unbounded 텍스트를 글자별 메시로 변환, extrusion/bevel, 바닥 중앙 원점, 기본 자간, 미터 단위, 단일 무광 클레이, GLB 두 경로에 동일 바이트 출력.
- `orion_settings.json`: 높이 0.12m, 폭 베이스라인 0.615m, extrusion 한쪽 0.009m, bevel 0.0015m, base color 0.8, roughness 1.0, metallic 0.0.
- `verify_glb.py`: 표준 라이브러리 GLB 파서, 정확한 노드/메시 5개, 삼각형 정점 병합 후 닫힌 연결 표면·오일러 특성·구멍 수, 바닥 중앙 원점, 재질, 폭/높이/두께, 두 사본 일치 검사.
- `register_three.mjs` / `resolve_three.mjs`: `--import` 로더 훅에서 vendor의 bare `three`만 로컬 모듈로 연결. vendor 수정 없음.
- `verify_three.mjs`: vendor GLTFLoader로 두 사본을 각각 파싱, Mesh 5개·이름·별도 geometry·재질·크기 확인.
- `verify.sh`: Python → Node → Unity EditMode. `--no-unity` 성공도 부분 통과로 명시. 기본 실행은 glTFast·프로세스 확인 및 Unity 결과 XML을 요구.
- `Assets/Tests/EditMode/OrionGlbTests.cs` 및 `.meta`: 동기 임포트, 자식 이름 5개 및 별도 Mesh 확인. 실행/컴파일 미확인.

폭은 폰트의 기본 advance와 글리프 bounds로 잡은 베이스라인이다. 실제 Blender 출력 치수는 아직 측정하지 못했다.
원본 R은 TTF glyph 248, 윤곽 2개다. 구멍 보존과 요청한 R 구멍 0개가 충돌하여 질문했다.
답변 전까지 생성기는 원본 글리프를 유지하며 `expected_holes.R`은 요청대로 0이다. 이 충돌은 미해결이며 통과 처리하지 않는다.

## 실행한 검증

```sh
python3 -B -m unittest discover -s tools/orion-glb -p test_verify_glb.py -v
```

`Ran 8 tests ... OK`. 구멍 없는 사면체, 토러스 구멍 1개, normal seam 중복 정점, 열린 표면/중복 면/분리된 표면/반대 winding 거부, GLB chunk와 길이 검사.
증거: L1, 검증 도구 자체에 한정. 실제 ORION에 대한 증거 아님.

```sh
node --import ./tools/orion-glb/register_three.mjs --input-type=module - <<'JS'
import { GLTFLoader } from './tools/orion-glb/vendor/three/addons/loaders/GLTFLoader.js';
const document = { asset: { version: '2.0' }, scene: 0, scenes: [{ nodes: [0] }], nodes: [{ name: 'loader-smoke' }] };
const result = await new GLTFLoader().parseAsync(JSON.stringify(document), '');
if (result.scene.children[0]?.name !== 'loader-smoke') throw Error('Loader parse failed');
console.log('PASS offline vendored GLTFLoader import + minimal glTF parse (not ORION evidence)');
JS
```

위 PASS 출력 확인. 네트워크 사용 없음. Python AST, `sh -n`, 세 `.mjs` 파일의 `node --check`도 통과.

```sh
BLENDER_USER_CONFIG="$PWD/.harness/state/blender/config" \
BLENDER_USER_SCRIPTS="$PWD/.harness/state/blender/scripts" \
TMPDIR="$PWD/.harness/state/blender/tmp" \
/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python tools/orion-glb/make_orion.py
```

exit 139. `supports_barycentric_whitelist → MTLBackend::metal_is_supported → GPU_backend_type_selection_detect`.
파이썬 실행 전 종료. 기본 환경 및 `--gpu-backend metal --debug-gpu-force-workarounds`도 같은 결과.
원본 크래시 로그: `.harness/state/blender/tmp/blender.crash.txt`.
샌드박스 세션에서 발생했으나 샌드박스 밖 실행과 비교하지 않아 근본 원인은 미확정.

```sh
sh tools/orion-glb/verify.sh --no-unity
node --import ./tools/orion-glb/register_three.mjs tools/orion-glb/verify_three.mjs
```

각각 `FAIL GLB: ... ORION.glb` / `FAIL GLTFLoader: ENOENT ... ORION.glb`.
에셋이 없으므로 노드·치수·구멍·웹 임포트 모두 미확인.

```sh
pgrep -f "projectPath .*unity/mood-picker"
```

exit 3, `pgrep: Cannot get process list`. `mr status`도 `ps` 실행에서 `Operation not permitted`.
`Packages/manifest.json`에 glTFast가 없는 것을 확인했으며 수정하지 않았다.
Unity는 중복 실행 여부를 확인할 수 없어 시작하지 않았다. 테스트 코드만 작성했다.

실기·에디터 화면 관찰 없음. 증거 L2~L4 미확인. 다음 카드 착수 없음.
