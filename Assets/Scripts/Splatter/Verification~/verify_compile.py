import json
import shutil
import subprocess
import tempfile
from pathlib import Path


root = Path(__file__).resolve().parents[4]
output = Path(tempfile.mkdtemp(prefix="splatter-core-check-"))
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

pure_refs = [p for p in refs if not p.name.startswith("UnityEngine")]
pigment_dll = compile_assembly("MoodPicker.Pigment", sorted((root / "Assets/Scripts/Pigment/Core").glob("*.cs")), pure_refs)
splatter_dll = compile_assembly("MoodPicker.Splatter", sorted((root / "Assets/Scripts/Splatter/Core").glob("*.cs")), pure_refs + [pigment_dll])

nunit = sorted((root / "Library/PackageCache").glob("com.unity.ext.nunit@*/net40/unity-custom/nunit.framework.dll"))[-1]
test_sources = [root / "Assets/Tests/EditMode/SplatterCoreTests.cs", root / "tools/hand-state-verify/RunCoreTests.cs"]
runner = compile_assembly("Splatter.CoreTests", test_sources, pure_refs + [pigment_dll, splatter_dll, nunit], True)
shutil.copyfile(nunit, output / nunit.name)
(output / "Splatter.CoreTests.runtimeconfig.json").write_text((output / "Checker.runtimeconfig.json").read_text())
result = subprocess.run(["dotnet", str(runner), str(output / "core-tests.xml")], capture_output=True, text=True)
(output / "core-tests.log").write_text(result.stdout + result.stderr)
print(result.stdout + result.stderr, flush=True)
if result.returncode:
    raise SystemExit(result.returncode)
print("PASS Splatter Core & PigmentCore NUnit tests", flush=True)
