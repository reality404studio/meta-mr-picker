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
    public class DabBucketTests
    {
        const float Dt = 1f / 72f;

        class Run
        {
            public readonly DabBucket<string> bucket = new DabBucket<string>(DabSettings.Defaults());
            public readonly List<(string paint, float t)> filled = new List<(string, float)>();
            public float t, contactT = -1f;
            public Run() { bucket.Filled += (p, x) => filled.Add((p, x)); }
            public Run Path(float seconds, Func<float, float> dist, string paint, bool tracked = true)
            {
                float t0 = t, end = t + seconds;
                while (t < end - 1e-6f)
                {
                    float d = dist(t - t0);
                    if (contactT < 0f && tracked && d <= 0f) contactT = t;
                    bucket.Update(t, d, tracked, paint);
                    t += Dt;
                }
                return this;
            }
        }

        static float Touch(float x) => x < 0.3f ? 0.05f - 0.2f * x : x < 0.6f ? -0.01f : -0.01f + 0.2f * (x - 0.6f);

        [Test]
        public void Bucket_OneTouchWithPaintFillsWholeObjectWithin300ms()
        {
            var r = new Run().Path(1.2f, Touch, "paint7");
            Assert.That(r.filled.Count, Is.EqualTo(1));
            Assert.That(r.filled[0].paint, Is.EqualTo("paint7"));
            Assert.That(r.bucket.Paint, Is.EqualTo("paint7"), "오브제 전체가 그 물감");
            float full = -1f;
            for (float t = r.contactT; t < r.contactT + 1f; t += Dt) if (r.bucket.Fill(t) >= 1f) { full = t; break; }
            Assert.That(full - r.contactT, Is.InRange(0f, 0.3f), "접촉부터 0.3초 안에 다 채워짐");
        }

        [Test]
        public void Bucket_EmptyFingertipChangesNothing()
        {
            var r = new Run().Path(1.2f, Touch, null);
            Assert.That(r.filled, Is.Empty);
            Assert.That(r.bucket.Paint, Is.Null);
            Assert.That(r.bucket.Fill(r.t), Is.EqualTo(0f));
        }

        [Test]
        public void Bucket_NewPaintReplacesOnNextTouchAndHoldingDoesNotRefill()
        {
            var r = new Run().Path(0.45f, Touch, "a");
            Assume.That(r.filled.Count, Is.EqualTo(1));
            r.Path(2f, _ => -0.01f, "b");
            Assert.That(r.filled.Count, Is.EqualTo(1), "대고 있는 동안 다시 안 채움");
            r.Path(0.3f, _ => 0.05f, "b").Path(1.2f, Touch, "b");
            Assert.That(r.filled.Select(x => x.paint), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(r.bucket.Paint, Is.EqualTo("b"));
        }

        [Test]
        public void Bucket_SlowBringCountsHoverDoesNot()
        {
            var slow = new Run().Path(4f, x => 0.03f - 0.012f * x, "p");
            Assert.That(slow.filled.Count, Is.EqualTo(1), "천천히 가져다 대도 채움");
            var hover = new Run().Path(2f, _ => 0.002f, "p");
            Assert.That(hover.filled, Is.Empty);
        }

        [Test]
        public void Bucket_RuntimeUsesPortalPaintAndSkipsWashedFingertip()
        {
            string src = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Dab", "Runtime", "DabController.cs"));
            StringAssert.Contains("portal.PaintPicked +=", src);
            StringAssert.Contains("portal.CurrentPaint == null", src);
            StringAssert.Contains("DabBucket<PaintRecord>", src);
            StringAssert.Contains("CelPaint.Apply(", src);
            Assert.That(File.Exists(Path.Combine(Application.dataPath, "Scripts", "Dab", "Core", "PaintMask.cs")), Is.False, "번짐 마스크는 버킷으로 대체 (REJECTED 2026-11-04)");
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
