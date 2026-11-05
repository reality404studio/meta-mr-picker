import json
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path


root = Path(__file__).resolve().parents[4]
output = Path(tempfile.mkdtemp(prefix="orion-hand-state-check-"))
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
]:
    path = root / directory
    definition = json.loads(next(path.glob("*.asmdef")).read_text())
    references = [by_name[name] for name in definition.get("references", [])]
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

core = root / "Assets/Scripts/HandState/Core"
core_sources = sorted(core.glob("*.cs"))
test_sources = [root / "Assets/Tests/EditMode/HandStateTests.cs", Path(__file__).with_name("RunCoreTests.cs")]
pure_refs = [p for p in refs if not p.name.startswith("UnityEngine")]
core_name = "MoodPicker.HandState"
runner_name = "HandState.CoreTests"
runner = compile_assembly(runner_name, test_sources, pure_refs + [by_name[core_name], nunit], True)
shutil.copyfile(nunit, output / nunit.name)
runtime_config = (output / "Checker.runtimeconfig.json").read_text()
(output / (runner_name + ".runtimeconfig.json")).write_text(runtime_config)
result = subprocess.run(["dotnet", str(runner), str(output / "core-tests.xml")], capture_output=True, text=True)
(output / "core-tests.log").write_text(result.stdout + result.stderr)
print(result.stdout + result.stderr, flush=True)
if result.returncode:
    raise SystemExit(result.returncode)

original_output = output
output = original_output / "mutation"
output.mkdir()
source = core / "HandStateMachine.cs"
before = "[HandState.Paint] = Array.AsReadOnly(new[] { HandAction.FillLetter })"
after = "[HandState.Paint] = Array.AsReadOnly(new[] { HandAction.FillLetter, HandAction.GrabLetter })"
assert source.read_text().count(before) == 1
mutant = output / source.name
mutant.write_text(source.read_text().replace(before, after))
compile_assembly(core_name, [mutant if p == source else p for p in core_sources], pure_refs)
for name in [runner_name + ".dll", runner_name + ".runtimeconfig.json", nunit.name]:
    shutil.copyfile(original_output / name, output / name)
result = subprocess.run(["dotnet", str(output / runner.name), str(output / "core-tests.xml")], capture_output=True, text=True)
(output / "core-tests.log").write_text(result.stdout + result.stderr)
tree = ET.parse(output / "core-tests.xml")
snapshot_failed = any(case.get("name") == "AllowedActionsSnapshotRejectsAnyAddedOrRemovedPermission"
                      and case.get("result") == "Failed" for case in tree.iter("test-case"))
if result.returncode != 1 or not snapshot_failed:
    print("FAIL: permission mutation did not fail the snapshot test", flush=True)
    raise SystemExit(1)
print("PASS mutation: Paint + GrabLetter permission rejected by snapshot test (temporary copy only)", flush=True)
print("PARTIAL PASS: file gates, Roslyn compilation, Core NUnit and mutation; Unity EditMode execution UNVERIFIED", flush=True)
