import json
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path


root = Path(__file__).resolve().parents[5]
output = Path(tempfile.mkdtemp(prefix="splat-sim-check-"))
version = (root / "ProjectSettings/ProjectVersion.txt").read_text().splitlines()[0].split(": ")[1]
unity = Path(f"/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/Resources/Scripting")
dotnet = Path("/usr/local/share/dotnet")
csc = sorted(dotnet.glob("sdk/*/Roslyn/bincore/csc.dll"))[-1]
refs = [unity / "NetStandard/ref/2.1.0/netstandard.dll"]
refs += sorted((unity / "NetStandard/compat/2.1.0/shims/netfx").glob("*.dll"))
mathematics = root / "Library/ScriptAssemblies/Unity.Mathematics.dll"


def compile_assembly(name, sources, references, executable=False):
    response = output / (name + ".rsp")
    response.write_text("\n".join(
        ['-r:"' + str(p) + '"' for p in references]
        + ['"' + str(p) + '"' for p in sources]
    ))
    assembly = output / (name + ".dll")
    command = ["dotnet", str(csc), "-nologo", "-noconfig", "-nostdlib",
               "-target:" + ("exe" if executable else "library"), "-langversion:9",
               "-nowarn:CS1701,CS1702", "-optimize+",
               "-out:" + str(assembly), "@" + str(response)]
    result = subprocess.run(command, capture_output=True, text=True)
    if result.returncode:
        print(result.stdout + result.stderr, flush=True)
        raise SystemExit(result.returncode)
    print(f"PASS Roslyn {name}: {len(sources)} source files", flush=True)
    return assembly


sim_dll = compile_assembly("MoodPicker.Splatter.Sim", sorted((root / "Assets/Scripts/Splatter/Sim").glob("*.cs")), refs + [mathematics])
nunit = sorted((root / "Library/PackageCache").glob("com.unity.ext.nunit@*/net40/unity-custom/nunit.framework.dll"))[-1]
tests = [root / "Assets/Tests/EditMode/SplatSimTests.cs", root / "tools/hand-state-verify/RunCoreTests.cs"]
tests += [Path(p) for p in sys.argv[1:]]
runner = compile_assembly("SplatSim.Tests", tests, refs + [mathematics, sim_dll, nunit], True)
shutil.copyfile(nunit, output / nunit.name)
shutil.copyfile(mathematics, output / mathematics.name)
(output / "SplatSim.Tests.runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {
    "tfm": "net10.0", "framework": {"name": "Microsoft.NETCore.App", "version": "10.0.0"}
}}))
xml = output / "sim-tests.xml"
result = subprocess.run(["dotnet", str(runner), str(xml)], capture_output=True, text=True)
print(result.stdout + result.stderr, flush=True)
if xml.exists():
    text = xml.read_text()
    for case in re.finditer(r'<test-case [^>]*methodname="([^"]+)"[^>]*result="(Failed)"[^>]*>(.*?)</test-case>', text, re.S):
        message = re.search(r"<message><!\[CDATA\[(.*?)\]\]>", case.group(3), re.S)
        print(f"FAIL {case.group(1)}: {message.group(1).strip() if message else ''}", flush=True)
if result.returncode:
    raise SystemExit(result.returncode)
print("PASS SplatSim kernel NUnit tests", flush=True)
