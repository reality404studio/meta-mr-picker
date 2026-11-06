import json
import shutil
import xml.etree.ElementTree as ET
import subprocess
import tempfile
from pathlib import Path


root = Path(__file__).resolve().parents[4]
output = Path(tempfile.mkdtemp(prefix="orion-chip-check-"))
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
    "Assets/Scripts/Tokens",
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
    "Assets/Scripts/Folio/Core",
    "Assets/Scripts/Folio/Runtime",
    "Assets/Scripts/Dab/Editor",
]:
    path = root / directory
    definition = json.loads(next(path.glob("*.asmdef")).read_text())
    if definition["name"] == "MoodPicker.Dab.Editor":
        definition["references"] += ["MoodPicker.Folio", "MoodPicker.Folio.Runtime", "MoodPicker.Tokens"]
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


shader = (root / "Assets/Shaders/Chip/OrionChip.shader").read_text()
import re
assert '"RenderType"="Opaque"' in shader and '"LightMode"="SRPDefaultUnlit"' in shader
assert "Blend Off" in shader and "ZWrite On" in shader
assert "half4(color, 1.0h)" in shader and not re.search(r"ColorMask\s+RGB\b", shader)
assert shader.count("SAMPLE_TEXTURE2D(_BaseMap") == 3
assert len(re.findall(r"\bTEXTURE2D\(", shader)) == 1
for symbol in ["#pragma multi_compile_instancing", "UNITY_SETUP_INSTANCE_ID", "UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO", "UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX", "UNITY_DEFINE_INSTANCED_PROP(float, _ChipNumber)", "UNITY_DEFINE_INSTANCED_PROP(float, _HasLoop)"]:
    assert symbol in shader, symbol
assert 'Name "ChipUnlit"' in shader and shader.count("HLSLPROGRAM") == 1
assert "DigitBits(number/10)" in shader and "DigitBits(number%10)" in shader
guid = re.search(r"guid: (\w+)", (root / "Assets/Shaders/Chip/OrionChip.shader.meta").read_text())[1]
assert "m_Shader: {fileID: 4800000, guid: " + guid + ", type: 3}" in (root / "Assets/Materials/Orion/Chip.mat").read_text()
for source in (root / "Assets/Scripts/Folio").rglob("*.cs"):
    assert "Shader.Find" not in source.read_text()
    assert not any(part.endswith("~") for part in source.parts)
print("PASS shader static: Opaque Unlit, alpha=1, single pass/texture, triplanar, stereo, instancing, digits, GUID", flush=True)

runner_name = "Folio.CoreTests"
sources = [root / "Assets/Tests/EditMode/ChipTests.cs", root / "tools/hand-state-verify/RunCoreTests.cs"]
if (root / "Assets/Tests/EditMode/FolioTests.cs").exists(): sources.append(root / "Assets/Tests/EditMode/FolioTests.cs")
runner = compile_assembly(runner_name, sources, refs + compiled + [nunit], True)
shutil.copyfile(nunit, output / nunit.name)
(output / (runner_name + ".runtimeconfig.json")).write_text((output / "Checker.runtimeconfig.json").read_text())
result = subprocess.run(["dotnet", str(runner), str(output / "core-tests.xml")], cwd=root, capture_output=True, text=True)
(output / "core-tests.log").write_text(result.stdout + result.stderr)
print(result.stdout + result.stderr, flush=True)
if result.returncode: raise SystemExit(result.returncode)
print("PARTIAL L1: Roslyn with pending asmdef references + core NUnit + static gates; Unity EditMode/GPU UNVERIFIED", flush=True)
