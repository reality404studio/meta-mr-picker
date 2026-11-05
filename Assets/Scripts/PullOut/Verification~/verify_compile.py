import json
import subprocess
import tempfile
from pathlib import Path


root = Path(__file__).resolve().parents[4]
output = Path(tempfile.mkdtemp(prefix="orion-pull-out-check-"))
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
    "Assets/Scripts/Experiments/DeskKeyboard/Core",
    "Assets/Scripts/MacAnchor/Core",
    "Assets/Scripts/MacAnchor/Runtime",
    "Assets/Scripts/PullOut/Core",
    "Assets/Scripts/PullOut/Runtime",
]:
    path = root / directory
    definition = json.loads(next(path.glob("*.asmdef")).read_text())
    references = [by_name[name] for name in definition.get("references", [])]
    assembly = compile_assembly(definition["name"], sorted(path.glob("*.cs")), refs + references)
    compiled.append(assembly)
    by_name[definition["name"]] = assembly

names = {p.name for p in compiled} | {"MoodPicker.Tests.EditMode.dll"}
dependencies = [p for p in (root / "Library/ScriptAssemblies").glob("*.dll") if p.name not in names]
editor = [unity / "Managed/UnityEditor.dll"] + sorted((unity / "Managed/UnityEngine").glob("UnityEditor*.dll"))
nunit = sorted((root / "Library/PackageCache").glob("com.unity.ext.nunit@*/net40/unity-custom/nunit.framework.dll"))[-1]
compile_assembly("MoodPicker.Tests.EditMode", sorted((root / "Assets/Tests/EditMode").glob("*.cs")),
                 refs + compiled + dependencies + editor + [nunit])
print("PARTIAL PASS: file gates and Roslyn compilation; Unity EditMode execution UNVERIFIED", flush=True)
