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
            testConfig.timeLabels = true;
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

        sealed class FakeMicrogestureInput : MonoBehaviour, IMicrogestureInput
        {
            public Microgesture left;
            public Microgesture right;
            public Microgesture Current(Handedness hand) => hand == Handedness.Left ? left : right;
        }

        static HandPose PinchPose(Vector3 pos, bool pinching)
        {
            float gap = pinching ? 0.005f : 0.055f;
            return new HandPose
            {
                tracked = true,
                thumbTip = pos,
                indexTip = pos + new Vector3(gap, 0f, 0f),
                middleTip = pos + new Vector3(0.02f, 0.01f, -0.04f),
                ringTip = pos + new Vector3(0.03f, 0f, -0.045f),
                littleTip = pos + new Vector3(0.04f, -0.01f, -0.05f),
                palm = pos + new Vector3(0f, -0.02f, -0.06f),
                palmNormal = Vector3.down
            };
        }

        [Test]
        public void ScenePlacesFocusedAndExploredInCenterAndWiresMicrogesture()
        {
            UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Scenes/TimeRitual.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                TimeRitual ritual = null;
                foreach (GameObject go in scene.GetRootGameObjects())
                {
                    ritual = go.GetComponentInChildren<TimeRitual>(true);
                    if (ritual != null) break;
                }
                Assert.That(ritual, Is.Not.Null);
                var pull = ritual.GetComponent<TimePullController>();
                Assert.That(pull, Is.Not.Null);
                Assert.That(pull.Heaps.Length, Is.EqualTo(5));
                Assert.That(pull.Heaps[0].Category.id, Is.EqualTo("sleep"));
                Assert.That(pull.Heaps[1].Category.id, Is.EqualTo("move"));
                Assert.That(pull.Heaps[2].Category.id, Is.EqualTo("work"));
                Assert.That(pull.Heaps[3].Category.id, Is.EqualTo("drift"));
                Assert.That(pull.Heaps[4].Category.id, Is.EqualTo("play"));
                for (int i = 0; i < 4; i++)
                    Assert.That(pull.Heaps[i].transform.localPosition.x, Is.LessThan(pull.Heaps[i + 1].transform.localPosition.x));

                var rso = new UnityEditor.SerializedObject(ritual);
                Object microObj = rso.FindProperty("microgestureSource").objectReferenceValue;
                Assert.That(microObj, Is.Not.Null);
                Assert.That(microObj, Is.InstanceOf<IMicrogestureInput>());
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void RightHandMicrogestureTogglesGuideTrailsAndUpdatesStreams()
        {
            TimeCategoryAsset[] cats =
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<TimeCategoryAsset>("Assets/Ritual/Category_sleep.asset"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<TimeCategoryAsset>("Assets/Ritual/Category_move.asset"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<TimeCategoryAsset>("Assets/Ritual/Category_work.asset"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<TimeCategoryAsset>("Assets/Ritual/Category_drift.asset"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<TimeCategoryAsset>("Assets/Ritual/Category_play.asset"),
            };
            var testConfig = Object.Instantiate(_config);
            testConfig.guideTrails = true;
            var ritualGo = new GameObject("GuideTestRitual");
            ritualGo.transform.SetParent(_root.transform, false);

            var heaps = new TimeHeap[cats.Length];
            for (int i = 0; i < cats.Length; i++)
            {
                var hgo = new GameObject("Heap_" + cats[i].id);
                hgo.transform.SetParent(ritualGo.transform, false);
                hgo.transform.localPosition = new Vector3((i - 2) * 0.12f, 0f, 0f);
                hgo.AddComponent<MeshFilter>();
                hgo.AddComponent<MeshRenderer>();
                hgo.AddComponent<SplatLetterRenderer>();
                var heap = hgo.AddComponent<TimeHeap>();
                var hso = new UnityEditor.SerializedObject(heap);
                hso.FindProperty("category").objectReferenceValue = cats[i];
                hso.FindProperty("config").objectReferenceValue = testConfig;
                hso.ApplyModifiedPropertiesWithoutUndo();
                heaps[i] = heap;
            }

            var pull = ritualGo.AddComponent<TimePullController>();
            var pso = new UnityEditor.SerializedObject(pull);
            var hp = pso.FindProperty("heaps");
            hp.arraySize = heaps.Length;
            for (int i = 0; i < heaps.Length; i++) hp.GetArrayElementAtIndex(i).objectReferenceValue = heaps[i];
            pso.ApplyModifiedPropertiesWithoutUndo();

            var micro = ritualGo.AddComponent<FakeMicrogestureInput>();
            var ritual = ritualGo.AddComponent<TimeRitual>();
            var rso = new UnityEditor.SerializedObject(ritual);
            rso.FindProperty("config").objectReferenceValue = testConfig;
            rso.FindProperty("pull").objectReferenceValue = pull;
            rso.FindProperty("microgestureSource").objectReferenceValue = micro;
            rso.FindProperty("piecesRoot").objectReferenceValue = ritualGo.transform;
            rso.FindProperty("autoPlace").boolValue = false;
            var cp = rso.FindProperty("categories");
            cp.arraySize = cats.Length;
            for (int i = 0; i < cats.Length; i++) cp.GetArrayElementAtIndex(i).objectReferenceValue = cats[i];
            rso.ApplyModifiedPropertiesWithoutUndo();

            try
            {
                ritual.Init();
                ritual.PlaceAt(new Vector3(0f, 1.0f, 0.42f), Quaternion.identity);
                Assert.That(ritual.GuideTrailsEnabled, Is.True);

                float now = 1f;
                for (int i = 0; i < 20; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }
                Assert.That(ritual.GuideTrails.Visible, Is.True);
                Assert.That(ritual.GuideTrails.Stream0Alpha, Is.GreaterThan(0.2f));
                Assert.That(ritual.GuideTrails.Stream1Alpha, Is.GreaterThan(0.2f));
                Assert.That(Vector3.Distance(ritual.GuideTrails.Stream0Start, heaps[2].transform.position), Is.LessThan(0.06f));
                Assert.That(Vector3.Distance(ritual.GuideTrails.Stream1Start, heaps[3].transform.position), Is.LessThan(0.06f));

                Vector3 workPos = heaps[2].transform.position;
                Vector3 start1 = workPos + Vector3.up * 0.01f;
                Vector3 farRight = workPos + new Vector3(0.5f, 0.5f, 0f);
                HandFrame pullFrame = new HandFrame
                {
                    t = now,
                    left = PinchPose(start1, false),
                    right = PinchPose(farRight, false)
                };
                now += Dt;
                pullFrame.t = now;
                ritual.Tick(now, Dt, pullFrame);
                for (int i = 0; i < 6; i++)
                {
                    now += Dt;
                    pullFrame.t = now;
                    pullFrame.left = PinchPose(start1, true);
                    ritual.Tick(now, Dt, pullFrame);
                }
                for (int i = 0; i < 30; i++)
                {
                    float u = i / 29f;
                    Vector3 tip = workPos + new Vector3(-0.07f * u, 0.01f + 0.07f * u, 0f);
                    now += Dt;
                    pullFrame.t = now;
                    pullFrame.left = PinchPose(tip, true);
                    ritual.Tick(now, Dt, pullFrame);
                }
                pullFrame.left = PinchPose(workPos + new Vector3(-0.07f, 0.08f, 0f), false);
                for (int i = 0; i < 20; i++)
                {
                    now += Dt;
                    pullFrame.t = now;
                    ritual.Tick(now, Dt, pullFrame);
                }

                Vector3 start2 = workPos + new Vector3(0.025f, -0.02f, 0f);
                now += Dt;
                pullFrame.t = now;
                pullFrame.right = PinchPose(start2, false);
                ritual.Tick(now, Dt, pullFrame);
                for (int i = 0; i < 6; i++)
                {
                    now += Dt;
                    pullFrame.t = now;
                    pullFrame.right = PinchPose(start2, true);
                    ritual.Tick(now, Dt, pullFrame);
                }
                for (int i = 0; i < 30; i++)
                {
                    float u = i / 29f;
                    Vector3 tip = start2 + Vector3.up * (0.085f * u);
                    now += Dt;
                    pullFrame.t = now;
                    pullFrame.right = PinchPose(tip, true);
                    ritual.Tick(now, Dt, pullFrame);
                }
                pullFrame.right = PinchPose(start2 + Vector3.up * 0.085f, false);
                for (int i = 0; i < 35; i++)
                {
                    now += Dt;
                    pullFrame.t = now;
                    ritual.Tick(now, Dt, pullFrame);
                }
                Assert.That(ritual.Pieces.Count, Is.EqualTo(2));
                Assert.That(Vector3.Distance(ritual.GuideTrails.Stream0End, ritual.Jar.Spout), Is.LessThan(0.02f));
                Assert.That(ritual.GuideTrails.Stream1Alpha, Is.GreaterThan(0.15f), "같은 카테고리 조각끼리는 합치기 빛 궤적이 이어진다");

                micro.left = Microgesture.ThumbTap;
                for (int i = 0; i < 5; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }
                Assert.That(ritual.GuideTrailsEnabled, Is.True, "왼손 마이크로제스쳐는 가이드를 끄지 않는다");
                micro.left = Microgesture.None;

                micro.right = Microgesture.ThumbTap;
                for (int i = 0; i < 40; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }
                Assert.That(ritual.GuideTrailsEnabled, Is.False, "오른손 마이크로제스쳐로 가이드를 끈다");
                Assert.That(ritual.GuideTrails.Visible, Is.False);

                micro.right = Microgesture.None;
                for (int i = 0; i < 30; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }

                micro.right = Microgesture.SwipeRight;
                for (int i = 0; i < 20; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }
                Assert.That(ritual.GuideTrailsEnabled, Is.True, "오른손 마이크로제스쳐로 가이드를 다시 켠다");
                Assert.That(ritual.GuideTrails.Visible, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(testConfig);
            }
        }

        [Test]
        public void SealedJarRotatesWithTwoHandPinchAtFixedScaleAndReturnsUprightOnRelease()
        {
            Material glowMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ritual/RitualGlow.mat");
            Assert.That(glowMat, Is.Not.Null);
            TimeCategoryAsset[] cats =
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<TimeCategoryAsset>("Assets/Ritual/Category_sleep.asset"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<TimeCategoryAsset>("Assets/Ritual/Category_move.asset"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<TimeCategoryAsset>("Assets/Ritual/Category_work.asset"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<TimeCategoryAsset>("Assets/Ritual/Category_drift.asset"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<TimeCategoryAsset>("Assets/Ritual/Category_play.asset"),
            };

            var testConfig = Object.Instantiate(_config);
            testConfig.showTotals = false;
            var ritualGo = new GameObject("JarRotateTestRitual");
            ritualGo.transform.SetParent(_root.transform, false);
            var ritual = ritualGo.AddComponent<TimeRitual>();
            var rso = new UnityEditor.SerializedObject(ritual);
            rso.FindProperty("config").objectReferenceValue = testConfig;
            rso.FindProperty("glowMaterial").objectReferenceValue = glowMat;
            rso.FindProperty("piecesRoot").objectReferenceValue = ritualGo.transform;
            rso.FindProperty("autoPlace").boolValue = false;
            var cp = rso.FindProperty("categories");
            cp.arraySize = cats.Length;
            for (int i = 0; i < cats.Length; i++) cp.GetArrayElementAtIndex(i).objectReferenceValue = cats[i];
            rso.ApplyModifiedPropertiesWithoutUndo();

            try
            {
                ritual.Init();
                ritual.PlaceAt(new Vector3(0f, 1.0f, 0.42f), Quaternion.identity);
                float now = 1f;
                for (int c = 1; c <= 2; c++)
                {
                    TimePieceView piece = Pulled(cats[c], c, 6, new Vector3(0.3f, 0.3f, 0f), Vector3.forward);
                    Assert.That(ritual.Jar.BeginPour(piece), Is.Not.Null);
                    for (int s = 0; s < 72 * 8 && ritual.Jar.Pouring; s++)
                    {
                        now += Dt;
                        ritual.Tick(now, Dt, default);
                    }
                }
                typeof(TimeRitual).GetMethod("Seal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(ritual, null);
                for (int i = 0; i < 20; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }

                Assert.That(ritual.Sealed, Is.True);
                Assert.That(ritual.Glow.Blooming, Is.True);
                MeshFilter auraFilter = ritual.Glow.root.GetComponentInChildren<MeshFilter>();
                Assert.That(auraFilter, Is.Not.Null);
                Vector3[] auraVerts = auraFilter.sharedMesh.vertices;
                float minY = float.MaxValue;
                float maxY = float.MinValue;
                for (int i = 0; i < auraVerts.Length; i++)
                {
                    if (auraVerts[i].y < minY) minY = auraVerts[i].y;
                    if (auraVerts[i].y > maxY) maxY = auraVerts[i].y;
                }
                Assert.That(maxY, Is.GreaterThan(0.14f), "3D 입체 오라가 병 높이를 감싸야 한다");
                Assert.That(minY, Is.GreaterThanOrEqualTo(0f));

                float initialY = ritual.Jar.root.position.y;
                Vector3 initialForward = ritual.Jar.root.forward;
                Vector3 bodyCenter = ritual.Jar.BodyCenter;
                Vector3 leftStart = bodyCenter + new Vector3(-0.06f, 0f, 0f);
                Vector3 rightStart = bodyCenter + new Vector3(0.06f, 0f, 0f);

                HandFrame frame = new HandFrame
                {
                    t = now,
                    left = PinchPose(leftStart, false),
                    right = PinchPose(rightStart, false)
                };
                now += Dt;
                frame.t = now;
                ritual.Tick(now, Dt, frame);

                for (int i = 0; i < 8; i++)
                {
                    now += Dt;
                    frame.t = now;
                    frame.left = PinchPose(leftStart, true);
                    frame.right = PinchPose(rightStart, true);
                    ritual.Tick(now, Dt, frame);
                }

                Vector3 lift = new Vector3(0f, 0.20f, 0f);
                Vector3 leftEnd = bodyCenter + lift + new Vector3(-0.035f, -0.045f, 0.042f);
                Vector3 rightEnd = bodyCenter + lift + new Vector3(0.035f, 0.045f, -0.042f);
                for (int i = 1; i <= 36; i++)
                {
                    float u = i / 36f;
                    now += Dt;
                    frame.t = now;
                    frame.left = PinchPose(Vector3.Lerp(leftStart, leftEnd, u), true);
                    frame.right = PinchPose(Vector3.Lerp(rightStart, rightEnd, u), true);
                    ritual.Tick(now, Dt, frame);
                }

                Assert.That(Vector3.Dot(ritual.Jar.root.up, Vector3.up), Is.LessThan(0.85f), "양손 핀치로 병을 3D 회전시킬 수 있다");
                Assert.That(ritual.Jar.root.localScale, Is.EqualTo(Vector3.one), "크기는 변하지 않아야 한다");
                Assert.That(Vector3.Distance(ritual.Glow.root.position, ritual.Jar.root.position), Is.LessThan(0.03f));

                frame.left = PinchPose(leftEnd, false);
                frame.right = PinchPose(rightEnd, false);
                for (int i = 0; i < 90; i++)
                {
                    now += Dt;
                    frame.t = now;
                    ritual.Tick(now, Dt, frame);
                }

                Assert.That(Vector3.Dot(ritual.Jar.root.up, Vector3.up), Is.GreaterThan(0.99f), "손을 놓으면 부드럽게 수직으로 선다");
                Assert.That(ritual.Jar.root.position.y, Is.GreaterThan(initialY + 0.12f), "들어 올린 높이 근처에 머문다");
                Assert.That(Mathf.Abs(Vector3.Dot(ritual.Jar.root.forward, initialForward)), Is.LessThan(0.85f), "돌려놓은 Yaw 방향을 유지한다");
                Assert.That(ritual.Jar.root.localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                Object.DestroyImmediate(testConfig);
            }
        }

        [Test]
        public void TiltedJarKeepsTopSandCapInsideAndClosesBottomWithGlassAndSand()
        {
            Material glassMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ritual/RitualGlass.mat");
            RitualJar jar = new RitualJar(_root.transform, _config, null, null, glassMat, c => _look);
            try
            {
                float now = 0f;
                TimePieceView first = Pulled(_move, 0, 8, new Vector3(0.3f, 0.3f, 0f), Vector3.forward);
                Assert.That(jar.BeginPour(first), Is.Not.Null);
                Finish(jar, ref now);

                TimePieceView second = Pulled(_work, 1, 6, new Vector3(0.3f, 0.3f, 0f), Vector3.forward);
                Assert.That(jar.BeginPour(second), Is.Not.Null);
                Finish(jar, ref now);

                jar.root.position = new Vector3(0.15f, 1.15f, 0.35f);
                jar.root.rotation = Quaternion.Euler(75f, 15f, 20f);
                now += Dt;
                jar.Tick(now, Dt);

                Assert.That(jar.Cap.DrawCount, Is.GreaterThan(0));
                int capActive = 0;
                for (int i = 0; i < jar.Cap.DrawCount; i++)
                {
                    if (jar.Cap.scale[i] < 0.01f) continue;
                    capActive++;
                    Vector3 worldPos = jar.Cap.WorldOf(i);
                    Vector3 localPos = jar.root.InverseTransformPoint(worldPos);
                    float radial = Mathf.Sqrt(localPos.x * localPos.x + localPos.z * localPos.z);
                    Assert.That(radial, Is.LessThanOrEqualTo(jar.Shape.bodyRadius), "기울여도 뚜껑 스플랫 반경이 병 안쪽에 있어야 한다");
                    Assert.That(localPos.y, Is.EqualTo(jar.SurfaceMean).Within(0.01f), "기울여도 윗면 모래 스플랫이 병 위쪽 표면에 붙어 있어야 한다");
                }
                Assert.That(capActive, Is.GreaterThan(100));

                Assert.That(jar.Floor, Is.Not.Null);
                Assert.That(jar.Floor.DrawCount, Is.GreaterThan(100), "모래 바닥면 스플랫이 채워져야 한다");
                int floorInterior = 0;
                for (int i = 0; i < jar.Floor.DrawCount; i++)
                {
                    if (jar.Floor.scale[i] < 0.01f) continue;
                    Vector3 localPos = jar.root.InverseTransformPoint(jar.Floor.WorldOf(i));
                    float radial = Mathf.Sqrt(localPos.x * localPos.x + localPos.z * localPos.z);
                    Assert.That(localPos.y, Is.InRange(jar.Shape.floor - 0.002f, jar.Shape.floor + 0.004f));
                    if (radial < jar.Shape.bodyRadius * 0.4f) floorInterior++;
                }
                Assert.That(floorInterior, Is.GreaterThan(20), "모래 바닥 안쪽 중심부까지 막혀 있어야 한다");

                if (jar.GlassMesh != null)
                {
                    Vector3[] glassVerts = jar.GlassMesh.vertices;
                    int glassBottomCenter = 0;
                    for (int i = 0; i < glassVerts.Length; i += 4)
                    {
                        Vector3 v = glassVerts[i];
                        float radial = Mathf.Sqrt(v.x * v.x + v.z * v.z);
                        if (v.y <= jar.Shape.floor && radial < jar.Shape.bodyRadius * 0.45f) glassBottomCenter++;
                    }
                    Assert.That(glassBottomCenter, Is.GreaterThan(20), "유리병 바닥면 중심부도 유리 스플랫으로 막혀 있어야 한다");
                }
            }
            finally
            {
                jar.Dispose();
            }
        }

        static TimeCategoryAsset[] ShippedCategories()
        {
            return new[]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<TimeCategoryAsset>("Assets/Ritual/Category_sleep.asset"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<TimeCategoryAsset>("Assets/Ritual/Category_move.asset"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<TimeCategoryAsset>("Assets/Ritual/Category_work.asset"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<TimeCategoryAsset>("Assets/Ritual/Category_drift.asset"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<TimeCategoryAsset>("Assets/Ritual/Category_play.asset"),
            };
        }

        TimeRitual BareRitual(string name, TimeRitualConfig cfg, TimeCategoryAsset[] cats, Material glowMat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var ritual = go.AddComponent<TimeRitual>();
            var so = new UnityEditor.SerializedObject(ritual);
            so.FindProperty("config").objectReferenceValue = cfg;
            so.FindProperty("glowMaterial").objectReferenceValue = glowMat;
            so.FindProperty("piecesRoot").objectReferenceValue = go.transform;
            so.FindProperty("autoPlace").boolValue = false;
            var cp = so.FindProperty("categories");
            cp.arraySize = cats.Length;
            for (int i = 0; i < cats.Length; i++) cp.GetArrayElementAtIndex(i).objectReferenceValue = cats[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            ritual.Init();
            ritual.PlaceAt(new Vector3(0f, 1.0f, 0.42f), Quaternion.identity);
            return ritual;
        }

        static System.Collections.Generic.List<TimePieceView> PiecesOf(TimeRitual ritual)
        {
            var field = typeof(TimeRitual).GetField("_pieces", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return (System.Collections.Generic.List<TimePieceView>)field.GetValue(ritual);
        }

        void DragHeldPieceToward(TimeRitual ritual, TimePieceView held, TimePieceView loose, out float looseShift, ref float now)
        {
            Vector3 grab = held.Center;
            HandFrame frame = new HandFrame { t = now, right = PinchPose(grab, false) };
            now += Dt;
            frame.t = now;
            ritual.Tick(now, Dt, frame);
            for (int i = 0; i < 8; i++)
            {
                now += Dt;
                frame.t = now;
                frame.right = PinchPose(grab, true);
                ritual.Tick(now, Dt, frame);
            }
            Assert.That(held.state, Is.EqualTo(PieceState.Held), "핀치로 조각을 집는다");
            float startX = loose.Center.x;
            looseShift = 0f;
            Vector3 stop = grab + new Vector3(-0.05f, 0f, 0f);
            for (int i = 1; i <= 120; i++)
            {
                float u = Mathf.Clamp01(i / 40f);
                now += Dt;
                frame.t = now;
                frame.right = PinchPose(Vector3.Lerp(grab, stop, u), true);
                ritual.Tick(now, Dt, frame);
                if (loose.Alive) looseShift = Mathf.Max(looseShift, loose.Center.x - startX);
            }
        }

        [Test]
        public void AHeldPieceDrawsInAFloatingPieceOfTheSameKindWithinSixCentimetresAndMerges()
        {
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            cfg.mergeMagnetRange = 0.06f;
            cfg.mergeTouchDistance = 0.012f;
            try
            {
                TimeRitual ritual = BareRitual("MagnetRitual", cfg, cats, null);
                TimePieceView loose = Pulled(cats[2], 2, 2, new Vector3(-0.14f, 1.25f, 0.42f), Vector3.right);
                TimePieceView held = Pulled(cats[2], 2, 3, new Vector3(0.02f, 1.25f, 0.42f), Vector3.right);
                PiecesOf(ritual).Add(loose);
                PiecesOf(ritual).Add(held);
                Assert.That(held.DistanceTo(loose), Is.GreaterThan(cfg.mergeMagnetRange), "처음엔 끌림 범위 밖");
                float now = 1f;
                DragHeldPieceToward(ritual, held, loose, out float shift, ref now);
                Assert.That(shift, Is.GreaterThan(0.005f), "6cm 안으로 들어오면 떠 있던 조각이 쥔 조각 쪽으로 끌려온다");
                Assert.That(loose.Alive, Is.False, "끌려와 닿으면 하나로 합쳐진다");
                Assert.That(ritual.Pieces.Count, Is.EqualTo(1));
                Assert.That(held.Units, Is.EqualTo(5), "합친 단위 수는 두 조각의 합");
                Assert.That(held.data.mergedCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void WithoutMagnetRangeAFloatingPieceStaysApart()
        {
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            cfg.mergeMagnetRange = 0f;
            try
            {
                TimeRitual ritual = BareRitual("NoMagnetRitual", cfg, cats, null);
                TimePieceView loose = Pulled(cats[2], 2, 2, new Vector3(-0.14f, 1.25f, 0.42f), Vector3.right);
                TimePieceView held = Pulled(cats[2], 2, 3, new Vector3(0.02f, 1.25f, 0.42f), Vector3.right);
                PiecesOf(ritual).Add(loose);
                PiecesOf(ritual).Add(held);
                float now = 1f;
                DragHeldPieceToward(ritual, held, loose, out float shift, ref now);
                Assert.That(shift, Is.LessThan(0.002f));
                Assert.That(loose.Alive, Is.True);
                Assert.That(ritual.Pieces.Count, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void DeskPoolGlowsUnderTheJarWhileFillingSwellsAfterTheSealAndFadesWhenLifted()
        {
            Assert.That(RitualDeskPool.RestShare(0.75f, 0.75f, 0.08f), Is.EqualTo(1f));
            Assert.That(RitualDeskPool.RestShare(0.95f, 0.75f, 0.08f), Is.EqualTo(0f));
            Assert.That(RitualDeskPool.RestShare(0.80f, 0.75f, 0.08f), Is.InRange(0.01f, 0.99f));

            Material glowMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ritual/RitualGlow.mat");
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            cfg.showTotals = false;
            try
            {
                TimeRitual ritual = BareRitual("PoolRitual", cfg, cats, glowMat);
                float now = 1f;
                for (int c = 1; c <= 2; c++)
                {
                    TimePieceView piece = Pulled(cats[c], c, 6, new Vector3(0.3f, 0.3f, 0f), Vector3.forward);
                    Assert.That(ritual.Jar.BeginPour(piece), Is.Not.Null);
                    for (int s = 0; s < 72 * 8 && ritual.Jar.Pouring; s++)
                    {
                        now += Dt;
                        ritual.Tick(now, Dt, default);
                    }
                }
                for (int i = 0; i < 40; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }
                Assert.That(ritual.DeskPool.Visible, Is.True, "병 아래 책상에 빛 웅덩이가 생긴다");
                float openGain = ritual.DeskPool.Gain;
                Assert.That(openGain, Is.InRange(cfg.deskPoolOpenMin - 1e-3f, cfg.deskPoolOpenMax + 1e-3f), "채우는 동안은 담긴 만큼만");
                typeof(TimeRitual).GetMethod("Seal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(ritual, null);
                now += Dt;
                ritual.Tick(now, Dt, default);
                Assert.That(ritual.DeskPool.Blooming, Is.True);
                Assert.That(ritual.DeskPool.Gain, Is.LessThan(openGain), "봉인 순간 빛이 병으로 한 번 가라앉는다");
                for (int i = 0; i < 150; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }
                Assert.That(ritual.Glow.Blooming, Is.True, "병을 감싸는 오라는 그대로");
                Assert.That(ritual.DeskPool.Visible, Is.True, "그다음 병빛이 책상 면에 번진다");
                Assert.That(ritual.DeskPool.Gain, Is.GreaterThan(0.95f));
                Assert.That(ritual.DeskPool.Strength, Is.GreaterThan(0.9f));
                Assert.That(ritual.DeskPool.root.position.y, Is.EqualTo(ritual.TableY + 0.0015f).Within(1e-4f));
                Vector3[] verts = ritual.DeskPool.root.GetComponentInChildren<MeshFilter>().sharedMesh.vertices;
                for (int i = 0; i < verts.Length; i++) Assert.That(verts[i].y, Is.EqualTo(0f).Within(1e-6f), "책상 면에 눕는다");

                Vector3 rest = ritual.Jar.root.position;
                ritual.Jar.root.position = rest + Vector3.up * 0.2f;
                for (int i = 0; i < 90; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }
                Assert.That(ritual.DeskPool.Strength, Is.LessThan(0.05f), "들어 올리면 책상 빛이 걷힌다");
                Assert.That(ritual.DeskPool.Visible, Is.False);

                ritual.Jar.root.position = rest;
                for (int i = 0; i < 90; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }
                Assert.That(ritual.DeskPool.Strength, Is.GreaterThan(0.9f), "내려놓으면 다시 번진다");
                Assert.That(Vector3.Distance(ritual.DeskPool.root.position, new Vector3(rest.x, ritual.TableY + 0.0015f, rest.z)), Is.LessThan(1e-4f));
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void ShippedConfigKeepsTheSpecFixedLightAndCleanup()
        {
            var shipped = UnityEditor.AssetDatabase.LoadAssetAtPath<TimeRitualConfig>("Assets/Ritual/TimeRitualConfig.asset");
            Assert.That(shipped, Is.Not.Null);
            Assert.That(shipped.mergeMagnetRange, Is.GreaterThan(shipped.mergeTouchDistance), "같은 카테고리의 두 조각을 가까이 대면 서로 끌린다");
            Assert.That(shipped.mergeTouchDistance, Is.GreaterThan(0f));
            Assert.That(shipped.deskPool, Is.True, "병 아래 책상에는 빛 웅덩이");
            Assert.That(shipped.roomDarkFromStart, Is.True, "리추얼이 시작되면 패스스루가 어두워진다");
            Assert.That(shipped.roomLight, Is.LessThan(1f));
            Assert.That(shipped.guideTrails, Is.False, "형상에서 병으로 계속 떨어지는 입자 줄기는 없앤다");
            Assert.That(shipped.halos && shipped.handLight, Is.True, "오브제가 광원, 손에 빛이 닿는다");
        }

        [Test]
        public void RoomDarkensFromTheStartByScalingPassthroughAndCanFallBack()
        {
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            cfg.roomDarkFromStart = true;
            cfg.roomLight = 0.38f;
            var old = Object.Instantiate(_config);
            old.roomDarkFromStart = false;
            try
            {
                TimeRitual dark = BareRitual("DarkRitual", cfg, cats, null);
                TimeRitual plain = BareRitual("PlainRitual", old, cats, null);
                Assert.That(dark.PassthroughLight, Is.EqualTo(1f), "처음에는 원래 밝기에서 시작");
                float now = 1f;
                for (int i = 0; i < 72 * 5; i++)
                {
                    now += Dt;
                    dark.Tick(now, Dt, default);
                    plain.Tick(now, Dt, default);
                }
                Assert.That(dark.PassthroughLight, Is.EqualTo(0.38f).Within(0.01f), "곧 원래 밝기의 30~40%로");
                Assert.That(dark.PassthroughDim, Is.EqualTo(0f).Within(1e-3f), "밝기 오프셋이 아니라 배율로 어둡힌다");
                Assert.That(plain.PassthroughLight, Is.EqualTo(1f), "플래그를 끄면 이전 동작");
                Assert.That(plain.PassthroughDim, Is.LessThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(cfg);
                Object.DestroyImmediate(old);
            }
        }

        [Test]
        public void FloatingPiecesGetAHaloAndLightANearbyHand()
        {
            Material glowMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ritual/RitualGlow.mat");
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            try
            {
                TimeRitual ritual = BareRitual("LightRitual", cfg, cats, glowMat);
                TimePieceView piece = Pulled(cats[1], 1, 3, new Vector3(0f, 1.25f, 0.42f), Vector3.right);
                PiecesOf(ritual).Add(piece);
                float now = 1f;
                now += Dt;
                ritual.Tick(now, Dt, default);
                Assert.That(ritual.LightField.Visible, Is.True);
                Assert.That(ritual.LightField.HaloSplats, Is.GreaterThanOrEqualTo(2), "떠 있는 조각 뒤에 후광");
                Assert.That(ritual.LightField.HandSplats, Is.EqualTo(0));

                HandPose far = PinchPose(piece.Center + new Vector3(0f, 0.5f, 0f), false);
                HandPose near = PinchPose(piece.Center + new Vector3(0f, 0.045f, 0f), false);
                now += Dt;
                ritual.Tick(now, Dt, new HandFrame { t = now, right = far });
                Assert.That(ritual.LightField.HandSplats, Is.EqualTo(0), "먼 손에는 빛이 닿지 않는다");
                now += Dt;
                ritual.Tick(now, Dt, new HandFrame { t = now, right = near });
                Assert.That(ritual.LightField.HandSplats, Is.EqualTo(6), "가까운 손가락과 손바닥에 빛이 닿는다");

                ritual.Config.halos = false;
                ritual.Config.handLight = false;
                now += Dt;
                ritual.Tick(now, Dt, new HandFrame { t = now, right = near });
                Assert.That(ritual.LightField.SplatCount, Is.EqualTo(0));
                Assert.That(ritual.LightField.Visible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void LeftMicrogestureSwitchesBetweenTimeNumbersAndTheNeckTag()
        {
            Font fraunces = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Fraunces/Fraunces_72pt-Black.ttf");
            Material waxMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ritual/RitualWax.mat");
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            cfg.showTotals = true;
            cfg.showMinutesLabel = true;
            cfg.timeLabels = false;
            var go = new GameObject("LabelRitual");
            go.transform.SetParent(_root.transform, false);
            var micro = go.AddComponent<FakeMicrogestureInput>();
            var ritual = go.AddComponent<TimeRitual>();
            var so = new UnityEditor.SerializedObject(ritual);
            so.FindProperty("config").objectReferenceValue = cfg;
            so.FindProperty("waxMaterial").objectReferenceValue = waxMat;
            so.FindProperty("labelFont").objectReferenceValue = fraunces;
            so.FindProperty("microgestureSource").objectReferenceValue = micro;
            so.FindProperty("piecesRoot").objectReferenceValue = go.transform;
            so.FindProperty("autoPlace").boolValue = false;
            var cp = so.FindProperty("categories");
            cp.arraySize = cats.Length;
            for (int i = 0; i < cats.Length; i++) cp.GetArrayElementAtIndex(i).objectReferenceValue = cats[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                ritual.Init();
                ritual.PlaceAt(new Vector3(0f, 1.0f, 0.42f), Quaternion.identity);
                var piece = new TimePieceView(_root.transform, cats[2], 2, _look, null, ritual.Config, null, fraunces, null);
                piece.BeginPull(new Vector3(0f, 1.25f, 0.42f), Vector3.right);
                float now = 1f;
                for (int i = 1; i <= 30; i++)
                {
                    float raw = 3 * cfg.unitLength * i / 30f + 0.001f;
                    piece.UpdatePull(new Vector3(raw, 1.25f, 0.42f), TimeUnits.Step(raw, piece.Units, cfg.unitLength, cfg.hysteresis, cfg.maxUnits), raw, Dt);
                    now += Dt;
                    piece.Tick(now, Dt);
                }
                piece.ReleasePull();
                PiecesOf(ritual).Add(piece);
                TimePieceView pourable = Pulled(cats[1], 1, 6, new Vector3(0.3f, 0.3f, 0f), Vector3.forward);
                Assert.That(ritual.Jar.BeginPour(pourable), Is.Not.Null);
                for (int s = 0; s < 72 * 8 && ritual.Jar.Pouring; s++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }
                for (int i = 0; i < 60; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }
                Assert.That(ritual.TimeLabels, Is.False);
                Assert.That(ritual.LabelShownText, Is.EqualTo("Pour your day into the vessel"), "꼬리표에는 한 줄뿐");
                Assert.That(System.Text.RegularExpressions.Regex.IsMatch(ritual.LabelShownText, "[0-9]"), Is.False, "시·분 숫자가 없다");
                Vector3 local = ritual.Jar.root.InverseTransformPoint(ritual.LabelRoot.position);
                Assert.That(local.y, Is.EqualTo(cfg.labelTagHeight).Within(0.005f), "병목에 묶인 꼬리표");
                Assert.That(ritual.LabelRoot.localScale.x, Is.EqualTo(cfg.labelTagScale).Within(0.01f));
                Assert.That(piece.LabelText, Is.Empty, "떠 있는 조각에는 글자가 없다");

                micro.left = Microgesture.ThumbTap;
                for (int i = 0; i < 3; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }
                micro.left = Microgesture.None;
                for (int i = 0; i < 60; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }
                Assert.That(ritual.TimeLabels, Is.True, "왼손 마이크로제스처로 숫자 라벨을 켠다");
                Assert.That(ritual.GuideTrailsEnabled, Is.False, "왼손은 가이드를 건드리지 않는다");
                Assert.That(ritual.LabelShownText, Does.Contain("TODAY'S TIME"));
                Assert.That(ritual.LabelShownText, Does.Contain("01h 00m"));
                Assert.That(piece.LabelText, Does.Contain("Focused"));
                Assert.That(piece.LabelText, Does.Contain("30m"));

                micro.left = Microgesture.SwipeLeft;
                for (int i = 0; i < 3; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }
                micro.left = Microgesture.None;
                typeof(TimeRitual).GetMethod("Seal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(ritual, null);
                for (int i = 0; i < 90; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }
                Assert.That(ritual.TimeLabels, Is.False, "다시 끈다");
                string date = System.DateTime.Now.ToString("MMMM d yyyy", System.Globalization.CultureInfo.InvariantCulture);
                Assert.That(ritual.LabelShownText, Is.EqualTo(date), "봉인하면 꼬리표의 글이 오늘 날짜로 바뀐다");
                local = ritual.Jar.root.InverseTransformPoint(ritual.LabelRoot.position);
                Assert.That(local.y, Is.EqualTo(cfg.labelTagHeight).Within(0.005f), "봉인 뒤에도 꼬리표는 병목에");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        static Vector3[] ShapeBox(BeadShape kind)
        {
            Vector3 lo = Vector3.one * 9f, hi = -Vector3.one * 9f;
            for (int i = 0; i < 600; i++)
            {
                float t = (i + 0.5f) / 600f;
                float z = 1f - 2f * t;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                float phi = i * 2.39996323f;
                Unity.Mathematics.float3 p = TimePieceView.ShapePoint(kind, new Unity.Mathematics.float3(r * Mathf.Cos(phi), r * Mathf.Sin(phi), z), 1f, out _);
                Vector3 v = new Vector3(p.x, p.y, p.z);
                Assert.That(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z), Is.False);
                Assert.That(v.magnitude, Is.LessThan(1.6f));
                lo = Vector3.Min(lo, v);
                hi = Vector3.Max(hi, v);
            }
            return new[] { lo, hi };
        }

        [Test]
        public void EachKindWearsTheShapeThePersonChoseAndKeepsItsOwnSilhouette()
        {
            TimeCategoryAsset[] cats = ShippedCategories();
            var expected = new System.Collections.Generic.Dictionary<string, BeadShape>
            {
                { "sleep", BeadShape.Crescent },
                { "move", BeadShape.Droplet },
                { "work", BeadShape.Star },
                { "drift", BeadShape.Diamond },
                { "play", BeadShape.Feather },
            };
            System.Type setup = System.Type.GetType("TimeRitualSceneSetup, Assembly-CSharp-Editor");
            Assert.That(setup, Is.Not.Null, "빌드가 카테고리 에셋을 다시 쓰는 장면 설정");
            var specs = (System.Array)setup.GetField("Categories", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
            foreach (object spec in specs)
            {
                string id = (string)spec.GetType().GetField("id").GetValue(spec);
                BeadShape shape = (BeadShape)spec.GetType().GetField("shape").GetValue(spec);
                Assert.That(shape, Is.EqualTo(expected[id]), id + " 형상은 빌드 때 다시 써도 사람이 고른 것");
            }
            var kinds = new System.Collections.Generic.List<BeadShape>();
            foreach (TimeCategoryAsset cat in cats)
            {
                BeadShape k = cat.EffectiveShape;
                Assert.That(k, Is.EqualTo(expected[cat.id]), cat.id + " 형상은 사람이 고른 것");
                Assert.That(kinds, Has.No.Member(k), "카테고리마다 한 종");
                kinds.Add(k);
            }
            for (int a = 0; a < kinds.Count; a++)
            {
                Vector3[] ba = ShapeBox(kinds[a]);
                for (int b = a + 1; b < kinds.Count; b++)
                {
                    Vector3[] bb = ShapeBox(kinds[b]);
                    float diff = Mathf.Max(Vector3.Distance(ba[0], bb[0]), Vector3.Distance(ba[1], bb[1]));
                    Assert.That(diff, Is.GreaterThan(0.08f), $"{kinds[a]} 와 {kinds[b]} 는 색을 빼도 모양으로 구분된다");
                }
            }
        }

        [Test]
        public void TheChainSagsUnderGravityWhilePulled()
        {
            foreach (float sag in new[] { 0.06f, 0f })
            {
                _config.threadSag = sag;
                var piece = new TimePieceView(_root.transform, _move, 0, _look, null, _config, null, null, null);
                Vector3 origin = new Vector3(0f, 1.2f, 0.4f);
                piece.BeginPull(origin, Vector3.right);
                float now = 0f;
                for (int i = 1; i <= 40; i++)
                {
                    float raw = 0.12f * i / 40f;
                    piece.UpdatePull(origin + Vector3.right * raw, TimeUnits.Step(raw, piece.Units, _config.unitLength, _config.hysteresis, _config.maxUnits), raw, Dt);
                    now += Dt;
                    piece.Tick(now, Dt);
                }
                if (sag > 0f) Assert.That(piece.Bend.y, Is.LessThan(-0.003f), "사슬은 중력에 살짝 처진다");
                else Assert.That(Mathf.Abs(piece.Bend.y), Is.LessThan(1e-3f));
                piece.Kill();
            }
        }

        [TestCase(FormStyle.Flow)]
        [TestCase(FormStyle.Gather)]
        public void ReleasingTheChainTurnsTheSameGrainsIntoTheShape(FormStyle style)
        {
            _config.formStyle = style;
            Vector3 origin = new Vector3(0f, 1.2f, 0.4f);
            TimePieceView piece = Pulled(_move, 0, 6, origin, Vector3.right);
            Vector3 tip = origin + Vector3.right * (6 * _config.unitLength + 0.001f);
            Vector3 middle = piece.Center;
            int splats = piece.SplatCount;
            Assert.That(piece.Crumple(), Is.True);
            Assert.That(piece.shape, Is.EqualTo(PieceShape.Blob));
            Assert.That(piece.Units, Is.EqualTo(6), "당긴 길이가 크기를 정한다");
            if (style == FormStyle.Flow) Assert.That(Vector3.Distance(piece.Center, tip), Is.LessThan(0.01f), "사슬이 놓은 손 쪽으로 흘러 모인다");
            else Assert.That(Vector3.Distance(piece.Center, Vector3.Lerp(middle, tip, 0.5f)), Is.LessThan(0.01f), "흩어졌다 사슬 가운데쯤에서 모인다");
            Assert.That(piece.BlobRadiusFull, Is.GreaterThanOrEqualTo(_config.formMinRadius), "짧게 당겨도 실한 덩어리");
            float now = 5f;
            for (int i = 0; i < 72 * 4; i++)
            {
                now += Dt;
                piece.Tick(now, Dt);
            }
            Assert.That(piece.Awake, Is.False, "형상으로 다 모이면 잠든다");
            Assert.That(piece.SplatCount, Is.EqualTo(splats), "알갱이는 사라지거나 생기지 않는다");
            piece.Kill();
        }

        [Test]
        public void AReleasedPieceDriftsSlowlyToItsPlaceAndStopsWhenHeld()
        {
            TimePieceView piece = Pulled(_move, 0, 3, new Vector3(0f, 1.2f, 0.4f), Vector3.right);
            Assert.That(piece.Crumple(), Is.True);
            Vector3 start = piece.Center;
            Vector3 target = start + new Vector3(0.1f, 0.12f, 0.08f);
            piece.FloatTo(target, 0.4f);
            float now = 10f;
            for (int i = 0; i < 20; i++)
            {
                now += Dt;
                piece.Tick(now, Dt);
            }
            Assert.That(Vector3.Distance(piece.Center, start), Is.LessThan(0.003f), "형상이 다 된 뒤에 떠난다");
            for (int i = 0; i < 30; i++)
            {
                now += Dt;
                piece.Tick(now, Dt);
            }
            float early = Vector3.Distance(piece.Center, start);
            Assert.That(early, Is.LessThan(0.05f), "튀지 않고 천천히 출발한다");
            for (int i = 0; i < 72 * 8; i++)
            {
                now += Dt;
                piece.Tick(now, Dt);
            }
            Assert.That(Vector3.Distance(piece.Center, target), Is.LessThan(0.01f), "자리에 가서 떠 있는다");
            Assert.That(piece.FloatingAway, Is.False);
            piece.FloatTo(start, 0f);
            piece.Hold();
            Assert.That(piece.FloatingAway, Is.False, "손에 쥐면 흘러가기를 멈춘다");
            piece.Kill();
        }

        [Test]
        public void FloatSlotsFillTheRoomInFrontWithinReach()
        {
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            var headGo = new GameObject("SlotHead");
            try
            {
                var go = new GameObject("SlotRitual");
                go.transform.SetParent(_root.transform, false);
                var ritual = go.AddComponent<TimeRitual>();
                var so = new UnityEditor.SerializedObject(ritual);
                so.FindProperty("config").objectReferenceValue = cfg;
                so.FindProperty("head").objectReferenceValue = headGo.transform;
                so.FindProperty("piecesRoot").objectReferenceValue = go.transform;
                so.FindProperty("autoPlace").boolValue = false;
                var cp = so.FindProperty("categories");
                cp.arraySize = cats.Length;
                for (int i = 0; i < cats.Length; i++) cp.GetArrayElementAtIndex(i).objectReferenceValue = cats[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                headGo.transform.position = new Vector3(0f, 1.2f, 0f);
                ritual.Init();
                ritual.PlaceAt(new Vector3(0f, 1.06f, 0.42f), Quaternion.identity);
                Vector3 eye = headGo.transform.position;
                var slots = new System.Collections.Generic.List<Vector3>();
                int left = 0, right = 0;
                for (int i = 0; i < 10; i++)
                {
                    Vector3 slot = ritual.NextFloatSlot();
                    Vector3 flat = slot - eye;
                    flat.y = 0f;
                    Assert.That(flat.magnitude, Is.InRange(0.35f, 0.6f), "앉은 채 팔이 닿는다");
                    Assert.That(slot.y - eye.y, Is.InRange(ritual.Config.floatLow - 1e-3f, ritual.Config.floatHigh + 1e-3f));
                    Assert.That(flat.z, Is.GreaterThan(0.2f), "앞쪽 방 안");
                    if (flat.x < -0.05f) left++;
                    if (flat.x > 0.05f) right++;
                    foreach (Vector3 other in slots) Assert.That(Vector3.Distance(other, slot), Is.GreaterThan(0.04f), "조각끼리 겹치지 않게 흩어진다");
                    slots.Add(slot);
                }
                Assert.That(left, Is.GreaterThanOrEqualTo(3));
                Assert.That(right, Is.GreaterThanOrEqualTo(3), "방이 양쪽으로 찬다");
            }
            finally
            {
                Object.DestroyImmediate(headGo);
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void SpringsAreLooseSwirlingSwarmsAndOneInvitesWhenNothingHappens()
        {
            Material splatMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Splatter/LetterSplat.mat");
            Assert.That(splatMat, Is.Not.Null);
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            cfg.springSwarm = true;
            cfg.springSplats = 300;
            cfg.hintIdleSeconds = 1f;
            var ritualGo = new GameObject("SwarmRitual");
            ritualGo.transform.SetParent(_root.transform, false);
            var heaps = new TimeHeap[cats.Length];
            for (int i = 0; i < cats.Length; i++)
            {
                var hgo = new GameObject("Heap_" + cats[i].id);
                hgo.transform.SetParent(ritualGo.transform, false);
                hgo.transform.localPosition = new Vector3((i - 2) * 0.12f, 0f, 0f);
                hgo.AddComponent<MeshFilter>();
                hgo.AddComponent<MeshRenderer>();
                hgo.AddComponent<SplatLetterRenderer>();
                var heap = hgo.AddComponent<TimeHeap>();
                var hso = new UnityEditor.SerializedObject(heap);
                hso.FindProperty("category").objectReferenceValue = cats[i];
                hso.FindProperty("config").objectReferenceValue = cfg;
                hso.FindProperty("splatMaterial").objectReferenceValue = splatMat;
                hso.ApplyModifiedPropertiesWithoutUndo();
                heaps[i] = heap;
            }
            var pull = ritualGo.AddComponent<TimePullController>();
            var pso = new UnityEditor.SerializedObject(pull);
            var hp = pso.FindProperty("heaps");
            hp.arraySize = heaps.Length;
            for (int i = 0; i < heaps.Length; i++) hp.GetArrayElementAtIndex(i).objectReferenceValue = heaps[i];
            pso.ApplyModifiedPropertiesWithoutUndo();
            var ritual = ritualGo.AddComponent<TimeRitual>();
            var rso = new UnityEditor.SerializedObject(ritual);
            rso.FindProperty("config").objectReferenceValue = cfg;
            rso.FindProperty("pull").objectReferenceValue = pull;
            rso.FindProperty("piecesRoot").objectReferenceValue = ritualGo.transform;
            rso.FindProperty("autoPlace").boolValue = false;
            var cp = rso.FindProperty("categories");
            cp.arraySize = cats.Length;
            for (int i = 0; i < cats.Length; i++) cp.GetArrayElementAtIndex(i).objectReferenceValue = cats[i];
            rso.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                ritual.Init();
                ritual.PlaceAt(new Vector3(0f, 1.0f, 0.42f), Quaternion.identity);
                TimeHeap center = heaps[2];
                Assert.That(center.Swarming, Is.True, "샘은 유리구슬이 아니라 알갱이 무리");
                Assert.That(center.Swarm.Count, Is.EqualTo(300));
                Assert.That(center.GetComponent<MeshRenderer>().enabled, Is.False);
                float now = 1f;
                now += Dt;
                ritual.Tick(now, Dt, default);
                Unity.Mathematics.float3 before = center.Swarm.Cloud.disp[7];
                for (int i = 0; i < 36; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }
                Unity.Mathematics.float3 after = center.Swarm.Cloud.disp[7];
                Assert.That(Unity.Mathematics.math.distance(before, after), Is.GreaterThan(0.002f), "느슨하게 맴돈다");
                float loose = cfg.heapDiameter * 0.5f * cfg.springLoose;
                Assert.That(center.Swarm.MaxReach, Is.InRange(loose * 0.5f, loose * 1.6f), "집을 수 있을 만큼 성긴 공으로 모여 있다");

                Assert.That(ritual.EverPulled, Is.False);
                for (int i = 0; i < 72 * 2; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }
                float invited = 0f;
                foreach (TimeHeap h in heaps) invited = Mathf.Max(invited, h.Invitation);
                Assert.That(invited, Is.GreaterThan(0.5f), "아무것도 안 하면 샘 하나가 숨 쉬듯 밝아지며 다가온다");

                Vector3 grab = center.transform.position + Vector3.up * 0.01f;
                HandFrame frame = new HandFrame { t = now, left = PinchPose(grab, false) };
                now += Dt;
                frame.t = now;
                ritual.Tick(now, Dt, frame);
                for (int i = 0; i < 8; i++)
                {
                    now += Dt;
                    frame.t = now;
                    frame.left = PinchPose(grab, true);
                    ritual.Tick(now, Dt, frame);
                }
                Assert.That(ritual.EverPulled, Is.True);
                for (int i = 0; i < 72 * 3; i++)
                {
                    now += Dt;
                    frame.t = now;
                    frame.left = PinchPose(grab, false);
                    ritual.Tick(now, Dt, frame);
                }
                foreach (TimeHeap h in heaps) Assert.That(h.Invitation, Is.LessThan(0.01f), "한 번 당기면 다시 나오지 않는다");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        struct SwarmSignature
        {
            public float fallSpeed;
            public float spreadSpeed;
            public float alignment;
            public float verticalRms;
            public float speed;
        }

        static SwarmSignature Measure(RitualSpringSwarm swarm, Transform host, float seconds)
        {
            float now = 0f;
            for (int i = 0; i < 72; i++)
            {
                now += Dt;
                swarm.Tick(now, Dt, 1f, 0f, 0f, 0f, Vector3.zero, 0f);
            }
            double vy = 0, vr = 0, wsum = 0, vy2 = 0, sp = 0, lmag = 0;
            Vector3 lsum = Vector3.zero;
            int frames = Mathf.RoundToInt(seconds / Dt);
            for (int f = 0; f < frames; f++)
            {
                now += Dt;
                swarm.Tick(now, Dt, 1f, 0f, 0f, 0f, Vector3.zero, 0f);
                RitualSplatCloud c = swarm.Cloud;
                for (int i = 0; i < c.Count; i++)
                {
                    float w = c.scale[i];
                    if (w < 0.05f) continue;
                    Vector3 p = (Vector3)c.disp[i];
                    Vector3 v = (Vector3)c.vel[i];
                    Vector3 flat = new Vector3(p.x, 0f, p.z);
                    float radial = flat.sqrMagnitude > 1e-8f ? Vector3.Dot(new Vector3(v.x, 0f, v.z), flat.normalized) : 0f;
                    vy += v.y * w;
                    vy2 += v.y * v.y * w;
                    vr += radial * w;
                    sp += v.magnitude * w;
                    Vector3 l = Vector3.Cross(p, v);
                    lsum += l * w;
                    lmag += l.magnitude * w;
                    wsum += w;
                }
            }
            return new SwarmSignature
            {
                fallSpeed = (float)(-vy / wsum),
                spreadSpeed = (float)(vr / wsum),
                alignment = lmag > 1e-9 ? lsum.magnitude / (float)lmag : 0f,
                verticalRms = Mathf.Sqrt((float)(vy2 / wsum)),
                speed = (float)(sp / wsum)
            };
        }

        [Test]
        public void TheFiveSpringsStayFormlessButMoveEachInTheirOwnWay()
        {
            Material splatMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Splatter/LetterSplat.mat");
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            cfg.springSplats = 240;
            cfg.springMotion = 1f;
            var sig = new System.Collections.Generic.Dictionary<SwarmMotion, SwarmSignature>();
            var made = new System.Collections.Generic.List<RitualSpringSwarm>();
            try
            {
                foreach (TimeCategoryAsset cat in cats)
                {
                    var host = new GameObject("Host_" + cat.id).transform;
                    host.SetParent(_root.transform, false);
                    var swarm = new RitualSpringSwarm(host, cfg, null, splatMat, _look, cat);
                    made.Add(swarm);
                    Assert.That(sig.ContainsKey(swarm.Motion), Is.False, "샘마다 움직임이 다르다");
                    sig[swarm.Motion] = Measure(swarm, host, 3f);
                    float loose = cfg.heapDiameter * 0.5f * cfg.springLoose;
                    Assert.That(swarm.MaxReach, Is.LessThan(loose * 1.25f), cat.id + " 무리는 성긴 공 안에 머문다");
                }
                Assert.That(cats[0].swarmMotion, Is.EqualTo(SwarmMotion.Settle));
                Assert.That(sig[SwarmMotion.Settle].fallSpeed, Is.GreaterThan(0.003f), "Rested 는 눈처럼 천천히 가라앉는다");
                Assert.That(sig[SwarmMotion.Ripple].spreadSpeed, Is.GreaterThan(0.004f), "Moved 는 잔물결처럼 퍼진다");
                Assert.That(sig[SwarmMotion.Orbit].alignment, Is.GreaterThan(0.7f), "Focused 는 한 방향으로 촘촘히 맴돈다");
                foreach (SwarmMotion m in new[] { SwarmMotion.Settle, SwarmMotion.Ripple, SwarmMotion.Wander, SwarmMotion.Bounce })
                    Assert.That(sig[m].alignment, Is.LessThan(sig[SwarmMotion.Orbit].alignment - 0.25f), m + " 는 한 방향으로 돌지 않는다");
                Assert.That(sig[SwarmMotion.Bounce].verticalRms, Is.GreaterThan(sig[SwarmMotion.Orbit].verticalRms * 2f), "Enjoyed 는 통통 튄다");
                Assert.That(sig[SwarmMotion.Wander].speed, Is.LessThan(sig[SwarmMotion.Orbit].speed * 0.6f), "Explored 는 느리게 떠돈다");
            }
            finally
            {
                foreach (RitualSpringSwarm s in made) s.Dispose();
                Object.DestroyImmediate(cfg);
            }
        }


        TimeRitual RitualWithHeaps(TimeRitualConfig cfg, TimeCategoryAsset[] cats, Material glowMat, out TimeHeap[] heaps, Material waxMat = null, Font font = null)
        {
            var go = new GameObject("SealRitual");
            go.transform.SetParent(_root.transform, false);
            heaps = new TimeHeap[cats.Length];
            for (int i = 0; i < cats.Length; i++)
            {
                var hgo = new GameObject("Heap_" + cats[i].id);
                hgo.transform.SetParent(go.transform, false);
                hgo.transform.localPosition = new Vector3((i - 2) * 0.12f, 0f, 0f);
                hgo.AddComponent<MeshFilter>();
                hgo.AddComponent<MeshRenderer>();
                hgo.AddComponent<SplatLetterRenderer>();
                var heap = hgo.AddComponent<TimeHeap>();
                var hso = new UnityEditor.SerializedObject(heap);
                hso.FindProperty("category").objectReferenceValue = cats[i];
                hso.FindProperty("config").objectReferenceValue = cfg;
                hso.ApplyModifiedPropertiesWithoutUndo();
                heaps[i] = heap;
            }
            var pull = go.AddComponent<TimePullController>();
            var pso = new UnityEditor.SerializedObject(pull);
            var hp = pso.FindProperty("heaps");
            hp.arraySize = heaps.Length;
            for (int i = 0; i < heaps.Length; i++) hp.GetArrayElementAtIndex(i).objectReferenceValue = heaps[i];
            pso.ApplyModifiedPropertiesWithoutUndo();
            var ritual = go.AddComponent<TimeRitual>();
            var rso = new UnityEditor.SerializedObject(ritual);
            rso.FindProperty("config").objectReferenceValue = cfg;
            rso.FindProperty("pull").objectReferenceValue = pull;
            rso.FindProperty("glowMaterial").objectReferenceValue = glowMat;
            rso.FindProperty("waxMaterial").objectReferenceValue = waxMat;
            rso.FindProperty("labelFont").objectReferenceValue = font;
            rso.FindProperty("piecesRoot").objectReferenceValue = go.transform;
            rso.FindProperty("autoPlace").boolValue = false;
            var cp = rso.FindProperty("categories");
            cp.arraySize = cats.Length;
            for (int i = 0; i < cats.Length; i++) cp.GetArrayElementAtIndex(i).objectReferenceValue = cats[i];
            rso.ApplyModifiedPropertiesWithoutUndo();
            ritual.Init();
            ritual.PlaceAt(new Vector3(0f, 1.0f, 0.42f), Quaternion.identity);
            return ritual;
        }

        void Run(TimeRitual ritual, ref float now, float seconds, HandFrame frame)
        {
            int n = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < n; i++)
            {
                now += Dt;
                frame.t = now;
                ritual.Tick(now, Dt, frame);
            }
        }

        void CloseTheLid(TimeRitual ritual, TimeCategoryAsset[] cats, ref float now)
        {
            TimePieceView piece = Pulled(cats[1], 1, 6, new Vector3(0.3f, 0.3f, 0f), Vector3.forward);
            Assert.That(ritual.Jar.BeginPour(piece), Is.Not.Null);
            Run(ritual, ref now, 4f, default);
            Assert.That(ritual.LidShown, Is.True, "다 부으면 병 옆에 뚜껑이 떠 있다");
            Vector3 lid = ritual.LidPosition;
            Assert.That(Vector3.Distance(lid, ritual.Jar.MouthCenter), Is.GreaterThan(0.08f), "뚜껑은 병 옆에 떠 있다");
            Run(ritual, ref now, Dt, new HandFrame { right = PinchPose(lid, false) });
            Run(ritual, ref now, 8 * Dt, new HandFrame { right = PinchPose(lid, true) });
            Vector3 mouth = ritual.Jar.MouthCenter;
            for (int i = 1; i <= 40 && !ritual.LidClosed; i++)
                Run(ritual, ref now, Dt, new HandFrame { right = PinchPose(Vector3.Lerp(lid, mouth, i / 40f), true) });
            Run(ritual, ref now, 4 * Dt, new HandFrame { right = PinchPose(mouth + Vector3.up * 0.3f, false) });
            Assert.That(ritual.LidClosed, Is.True, "뚜껑을 병에 얹으면 닫힌다");
        }

        [Test]
        public void PuttingTheLidOnMeltsTheSpringsIntoOneWaxDropAboveIt()
        {
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            cfg.waxAppearDelay = 0.5f;
            try
            {
                TimeRitual ritual = RitualWithHeaps(cfg, cats, null, out TimeHeap[] heaps);
                float now = 1f;
                CloseTheLid(ritual, cats, ref now);
                Run(ritual, ref now, cfg.meltSeconds + 0.6f, default);
                Assert.That(Vector3.Distance(ritual.LidPosition, ritual.Jar.MouthCenter), Is.LessThan(0.005f), "뚜껑이 입구에 앉는다");
                foreach (TimeHeap h in heaps) Assert.That(h.Present, Is.False, "남은 샘이 녹아 사라진다");
                Assert.That(ritual.Wax.Ready, Is.True, "샘이 녹아 하나의 밀랍 방울이 된다");
                Assert.That(ritual.Wax.StickShown, Is.False, "밀랍 막대 소품은 없다");
                Assert.That(ritual.Wax.Color, Is.EqualTo(cats[1].waxColor), "방울 색은 그날 담긴 색에서 온다");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [TestCase(ClimaxStyle.Ring)]
        [TestCase(ClimaxStyle.Shell)]
        [TestCase(ClimaxStyle.Bloom)]
        public void PressingSpreadsTheWaxAndTheDaySealsWhenTheThumbLifts(ClimaxStyle style)
        {
            Material glowMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ritual/RitualGlow.mat");
            Material waxMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ritual/RitualWax.mat");
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            cfg.waxAppearDelay = 0.5f;
            cfg.climaxStyle = style;
            try
            {
                TimeRitual ritual = RitualWithHeaps(cfg, cats, glowMat, out _, waxMat);
                float now = 1f;
                CloseTheLid(ritual, cats, ref now);
                Run(ritual, ref now, cfg.meltSeconds + 0.6f, default);
                Assert.That(ritual.Wax.Ready, Is.True);

                HandPose Thumb(float below)
                {
                    float top = ritual.Wax.TopY(ritual.HoldProgress);
                    Vector3 p = ritual.Wax.root.TransformPoint(new Vector3(0f, top - below, 0f));
                    return new HandPose { tracked = true, thumbTip = p, indexTip = p + new Vector3(0.06f, 0.04f, 0f), middleTip = p + new Vector3(0.07f, 0.05f, 0.02f), ringTip = p + new Vector3(0.08f, 0.05f, 0.03f), littleTip = p + new Vector3(0.09f, 0.04f, 0.04f), palm = p + new Vector3(0.05f, 0.08f, 0.02f), palmNormal = Vector3.down };
                }
                HandPose Away() => PinchPose(ritual.Jar.MouthCenter + new Vector3(0f, 0.35f, 0f), false);

                for (int i = 0; i < 30; i++) Run(ritual, ref now, Dt, new HandFrame { right = Thumb(0.012f) });
                Run(ritual, ref now, 60 * Dt, new HandFrame { right = Away() });
                Assert.That(ritual.Sealed, Is.False, "도중에 떼면 닫히지 않는다");
                Assert.That(ritual.HoldProgress, Is.EqualTo(0f).Within(1e-3f), "밀랍이 다시 오므라든다");

                float light0 = ritual.PassthroughLight;
                int frames = Mathf.CeilToInt((cfg.holdSeconds + 0.4f) / Dt);
                for (int i = 0; i < frames; i++) Run(ritual, ref now, Dt, new HandFrame { right = Thumb(0.012f) });
                Assert.That(ritual.PressFull, Is.True, "누르고 있으면 뚜껑 가장자리까지 번진다");
                Assert.That(ritual.Sealed, Is.False, "누르는 동안은 아직");
                Assert.That(ritual.Wax.Press, Is.GreaterThan(0.8f), "누르는 손에 가려도 보일 만큼 번져 있다");
                Assert.That(ritual.PassthroughLight, Is.LessThan(light0 - 0.05f), "누르는 동안 방의 빛이 병으로 모인다");

                Run(ritual, ref now, 2 * Dt, new HandFrame { right = Away() });
                Assert.That(ritual.Sealed, Is.True, "엄지를 떼는 순간 봉인");
                Assert.That(ritual.SealWave.Active, Is.True);
                Assert.That(ritual.SealWave.Style, Is.EqualTo(style));
                if (style == ClimaxStyle.Bloom)
                {
                    float dur = cfg.bloomSeconds;
                    Run(ritual, ref now, dur * 0.08f, default);
                    Assert.That(ritual.SealWave.JarGlow, Is.GreaterThan(0.8f), "병 자체가 발광한다");
                    Run(ritual, ref now, dur * 0.3f, default);
                    Assert.That(ritual.SealWave.MoteSpread, Is.GreaterThan(0.6f), "파티클이 병 근처 2미터 공간으로 퍼진다");
                    Assert.That(ritual.SealWave.Radius, Is.LessThanOrEqualTo(cfg.climaxReach * 1.1f));
                    Assert.That(ritual.SealWave.FogCount, Is.GreaterThanOrEqualTo(5), "구름 안개 발광이 함께 퍼진다");
                    Run(ritual, ref now, dur * 0.56f, default);
                    Assert.That(ritual.SealWave.MoteSpread, Is.LessThan(0.3f), "다시 병으로 흡수된다");
                    Run(ritual, ref now, dur * 0.2f, default);
                }
                else
                {
                    Run(ritual, ref now, cfg.climaxSeconds * 0.6f, default);
                    Assert.That(ritual.SealWave.Radius, Is.GreaterThan(cfg.climaxReach * 0.8f), "빛이 방으로 퍼진다");
                    Assert.That(ritual.SealWave.SplatCount, Is.GreaterThan(100));
                    Run(ritual, ref now, cfg.climaxSeconds, default);
                }
                Assert.That(ritual.SealWave.Active, Is.False, "큰 순간은 한 번 지나간다");
                Run(ritual, ref now, cfg.coolSeconds, default);
                Assert.That(ritual.Wax.Cool, Is.GreaterThan(0.95f), "밀랍은 몇 초에 걸쳐 식는다");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void TheSingingBowlRingsLongAndTheRoomFallsSilentForAMoment()
        {
            var go = new GameObject("BowlAudio");
            go.transform.SetParent(_root.transform, false);
            var audio = go.AddComponent<RitualAudio>();
            AudioClip bowl = RitualAudio.BakeBowl(24000);
            Assert.That(bowl.length, Is.GreaterThan(5f), "싱잉볼은 길게 울린다");
            Object.DestroyImmediate(bowl);
            audio.Hush(1f);
            Assert.That(audio.Hushed, Is.True);
            for (int i = 0; i < 36; i++) audio.Tick(Dt, 0.7f);
            Assert.That(audio.Hushed, Is.True, "약 1초 정적");
            for (int i = 0; i < 48; i++) audio.Tick(Dt, 0.7f);
            Assert.That(audio.Hushed, Is.False);
        }


        [Test]
        public void MeltingSpringsSpreadIntoTheSpaceThenCurlIntoTheWaxLikeSmoke()
        {
            Material splatMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Splatter/LetterSplat.mat");
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            cfg.springSplats = 200;
            var host = new GameObject("MeltHost").transform;
            host.SetParent(_root.transform, false);
            host.position = new Vector3(0f, 1f, 0.4f);
            var swarm = new RitualSpringSwarm(host, cfg, null, splatMat, _look, cats[2]);
            try
            {
                Vector3 target = new Vector3(0.25f, 0.8f, 0.35f);
                float now = 0f;
                for (int i = 0; i < 20; i++) swarm.Tick(now += Dt, Dt, 1f, 0f, 0f, 0f, Vector3.zero, 0f);
                int frames = 240;
                float farthest = 0f;
                float swept = 0f;
                float prevAngle = float.NaN;
                Vector3 start = host.position + (Vector3)swarm.Cloud.disp[3];
                float bend = 0f;
                Vector3 spreadPoint = start;
                for (int f = 1; f <= frames; f++)
                {
                    float m = f / (float)frames;
                    swarm.Tick(now += Dt, Dt, 1f, 0f, 0f, 0f, Vector3.zero, 0f, m, target);
                    Vector3 p = host.position + (Vector3)swarm.Cloud.disp[3];
                    farthest = Mathf.Max(farthest, Vector3.Distance(p, host.position));
                    if (m < 0.4f) spreadPoint = p;
                    if (m > 0.62f && m < 0.9f)
                    {
                        float a = Mathf.Atan2(p.z - target.z, p.x - target.x);
                        if (!float.IsNaN(prevAngle)) swept += Mathf.Abs(Mathf.DeltaAngle(prevAngle * Mathf.Rad2Deg, a * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                        prevAngle = a;
                        Vector3 line = target - spreadPoint;
                        float along = Mathf.Clamp01(Vector3.Dot(p - spreadPoint, line) / Mathf.Max(1e-6f, line.sqrMagnitude));
                        bend = Mathf.Max(bend, Vector3.Distance(p, spreadPoint + line * along));
                    }
                }
                Vector3 end = host.position + (Vector3)swarm.Cloud.disp[3];
                Assert.That(farthest, Is.GreaterThan(0.2f), "샘의 알갱이가 먼저 공간으로 퍼진다");
                Assert.That(swept, Is.GreaterThan(1.2f), "연기가 빨려 들듯 소용돌이치며 들어간다");
                Assert.That(bend, Is.GreaterThan(0.03f), "직선이 아니라 곡선으로 모인다");
                Assert.That(Vector3.Distance(end, target), Is.LessThan(0.02f), "밀랍 자리로 수렴한다");
            }
            finally
            {
                swarm.Dispose();
                Object.DestroyImmediate(cfg);
            }
        }


        [Test]
        public void TheClockFontDrawsEveryDigitWithItsOwnShape()
        {
            var pts = new Vector2[80];
            float Wide(char c)
            {
                RitualClockFont.Sample(c, 80, pts, 0, 0f, 1);
                float lo = 9f, hi = -9f;
                foreach (Vector2 p in pts)
                {
                    Assert.That(p.x, Is.InRange(-0.05f, 0.7f));
                    Assert.That(p.y, Is.InRange(-0.05f, 1.05f));
                    lo = Mathf.Min(lo, p.x);
                    hi = Mathf.Max(hi, p.x);
                }
                return hi - lo;
            }
            foreach (char c in "0123456789:.") Assert.That(RitualClockFont.Strokes(c).Count, Is.GreaterThan(0), c + " 는 획이 있다");
            Assert.That(Wide('1'), Is.LessThan(Wide('0') * 0.5f), "1 은 좁다");
            Assert.That(Wide(':'), Is.LessThan(0.15f));
            RitualClockFont.Sample('0', 80, pts, 0, 0f, 1);
            var zero = (Vector2[])pts.Clone();
            RitualClockFont.Sample('8', 80, pts, 0, 0f, 1);
            float diff = 0f;
            for (int i = 0; i < 80; i++) diff += Vector2.Distance(zero[i], pts[i]);
            Assert.That(diff / 80f, Is.GreaterThan(0.05f), "0 과 8 은 모양이 다르다");
        }

        [Test]
        public void TheClockShowsTheRealTimeAtEyeLevelAndTheSecondsFlow()
        {
            Font fraunces = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Fraunces/Fraunces_72pt-Black.ttf");
            var clock = new RitualClock(_config, fraunces, null);
            try
            {
                Vector3 center = new Vector3(0f, 1.16f, 0.55f);
                clock.Place(center, Quaternion.identity);
                var t0 = new System.DateTime(2026, 11, 15, 19, 52, 7);
                clock.Show(t0);
                Assert.That(clock.Text, Is.EqualTo("19:52:07"), "실제 시각");
                Assert.That(clock.DateText, Is.EqualTo("11.15"), "오늘 날짜 MM.DD");
                Assert.That(clock.Count, Is.GreaterThan(300), "큰 숫자는 작은 숫자 글리프 수백 개로 되어 있다");
                Assert.That(clock.Width, Is.InRange(0.35f, 0.7f), "눈높이에 크게");
                int ticks = 0;
                clock.Ticked += (p, tock) => ticks++;
                var before = new Vector3[clock.Count];
                for (int i = 0; i < clock.Count; i++) before[i] = clock.ParticlePosition(i);
                for (int i = 0; i < 40; i++) clock.Tick(Dt, t0.AddSeconds(1), null, Vector3.zero);
                Assert.That(clock.Text, Is.EqualTo("19:52:08"), "초가 흐른다");
                Assert.That(ticks, Is.EqualTo(1), "초마다 째깍");
                float moved = 0f;
                for (int i = 0; i < clock.Count; i++) moved = Mathf.Max(moved, Vector3.Distance(before[i], clock.ParticlePosition(i)));
                Assert.That(moved, Is.GreaterThan(0.001f), "바뀐 숫자의 알갱이가 새 모양으로 흐른다");
                for (int i = 0; i < clock.Count; i++) Assert.That(Vector3.Distance(clock.ParticlePosition(i), center), Is.LessThan(clock.Width * 0.6f));
            }
            finally
            {
                clock.Dispose();
            }
        }

        [TestCase(ClockCollapse.Crumble)]
        [TestCase(ClockCollapse.Burst)]
        public void TouchingTheClockStopsItThenItsGrainsFillTheRoomAndGatherIntoTheFiveSprings(ClockCollapse style)
        {
            Font fraunces = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Fraunces/Fraunces_72pt-Black.ttf");
            var cfg = Object.Instantiate(_config);
            cfg.clockCollapse = style;
            var clock = new RitualClock(cfg, fraunces, null);
            try
            {
                Vector3 center = new Vector3(0f, 1.16f, 0.55f);
                clock.Place(center, Quaternion.identity);
                var springs = new Vector3[5];
                for (int i = 0; i < 5; i++) springs[i] = new Vector3((i - 2) * 0.12f, 1.02f, 0.42f);
                clock.SetSprings(springs, null);
                var t0 = new System.DateTime(2026, 11, 15, 19, 52, 7);
                clock.Show(t0);
                for (int i = 0; i < 30; i++) clock.Tick(Dt, t0, null, Vector3.zero);
                var hands = new RitualHands();
                hands.Update(new HandFrame { t = 0f, right = PinchPose(center, false) }, null, cfg, Dt);
                clock.Tick(Dt, t0, hands, Vector3.zero);
                Assert.That(clock.Phase, Is.EqualTo(ClockPhase.Frozen), "손이 닿으면 시계가 멈춘다");
                int ticks = 0;
                clock.Ticked += (p, tock) => ticks++;
                var empty = new RitualHands();
                int silence = Mathf.FloorToInt(cfg.clockSilence / Dt) - 2;
                for (int i = 0; i < silence; i++) clock.Tick(Dt, t0.AddSeconds(1), empty, Vector3.zero);
                Assert.That(ticks, Is.EqualTo(0), "소리도 멈추고 짧은 정적");
                Assert.That(clock.Phase, Is.EqualTo(ClockPhase.Frozen));
                for (int i = 0; i < 4; i++) clock.Tick(Dt, t0.AddSeconds(1), empty, Vector3.zero);
                Assert.That(clock.Phase, Is.EqualTo(ClockPhase.Collapsing), "정적 뒤에 무너진다");
                float far = 0f;
                int frames = Mathf.CeilToInt(cfg.clockCollapseSeconds / Dt) + 4;
                for (int i = 0; i < frames && clock.Phase == ClockPhase.Collapsing; i++)
                {
                    clock.Tick(Dt, t0.AddSeconds(2), empty, Vector3.zero);
                    far = Mathf.Max(far, clock.Farthest);
                }
                Assert.That(far, Is.GreaterThan(1.0f), "알갱이가 방 크기로 퍼진다");
                Assert.That(clock.Phase, Is.EqualTo(ClockPhase.Done));
                for (int i = 0; i < 5; i++) Assert.That(clock.ArrivalShare(i), Is.EqualTo(1f), "다섯 샘으로 모인다");
                int near = 0;
                for (int i = 0; i < clock.Count; i++)
                {
                    float best = float.MaxValue;
                    foreach (Vector3 sp in springs) best = Mathf.Min(best, Vector3.Distance(clock.ParticlePosition(i), sp));
                    if (best < 0.02f) near++;
                }
                Assert.That(near, Is.EqualTo(clock.Count), "모든 글리프가 샘 자리에 닿는다");
            }
            finally
            {
                clock.Dispose();
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void TheRitualOpensWithTheClockAndTheSpringsGrowFromItsGrains()
        {
            Font fraunces = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Fraunces/Fraunces_72pt-Black.ttf");
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            cfg.hintIdleSeconds = 1f;
            try
            {
                TimeRitual ritual = RitualWithHeaps(cfg, cats, null, out TimeHeap[] heaps, null, fraunces);
                Assert.That(ritual.ClockDone, Is.False, "시작하면 시계가 먼저");
                float now = 1f;
                Run(ritual, ref now, 3f, default);
                foreach (TimeHeap h in heaps) Assert.That(h.Present, Is.False, "시계가 있는 동안 샘은 없다");
                foreach (TimeHeap h in heaps) Assert.That(h.Invitation, Is.EqualTo(0f), "당기기 힌트도 아직");
                Vector3 c = ritual.Clock.Center;
                Run(ritual, ref now, 2 * Dt, new HandFrame { right = PinchPose(c, false) });
                Assert.That(ritual.Clock.Phase, Is.Not.EqualTo(ClockPhase.Showing), "손이 닿으면 멈춘다");
                Run(ritual, ref now, cfg.clockSilence + cfg.clockCollapseSeconds * 0.75f, default);
                float growing = 0f;
                foreach (TimeHeap h in heaps) growing = Mathf.Max(growing, h.Melt + (h.Present ? 1f : 0f));
                Run(ritual, ref now, cfg.clockCollapseSeconds * 0.5f + 2f, default);
                Assert.That(ritual.ClockDone, Is.True);
                foreach (TimeHeap h in heaps) Assert.That(h.Present, Is.True, "글리프가 닿은 자리에 샘이 생긴다");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }
    }
}
