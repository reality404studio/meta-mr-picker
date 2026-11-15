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
    }
}
