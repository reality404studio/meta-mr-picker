using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Reality404.BuildGate;
using UnityEngine;

namespace MoodPicker.Tests
{
    public class ProjectRuleTests
    {
        static string Assets => Application.dataPath;
        static string Root => Directory.GetParent(Assets).FullName;

        static IEnumerable<string> RuntimeScripts() =>
            Directory.GetFiles(Path.Combine(Assets, "Scripts"), "*.cs", SearchOption.AllDirectories);

        static string Rel(string p) => p.Substring(Root.Length + 1).Replace('\\', '/');

        [Test]
        public void HandTracking_IsNotControllersOnly()
        {
            string cfg = File.ReadAllText(Path.Combine(Assets, "Oculus", "OculusProjectConfig.asset"));
            var m = Regex.Match(cfg, @"handTrackingSupport: (\d+)");
            Assert.That(m.Success, "handTrackingSupport 없음");
            Assert.That(new[] { "1", "2" }, Has.Member(m.Groups[1].Value));
        }

        [Test]
        public void CameraPermission_IsRequestedOnlyByOvrManager()
        {
            string scene = File.ReadAllText(Path.Combine(Assets, "Scenes", "FirstLight.unity"));
            Assert.That(Regex.IsMatch(scene, @"propertyPath: requestPassthroughCameraAccessPermissionOnStartup\s*\n\s*value: 1"),
                "OVRManager 가 카메라 권한을 요청해야 함");
            var hits = RuntimeScripts()
                .SelectMany(f => File.ReadAllLines(f).Select((l, i) => (f, l, i)))
                .Where(x => Regex.IsMatch(x.l, @"RequestUserPermission|OVRPermissionsRequester|RequestPermissionsOnce"))
                .Select(x => $"{Rel(x.f)}:{x.i + 1}").ToList();
            Assert.That(hits, Is.Empty, "권한 요청은 OVRManager 한 곳만:\n" + string.Join("\n", hits));
        }

        [Test]
        public void RuntimeScripts_DoNotHardcodeCameraResolution()
        {
            var hits = RuntimeScripts()
                .SelectMany(f => File.ReadAllLines(f).Select((l, i) => (f, l, i)))
                .Where(x => Regex.IsMatch(x.l, @"\b(1280|960)\b"))
                .Select(x => $"{Rel(x.f)}:{x.i + 1}").ToList();
            Assert.That(hits, Is.Empty, "CurrentResolution 을 읽을 것:\n" + string.Join("\n", hits));
        }

        [Test]
        public void PcaGetTexture_IsGuardedByIsPlaying()
        {
            var bad = new List<string>();
            foreach (string f in RuntimeScripts())
            {
                string[] lines = File.ReadAllLines(f);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!lines[i].Contains(".GetTexture()")) continue;
                    bool guarded = Enumerable.Range(System.Math.Max(0, i - 5), i - System.Math.Max(0, i - 5))
                        .Any(j => lines[j].Contains(".IsPlaying"));
                    if (!guarded) bad.Add($"{Rel(f)}:{i + 1}");
                }
            }
            Assert.That(bad, Is.Empty, "GetTexture() 앞 5줄 안에 IsPlaying 확인이 있어야 함:\n" + string.Join("\n", bad));
        }

        [TestCase("Editor/MoodPickerBuild.cs", "BuildDevApkBatch", "BuildDevApk")]
        [TestCase("Editor/MoodPickerBuild.cs", "BuildDeskKeyboardApkBatch", "BuildDeskKeyboardApk")]
        [TestCase("Editor/MoodPickerSceneSetup.cs", "RunBatch", "Run")]
        [TestCase("Editor/DeskKeyboardSceneSetup.cs", "RunBatch", "Run")]
        [TestCase("Editor/CatchSceneSetup.cs", "RunBatch", "Run")]
        [TestCase("Editor/MoodPickerBuild.cs", "BuildCatchApkBatch", "BuildCatchApk")]
        public void BatchEntry_WaitsForMetaEditorReady(string file, string entry, string work)
        {
            string src = File.ReadAllText(Path.Combine(Assets, file));
            var body = Regex.Match(src, @"static void " + entry + @"\(\)\s*\{(?<b>[^}]*)\}");
            Assert.That(body.Success, entry + " 없음");
            StringAssert.Contains("EditorApplication.update +=", body.Groups["b"].Value);
            Assert.That(Regex.IsMatch(body.Groups["b"].Value, @"\b" + work + @"\s*\("), Is.False, entry + " 가 " + work + " 를 바로 부르면 안 됨");
            StringAssert.Contains("OVRProjectConfig.CachedProjectConfig == null", src);
        }

        [Test]
        public void CommentLint_FlagsCommentsButNotUsageHeaderOrStrings()
        {
            string[] src =
            {
                "// Unity -batchmode -executeMethod X.Run",
                "using UnityEngine;",
                "class A {",
                "  string u = \"https://example.com\"; string v = @\"a//b\"; char c = '/';",
                "  void F() { } // 이유",
                "  /* 블록 */",
                "  // 줄 주석",
                "}",
            };
            Assert.That(GateFileChecks.CommentLines(src), Is.EqualTo(new[] { 5, 6, 7 }));
        }

        [Test]
        public void CommentLint_HeaderWithoutUsageIsFlagged()
        {
            Assert.That(GateFileChecks.CommentLines(new[] { "// 이 파일은 왜 이렇게", "using X;" }), Is.EqualTo(new[] { 1 }));
        }
    }
}
