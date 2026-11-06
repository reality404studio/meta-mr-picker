import json
import shutil
import xml.etree.ElementTree as ET
import subprocess
import tempfile
from pathlib import Path


root = Path(__file__).resolve().parents[2]
output = Path(tempfile.mkdtemp(prefix="orion-tokens-check-"))
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

runner_name = "OrionTokens.CoreTests"
runner = compile_assembly(runner_name, [
    root / "Assets/Tests/EditMode/OrionTokensTests.cs",
    root / "Assets/Tests/EditMode/HandStateTests.cs",
    root / "tools/hand-state-verify/RunCoreTests.cs",
], refs + compiled + [nunit], True)
shutil.copyfile(nunit, output / nunit.name)
(output / (runner_name + ".runtimeconfig.json")).write_text((output / "Checker.runtimeconfig.json").read_text())


def run_tests(directory, label):
    result = subprocess.run(["dotnet", str(runner), str(output / (label + ".xml"))],
                            cwd=directory, capture_output=True, text=True)
    (output / (label + ".log")).write_text(result.stdout + result.stderr)
    return result


result = run_tests(root, "core-tests")
print(result.stdout + result.stderr, flush=True)
if result.returncode:
    raise SystemExit(result.returncode)

fixture = output / "mutation-project"
fixture_json = fixture / "design/tokens/orion.tokens.json"
fixture_json.parent.mkdir(parents=True)
original = json.loads((root / "design/tokens/orion.tokens.json").read_text())
for label, section, key, replacement in [
    ("color", "color", "paper.face", "#102030"),
    ("time", "env", "passthrough.transitionSeconds", 0.91),
]:
    data = json.loads(json.dumps(original))
    data[section][key] = replacement
    fixture_json.write_text(json.dumps(data))
    result = run_tests(fixture, "mutation-" + label)
    tree = ET.parse(output / ("mutation-" + label + ".xml"))
    rejected = any(case.get("name") == "EveryJsonLeafMatchesGeneratedConstantIncludingColorsTimesAndPalmHysteresis"
                   and case.get("result") == "Failed" for case in tree.iter("test-case"))
    if result.returncode != 1 or not rejected:
        print("FAIL JSON mutation was not rejected: " + label, flush=True)
        print(result.stdout + result.stderr)
        raise SystemExit(1)
    print("PASS JSON mutation: " + label + " rejected by same NUnit test (temporary JSON only)", flush=True)
print("PASS L1: file gates, Roslyn, token/HandState .NET NUnit, JSON mutations; Unity EditMode execution UNVERIFIED", flush=True)
