import json
import re
import subprocess
import tempfile
from pathlib import Path


root = Path(__file__).resolve().parents[5]
output = Path(tempfile.mkdtemp(prefix="orion-look-check-"))
version = (root / "ProjectSettings/ProjectVersion.txt").read_text().splitlines()[0].split(": ")[1]
unity = Path(f"/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/Resources/Scripting")
dotnet = Path("/usr/local/share/dotnet")
csc = sorted(dotnet.glob("sdk/*/Roslyn/bincore/csc.dll"))[-1]
refs = [unity / "NetStandard/ref/2.1.0/netstandard.dll"]
refs += sorted((unity / "NetStandard/compat/2.1.0/shims/netfx").glob("*.dll"))
refs += sorted((unity / "Managed/UnityEngine").glob("UnityEngine*.dll"))


def compile_assembly(name, sources, references, executable=False):
    response = output / (name + ".rsp")
    response.write_text("\n".join(
        ['-r:"' + str(p) + '"' for p in references]
        + ['"' + str(p) + '"' for p in sources]
    ))
    assembly = output / (name + ".dll")
    command = ["dotnet", str(csc), "-nologo", "-noconfig", "-nostdlib",
               "-target:" + ("exe" if executable else "library"), "-langversion:9",
               "-nowarn:CS1701,CS1702",
               "-define:UNITY_EDITOR;UNITY_INCLUDE_TESTS;UNITY_ANDROID;UNITY_6000_0_OR_NEWER;UNITY_2023_1_OR_NEWER;UNITY_2022_3_OR_NEWER;ENABLE_INPUT_SYSTEM",
               "-out:" + str(assembly), "@" + str(response)]
    result = subprocess.run(command, capture_output=True, text=True)
    (output / (name + ".log")).write_text(result.stdout + result.stderr)
    if result.returncode:
        print(result.stdout + result.stderr, flush=True)
        raise SystemExit(result.returncode)
    print(f"PASS Roslyn {name}: {len(sources)} source files", flush=True)
    return assembly


print(f"Evidence directory: {output}", flush=True)
gate = root.parent / "shared/BuildGate"
framework = sorted((dotnet / "packs/Microsoft.NETCore.App.Ref").glob("*/ref/net10.0"))[-1]
checker = compile_assembly("Checker", [gate / "Preflight~/Checker/Program.cs", gate / "Editor/GateFileChecks.cs"],
                           sorted(framework.glob("*.dll")), True)
(output / "Checker.runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {
    "tfm": "net10.0", "framework": {"name": "Microsoft.NETCore.App", "version": "10.0.0"}
}}))
result = subprocess.run(["dotnet", str(checker), str(root)], capture_output=True, text=True)
(output / "file-gates.log").write_text(result.stdout + result.stderr)
print(result.stdout + result.stderr, flush=True)
if result.returncode:
    raise SystemExit(result.returncode)

compiled = []
by_name = {}
for directory in [
    "Assets/Scripts/Hands/Core",
    "Assets/Scripts/Catch/Core",
    "Assets/Scripts/Portal/Core",
    "Assets/Scripts/Portal/Runtime",
    "Assets/Scripts/Experiments/DeskKeyboard/Core",
    "Assets/Scripts/MacAnchor/Core",
    "Assets/Scripts/MacAnchor/Runtime",
    "Assets/Scripts/PullOut/Core",
    "Assets/Scripts/PullOut/Runtime",
    "Assets/Scripts/HandState/Core",
    "Assets/Scripts/HandState/Runtime",
    "Assets/Scripts/Dab/Core",
    "Assets/Scripts/Dab/Runtime",
    "Assets/Scripts/Dab/Editor",
]:
    path = root / directory
    definition = json.loads(next(path.glob("*.asmdef")).read_text())
    references = [by_name[name] if name in by_name else root / "Library/ScriptAssemblies" / (name + ".dll")
                  for name in definition.get("references", [])]
    if definition.get("includePlatforms") == ["Editor"]:
        references += [unity / "Managed/UnityEditor.dll"]
        references += sorted((unity / "Managed/UnityEngine").glob("UnityEditor*.dll"))
    base_refs = [p for p in refs if not p.name.startswith("UnityEngine")] if definition.get("noEngineReferences") else refs
    assembly = compile_assembly(definition["name"], sorted(path.glob("*.cs")), base_refs + references)
    compiled.append(assembly)
    by_name[definition["name"]] = assembly

names = {p.name for p in compiled} | {"MoodPicker.Tests.EditMode.dll"}
dependencies = [p for p in (root / "Library/ScriptAssemblies").glob("*.dll") if p.name not in names]
editor = [unity / "Managed/UnityEditor.dll"] + sorted((unity / "Managed/UnityEngine").glob("UnityEditor*.dll"))
nunit = sorted((root / "Library/PackageCache").glob("com.unity.ext.nunit@*/net40/unity-custom/nunit.framework.dll"))[-1]
compile_assembly("MoodPicker.Tests.EditMode", sorted((root / "Assets/Tests/EditMode").glob("*.cs")),
                 refs + compiled + dependencies + editor + [nunit])

asmdef_dirs = [p.parent for p in (root / "Assets").rglob("*.asmdef")]
plain = [p for p in (root / "Assets").rglob("*.cs")
         if not any(d in p.parents for d in asmdef_dirs) and not any(x.endswith("~") for x in p.parts)]
runtime_sources = [p for p in plain if "Editor" not in p.parts]
editor_sources = [p for p in plain if "Editor" in p.parts]
base_dependencies = [p for p in dependencies if not p.name.startswith("Assembly-CSharp")]
runtime = compile_assembly("Assembly-CSharp", runtime_sources, refs + compiled + base_dependencies)
compile_assembly("Assembly-CSharp-Editor", editor_sources, refs + compiled + base_dependencies + editor + [runtime])

shader = (root / "Assets/Shaders/Cel/CelPaint.shader").read_text()
assert re.search(r'_ToneSteps\s*\([^\n]+\)\s*=\s*0', shader)
assert "if (_ToneSteps > 0.0)" in shader and "floor(t * steps)" in shader
assert "smoothstep(-softness, softness, ndl - _Threshold)" in shader
assert "lerp(baseCol, tone, _ToneMix)" in shader
assert "half3 tone = ToneRamp(ndl)" in shader
assert "_MidTone.rgb" in shader
assert shader.count("SAMPLE_TEXTURE2D(_BaseMap") == 3
assert shader.count("#pragma multi_compile_instancing") == 3
assert shader.count("UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO") == 3
outline = shader.split('Name "Outline"')[1].split("ENDHLSL")[0]
assert "Cull Front" in outline and "clip(_Outline - 0.5)" in outline
assert "half4(_OutlineColor.rgb, 1.0h)" in outline
assert "half4(lerp(bare, painted, mask), 1.0h)" in shader
assert "half4(0, 0, 0, 1)" in shader
assert not re.search(r"ColorMask\s+RGB\b", shader)
assert "Shader.Find" not in (root / "Assets/Scripts/Dab/Core/CelPaint.cs").read_text()
guid = re.search(r"guid: (\w+)", (root / "Assets/Shaders/Cel/CelPaint.shader.meta").read_text())[1]
clay = (root / "Assets/Materials/Orion/Clay.mat").read_text()
assert "m_Shader: {fileID: 4800000, guid: " + guid + ", type: 3}" in clay
assert "- _Outline: 0" in clay and "- _ToneSteps: 0" in clay
print("PASS shader static: continuous/stepped ramp, triplanar mix, outline cull, alpha, stereo, Clay GUID", flush=True)

fixture = output / "fixture-project"
(fixture / "Assets/Shaders").mkdir(parents=True)
(fixture / "ProjectSettings").mkdir()
(fixture / "Assets/Shaders/BadMask.shader").write_text('Shader "Fixture/BadMask" { SubShader { Pass { ColorMask RGB } } }')
(fixture / "Assets/Shaders/BadOutline.shader").write_text('Shader "Fixture/BadOutline" { SubShader { Pass { Cull Back } } }')
(fixture / "Assets/Shaders/GoodMask.shader").write_text('Shader "Fixture/GoodMask" { SubShader { Pass { ColorMask RGBA } Pass { ColorMask 0 } } }')
result = subprocess.run(["dotnet", str(checker), str(fixture)], capture_output=True, text=True)
(output / "fixture.log").write_text(result.stdout + result.stderr)
assert result.returncode == 1
assert "[FAIL] shader-colormask: Assets/Shaders/BadMask.shader" in result.stdout
assert "[FAIL] outline-shader: Assets/Shaders/BadOutline.shader" in result.stdout
assert "shader-colormask: Assets/Shaders/GoodMask.shader" not in result.stdout
print("PASS gate fixtures: ColorMask RGB and Cull Back rejected; RGBA/0 accepted", flush=True)
print("PARTIAL PASS: Roslyn + file gates + shader static; Unity EditMode/GPU render UNVERIFIED", flush=True)
