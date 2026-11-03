using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MoodPicker.Hands;
using NUnit.Framework;
using UnityEngine;

namespace MoodPicker.Tests
{
    public class HandInputTests
    {
        static string Root => Directory.GetParent(Application.dataPath).FullName;
        static string FixtureDir => Path.Combine(Application.dataPath, "Tests", "EditMode", "Fixtures");

        static (List<PinchEvent> starts, List<PinchEvent> ends) Replay(string file)
        {
            var traj = HandTrajectory.FromJson(File.ReadAllText(Path.Combine(FixtureDir, file)));
            IHandInput input = new RecordedHandInput(traj);
            var det = new PinchDetector();
            var starts = new List<PinchEvent>();
            var ends = new List<PinchEvent>();
            det.PinchStarted += starts.Add;
            det.PinchEnded += ends.Add;
            while (input.TryGetFrame(out var f)) det.Update(f);
            return (starts, ends);
        }

        [Test]
        public void SyntheticFixture_RightPinchStartsAndEnds()
        {
            var (starts, ends) = Replay("hand_pinch_synthetic.json");
            var rs = starts.Where(e => e.hand == Handedness.Right).ToList();
            var re = ends.Where(e => e.hand == Handedness.Right).ToList();
            Assert.That(rs.Count, Is.EqualTo(1), "1프레임 흔들림(1.5s)은 핀치가 아니어야 함");
            Assert.That(re.Count, Is.EqualTo(1));
            Assert.That(rs[0].t, Is.InRange(0.5f, 0.6f));
            Assert.That(re[0].t, Is.InRange(1.19f, 1.22f));
            Assert.That(rs[0].point.x, Is.EqualTo(0.154f).Within(0.002f));
        }

        [Test]
        public void SyntheticFixture_TrackingLossEndsPinch()
        {
            var (starts, ends) = Replay("hand_pinch_synthetic.json");
            var ls = starts.Where(e => e.hand == Handedness.Left).ToList();
            var le = ends.Where(e => e.hand == Handedness.Left).ToList();
            Assert.That(ls.Count, Is.EqualTo(1));
            Assert.That(le.Count, Is.EqualTo(1));
            Assert.That(le[0].t, Is.InRange(0.79f, 0.82f));
        }

        [Test]
        public void RecordedDeviceFixtures_HaveAtLeastOnePinchPair()
        {
            var files = Directory.GetFiles(FixtureDir, "hand_device_*.json").Select(Path.GetFileName).ToList();
            if (files.Count == 0) Assert.Ignore("실기 녹화 픽스처 없음 (hand_device_*.json)");
            foreach (var f in files)
            {
                var (starts, ends) = Replay(f);
                Assert.That(starts.Count, Is.GreaterThan(0), f + ": 핀치 시작 없음");
                Assert.That(ends.Count, Is.GreaterThan(0), f + ": 핀치 끝 없음");
            }
        }

        [Test]
        public void TrajectoryJson_RoundTrips()
        {
            var a = HandTrajectory.FromJson(File.ReadAllText(Path.Combine(FixtureDir, "hand_pinch_synthetic.json")));
            var b = HandTrajectory.FromJson(a.ToJson());
            Assert.That(b.frames.Count, Is.EqualTo(a.frames.Count));
            Assert.That(b.frames[40].right.indexTip, Is.EqualTo(a.frames[40].right.indexTip));
        }

        // ---- 🔒 제스처 코드는 IHandInput 만 읽는다
        static readonly string[] OvrHandTypes = { "OVRHand", "OVRSkeleton", "OVRInput", "OVRPlugin", "OVRBone" };
        static readonly string[] AllowedOvrDirs = { "Assets/Scripts/Hands/Ovr/", "Assets/Scripts/FirstLight/" };

        [Test]
        public void OvrHandInput_OnlyReportsTrackedHighConfidenceHands()
        {
            string src = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Hands", "Ovr", "OvrHandInput.cs"));
            Assert.That(Regex.IsMatch(src, @"!\s*hand\.IsTracked"), "앱 시작 직후 원점에 있는 빈 스켈레톤이 '추적 중'으로 새어 나감");
            Assert.That(Regex.IsMatch(src, @"!\s*hand\.IsDataHighConfidence"), "저신뢰 프레임이 새어 나감");
        }

        [Test]
        public void HandsCoreAssembly_DoesNotReferenceOculus()
        {
            var refs = typeof(PinchDetector).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
            Assert.That(refs, Has.None.StartsWith("Oculus"), string.Join(", ", refs));
            string asmdef = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Hands", "Core", "MoodPicker.Hands.asmdef"));
            StringAssert.DoesNotContain("Oculus", asmdef);
        }

        [Test]
        public void GestureScripts_DoNotReadOvrHandDirectly()
        {
            var hits = new List<string>();
            foreach (string cs in Directory.GetFiles(Path.Combine(Application.dataPath, "Scripts"), "*.cs", SearchOption.AllDirectories))
            {
                string rel = cs.Substring(Root.Length + 1).Replace('\\', '/');
                if (AllowedOvrDirs.Any(rel.StartsWith)) continue;
                string[] lines = File.ReadAllLines(cs);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = Regex.Replace(lines[i], "//.*$", "");
                    foreach (string t in OvrHandTypes)
                        if (Regex.IsMatch(code, @"\b" + t + @"\b")) hits.Add($"{rel}:{i + 1} {t}");
                }
            }
            Assert.That(hits, Is.Empty, "IHandInput 으로 읽을 것:\n" + string.Join("\n", hits));
        }
    }
}
