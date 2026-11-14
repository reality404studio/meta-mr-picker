using MoodPicker.Hands;
using MoodPicker.Ritual;
using MoodPicker.Splatter;
using NUnit.Framework;
using UnityEngine;

namespace MoodPicker.Tests
{
    public class RitualRuntimeTests
    {
        const float Dt = 1f / 72f;
        GameObject _root;
        TimeRitualConfig _config;
        TimeCategoryAsset _move;
        TimeCategoryAsset _work;
        SplatSurfaceMaterial _look;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("RitualTestRoot");
            _config = ScriptableObject.CreateInstance<TimeRitualConfig>();
            _config.showMinutesLabel = false;
            _move = ScriptableObject.CreateInstance<TimeCategoryAsset>();
            _move.id = "move";
            _work = ScriptableObject.CreateInstance<TimeCategoryAsset>();
            _work.id = "work";
            _look = SplatSurfaceMaterial.Clay("test");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_move);
            Object.DestroyImmediate(_work);
        }

        TimePieceView Pulled(TimeCategoryAsset category, int index, int units, Vector3 origin, Vector3 dir)
        {
            var piece = new TimePieceView(_root.transform, category, index, _look, null, _config, null, null, null);
            piece.BeginPull(origin, dir);
            float length = units * _config.unitLength + 0.001f;
            int current = 0;
            float now = 0f;
            for (int i = 1; i <= 60; i++)
            {
                float raw = length * i / 60f;
                current = TimeUnits.Step(raw, current, _config.unitLength, _config.hysteresis, _config.maxUnits);
                piece.UpdatePull(origin + dir.normalized * raw, current, raw, Dt);
                now += Dt;
                piece.Tick(now, Dt);
            }
            piece.ReleasePull();
            for (int i = 0; i < 90; i++)
            {
                now += Dt;
                piece.Tick(now, Dt);
            }
            return piece;
        }

        [Test]
        public void APullKeepsExactlyTheSplatsOfItsTenMinuteUnits()
        {
            TimePieceView piece = Pulled(_move, 0, 4, Vector3.zero, Vector3.forward);
            Assert.That(piece.Units, Is.EqualTo(4));
            Assert.That(piece.state, Is.EqualTo(PieceState.Floating));
            Assert.That(piece.SplatCount, Is.EqualTo(2 * _config.capSplats + 4 * _config.SplatsPerUnit));
            Assert.That(piece.Length, Is.EqualTo(4 * _config.unitLength).Within(1e-6f));
            Assert.That(piece.NodeAmount, Is.EqualTo(0f), "놓으면 마디가 녹는다");
            piece.Kill();
        }

        [Test]
        public void NodesStayWhenTheTesterAsksForAlwaysVisible()
        {
            _config.nodesAlwaysVisible = true;
            TimePieceView piece = Pulled(_move, 0, 3, Vector3.zero, Vector3.forward);
            Assert.That(piece.NodeAmount, Is.EqualTo(1f));
            piece.Kill();
        }

        [TestCase(MergedShape.Blob, PieceShape.Blob)]
        [TestCase(MergedShape.Rod, PieceShape.Rod)]
        public void MergingAddsUnitsAndLosesNoMatter(MergedShape wanted, PieceShape expected)
        {
            TimePieceView a = Pulled(_move, 0, 4, Vector3.zero, Vector3.forward);
            TimePieceView b = Pulled(_move, 0, 2, new Vector3(0.04f, 0f, 0f), Vector3.forward);
            Assert.That(a.DistanceTo(b), Is.LessThan(_config.mergeDistance));
            a.Absorb(b, wanted);
            Assert.That(a.Units, Is.EqualTo(6), "합친 단위 수는 두 조각의 합");
            Assert.That(a.data.mergedCount, Is.EqualTo(1));
            Assert.That(a.shape, Is.EqualTo(expected));
            Assert.That(b.Alive, Is.False);
            Assert.That(a.SplatCount, Is.EqualTo(2 * _config.capSplats + 6 * _config.SplatsPerUnit));
            float now = 10f;
            for (int i = 0; i < 200; i++)
            {
                now += Dt;
                a.Tick(now, Dt);
            }
            Assert.That(a.Awake, Is.False, "합쳐진 뒤에는 계산이 잠든다");
            a.Kill();
        }

        RitualJar Jar()
        {
            return new RitualJar(_root.transform, _config, null, null, null, c => _look);
        }

        static void Finish(RitualJar jar, ref float now)
        {
            for (int i = 0; i < 72 * 8 && jar.Pouring; i++)
            {
                now += Dt;
                jar.Tick(now, Dt);
            }
        }

        [Test]
        public void PouringStacksLayersFromTheBottomInOrder()
        {
            RitualJar jar = Jar();
            float now = 0f;
            TimePieceView first = Pulled(_move, 0, 6, new Vector3(0.3f, 0.3f, 0f), Vector3.forward);
            RitualJar.Layer bottom = jar.BeginPour(first);
            Assert.That(bottom, Is.Not.Null);
            Finish(jar, ref now);
            Assert.That(jar.Pouring, Is.False, "붓기는 끝난다");
            Assert.That(first.Alive, Is.False, "조각은 전부 모래가 된다");
            Assert.That(jar.TotalUnits, Is.EqualTo(6));
            float oneHour = RitualFill.HeightAfter(_config.jar, _config.hoursWhenFull, 6);
            Assert.That(jar.SurfaceMean, Is.EqualTo(oneHour).Within(0.0003f), "쌓이는 높이는 시간에 비례");

            TimePieceView second = Pulled(_work, 1, 3, new Vector3(0.3f, 0.3f, 0f), Vector3.forward);
            RitualJar.Layer upper = jar.BeginPour(second);
            Finish(jar, ref now);
            Assert.That(jar.Layers.Count, Is.EqualTo(2));
            Assert.That(jar.TotalUnits, Is.EqualTo(9));
            for (int i = 0; i < upper.cloud.Count; i++)
            {
                float angle = Mathf.Atan2(upper.cloud.home[i].z, upper.cloud.home[i].x);
                Assert.That(upper.cloud.home[i].y, Is.GreaterThanOrEqualTo(Strata.At(bottom.top, angle) - 1e-5f), "넣은 순서대로 위에 쌓인다");
            }
            Assert.That(bottom.landedCount, Is.EqualTo(bottom.cloud.Count));
            Assert.That(bottom.cloud.Count, Is.GreaterThan(500), "1시간 층이 눈에 띌 만큼 알갱이가 있다");
            Assert.That(jar.TotalGrains, Is.LessThanOrEqualTo(_config.maxGrains));
            jar.Dispose();
        }

        [Test]
        public void TheJarRefusesWhatDoesNotFitAndNeverExceedsTheGrainCap()
        {
            _config.maxGrains = 6000;
            _config.capGrains = 600;
            RitualJar jar = Jar();
            float now = 0f;
            Assert.That(jar.CanAccept(jar.Capacity + 1), Is.False);
            int pours = Mathf.RoundToInt(_config.hoursWhenFull * RitualFill.UnitsPerHour / 24f);
            for (int k = 0; k < pours; k++)
            {
                TimePieceView piece = Pulled(k % 2 == 0 ? _move : _work, k % 2, 24, new Vector3(0.3f, 0.3f, 0f), Vector3.forward);
                Assert.That(jar.BeginPour(piece), Is.Not.Null);
                Finish(jar, ref now);
            }
            Assert.That(jar.TotalUnits, Is.EqualTo(144), "기본은 하루 24시간이 몸통을 채운다");
            Assert.That(jar.TotalGrains, Is.LessThanOrEqualTo(_config.maxGrains), "알갱이 수 상한");
            Assert.That(jar.SurfaceMean, Is.EqualTo(_config.jar.bodyTop).Within(0.001f), "24시간이면 몸통이 가득 찬다");
            jar.Dispose();
        }

        [Test]
        public void BottleLabelUsesSmoothSurfaceAndSeparatesHeaderFromBody()
        {
            Font fraunces = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Fraunces/Fraunces_72pt-Black.ttf");
            Material waxMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ritual/RitualWax.mat");
            var cats = new[]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<TimeCategoryAsset>("Assets/Ritual/Category_sleep.asset"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<TimeCategoryAsset>("Assets/Ritual/Category_move.asset"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<TimeCategoryAsset>("Assets/Ritual/Category_work.asset"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<TimeCategoryAsset>("Assets/Ritual/Category_play.asset"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<TimeCategoryAsset>("Assets/Ritual/Category_drift.asset"),
            };
            Assert.That(fraunces, Is.Not.Null);
            Assert.That(waxMat, Is.Not.Null);

            var testConfig = Object.Instantiate(_config);
            testConfig.showTotals = true;
            var headGo = new GameObject("TestHead");
            headGo.transform.position = new Vector3(0f, 1.17f, 0f);
            var ritualGo = new GameObject("TestRitual");
            ritualGo.transform.SetParent(_root.transform, false);
            var ritual = ritualGo.AddComponent<TimeRitual>();
            var so = new UnityEditor.SerializedObject(ritual);
            so.FindProperty("config").objectReferenceValue = testConfig;
            so.FindProperty("waxMaterial").objectReferenceValue = waxMat;
            so.FindProperty("labelFont").objectReferenceValue = fraunces;
            so.FindProperty("head").objectReferenceValue = headGo.transform;
            so.FindProperty("piecesRoot").objectReferenceValue = ritualGo.transform;
            so.FindProperty("autoPlace").boolValue = false;
            var catsProp = so.FindProperty("categories");
            catsProp.arraySize = cats.Length;
            for (int i = 0; i < cats.Length; i++) catsProp.GetArrayElementAtIndex(i).objectReferenceValue = cats[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            try
            {
                ritual.Init();
                ritual.PlaceAt(new Vector3(0f, 1.0f, 0.42f), Quaternion.identity);
                Transform label = ritual.Jar.root.Find("BottleLabel");
                Assert.That(label, Is.Not.Null);

                Mesh cardMesh = label.Find("LabelCard").GetComponent<MeshFilter>().sharedMesh;
                Mesh borderMesh = label.Find("LabelBorder").GetComponent<MeshFilter>().sharedMesh;
                Mesh textMesh = label.Find("LabelText").GetComponent<MeshFilter>().sharedMesh;
                Assert.That(cardMesh.vertexCount, Is.EqualTo(33 * 13));
                Assert.That(cardMesh.GetIndexCount(0), Is.EqualTo(32 * 12 * 6));
                Assert.That(borderMesh.vertexCount, Is.GreaterThan(100));

                float now = 1f;
                for (int i = 0; i < 40; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }
                Assert.That(textMesh.vertexCount, Is.GreaterThan(100));
                Vector3[] flatVerts = textMesh.vertices;
                for (int i = 0; i < flatVerts.Length; i++)
                    Assert.That(flatVerts[i].z, Is.InRange(-0.0022f, -0.0010f));

                int[] testUnits = { 4, 3, 9, 6, 5 };
                for (int i = 0; i < cats.Length; i++)
                {
                    TimePieceView piece = Pulled(cats[i], i, testUnits[i], new Vector3(0.3f, 0.3f, 0f), Vector3.forward);
                    Assert.That(ritual.Jar.BeginPour(piece), Is.Not.Null);
                    for (int s = 0; s < 72 * 8 && ritual.Jar.Pouring; s++)
                    {
                        now += Dt;
                        ritual.Tick(now, Dt, default);
                    }
                }
                for (int i = 0; i < 30; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }

                typeof(TimeRitual).GetMethod("Seal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(ritual, null);
                for (int i = 0; i < 150; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }

                Vector3[] curvedVerts = textMesh.vertices;
                float cylR = (testConfig.jar.bodyRadius + 0.003f) / 0.56f;
                int outerCurved = 0;
                int farOuterCurved = 0;
                for (int i = 0; i < curvedVerts.Length; i++)
                {
                    if (Mathf.Abs(curvedVerts[i].x) > 0.015f)
                    {
                        outerCurved++;
                        float theta = Mathf.Asin(Mathf.Clamp(curvedVerts[i].x / cylR, -0.99f, 0.99f));
                        float cylZ = cylR * (1f - Mathf.Cos(theta));
                        Assert.That(curvedVerts[i].z, Is.InRange(cylZ - 0.0020f, cylZ - 0.0012f));
                        if (Mathf.Abs(curvedVerts[i].x) > 0.024f)
                        {
                            farOuterCurved++;
                            Assert.That(curvedVerts[i].z, Is.GreaterThan(0.0005f));
                        }
                    }
                }
                Assert.That(outerCurved, Is.GreaterThan(20));
                Assert.That(farOuterCurved, Is.GreaterThan(8));
            }
            finally
            {
                Object.DestroyImmediate(headGo);
                Object.DestroyImmediate(testConfig);
            }
        }
    }
}
