using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MoodPicker.Dab;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MoodPicker.Tests
{
    public class DabMaskTests
    {
        static DabSettings S()
        {
            var s = DabSettings.Defaults();
            s.noiseAmount = 0.2f;
            return s;
        }

        [Test]
        public void Mask_PeaksAtContactAndFallsWithDistance()
        {
            var m = new PaintMask(S());
            var c = new Vector2(0.5f, 0.5f);
            m.Stamp(c, 1f, 0f, true);
            m.Update(1f);
            float prev = m.Sample(c);
            Assert.That(prev, Is.EqualTo(1f).Within(0.01f), "접촉 UV 에서 최대");
            for (int i = 1; i <= 8; i++)
            {
                float v = m.Sample(c + new Vector2(i * 0.03f, 0f));
                Assert.That(v, Is.LessThanOrEqualTo(prev + 0.15f), $"step {i}");
                prev = Mathf.Min(prev, v);
            }
            Assert.That(m.Sample(c + new Vector2(0.3f, 0f)), Is.EqualTo(0f), "멀면 0");
        }

        static float Area(PaintMask m) => m.Values.Count(v => v > 0.5f) / (float)m.Values.Length;

        [Test]
        public void Mask_RadiusGrowsOverTime()
        {
            var s = S();
            var a = new PaintMask(s);
            a.Stamp(new Vector2(0.3f, 0.5f), 1f, 0f, false);
            var areas = new List<float>();
            foreach (float t in new[] { 0.05f, 0.2f, 0.4f, 0.8f })
            {
                var m = new PaintMask(s);
                m.Stamp(new Vector2(0.3f, 0.5f), 1f, 0f, false);
                m.Update(t);
                areas.Add(Area(m));
            }
            for (int i = 1; i < areas.Count; i++) Assert.That(areas[i], Is.GreaterThan(areas[i - 1]), string.Join(",", areas));
            var press = new PaintMask(s);
            press.Stamp(new Vector2(0.3f, 0.5f), 1f, 0f, true);
            press.Update(1f);
            Assert.That(Area(press), Is.GreaterThan(areas.Last()), "꾹 > 톡");
        }

        [Test]
        public void Mask_WrapsAroundBottleSeam()
        {
            var m = new PaintMask(S());
            m.Stamp(new Vector2(0.01f, 0.5f), 1f, 0f, true);
            m.Update(1f);
            Assert.That(m.Sample(new Vector2(0.97f, 0.5f)), Is.GreaterThan(0.5f), "U 가 이어짐");
        }

        [Test]
        public void Mask_ResolutionIsCapped()
        {
            var s = DabSettings.Defaults();
            s.maskResolution = 2048;
            Assert.That(new PaintMask(s).Size, Is.EqualTo(DabSettings.MaxMaskResolution));
            Assert.That(DabSettings.ClampResolution(4096), Is.LessThanOrEqualTo(256));
            var asset = AssetDatabase.LoadAssetAtPath<DabSettings>(DabBottleAssets.SettingsPath);
            if (asset != null) Assert.That(asset.maskResolution, Is.LessThanOrEqualTo(DabSettings.MaxMaskResolution));
        }

        [Test]
        public void Mask_RuntimeTakesPaintFromPortal()
        {
            string src = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Dab", "Runtime", "DabController.cs"));
            StringAssert.Contains("portal.PaintPicked +=", src);
            StringAssert.Contains("CelPaint.Apply(", src);
            StringAssert.Contains("portal.CurrentPaint == null", src);
        }
    }

    public class DabJudgeTests
    {
        const float Dt = 1f / 72f;

        class Run
        {
            public readonly HandJudge judge = new HandJudge(DabSettings.Defaults());
            public readonly List<DabJudgement> out_ = new List<DabJudgement>();
            public float t;
            public Run() { judge.Judged += j => out_.Add(j); }
            public Run Path(float seconds, Func<float, float> dist, bool tracked = true)
            {
                float t0 = t, end = t + seconds;
                while (t < end - 1e-6f) { judge.Update(t, dist(t - t0), tracked); t += Dt; }
                return this;
            }
        }

        static Func<float, float> Poke(float speed, float depth, float hold)
        {
            float tIn = (0.05f + depth) / speed;
            return x =>
            {
                if (x < tIn) return 0.05f - speed * x;
                if (x < tIn + hold) return -depth;
                return -depth + speed * (x - tIn - hold);
            };
        }

        [Test]
        public void Judge_TapWithin300ms()
        {
            var r = new Run().Path(1f, Poke(0.4f, 0.006f, 0.05f));
            Assert.That(r.out_.Count, Is.EqualTo(1));
            Assert.That(r.out_[0].kind, Is.EqualTo(DabKind.Tap));
            Assert.That(r.out_[0].t - r.out_[0].contactT, Is.LessThanOrEqualTo(0.3f));
        }

        [Test]
        public void Judge_PressByDepthOrHoldWithin300ms()
        {
            var deep = new Run().Path(1.5f, Poke(0.3f, 0.02f, 0.3f));
            Assert.That(deep.out_.Select(j => j.kind), Is.EqualTo(new[] { DabKind.Press }));
            Assert.That(deep.out_[0].t - deep.out_[0].contactT, Is.LessThanOrEqualTo(0.3f));
            var hold = new Run().Path(2f, Poke(0.3f, 0.008f, 0.6f));
            Assert.That(hold.out_.Select(j => j.kind), Is.EqualTo(new[] { DabKind.Press }));
            Assert.That(hold.out_[0].t - hold.out_[0].contactT, Is.LessThanOrEqualTo(0.3f));
        }

        static IEnumerable<(string, Action<Run>)> NonActions()
        {
            yield return ("hover 5mm", r => r.Path(2f, _ => 0.005f));
            yield return ("slow rest into surface", r => r.Path(3f, x => 0.03f - 0.015f * x));
            yield return ("graze 1mm", r => r.Path(2f, x => 0.001f + 0.001f * Mathf.Sin(x * 10f)));
            yield return ("untracked garbage", r => r.Path(2f, x => -0.05f * Mathf.Sin(x * 40f), tracked: false));
            yield return ("jitter around surface", r => r.Path(2f, x => 0.002f * Mathf.Sin(x * 70f)));
            yield return ("start inside, pull out", r => r.Path(1f, x => -0.02f + 0.1f * x));
            yield return ("approach 1mm and back", r => r.Path(1f, x => 0.05f - 0.098f * Mathf.Sin(Mathf.Clamp01(x) * Mathf.PI) * 0.5f));
            yield return ("resting inside", r => r.Path(2f, _ => -0.01f));
            yield return ("tracking blip near surface", r => { r.Path(0.3f, _ => 0.004f); r.Path(0.1f, _ => -0.02f, tracked: false); r.Path(0.5f, _ => -0.004f); });
            yield return ("slow drift out", r => r.Path(2f, x => 0.002f + 0.01f * x));
        }

        [Test]
        public void Judge_NonActionFixturesFalsePositiveAtMostOneInTen()
        {
            var fired = new List<string>();
            var all = NonActions().ToList();
            Assert.That(all.Count, Is.EqualTo(10));
            foreach (var (name, act) in all)
            {
                var r = new Run();
                act(r);
                if (r.out_.Count > 0) fired.Add(name);
            }
            Assert.That(fired.Count, Is.LessThanOrEqualTo(1), "false dabs: " + string.Join(", ", fired));
        }

        [Test]
        public void Judge_NoMlPackagesOrReferences()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string manifest = File.ReadAllText(Path.Combine(root, "Packages", "manifest.json"));
            string[] banned = { "com.unity.sentis", "com.unity.ai.inference", "com.unity.barracuda", "com.unity.ml-agents" };
            foreach (string b in banned) Assert.That(manifest.Contains(b), Is.False, b + " 금지 (휴리스틱만, 2026-11-03)");
            foreach (string asm in Directory.GetFiles(Path.Combine(root, "Assets"), "*.asmdef", SearchOption.AllDirectories))
            {
                string a = File.ReadAllText(asm);
                Assert.That(a.Contains("Sentis") || a.Contains("InferenceEngine") || a.Contains("Barracuda"), Is.False, asm);
            }
        }
    }

    public class DabObjectTests
    {
        [Test]
        public void Bottle_WithinPolyBudgetWithUvAndRoughBottleSize()
        {
            var m = DabBottleAssets.BuildMesh();
            Assert.That(m.triangles.Length / 3, Is.LessThanOrEqualTo(DabBottleAssets.TriangleBudget));
            Assert.That(m.uv.Length, Is.EqualTo(m.vertexCount));
            Assert.That(m.bounds.size.y, Is.InRange(0.2f, 0.26f));
            Assert.That(m.bounds.size.x, Is.InRange(0.05f, 0.08f));
            UnityEngine.Object.DestroyImmediate(m);
        }

        [Test]
        public void Bottle_InCatchSceneWithCelMaterialsAndColliders()
        {
            string scenePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Assets", "Scenes", "Catch.unity");
            string scene = File.ReadAllText(scenePath);
            StringAssert.Contains("m_Name: PetBottle", scene);
            var bottleMat = AssetDatabase.LoadAssetAtPath<Material>(DabBottleAssets.BottleMatPath);
            var outlineMat = AssetDatabase.LoadAssetAtPath<Material>(DabBottleAssets.OutlineMatPath);
            Assert.That(bottleMat, Is.Not.Null);
            Assert.That(outlineMat, Is.Not.Null);
            Assert.That(bottleMat.shader.name, Is.EqualTo(CelPaint.ShaderName));
            Assert.That(outlineMat.shader.name, Is.EqualTo("MoodPicker/CelOutline"));
            Assert.That(outlineMat.GetFloat("_Cull"), Is.EqualTo(1f));
            Assert.That(scene.Contains(AssetDatabase.AssetPathToGUID(DabBottleAssets.BottleMatPath)), "씬 병에 셀 머티리얼");
            Assert.That(scene.Contains(AssetDatabase.AssetPathToGUID(DabBottleAssets.OutlineMatPath)), "씬 병에 외곽선 머티리얼");
            Assert.That(scene.Contains(AssetDatabase.AssetPathToGUID(DabBottleAssets.MeshPath)), "씬 병에 메시");
            Assert.That(System.Text.RegularExpressions.Regex.Matches(scene, @"--- !u!64 ").Count, Is.GreaterThanOrEqualTo(2), "MeshCollider 2개 (볼록 접촉 + UV)");
        }
    }
}
