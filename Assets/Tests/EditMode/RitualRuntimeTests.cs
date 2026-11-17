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
        const string TestDaysFile = "editmode-test-ritual-days.json";
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
            _config.shelfFile = TestDaysFile;
            System.IO.File.Delete(System.IO.Path.Combine(Application.persistentDataPath, TestDaysFile));
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
            System.IO.File.Delete(System.IO.Path.Combine(Application.persistentDataPath, TestDaysFile));
            Object.DestroyImmediate(_move);
            Object.DestroyImmediate(_work);
        }

        TimePieceView Pulled(TimeCategoryAsset category, int index, int units, Vector3 origin, Vector3 dir, Font font = null)
        {
            var piece = new TimePieceView(_root.transform, category, index, _look, null, _config, null, font, null);
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
                Assert.That(ritual.TutorialEnabled, Is.False, "오른손 마이크로제스쳐 하나로 튜토리얼(카드)과 빛 궤적을 같이 끈다 = 녹화 모드");
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

        TimePieceView Shaped(TimeCategoryAsset category, int index, int units, Vector3 origin, ref float now)
        {
            TimePieceView piece = Pulled(category, index, units, origin, Vector3.right);
            piece.Crumple();
            for (int i = 0; i < 120; i++)
            {
                now += Dt;
                piece.Tick(now, Dt);
            }
            return piece;
        }

        Vector3[] FarProbes(out float[] radii)
        {
            var probes = new Vector3[6];
            radii = new float[6];
            for (int k = 0; k < 6; k++)
            {
                probes[k] = Vector3.one * 10f;
                radii[k] = k == 5 ? _config.fieldPalm : _config.fieldFinger;
            }
            return probes;
        }

        static HandPose GapPose(Vector3 pos, float gap)
        {
            HandPose pose = PinchPose(pos, true);
            pose.indexTip = pos + new Vector3(gap, 0f, 0f);
            return pose;
        }

        [Test]
        public void SlowFingersPartTheGrainsAndTheyCloseBehindWithoutScattering()
        {
            float now = 0f;
            TimePieceView piece = Shaped(_move, 0, 4, Vector3.zero, ref now);
            Vector3 c = piece.Center;
            Vector3[] probes = FarProbes(out float[] radii);
            float parted = 0f;
            for (int i = 0; i <= 48; i++)
            {
                probes[1] = c + new Vector3(-0.036f + 0.0015f * i, 0f, 0f);
                piece.Field(probes, radii, 6, new Vector3(0.1f, 0f, 0f), false, Dt);
                now += Dt;
                piece.Tick(now, Dt);
                parted = Mathf.Max(parted, piece.MaxDisplacement());
                Assert.That(piece.LooseCount, Is.EqualTo(0), "천천히 넣은 손은 흩뜨리지 않는다");
            }
            Assert.That(parted, Is.GreaterThan(0.004f), "손가락 사이로 알갱이가 갈라진다");
            for (int i = 0; i < 90; i++)
            {
                now += Dt;
                piece.Tick(now, Dt);
            }
            Assert.That(piece.MaxDisplacement(), Is.LessThan(0.002f), "손가락이 지나가면 다시 닫힌다");
            piece.Kill();
        }

        [Test]
        public void AFastStirScattersGrainsThatDriftBackOverSecondsNotAtOnce()
        {
            float now = 0f;
            TimePieceView piece = Shaped(_move, 0, 4, Vector3.zero, ref now);
            Vector3 c = piece.Center;
            Vector3[] probes = FarProbes(out float[] radii);
            for (int i = 0; i < 4; i++)
            {
                probes[5] = c + new Vector3(-0.02f + 0.017f * i, 0f, 0f);
                probes[1] = probes[5] + new Vector3(0f, 0.01f, 0.03f);
                piece.Field(probes, radii, 6, new Vector3(1.2f, 0f, 0f), true, Dt);
                now += Dt;
                piece.Tick(now, Dt);
            }
            Assert.That(piece.LooseCount, Is.GreaterThan(50), "빠르게 휘저으면 흩어진다");
            float t = 0f;
            for (; t < 0.8f; t += Dt)
            {
                now += Dt;
                piece.Tick(now, Dt);
            }
            Assert.That(piece.MaxDisplacement(), Is.GreaterThan(0.03f), "흩어진 알갱이는 바로 붙지 않는다");
            Assert.That(piece.LooseCount, Is.GreaterThan(0));
            float back = -1f;
            for (; t < 8f; t += Dt)
            {
                now += Dt;
                piece.Tick(now, Dt);
                if (piece.LooseCount == 0 && piece.MaxDisplacement() < 0.004f)
                {
                    back = t;
                    break;
                }
            }
            Assert.That(back, Is.InRange(1.5f, _config.scatterFlight + _config.returnSeconds + 1.5f), "몇 초에 걸쳐 원래 조각으로 돌아온다");
            piece.Kill();
        }

        [Test]
        public void AHandShapingAPinchOrJustLettingGoNeverScatters()
        {
            var open = new RitualHand
            {
                tracked = true,
                thumb = Vector3.zero,
                index = new Vector3(0.09f, 0f, 0f),
                palm = new Vector3(0.04f, -0.02f, -0.06f),
                palmVelocity = new Vector3(1.2f, 0f, 0f)
            };
            Assert.That(TimeRitual.WouldScatter(_config, open, 5f), Is.True, "빠르게 휘저으면 흩어진다");
            RitualHand shaping = open;
            shaping.index = new Vector3(0.03f, 0f, 0f);
            Assert.That(TimeRitual.WouldScatter(_config, shaping, 5f), Is.False, "핀치 자세가 만들어지는 동안은 꺼진다");
            RitualHand pinching = open;
            pinching.pinching = true;
            Assert.That(TimeRitual.WouldScatter(_config, pinching, 5f), Is.False);
            Assert.That(TimeRitual.WouldScatter(_config, open, 0.1f), Is.False, "막 놓은 손은 흩뜨리지 않는다");
            RitualHand slow = open;
            slow.palmVelocity = new Vector3(0.35f, 0f, 0f);
            Assert.That(TimeRitual.WouldScatter(_config, slow, 5f), Is.False, "천천히 넣은 손은 흩뜨리지 않는다");
        }

        [TestCase(0.03f, false)]
        [TestCase(0.09f, true)]
        public void ReachingFastToPinchAPieceDoesNotScatterButAnOpenSwipeDoes(float gap, bool scatters)
        {
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            try
            {
                TimeRitual ritual = BareRitual("ReachRitual", cfg, cats, null);
                float now = 1f;
                TimePieceView piece = Shaped(cats[2], 2, 4, new Vector3(-0.03f, 1.25f, 0.42f), ref now);
                PiecesOf(ritual).Add(piece);
                Vector3 c = piece.Center;
                HandFrame frame = default;
                int peak = 0;
                for (int i = 0; i <= 12; i++)
                {
                    now += Dt;
                    frame.t = now;
                    frame.right = GapPose(c + new Vector3(0f, 0f, -0.25f + 0.25f * i / 12f), gap);
                    ritual.Tick(now, Dt, frame);
                    peak = Mathf.Max(peak, piece.LooseCount);
                }
                if (scatters)
                {
                    Assert.That(peak, Is.GreaterThan(0), "손을 펴고 빠르게 휘저으면 흩어진다");
                    Assert.That(ritual.ScatterEvents, Is.EqualTo(1));
                    return;
                }
                Assert.That(peak, Is.EqualTo(0), "집으려고 빠르게 뻗은 손은 조각을 흩뜨리지 않는다");
                for (int i = 0; i < 8; i++)
                {
                    now += Dt;
                    frame.t = now;
                    frame.right = PinchPose(c, true);
                    ritual.Tick(now, Dt, frame);
                }
                Assert.That(piece.state, Is.EqualTo(PieceState.Held));
                Assert.That(ritual.ScatterEvents, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        float LagAfterJerk(int units)
        {
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            try
            {
                TimeRitual ritual = BareRitual("WeightRitual" + units, cfg, cats, null);
                float now = 1f;
                TimePieceView piece = Shaped(cats[2], 2, units, new Vector3(-0.03f, 1.25f, 0.42f), ref now);
                PiecesOf(ritual).Add(piece);
                Vector3 grab = piece.Center;
                HandFrame frame = new HandFrame { t = now, right = PinchPose(grab, false) };
                now += Dt;
                ritual.Tick(now, Dt, frame);
                for (int i = 0; i < 10; i++)
                {
                    now += Dt;
                    frame.t = now;
                    frame.right = PinchPose(grab, true);
                    ritual.Tick(now, Dt, frame);
                }
                Assert.That(piece.state, Is.EqualTo(PieceState.Held));
                float startX = piece.Center.x;
                for (int i = 0; i < 8; i++)
                {
                    now += Dt;
                    frame.t = now;
                    frame.right = PinchPose(grab + new Vector3(0.06f, 0f, 0f), true);
                    ritual.Tick(now, Dt, frame);
                }
                return 0.06f - (piece.Center.x - startX);
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void BiggerPiecesTrailTheHandAndAMergedPieceMovesHeavier()
        {
            Assert.That(TimeRitual.WeightOmega(_config, 10), Is.LessThan(TimeRitual.WeightOmega(_config, 5) * 0.92f), "합친 조각(5+5)은 합치기 전보다 묵직하다");
            Assert.That(TimeRitual.WeightOmega(_config, 46), Is.GreaterThan(12f), "아주 큰 조각도 너무 무겁지 않다 (사람 11-16: 너무 무겁긴 함)");
            float light = LagAfterJerk(2);
            float heavy = LagAfterJerk(10);
            Assert.That(heavy, Is.GreaterThan(light * 1.25f), "큰 조각일수록 손을 느리게 따라온다");
            Assert.That(heavy, Is.LessThan(_config.weightMaxLag + 0.005f), "그래도 손에서 떨어지지 않는다");
            Assert.That(light, Is.LessThan(0.03f), "작은 조각은 손에 붙어 다닌다");
        }

        void GrabAndApproach(TimeRitual ritual, TimePieceView held, TimePieceView loose, ref float now, ref HandFrame frame, out Vector3 grab, out Vector3 stop)
        {
            grab = held.Center;
            frame.right = PinchPose(grab, false);
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
            Assert.That(held.state, Is.EqualTo(PieceState.Held));
            Vector3 toward = loose.Center - held.Center;
            float travel = toward.magnitude - held.Radius - loose.Radius - 0.008f;
            stop = grab + toward.normalized * travel;
        }

        [Test]
        public void SameKindPiecesBridgeForAboutASecondThenBecomeOneWithAGoldSeam()
        {
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            try
            {
                TimeRitual ritual = BareRitual("BondRitual", cfg, cats, null);
                float now = 1f;
                TimePieceView loose = Shaped(cats[2], 2, 3, new Vector3(-0.16f, 1.25f, 0.42f), ref now);
                TimePieceView held = Shaped(cats[2], 2, 3, new Vector3(0.04f, 1.25f, 0.42f), ref now);
                PiecesOf(ritual).Add(loose);
                PiecesOf(ritual).Add(held);
                HandFrame frame = default;
                GrabAndApproach(ritual, held, loose, ref now, ref frame, out Vector3 grab, out Vector3 stop);
                float t = 0f, bondAt = -1f, mergeAt = -1f, bridge = 0f, minGap = 1f;
                for (int i = 0; i < 72 * 4 && mergeAt < 0f; i++)
                {
                    now += Dt;
                    t += Dt;
                    frame.t = now;
                    frame.right = PinchPose(Vector3.Lerp(grab, stop, Mathf.Clamp01(i / 50f)), true);
                    ritual.Tick(now, Dt, frame);
                    if (bondAt < 0f && ritual.BondProgress(1) > 0f)
                    {
                        bondAt = t;
                        Assert.That(loose.Alive, Is.True, "닿자마자 합쳐지지 않는다");
                    }
                    if (bondAt >= 0f && loose.Alive)
                    {
                        bridge = Mathf.Max(bridge, Mathf.Min(loose.BridgeAmount, held.BridgeAmount));
                        minGap = Mathf.Min(minGap, held.DistanceTo(loose));
                    }
                    if (!loose.Alive) mergeAt = t;
                }
                Assert.That(bondAt, Is.GreaterThanOrEqualTo(0f), "가까이 대면 서로 이어지기 시작한다");
                Assert.That(bridge, Is.GreaterThan(0.5f), "두 조각의 알갱이가 다리처럼 이어진다");
                Assert.That(minGap, Is.GreaterThan(-0.012f), "이어지는 동안 서로 파고들지 않는다");
                Assert.That(mergeAt - bondAt, Is.InRange(cfg.bondSeconds * 0.8f, cfg.bondSeconds * 1.6f), "이어진 뒤 잠시 있다가 하나가 된다");
                Assert.That(held.Units, Is.EqualTo(6));
                Assert.That(held.data.mergedCount, Is.EqualTo(1));
                Assert.That(held.shape, Is.EqualTo(PieceShape.Blob), "같은 종의 더 큰 형상");
                Assert.That(held.SeamCount, Is.EqualTo(0), "금선은 없앤다 (사람 결정 11-16)");
                Assert.That(ritual.Snaps, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void PullingApartBeforeTheBondCompletesStretchesThenSnaps()
        {
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            try
            {
                TimeRitual ritual = BareRitual("SnapRitual", cfg, cats, null);
                float now = 1f;
                TimePieceView loose = Shaped(cats[2], 2, 3, new Vector3(-0.16f, 1.25f, 0.42f), ref now);
                TimePieceView held = Shaped(cats[2], 2, 3, new Vector3(0.04f, 1.25f, 0.42f), ref now);
                PiecesOf(ritual).Add(loose);
                PiecesOf(ritual).Add(held);
                HandFrame frame = default;
                GrabAndApproach(ritual, held, loose, ref now, ref frame, out Vector3 grab, out Vector3 stop);
                Vector3 last = grab;
                for (int i = 0; i < 72 * 3 && ritual.BondProgress(1) < 0.3f; i++)
                {
                    now += Dt;
                    frame.t = now;
                    last = Vector3.Lerp(grab, stop, Mathf.Clamp01(i / 50f));
                    frame.right = PinchPose(last, true);
                    ritual.Tick(now, Dt, frame);
                }
                Assert.That(ritual.BondProgress(1), Is.InRange(0.3f, 0.99f), "이어지는 중");
                Vector3 away = last + (grab - stop).normalized * 0.14f;
                float stretched = 0f;
                for (int i = 1; i <= 40; i++)
                {
                    now += Dt;
                    frame.t = now;
                    frame.right = PinchPose(Vector3.Lerp(last, away, Mathf.Clamp01(i / 10f)), true);
                    ritual.Tick(now, Dt, frame);
                    if (ritual.Snaps == 0 && loose.Alive) stretched = Mathf.Max(stretched, held.DistanceTo(loose));
                }
                Assert.That(ritual.Snaps, Is.EqualTo(1), "다 이어지기 전에 떼면 끊어진다");
                Assert.That(stretched, Is.GreaterThan(cfg.bondStart), "끊어지기 전에 늘어난다");
                Assert.That(loose.Alive && held.Alive, Is.True);
                Assert.That(held.data.mergedCount, Is.EqualTo(0));
                Assert.That(held.SeamCount, Is.EqualTo(0));
                Assert.That(held.BridgeAmount + loose.BridgeAmount, Is.EqualTo(0f).Within(1e-6f));
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void RestSandKeptBehindItsFlagStillFillsTheGapWhenTurnedOn()
        {
            RitualJar jar = Jar();
            try
            {
                float now = 0f;
                TimePieceView piece = Pulled(_move, 0, 2, new Vector3(0.3f, 0.3f, 0f), Vector3.forward);
                Assert.That(jar.BeginPour(piece), Is.Not.Null);
                Finish(jar, ref now);
                Assert.That(jar.Room, Is.GreaterThan(100), "아주 조금만 꺼낸 날");
                RitualJar.Layer rest = jar.BeginRest();
                Assert.That(rest, Is.Not.Null);
                Assert.That(rest.rest && rest.category == null, Is.True, "고운 옅은 모래");
                Finish(jar, ref now);
                Assert.That(jar.Room, Is.EqualTo(0), "얼마나 꺼냈든 병은 항상 가득 찬다");
                Assert.That(jar.TotalUnits, Is.EqualTo(RitualFill.DayUnits), "가득 = 하루 24시간 (사람 11-17)");
                Assert.That(jar.SurfaceMean, Is.EqualTo(jar.Shape.bodyTop).Within(0.004f), "몸통 꼭대기까지, 목은 비어 있다");
                Assert.That(jar.BeginRest(), Is.Null, "이미 가득하면 더 붓지 않는다");
            }
            finally
            {
                jar.Dispose();
            }
        }

        [Test]
        public void SettledLayersHoldPlainSandWithNoDigitGrainsLeftGlowing()
        {
            RitualJar jar = Jar();
            try
            {
                float now = 0f;
                TimePieceView piece = Pulled(_move, 0, 12, new Vector3(0.3f, 0.3f, 0f), Vector3.forward);
                RitualJar.Layer layer = jar.BeginPour(piece);
                Finish(jar, ref now);
                int lit = 0;
                for (int i = 0; i < layer.cloud.Count; i++) if (layer.cloud.glow[i] > 0f) lit++;
                Assert.That(lit, Is.EqualTo(0), "병 속 숫자 글리프는 없앤다 (사람 결정 11-16: 핵심 경험이 아님)");
            }
            finally
            {
                jar.Dispose();
            }
        }

        static HandPose PalmPose(Vector3 palm, Vector3 normal)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(Vector3.forward, normal);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(Vector3.right, normal);
            fwd.Normalize();
            Vector3 side = Vector3.Cross(normal, fwd).normalized;
            return new HandPose
            {
                tracked = true,
                palm = palm,
                palmNormal = normal,
                thumbTip = palm + side * 0.07f + fwd * 0.03f,
                indexTip = palm + fwd * 0.09f + side * 0.02f,
                middleTip = palm + fwd * 0.095f,
                ringTip = palm + fwd * 0.09f - side * 0.02f,
                littleTip = palm + fwd * 0.075f - side * 0.04f
            };
        }

        TimePieceView CupOnPalm(TimeRitual ritual, TimeRitualConfig cfg, TimeCategoryAsset cat, int index, ref float now, ref HandFrame frame, out Vector3 palm)
        {
            TimePieceView piece = Shaped(cat, index, 4, new Vector3(-0.05f, 1.25f, 0.30f), ref now);
            PiecesOf(ritual).Add(piece);
            palm = piece.Center - Vector3.up * (cfg.palmCupLift + 0.01f);
            for (int i = 0; i < 30; i++)
            {
                now += Dt;
                frame.t = now;
                frame.right = PalmPose(palm, Vector3.up);
                ritual.Tick(now, Dt, frame);
            }
            Assert.That(piece.state, Is.EqualTo(PieceState.Held), "손바닥에 올리면 손에 실린다");
            Assert.That(piece.Squeeze, Is.GreaterThan(0.5f), "모래처럼 내려앉는다");
            return piece;
        }

        [Test]
        public void APalmTurnedUpCupsAPieceLikeSandAndTiltingItOverTheJarPoursIt()
        {
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            try
            {
                TimeRitual ritual = BareRitual("PalmRitual", cfg, cats, null);
                float now = 1f;
                HandFrame frame = default;
                TimePieceView piece = CupOnPalm(ritual, cfg, cats[1], 1, ref now, ref frame, out Vector3 palm);
                Vector3 over = ritual.Jar.MouthCenter + Vector3.up * 0.12f;
                for (int i = 1; i <= 50; i++)
                {
                    now += Dt;
                    frame.t = now;
                    frame.right = PalmPose(Vector3.Lerp(palm, over, Mathf.Clamp01(i / 40f)), Vector3.up);
                    ritual.Tick(now, Dt, frame);
                }
                Assert.That(piece.state, Is.EqualTo(PieceState.Held), "손바닥에 실린 채 병 위로");
                Assert.That(ritual.Jar.Pouring, Is.False, "기울이기 전엔 붓지 않는다");
                now += Dt;
                frame.t = now;
                frame.right = PalmPose(over, new Vector3(1f, 0.1f, 0f).normalized);
                ritual.Tick(now, Dt, frame);
                Assert.That(ritual.Jar.Pouring, Is.True, "병 위에서 손을 기울이면 흘러 들어간다");
                Assert.That(ritual.PalmPours, Is.EqualTo(1));
                for (int i = 0; i < 72 * 8 && ritual.Jar.Pouring; i++)
                {
                    now += Dt;
                    frame.t = now;
                    ritual.Tick(now, Dt, frame);
                }
                Assert.That(ritual.Jar.Layers.Count, Is.EqualTo(1));
                Assert.That(piece.Alive, Is.False, "조각의 알갱이가 그대로 층이 된다");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void TiltingThePalmAwayFromTheJarLetsThePieceSlideOffWithoutPouring()
        {
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            try
            {
                TimeRitual ritual = BareRitual("SlideRitual", cfg, cats, null);
                float now = 1f;
                HandFrame frame = default;
                TimePieceView piece = CupOnPalm(ritual, cfg, cats[1], 1, ref now, ref frame, out Vector3 palm);
                now += Dt;
                frame.t = now;
                frame.right = PalmPose(palm, new Vector3(1f, 0.1f, 0f).normalized);
                ritual.Tick(now, Dt, frame);
                Assert.That(piece.state, Is.EqualTo(PieceState.Floating), "병 밖에서 기울이면 미끄러져 떠 있는다");
                Assert.That(ritual.Jar.Pouring, Is.False);
                Assert.That(ritual.Jar.Layers.Count, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void ClosingTheLidByDefaultLeavesTheUnpouredSpaceClear()
        {
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = ScriptableObject.CreateInstance<TimeRitualConfig>();
            try
            {
                TimeRitual ritual = BareRitual("ClearRitual", cfg, cats, null);
                float now = 1f;
                TimePieceView piece = Pulled(cats[2], 2, 3, new Vector3(0.3f, 0.3f, 0f), Vector3.forward);
                Assert.That(ritual.Jar.BeginPour(piece), Is.Not.Null);
                for (int i = 0; i < 72 * 8 && ritual.Jar.Pouring; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }
                int room = ritual.Jar.Room;
                int layers = ritual.Jar.Layers.Count;
                typeof(TimeRitual).GetMethod("CloseLid", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(ritual, null);
                for (int i = 0; i < 72 * 4; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }
                Assert.That(ritual.Jar.Layers.Count, Is.EqualTo(layers), "뿌연 고운 모래는 뺀다 (사람 결정 11-16)");
                Assert.That(ritual.Jar.Room, Is.EqualTo(room));
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void ClosingTheLidSiftsTheRestOfTheDayAndTheRecordKeepsOnlyWhatWasPoured()
        {
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            cfg.restSand = true;
            try
            {
                TimeRitual ritual = BareRitual("RestRitual", cfg, cats, null);
                float now = 1f;
                TimePieceView piece = Pulled(cats[2], 2, 3, new Vector3(0.3f, 0.3f, 0f), Vector3.forward);
                Assert.That(ritual.Jar.BeginPour(piece), Is.Not.Null);
                for (int i = 0; i < 72 * 8 && ritual.Jar.Pouring; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }
                typeof(TimeRitual).GetMethod("CloseLid", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(ritual, null);
                now += Dt;
                ritual.Tick(now, Dt, default);
                Assert.That(ritual.Jar.Pouring, Is.True, "뚜껑을 닫으면 남은 빈틈에 고운 모래가 내려앉는다");
                for (int i = 0; i < 72 * 8 && ritual.Jar.Pouring; i++)
                {
                    now += Dt;
                    ritual.Tick(now, Dt, default);
                }
                Assert.That(ritual.Jar.Room, Is.EqualTo(0));
                var rec = (DayRecord)typeof(TimeRitual).GetMethod("RecordToday", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(ritual, null);
                Assert.That(rec.layers.Count, Is.EqualTo(1), "기록에는 부은 하루만, 나머지 모래는 다시 채운다");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void AMergedPieceNeverShowsHowManyFragmentsItHolds()
        {
            _config.showMinutesLabel = true;
            _config.timeLabels = true;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            TimePieceView a = Pulled(_move, 0, 3, Vector3.zero, Vector3.forward, font);
            TimePieceView b = Pulled(_move, 0, 2, new Vector3(0.04f, 0f, 0f), Vector3.forward, font);
            a.Absorb(b, MergedShape.Blob);
            a.Hold();
            a.Tick(1f, Dt);
            Assert.That(a.LabelText, Is.Not.Empty);
            Assert.That(a.LabelText, Does.Not.Contain("fragments"), "개수 표기는 없다");
            Assert.That(a.LabelText, Does.Not.Contain("(2)"));
            a.Kill();
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
            Assert.That(shipped.handField, Is.True, "손은 알갱이에 힘을 준다");
            Assert.That(shipped.scatterSpeed, Is.GreaterThanOrEqualTo(0.4f), "흩어짐은 손 속도가 문턱값 이상일 때만");
            Assert.That(shipped.returnSeconds, Is.GreaterThanOrEqualTo(1f), "몇 초에 걸쳐 돌아온다");
            Assert.That(shipped.weightLag, Is.GreaterThan(0f), "무게는 지연으로 표현한다");
            Assert.That(shipped.stickyMerge, Is.True, "알갱이 다리로 끈적하게 합쳐진다");
            Assert.That(shipped.seam, Is.False, "금선은 없앤다 (사람 결정 11-16)");
            Assert.That(shipped.releaseGlideMax, Is.LessThanOrEqualTo(0.1f), "던지기는 없다");
            Assert.That(shipped.palmPour, Is.True, "손바닥에 올려 기울여 붓는다 (핀치로 들고 가 놓기는 보조)");
            Assert.That(shipped.restSand, Is.False, "뿌연 고운 모래로 빈틈을 채우지 않는다 (사람 결정 11-16)");
            Assert.That(shipped.pourPitchHigh, Is.GreaterThan(shipped.pourPitchLow), "병이 찰수록 붓는 소리가 올라간다");
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
        public void TutorialCardsWalkTheRitualInOrderAndNeverGoBack()
        {
            var cfg = Object.Instantiate(_config);
            try
            {
                var s = new TutorialState();
                TutorialStep step = TutorialStep.Start;
                TutorialStep Go(TutorialState st, float idle) => step = RitualTutorial.Advance(step, st, idle, cfg);
                Assert.That(RitualTutorial.Show(step, s, true, cfg), Is.EqualTo(TutorialCard.None));
                s.clockShowing = true;
                Go(s, 0f);
                Assert.That(RitualTutorial.Show(step, s, true, cfg), Is.EqualTo(TutorialCard.Clock));
                s.clockShowing = false;
                s.springsReady = true;
                Go(s, 0f);
                Assert.That(RitualTutorial.Show(step, s, true, cfg), Is.EqualTo(TutorialCard.Pull));
                s.pulling = true;
                Go(s, 0f);
                Assert.That(RitualTutorial.Show(step, s, true, cfg), Is.EqualTo(TutorialCard.Pulling), "당기는 동안에는 놓을 때를 알려 준다");
                s.pulling = false;
                s.pieces = 1;
                Go(s, 0f);
                Assert.That(RitualTutorial.Show(step, s, true, cfg), Is.EqualTo(TutorialCard.PullMore));
                Go(s, cfg.tutorialPourIdle);
                Assert.That(step, Is.EqualTo(TutorialStep.PullMore), "조각 하나에 잠깐 쉰 것으로는 붓기로 넘어가지 않는다");
                s.pieces = 2;
                s.pairOfKind = true;
                Go(s, 0f);
                Assert.That(RitualTutorial.Show(step, s, true, cfg), Is.EqualTo(TutorialCard.Merge));
                Assert.That(RitualTutorial.Show(step, s, false, cfg), Is.EqualTo(TutorialCard.PullMore), "합치기 카드는 정해진 시간만");
                s.merged = true;
                s.pairOfKind = false;
                s.pieces = 1;
                Go(s, cfg.tutorialPourIdle * 2f);
                Assert.That(RitualTutorial.Show(step, s, true, cfg), Is.EqualTo(TutorialCard.Pour));
                s.poured = 1;
                s.pieces = 0;
                Go(s, 0f);
                Assert.That(RitualTutorial.Show(step, s, true, cfg), Is.EqualTo(TutorialCard.PourMore));
                Go(s, 1.2f);
                Assert.That(RitualTutorial.Show(step, s, true, cfg), Is.EqualTo(TutorialCard.Lid));
                s.pieces = 3;
                Go(s, 0f);
                Assert.That(step, Is.EqualTo(TutorialStep.Lid), "뒤로 가지 않는다");
                s.lidClosed = true;
                Go(s, 0f);
                Assert.That(RitualTutorial.Show(step, s, true, cfg), Is.EqualTo(TutorialCard.None), "밀랍이 생기기 전엔 비워 둔다");
                s.waxReady = true;
                Assert.That(RitualTutorial.Show(step, s, true, cfg), Is.EqualTo(TutorialCard.Wax));
                s.vesselSealed = true;
                Go(s, 0f);
                Assert.That(RitualTutorial.Show(step, s, true, cfg), Is.EqualTo(TutorialCard.None), "봉인 절정 동안엔 카드가 없다");
                s.sealAge = cfg.tutorialShelfDelay + 0.1f;
                Assert.That(RitualTutorial.Show(step, s, true, cfg), Is.EqualTo(TutorialCard.Shelf));
                s.shelved = true;
                Go(s, 0f);
                Assert.That(RitualTutorial.Show(step, s, true, cfg), Is.EqualTo(TutorialCard.Look));
                s.endOrbReady = true;
                Go(s, 0f);
                Assert.That(RitualTutorial.Show(step, s, true, cfg), Is.EqualTo(TutorialCard.End));
                s.ended = true;
                Go(s, 0f);
                Assert.That(RitualTutorial.Show(step, s, true, cfg), Is.EqualTo(TutorialCard.None));
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void TutorialCardsAreOneEnglishSentenceWithoutScoresOrNumbers()
        {
            foreach (TutorialCard card in RitualTutorial.Cards)
            {
                string line = RitualTutorial.Line(card);
                Assert.That(line, Is.Not.Empty, card.ToString());
                Assert.That(line.Contains("\n"), Is.False, "카드 문구는 한 줄 (사람 11-17)");
                Assert.That(line.EndsWith("."), Is.True, line);
                Assert.That(line.IndexOf('!') < 0, Is.True, "느낌표·칭찬 없음");
                foreach (char c in line)
                {
                    Assert.That(c < 128, Is.True, "영어 100% (사람 11-17): " + line);
                    Assert.That(char.IsDigit(c), Is.False, "숫자 없음: " + line);
                }
            }
            foreach (string foot in new[] { RitualTutorial.HideFull, RitualTutorial.HideShort, RitualTutorial.HoursLine })
                foreach (char c in foot) Assert.That(c < 128 || c == '·', Is.True, foot);
            Assert.That(RitualTutorial.Foot(TutorialStep.Clock, true), Does.Contain("Right hand"), "처음 카드에 끄는 법을 풀어 쓴다");
            Assert.That(RitualTutorial.Foot(TutorialStep.Pull, false), Does.Not.Contain("Left"), "왼손 안내는 붓기부터");
            Assert.That(RitualTutorial.Foot(TutorialStep.Pour, false), Does.Contain("Left thumb"));
        }

        [Test]
        public void TutorialCardHoldsItsTextAndFoldsAwayWhenTurnedOff()
        {
            Font font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Fraunces/Fraunces_72pt-Black.ttf");
            Material frost = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ritual/RitualFrost.mat");
            Assert.That(font, Is.Not.Null);
            Assert.That(frost, Is.Not.Null);
            var cfg = Object.Instantiate(_config);
            var host = new GameObject("TutorialHost").transform;
            host.SetParent(_root.transform, false);
            var tut = new RitualTutorial(host, cfg, font, frost);
            try
            {
                var s = new TutorialState { clockShowing = true };
                var right = new RitualHand { tracked = true, thumb = new Vector3(0.1f, 1f, 0.3f), index = new Vector3(0.11f, 1f, 0.31f) };
                Vector3 anchor = new Vector3(-0.08f, 0.8f, 0.3f);
                Vector3 jar = new Vector3(0.17f, 0.75f, 0.36f);
                Vector3 eye = new Vector3(0f, 1.15f, 0f);
                for (int i = 0; i < 60; i++) tut.Tick(Dt, s, 0f, anchor, jar, eye, right);
                Assert.That(tut.Card, Is.EqualTo(TutorialCard.Clock));
                Assert.That(tut.Openness, Is.EqualTo(1f).Within(1e-4f));
                Assert.That(Vector3.Distance(tut.Root.position, anchor), Is.LessThan(0.002f), "다 펼쳐지면 병 왼쪽 자리에");
                Assert.That(tut.MainText.Replace("\n", " "), Is.EqualTo(RitualTutorial.Line(TutorialCard.Clock)));
                Assert.That(tut.FootText.Replace("\n", " "), Does.Contain("Right hand"));
                Assert.That(tut.WidestLine, Is.LessThanOrEqualTo(tut.Width), "글자가 카드 폭 안에");
                Assert.That(tut.Height, Is.InRange(0.03f, 0.14f));
                Assert.That(tut.FrameRenderer.gameObject.activeInHierarchy, Is.True, "카드 가장자리 안쪽에 얇은 선 테두리 (사람 11-17)");
                Bounds frame = tut.FrameBounds;
                Assert.That(frame.size.x, Is.LessThan(tut.Width).And.GreaterThan(tut.Width - 0.02f), "테두리는 카드 가장자리 안쪽을 두른다");
                Assert.That(frame.size.y, Is.LessThan(tut.Height).And.GreaterThan(tut.Height - 0.02f));
                Assert.That(cfg.tutorialFrameWidth, Is.InRange(0.0005f, 0.0015f), "얇은 선 한 줄");
                Assert.That(cfg.tutorialFrameInset + cfg.tutorialFrameWidth, Is.LessThan(RitualTutorial.TextPad), "선이 글자에 닿지 않는다");

                tut.SetEnabled(false);
                for (int i = 0; i < 60; i++) tut.Tick(Dt, s, 0f, anchor, jar, eye, right);
                Assert.That(tut.Openness, Is.EqualTo(0f), "끄면 병 쪽으로 접혀 들어간다");
                Assert.That(tut.Card, Is.EqualTo(TutorialCard.None));
                bool glowed = false;
                for (int i = 0; i < 72 * 10; i++)
                {
                    tut.Tick(Dt, s, i * Dt, anchor, jar, eye, right);
                    glowed |= tut.OffHintShowing;
                }
                Assert.That(glowed, Is.True, "꺼진 뒤 가만히 있으면 오른손 엄지·검지 끝이 한 번 숨 쉰다");
                Assert.That(tut.OffHintShown, Is.True);
                Assert.That(tut.OffHintShowing, Is.False, "한 번만");

                tut.SetEnabled(true);
                for (int i = 0; i < 60; i++) tut.Tick(Dt, s, 0f, anchor, jar, eye, right);
                Assert.That(tut.Card, Is.EqualTo(TutorialCard.Clock), "다시 켜면 그 단계 카드가 펼쳐진다");
                Assert.That(tut.FootText, Does.Not.Contain("Right hand:"), "두 번째부터는 짧은 안내");
            }
            finally
            {
                tut.Dispose();
                Object.DestroyImmediate(cfg);
            }
        }

        RitualAudio SampledAudio(TimeRitualConfig cfg)
        {
            var go = new GameObject("SampledAudio");
            go.transform.SetParent(_root.transform, false);
            var audio = go.AddComponent<RitualAudio>();
            audio.Init(0.7f, cfg);
            return audio;
        }

        [Test]
        public void TheRitualSoundFolderHoldsExactlyTheSoundsThePersonPicked()
        {
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Ritual/Audio/Resources/" + RitualAudio.SfxFolder }))
                names.Add(System.IO.Path.GetFileNameWithoutExtension(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)));
            Assert.That(names, Is.EquivalentTo(RitualAudio.SfxNames), "고른 파일만, 빠짐없이");
            foreach (string n in names)
            {
                Assert.That(n.StartsWith("hand_sand"), Is.False, "알갱이 속 손 소리는 쓰지 않는다 (건드리기로 통일)");
                Assert.That(n.Contains("harp_oct"), Is.False, "봉인 여운은 open5 만");
            }
            Assert.That(RitualAudio.WarmTouchNames, Is.EqualTo(new[] { "touch_warm_chime_08_soft", "touch_warm_chime_06_soft" }), "빨강·노랑 건드리기는 차임 8번·6번을 무디게");
            foreach (string n in RitualAudio.SfxNames)
            {
                AudioClip clip = Resources.Load<AudioClip>(RitualAudio.SfxFolder + "/" + n);
                Assert.That(clip, Is.Not.Null, n);
                Assert.That(clip.channels, Is.EqualTo(1), n);
                Assert.That(clip.length, Is.GreaterThan(0.2f), n);
            }
            TextAsset table = Resources.Load<TextAsset>(RitualAudio.SfxFolder + "/sfx_gains");
            Assert.That(table, Is.Not.Null, "음량 보정표");
            var gains = JsonUtility.FromJson<RitualAudio.SfxGains>(table.text);
            Assert.That(gains.names, Is.EquivalentTo(RitualAudio.SfxNames), "음원마다 보정값이 있다");
            foreach (float g in gains.gains) Assert.That(g, Is.InRange(0.25f, 4f));
        }

        [Test]
        public void TouchingAPieceAndMovingThroughItsGrainsSoundTheSame()
        {
            var cfg = Object.Instantiate(_config);
            try
            {
                RitualAudio audio = SampledAudio(cfg);
                Assert.That(audio.Sampled, Is.True);
                for (int i = 0; i < 4; i++)
                {
                    audio.Knead(new Vector3(0.1f * i, 1f, 0.3f), PieceFeel.Jelly, true, 0.2f + 0.3f * i);
                    Assert.That(RitualAudio.WarmTouchNames, Has.Member(audio.LastSampled));
                }
                audio.Knead(Vector3.one, PieceFeel.Water, false, 0.4f);
                Assert.That(audio.LastSampled, Is.EqualTo("touch_cool_drop"));
                audio.Knead(Vector3.one, PieceFeel.Fur, false);
                Assert.That(audio.LastSampled, Is.EqualTo("touch_cool_drop"));
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void ThePulledChainRisesInPitchAndTheStickyMergeClimbsThenResolves()
        {
            var cfg = Object.Instantiate(_config);
            try
            {
                float prev = 0f;
                for (int u = 1; u <= cfg.maxUnits; u++)
                {
                    float p = RitualAudio.PullPitch(u, cfg.pullUnitsPerStep, cfg.pullBaseSemitones, cfg.pullRiseSemitones);
                    Assert.That(p, Is.GreaterThanOrEqualTo(prev), "길어질수록 음이 올라간다");
                    Assert.That(p, Is.LessThanOrEqualTo(RitualAudio.Semi(cfg.pullBaseSemitones + cfg.pullRiseSemitones) + 1e-4f));
                    prev = p;
                }
                Assert.That(RitualAudio.PullPitch(6, cfg.pullUnitsPerStep, cfg.pullBaseSemitones, cfg.pullRiseSemitones),
                    Is.GreaterThan(RitualAudio.PullPitch(1, cfg.pullUnitsPerStep, cfg.pullBaseSemitones, cfg.pullRiseSemitones) * 1.1f), "한 시간 길이면 귀에 띄게 오른다");
                for (int warm = 0; warm < 2; warm++)
                    for (int beat = 1; beat <= cfg.bondBeats; beat++)
                        Assert.That(RitualAudio.BondPitch(beat, warm == 1), Is.GreaterThan(RitualAudio.BondPitch(beat - 1, warm == 1)), "다리가 이어지는 동안 오르고 합쳐질 때 그 위에서 끝난다");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void EachLayerHasItsOwnNoteAndTheSealRingsTheLowGlassHarp()
        {
            var cfg = Object.Instantiate(_config);
            try
            {
                RitualAudio audio = SampledAudio(cfg);
                var heard = new System.Collections.Generic.HashSet<string>();
                for (int i = 0; i < RitualAudio.NoteIds.Length; i++)
                {
                    audio.Layer(Vector3.zero, RitualAudio.NoteIds[i]);
                    Assert.That(audio.LastSampled, Is.EqualTo(RitualAudio.NoteNames[i]));
                    heard.Add(audio.LastSampled);
                }
                Assert.That(heard.Count, Is.EqualTo(5), "카테고리마다 음이 다르다");
                Assert.That(cfg.sealVoice, Is.EqualTo(1), "봉인은 B안: 싱잉볼 없이 낮춘 글라스 하프 open5 만 (사람 11-17 \"b가 훨씬 나음\")");
                float full = 12f - cfg.afterglowLead;
                float harp = RitualAudio.HarpLength(cfg, 12f);
                Assert.That(harp, Is.EqualTo(full * 0.6f).Within(0.05f), "여운은 지금 길이의 60% (사람 11-17)");
                float prevFade = 1f;
                for (float t = 0f; t <= harp + 0.1f; t += 0.1f)
                {
                    float f = RitualAudio.HarpFade(t, harp, cfg.afterglowHoldShare);
                    Assert.That(f, Is.LessThanOrEqualTo(prevFade + 1e-5f), "잦아들기만 한다");
                    prevFade = f;
                }
                Assert.That(RitualAudio.HarpFade(0f, harp, cfg.afterglowHoldShare), Is.EqualTo(1f));
                Assert.That(RitualAudio.HarpFade(harp, harp, cfg.afterglowHoldShare), Is.EqualTo(0f), "60% 지점에서 끝난다");
                audio.ClockTouch(Vector3.zero);
                Assert.That(audio.LastSampled, Is.EqualTo("clock_touch_cymbal"), "시계를 건드리면 핑거 심벌");
                audio.Shelved(Vector3.zero);
                Assert.That(audio.LastSampled, Is.EqualTo("shelf_glass_touch"), "유리 닿는 소리로 닫는다");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void WaterKindsMergeWithBubblesThatDifferFromTheBridgeBeats()
        {
            var cfg = Object.Instantiate(_config);
            try
            {
                RitualAudio audio = SampledAudio(cfg);
                audio.Bond(Vector3.zero, false, 1);
                string beat = audio.LastSampled;
                audio.Chimes(Vector3.zero, 3, 4, cfg.chimeInterval, false);
                Assert.That(audio.LastSampled, Is.EqualTo("merge_cool_bubbles"), "물방울 계열이 합쳐지는 순간은 거품 소리 (사람 11-17)");
                Assert.That(audio.LastSampled, Is.Not.EqualTo(beat), "다리 박자와 귀로 구분된다");
                audio.Chimes(Vector3.zero, 3, 4, cfg.chimeInterval, true);
                Assert.That(audio.LastSampled, Is.EqualTo("obj_warm_ping"), "빨강·노랑은 그대로 (사람 11-17 통과)");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void PouringStopsWithoutATailWhileTheCrumblingClockKeepsIts()
        {
            var cfg = Object.Instantiate(_config);
            try
            {
                Assert.That(RitualAudio.PourTail(cfg, false), Is.EqualTo(0f), "다 부으면 소리도 끝난다, 꼬리 없음 (사람 11-17)");
                Assert.That(RitualAudio.PourTail(cfg, true), Is.GreaterThan(0.5f), "시계가 무너지는 소리는 꼬리까지");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void ThePourStreamEndsWhenTheLastGrainReachesTheSandLongBeforeThePourSettles()
        {
            RitualJar jar = Jar();
            float now = 0f;
            int units = 12;
            TimePieceView piece = Pulled(_move, 0, units, new Vector3(0.3f, 0.3f, 0f), Vector3.forward);
            float streamEnd = -1f;
            float finished = -1f;
            jar.StreamEnded += l => streamEnd = now;
            jar.PourFinished += l => finished = now;
            Assert.That(jar.BeginPour(piece), Is.Not.Null);
            Assert.That(jar.Streaming, Is.True);
            Finish(jar, ref now);
            float pourSeconds = Mathf.Clamp(_config.pourSecondsMin + units * _config.pourSecondsPerUnit, _config.pourSecondsMin, _config.pourSecondsMax);
            Assert.That(streamEnd, Is.GreaterThan(0f), "흐름이 끝나는 순간이 있다");
            Assert.That(streamEnd, Is.LessThanOrEqualTo(pourSeconds + _config.grainFlight * 1.2f * RitualJar.SurfaceTau + 2f * Dt), "마지막 알갱이가 모래 면에 닿을 때");
            Assert.That(finished, Is.GreaterThan(streamEnd + 0.3f), "붓기 소리는 층이 가라앉기를 기다리지 않는다");
            Assert.That(jar.Streaming, Is.False);
            jar.Dispose();
        }

        [Test]
        public void TiltingOrShakingTheSealedJarMovesOnlyTheTopOfTheSandThenSettles()
        {
            RitualJar jar = Jar();
            float now = 0f;
            Assert.That(jar.BeginPour(Pulled(_move, 0, 24, new Vector3(0.3f, 0.3f, 0f), Vector3.forward)), Is.Not.Null);
            Finish(jar, ref now);
            Assert.That(jar.BeginPour(Pulled(_work, 1, 24, new Vector3(0.3f, 0.3f, 0f), Vector3.forward)), Is.Not.Null);
            Finish(jar, ref now);
            RitualJar.Layer bottom = jar.Layers[0];
            RitualJar.Layer top = jar.Layers[1];
            var rest = new Vector3(0f, TimeRitualLook.RestLift, 0f);
            Quaternion upright = jar.root.rotation;
            Vector3 home = jar.root.position;
            jar.SloshEnabled = true;
            for (int i = 0; i < 72; i++) { now += Dt; jar.Tick(now, Dt); }
            Assert.That(jar.Sloshing, Is.False, "세워 두면 가만히 있다 (비용 0)");

            jar.root.rotation = Quaternion.AngleAxis(55f, Vector3.right) * upright;
            for (int i = 0; i < 72 * 2; i++) { now += Dt; jar.Tick(now, Dt); }
            Vector3 down = jar.root.InverseTransformDirection(Vector3.down);
            var flow = new Vector2(down.x, down.z).normalized;
            Assert.That(jar.SloshOffset.magnitude, Is.GreaterThan(_config.sloshMaxShift * 0.8f), "기울이면 윗면이 낮은 쪽으로 흐른다");
            Assert.That(Vector2.Dot(jar.SloshOffset.normalized, flow), Is.GreaterThan(0.95f));
            Assert.That(jar.SloshMoved, Is.GreaterThan(0));
            Assert.That(jar.SloshMoved, Is.LessThan(top.cloud.Count + _config.capGrains + 1), "움직이는 건 윗면 띠뿐");
            float surface = jar.SurfaceMean;
            bool rose = false;
            for (int i = 0; i < top.cloud.Count; i++)
            {
                Vector3 h = top.cloud.home[i];
                Vector3 d = top.cloud.disp[i];
                if (surface - h.y >= _config.sloshBand) Assert.That(Vector3.Distance(d, rest), Is.LessThan(1e-6f), "띠 아래 알갱이는 그대로");
                else if (new Vector2(h.x, h.z).normalized.x * flow.x + new Vector2(h.x, h.z).normalized.y * flow.y > 0.7f) rose |= jar.root.InverseTransformDirection(d - rest).y > 0.001f;
            }
            Assert.That(rose, Is.True, "낮은 쪽 표면은 쌓여 올라간다");
            for (int i = 0; i < bottom.cloud.Count; i++)
                Assert.That(Vector3.Distance(bottom.cloud.disp[i], rest), Is.LessThan(1e-6f), "아래 층(그날의 기록)은 섞이지 않는다");

            jar.root.rotation = upright;
            float fastest = 0f;
            for (int i = 0; i < 72; i++)
            {
                now += Dt;
                jar.root.position = home + Vector3.right * (0.03f * Mathf.Sin(now * 2f * Mathf.PI * 3f));
                jar.Tick(now, Dt);
                fastest = Mathf.Max(fastest, jar.SloshSpeed);
            }
            Assert.That(fastest, Is.GreaterThan(0.002f), "흔들면 출렁인다");
            jar.root.position = home;
            jar.SloshEnabled = false;
            for (int i = 0; i < 72 * 4; i++) { now += Dt; jar.Tick(now, Dt); }
            Assert.That(jar.Sloshing, Is.False, "놓으면 가라앉고 멈춘다");
            for (int i = 0; i < top.cloud.Count; i++)
                Assert.That(Vector3.Distance(top.cloud.disp[i], rest), Is.LessThan(1e-5f), "제자리로");
            jar.Dispose();
        }

        [Test]
        public void SandShiftSoundsOnlyWhileTheSandMovesAndStaysUnderThePour()
        {
            var cfg = Object.Instantiate(_config);
            try
            {
                Assert.That(RitualAudio.SandShiftVolume(cfg, 0f), Is.EqualTo(0f), "가만히 있으면 무음");
                Assert.That(RitualAudio.SandShiftVolume(cfg, 3f), Is.EqualTo(cfg.sandShiftVolume));
                Assert.That(cfg.sandShiftVolume, Is.LessThan(cfg.pourBedVolume), "붓기보다 작게");
                Assert.That(cfg.sandSlosh, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void TheWarmLightGradeTintsTowardIncandescentAndKeepsDarkDark()
        {
            var cfg = Object.Instantiate(_config);
            try
            {
                Color same = TimeRitualLook.WarmGrade(new Color(0.3f, 0.6f, 0.9f), Color.white, 1f, 1f);
                Assert.That(same.r, Is.EqualTo(0.3f).Within(1e-4f), "흰 색조·채도 1·감마 1 이면 그대로");
                Assert.That(same.b, Is.EqualTo(0.9f).Within(1e-4f));
                Color white = TimeRitualLook.WarmGrade(Color.white, cfg.warmTint, cfg.warmSaturation, cfg.warmGamma);
                Assert.That(white.r, Is.GreaterThan(white.g).And.GreaterThan(white.b), "흰 벽이 백열등 빛으로");
                Assert.That(white.g, Is.GreaterThan(white.b));
                Color black = TimeRitualLook.WarmGrade(Color.black, cfg.warmTint, cfg.warmSaturation, cfg.warmGamma);
                Assert.That(black.r + black.g + black.b, Is.LessThan(1e-4f), "어둠은 어둠으로");
                Color gray = TimeRitualLook.WarmGrade(new Color(0.5f, 0.5f, 0.5f), cfg.warmTint, cfg.warmSaturation, cfg.warmGamma);
                Assert.That(0.2126f * gray.r + 0.7152f * gray.g + 0.0722f * gray.b, Is.LessThan(0.5f), "중간 밝기는 조금 가라앉는다");
                int n = TimeRitualLook.WarmLutSize;
                var lut = new Color[n * n * n];
                TimeRitualLook.WarmLut(lut, cfg.warmTint, cfg.warmSaturation, cfg.warmGamma);
                Color red = lut[(n - 1)];
                Color blue = lut[(n - 1) * n * n];
                Assert.That(red.r, Is.GreaterThan(red.b), "LUT 는 r 이 가장 빠르게 바뀐다 (Meta 텍스처 변환과 같은 순서)");
                Assert.That(blue.b, Is.GreaterThan(red.b));
                Assert.That(cfg.afterShelfLight, Is.EqualTo(0.34f).Within(1e-4f), "사람 11-17 헤드셋에서 컨펌한 값 (\"딱좋아요\")");
                Assert.That(cfg.warmTint.g, Is.EqualTo(0.78f).Within(1e-4f), "붉은 쪽이 아니라 주황·전구색 쪽");
                Assert.That(cfg.warmTint.b, Is.EqualTo(0.5f).Within(1e-4f));
                Assert.That(cfg.warmSaturation, Is.EqualTo(0.45f).Within(1e-4f), "현실 색을 더 뺀다");
                Assert.That(cfg.warmGamma, Is.EqualTo(1.05f).Within(1e-4f));
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void TheOurglassIconIsReadyForEveryAndroidIconKind()
        {
            Texture2D icon = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Ritual/Icon/ourglass-icon.png");
            Texture2D back = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Ritual/Icon/ourglass-icon-adaptive-back.png");
            Texture2D fore = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Ritual/Icon/ourglass-icon-adaptive-fore.png");
            Assert.That(icon, Is.Not.Null, "사람이 준 앱 아이콘 (11-17)");
            Assert.That(back, Is.Not.Null, "적응형 아이콘 배경: 가장자리가 잘리지 않게 여백");
            Assert.That(fore, Is.Not.Null, "적응형 아이콘 앞면: 투명");
            Assert.That(icon.width, Is.EqualTo(icon.height).And.GreaterThanOrEqualTo(432));
            Assert.That(back.width, Is.EqualTo(512).And.EqualTo(back.height), "2의 거듭제곱이라 가져오며 늘어나지 않는다");
            Assert.That(fore.width, Is.EqualTo(512).And.EqualTo(fore.height));
        }

        [Test]
        public void SampledSoundCanBeSwitchedBackToTheSynthesizedSet()
        {
            var cfg = Object.Instantiate(_config);
            try
            {
                RitualAudio audio = SampledAudio(cfg);
                Assert.That(audio.Sampled, Is.True);
                cfg.sampledSound = false;
                Assert.That(audio.Sampled, Is.False);
                audio.ClockTouch(Vector3.zero);
                Assert.That(audio.LastSampled, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
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


        [Test]
        public void DaysAreSavedByDateAndReadBackWithoutDemoRecords()
        {
            string path = System.IO.Path.Combine(Application.temporaryCachePath, "store-test-" + System.Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var store = new RitualDayStore(path);
                var a = new DayRecord { date = "2026-11-13", dominant = "work" };
                a.layers.Add(new DayLayer { category = "work", units = 30 });
                var b = new DayRecord { date = "2026-11-14", dominant = "sleep" };
                b.layers.Add(new DayLayer { category = "sleep", units = 40 });
                b.layers.Add(new DayLayer { category = "play", units = 10 });
                store.Save(b);
                store.Save(a);
                store.Save(new DayRecord { date = "2026-11-12", demo = true });
                var again = new DayRecord { date = "2026-11-14", dominant = "move" };
                again.layers.Add(new DayLayer { category = "move", units = 12 });
                store.Save(again);
                var reread = new RitualDayStore(path);
                Assert.That(reread.Days.Count, Is.EqualTo(2), "데모 기록은 실제 저장과 섞이지 않는다");
                Assert.That(reread.Days[0].date, Is.EqualTo("2026-11-13"), "날짜 순");
                Assert.That(reread.Days[1].dominant, Is.EqualTo("move"), "같은 날은 마지막 봉인으로");
                var before = reread.Before(new System.DateTime(2026, 11, 14), 6);
                Assert.That(before.Count, Is.EqualTo(1), "오늘 이전 날만");
                Assert.That(reread.Find(new System.DateTime(2026, 11, 13)).TotalUnits, Is.EqualTo(30));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        [Test]
        public void DemoFillsSixPastDaysWithDifferentLayerRatios()
        {
            var today = new System.DateTime(2026, 11, 15);
            var demo = RitualDayStore.Demo(today, 6);
            Assert.That(demo.Count, Is.EqualTo(6));
            var ratios = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 6; i++)
            {
                Assert.That(demo[i].demo, Is.True);
                Assert.That(demo[i].date, Is.EqualTo(RitualDayStore.Key(today.AddDays(i - 6))), "어제까지 차례로");
                Assert.That(demo[i].layers.Count, Is.GreaterThanOrEqualTo(2));
                string sig = "";
                foreach (DayLayer l in demo[i].layers) sig += l.category + Mathf.RoundToInt(10f * l.units / demo[i].TotalUnits);
                ratios.Add(sig);
            }
            Assert.That(new System.Collections.Generic.HashSet<string>(ratios).Count, Is.GreaterThanOrEqualTo(5), "층 비율이 서로 다르다");
        }

        [Test]
        public void TheShelfIsAFrostedLedgeOfFivePlacesWithDatesAndTwoPastJarsYouCanPoke()
        {
            Material glowMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ritual/RitualGlow.mat");
            Material glassMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ritual/RitualGlass.mat");
            Shader frostShader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/Ritual/RitualFrost.shader");
            Assert.That(frostShader, Is.Not.Null, "반투명 유리 단 셰이더");
            var frostMat = new Material(frostShader);
            Font font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Fraunces/Fraunces_72pt-Black.ttf");
            TimeCategoryAsset[] cats = ShippedCategories();
            System.Func<string, TimeCategoryAsset> of = id => System.Array.Find(cats, c => c.id == id);
            var shelf = new RitualShelf(_config, glowMat, frostMat, glassMat, font, id => of(id).glowColor, id => of(id).displayEn);
            try
            {
                var day = new System.DateTime(2026, 11, 15);
                Vector3 eye = new Vector3(0f, 1.2f, 0f);
                shelf.Place(eye, Vector3.forward, 0.92f, day);
                Assert.That(shelf.SlotCount, Is.EqualTo(5), "자리 다섯");
                Assert.That(shelf.TodaySlot, Is.EqualTo(2), "오늘은 세 번째 고리");
                for (int i = 0; i < 5; i++)
                {
                    Vector3 p = shelf.Slot(i);
                    Vector3 flat = p - eye;
                    flat.y = 0f;
                    Assert.That(flat.magnitude, Is.EqualTo(_config.shelfDistance).Within(1e-3f), "사용자를 감싸는 호");
                    Assert.That(p.y, Is.EqualTo(0.92f).Within(1e-4f), "책상 위에 뜬 단");
                    if (i > 0) Assert.That(p.x, Is.GreaterThan(shelf.Slot(i - 1).x), "왼쪽이 지난 날, 오른쪽이 앞으로 올 날");
                }
                Assert.That(shelf.DateText(0), Is.EqualTo("Nov 13"));
                Assert.That(shelf.DateText(2), Is.EqualTo("Nov 15"));
                Assert.That(shelf.DateText(4), Is.EqualTo("Nov 17"), "빈 자리에는 날짜만");
                Assert.That(_config.shelfStyle, Is.EqualTo(ShelfStyle.Pedestals), "B 확정");
                Assert.That(shelf.Surfaces, Is.EqualTo(5), "떠 있는 받침 다섯");

                Vector3 restSeat = shelf.TodaySeat;
                Vector3 corner = new Vector3(-0.38f, 0.75f, 0.38f);
                shelf.SetCorner(corner);
                shelf.SetStage(0f);
                Assert.That(shelf.Scale, Is.EqualTo(0.25f).Within(0.01f), "만드는 동안은 1/4 미니어처");
                Assert.That(Vector3.Distance(shelf.TodaySeat, corner), Is.LessThan(1e-3f), "책상 왼쪽 구석");
                Assert.That(shelf.TodaySeat.z, Is.LessThan(restSeat.z), "뒤로 물러나 벽을 뚫지 않는다");
                shelf.SetStage(0.5f);
                Assert.That(shelf.Scale, Is.GreaterThan(0.5f).And.LessThan(0.75f), "커지면서 올라온다");
                shelf.SetStage(1f);
                Assert.That(Vector3.Distance(shelf.TodaySeat, restSeat), Is.LessThan(1e-4f), "봉인 뒤 원래 자리·크기로");
                Assert.That(shelf.Scale, Is.EqualTo(1f).Within(1e-4f));

                shelf.Show(RitualDayStore.Demo(day, _config.shelfDemoDays));
                Assert.That(shelf.MemoryJars, Is.EqualTo(2), "어제·그저께 병 두 개");
                Assert.That(shelf.Jars[0].slot, Is.EqualTo(0));
                Assert.That(shelf.Jars[1].slot, Is.EqualTo(1));
                Color calm = of(shelf.Jars[0].record.dominant).glowColor;
                Color lively = of(shelf.Jars[1].record.dominant).glowColor;
                Assert.That(calm.b, Is.GreaterThan(calm.r), "그저께는 푸른빛 차분한 날");
                Assert.That(lively.r, Is.GreaterThan(lively.b), "어제는 금빛 활기찬 날");

                float hi = 0f, lo = 1f;
                for (int i = 0; i < 200; i++)
                {
                    shelf.Tick(Dt, 1f);
                    if (i > 100)
                    {
                        hi = Mathf.Max(hi, shelf.TodayPulse);
                        lo = Mathf.Min(lo, shelf.TodayPulse);
                    }
                }
                Assert.That(hi - lo, Is.GreaterThan(0.3f), "오늘 고리가 숨 쉬듯 깜빡인다");
                for (int i = 0; i < 30; i++) shelf.Tick(Dt, 1f, 1f);
                Assert.That(shelf.TodayNear, Is.GreaterThan(0.9f), "병을 가져가면 오늘 고리가 밝아진다");

                RitualShelf.ShelfJar past = shelf.Jars[0];
                Vector3 tip = past.root.position + Vector3.up * 0.05f;
                Assert.That(shelf.Touch(0, tip + Vector3.forward * 0.3f, true), Is.EqualTo(-1));
                Assert.That(shelf.Touch(0, tip, true), Is.EqualTo(0), "손가락으로 톡");
                shelf.Poke(0);
                Assert.That(shelf.Touch(0, tip, true), Is.EqualTo(-1), "머물러 있으면 다시 울리지 않는다");
                for (int i = 0; i < 20; i++) shelf.Tick(Dt, 1f);
                Assert.That(past.swirl.Energy, Is.GreaterThan(10f), "스노우볼처럼 소용돌이");
                Assert.That(Mathf.Abs(past.swirl.AngleOf(0)), Is.GreaterThan(5f));
                Assert.That(shelf.NoteText, Does.StartWith("Rested 5h"), "그날의 시간 기록이 피어오른다");
                Assert.That(shelf.NoteText, Does.Contain("Moved 1h 50m"));
                for (int i = 0; i < 600; i++) shelf.Tick(Dt, 1f);
                Assert.That(past.swirl.Energy, Is.EqualTo(0f), "잦아든다");
                Assert.That(shelf.NoteText, Is.EqualTo(""), "기록은 잠깐만");

                shelf.Fill(true);
                shelf.Tick(Dt, 1f);
                Assert.That(shelf.TodayPulse, Is.EqualTo(0f), "채우면 깜빡임이 멈춘다");

                _config.shelfStyle = ShelfStyle.Ledge;
                shelf.Place(eye, Vector3.forward, 0.92f, day);
                Assert.That(shelf.Surfaces, Is.EqualTo(1), "A 는 하나로 이어진 유리 단");
            }
            finally
            {
                shelf.Dispose();
                Object.DestroyImmediate(frostMat);
            }
        }

        [Test]
        public void PlacingTheSealedJarOnTodaysPlaceEndsTheRitualAndTheDayIsRemembered()
        {
            Material glowMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ritual/RitualGlow.mat");
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            cfg.shelfDemo = false;
            try
            {
                TimeRitual ritual = BareRitual("ShelfRitual", cfg, cats, glowMat);
                Assert.That(ritual.Shelf, Is.Not.Null);
                Assert.That(ritual.Shelf.MemoryJars, Is.EqualTo(0), "기록이 없으면 지난 병도 없다");
                Assert.That(ritual.Shelf.Stage, Is.EqualTo(0f), "만드는 동안 선반은 미니어처");
                Assert.That(ritual.Shelf.Scale, Is.EqualTo(cfg.shelfMiniScale).Within(1e-3f));
                Assert.That(Vector3.Distance(ritual.Shelf.TodaySeat, ritual.transform.TransformPoint(cfg.shelfCorner)), Is.LessThan(1e-3f), "책상 왼쪽 구석");
                float now = 1f;
                for (int c = 1; c <= 3; c++)
                {
                    TimePieceView piece = Pulled(cats[c], c, 6 * c, new Vector3(0.3f, 0.3f, 0f), Vector3.forward);
                    Assert.That(ritual.Jar.BeginPour(piece), Is.Not.Null);
                    Run(ritual, ref now, 4f, default);
                }
                typeof(TimeRitual).GetMethod("Seal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(ritual, null);
                Run(ritual, ref now, 6f, default);
                Assert.That(ritual.Shelf.Stage, Is.EqualTo(1f), "봉인이 끝나면 선반이 앞으로 온다");
                Assert.That(ritual.Shelf.Scale, Is.EqualTo(1f).Within(1e-4f));
                Assert.That(ritual.Today, Is.Not.Null);
                Assert.That(ritual.Store.Find(System.DateTime.Now), Is.Not.Null, "봉인하면 오늘이 날짜로 저장된다");
                Assert.That(ritual.Store.Find(System.DateTime.Now).TotalUnits, Is.EqualTo(36));
                float lightBefore = ritual.PassthroughLight;

                ritual.Jar.root.rotation = Quaternion.AngleAxis(140f, Vector3.up) * ritual.Jar.root.rotation;
                Vector3 grab = ritual.Jar.BodyCenter + new Vector3(0.06f, 0f, 0f);
                Run(ritual, ref now, Dt, new HandFrame { right = PinchPose(grab, false) });
                Run(ritual, ref now, 8 * Dt, new HandFrame { right = PinchPose(grab, true) });
                Vector3 offset = ritual.Jar.root.position - grab;
                Vector3 slot = ritual.Shelf.TodaySeat;
                Vector3 end = slot - offset + new Vector3(0.02f, 0.03f, 0f);
                for (int i = 1; i <= 60; i++) Run(ritual, ref now, Dt, new HandFrame { right = PinchPose(Vector3.Lerp(grab, end, i / 60f), true) });
                Run(ritual, ref now, 4 * Dt, new HandFrame { right = PinchPose(end + Vector3.up * 0.15f, false) });
                Assert.That(ritual.Shelved, Is.True, "선반의 오늘 자리에 놓는다");
                Run(ritual, ref now, 1f, default);
                Assert.That(Vector3.Distance(ritual.Jar.root.position, slot), Is.LessThan(0.005f), "오늘 자리에 선다");
                Assert.That(ritual.Shelf.NoteText, Is.EqualTo(RitualShelf.LoggedLabel(System.DateTime.Now)), "November 15, Logged.");
                Assert.That(ritual.Shelf.Rippling, Is.True, "단에 물결이 한 번 퍼진다");
                Assert.That(Vector3.Dot(ritual.Jar.root.up, Vector3.up), Is.GreaterThan(0.99f));
                Vector3 toEye = Vector3.ProjectOnPlane(ritual.Shelf.Eye - ritual.Jar.root.position, Vector3.up).normalized;
                Assert.That(Vector3.Dot(-ritual.Jar.root.forward, toEye), Is.GreaterThan(0.995f), "병을 돌려 들고 와도 라벨은 무조건 정면 (사람 11-17)");
                Assert.That(Quaternion.Angle(ritual.Jar.root.rotation, ritual.Shelf.TodayFacing), Is.LessThan(0.5f), "지난 병들과 같은 방향");
                Run(ritual, ref now, cfg.shelfLightBack + 0.5f, default);
                Assert.That(lightBefore, Is.LessThan(0.5f));
                Assert.That(ritual.PassthroughLight, Is.EqualTo(cfg.afterShelfLight).Within(0.01f), "선반에 올린 뒤에도 방은 어둑하게 (사람 11-17, 날것의 현실이 보이지 않게)");
                Assert.That(ritual.PassthroughLight, Is.LessThan(0.6f));
                Assert.That(ritual.PassthroughWarmth, Is.EqualTo(cfg.afterShelfWarmth).Within(0.01f), "어둠 속 백열등처럼 따뜻하게");

                var yesterday = new DayRecord { date = RitualDayStore.Key(System.DateTime.Now.AddDays(-1)), dominant = "sleep" };
                yesterday.layers.Add(new DayLayer { category = "sleep", units = 42 });
                ritual.Store.Save(yesterday);
                TimeRitual next = BareRitual("NextDay", cfg, cats, glowMat);
                Assert.That(next.Shelf.MemoryJars, Is.EqualTo(1), "저장된 지난 날의 병이 다음에 다시 보인다");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void PastJarsOnTheMiniatureShelfDrawAQuarterOfTheirGlassAndLidAndAllOfItOnceTheShelfComesForward()
        {
            Material glowMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ritual/RitualGlow.mat");
            Material glassMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ritual/RitualGlass.mat");
            Material waxMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ritual/RitualWax.mat");
            Assert.That(glassMat, Is.Not.Null);
            Assert.That(waxMat, Is.Not.Null);
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            cfg.shelfDemo = true;
            Assert.That(cfg.shelfMiniDetail, Is.LessThanOrEqualTo(0.3f), "미니어처 선반의 지난 병은 솎아서 그린다 (성능 측정 11-16, build-2c40)");
            try
            {
                var go = new GameObject("MiniShelf");
                go.transform.SetParent(_root.transform, false);
                var ritual = go.AddComponent<TimeRitual>();
                var so = new UnityEditor.SerializedObject(ritual);
                so.FindProperty("config").objectReferenceValue = cfg;
                so.FindProperty("glowMaterial").objectReferenceValue = glowMat;
                so.FindProperty("glassMaterial").objectReferenceValue = glassMat;
                so.FindProperty("waxMaterial").objectReferenceValue = waxMat;
                so.FindProperty("piecesRoot").objectReferenceValue = go.transform;
                so.FindProperty("autoPlace").boolValue = false;
                var cp = so.FindProperty("categories");
                cp.arraySize = cats.Length;
                for (int i = 0; i < cats.Length; i++) cp.GetArrayElementAtIndex(i).objectReferenceValue = cats[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                ritual.Init();
                ritual.PlaceAt(new Vector3(0f, 1.0f, 0.42f), Quaternion.identity);

                Assert.That(ritual.Shelf.Stage, Is.EqualTo(0f), "만드는 동안 선반은 미니어처");
                Assert.That(ritual.Shelf.Jars.Count, Is.GreaterThanOrEqualTo(2), "지난 병 둘");
                foreach (RitualShelf.ShelfJar past in ritual.Shelf.Jars)
                {
                    Assert.That(past.vessel, Is.Not.Null);
                    Assert.That(past.wax, Is.Not.Null);
                    Assert.That(past.vessel.GlassSplatsFull, Is.EqualTo(Mathf.Max(200, cfg.glassSplats)));
                    Assert.That(past.wax.LidSplatsFull, Is.GreaterThan(cfg.coverSplats));
                    Assert.That(past.vessel.Thin, Is.True, "미니어처일 때 유리를 솎는다");
                    Assert.That(past.wax.Thin, Is.True, "미니어처일 때 밀랍·덮개·끈을 솎는다");
                    Assert.That(past.vessel.GlassSplats, Is.InRange(past.vessel.GlassSplatsFull * 0.15f, past.vessel.GlassSplatsFull * 0.35f));
                    Assert.That(past.wax.LidSplats, Is.InRange(past.wax.LidSplatsFull * 0.15f, past.wax.LidSplatsFull * 0.35f));
                    Assert.That(past.vessel.root.Find("Glass").GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.EqualTo(past.vessel.GlassSplats * 4), "실제로 그리는 메시가 솎은 것");
                }
                Assert.That(ritual.Jar.Thin, Is.False, "오늘 병은 솎지 않는다");
                ritual.Jar.SetThin(true);
                Assert.That(ritual.Jar.GlassSplats, Is.EqualTo(ritual.Jar.GlassSplatsFull));

                ritual.Shelf.SetStage(1f);
                foreach (RitualShelf.ShelfJar past in ritual.Shelf.Jars)
                {
                    Assert.That(past.vessel.Thin, Is.False, "앞으로 나오면 원래대로");
                    Assert.That(past.wax.Thin, Is.False);
                    Assert.That(past.vessel.GlassSplats, Is.EqualTo(past.vessel.GlassSplatsFull));
                    Assert.That(past.wax.LidSplats, Is.EqualTo(past.wax.LidSplatsFull));
                    Assert.That(past.vessel.root.Find("Glass").GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.EqualTo(past.vessel.GlassSplatsFull * 4));
                }
                ritual.Shelf.SetStage(0f);
                Assert.That(ritual.Shelf.Jars[0].vessel.Thin, Is.True, "다시 미니어처가 되면 다시 솎는다");
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void EveryShippedLookStaysInsideWhatTheLeanRitualShaderStillDraws()
        {
            foreach (TimeCategoryAsset c in ShippedCategories())
            {
                Assert.That(c.look, Is.Not.Null, c.id);
                SplatSurfaceMaterial look = SplatSurfaceMaterial.FromAsset(c.look);
                Assert.That(look, Is.Not.Null, c.id);
                int mode = look.ResolveMode(-1);
                Assert.That(mode == (int)SplatShaderMode.Raw || mode == (int)SplatShaderMode.Light, c.id + " 모드 " + mode + ": 의식 변형은 모드 0·5 만 그린다 (SPLAT_RITUAL)");
            }
            Assert.That(SplatSurfaceMaterial.Clay("none").ResolveMode(-1), Is.EqualTo(0));
            foreach (PieceFeel feel in System.Enum.GetValues(typeof(PieceFeel)))
                Assert.That(TimeRitualLook.Piece(null, feel).fluff, Is.EqualTo(0f), "의식 변형에는 털이 없다");
            Assert.That(TimeRitualLook.Sand(null, null, true, 1f).fluff, Is.EqualTo(0f));
        }

        [Test]
        public void ThinningKeepsRoughlyTheAskedShareAndGrowsEachSplatToCoverTheSameArea()
        {
            int kept = 0;
            const int total = 4000;
            for (int q = 0; q < total; q++)
                if (RitualStaticSplats.Keeps(q, 0.25f)) kept++;
            Assert.That(kept, Is.InRange(total * 0.2f, total * 0.3f));
            Assert.That(RitualStaticSplats.Keeps(7, 1f), Is.True);
            Assert.That(RitualStaticSplats.ThinRadius(0.25f), Is.EqualTo(2f).Within(1e-4f), "1/4 만 남기면 반지름 2배 = 같은 넓이");
            Assert.That(RitualStaticSplats.ThinRadius(1f), Is.EqualTo(1f).Within(1e-5f));
        }

        TimeRitual SealedShelfRitual(string name, TimeRitualConfig cfg, TimeCategoryAsset[] cats, Material glowMat, ref float now)
        {
            TimeRitual ritual = BareRitual(name, cfg, cats, glowMat);
            TimePieceView piece = Pulled(cats[1], 1, 12, new Vector3(0.3f, 0.3f, 0f), Vector3.forward);
            Assert.That(ritual.Jar.BeginPour(piece), Is.Not.Null);
            Run(ritual, ref now, 4f, default);
            typeof(TimeRitual).GetMethod("Seal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(ritual, null);
            Run(ritual, ref now, 6f, default);
            return ritual;
        }

        Vector3 CarryJar(TimeRitual ritual, ref float now, Vector3 jarBaseTarget, bool letGo)
        {
            Vector3 grab = ritual.Jar.BodyCenter + new Vector3(0.06f, 0f, 0f);
            Run(ritual, ref now, Dt, new HandFrame { right = PinchPose(grab, false) });
            Run(ritual, ref now, 8 * Dt, new HandFrame { right = PinchPose(grab, true) });
            Vector3 offset = ritual.Jar.root.position - grab;
            Vector3 end = jarBaseTarget - offset;
            for (int i = 1; i <= 40; i++) Run(ritual, ref now, Dt, new HandFrame { right = PinchPose(Vector3.Lerp(grab, end, i / 40f), true) });
            if (letGo) Run(ritual, ref now, 4 * Dt, new HandFrame { right = PinchPose(end, false) });
            return end;
        }

        [Test]
        public void TheSealedJarIsDrawnIntoTodaysPlaceWhenCarriedCloseAndForgivesDepth()
        {
            Material glowMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ritual/RitualGlow.mat");
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            cfg.shelfDemo = true;
            try
            {
                float now = 1f;
                TimeRitual a = SealedShelfRitual("Magnet", cfg, cats, glowMat, ref now);
                Vector3 seat = a.Shelf.TodaySeat;
                Vector3 hand = CarryJar(a, ref now, seat + new Vector3(0f, 0.05f, 0f), false);
                Assert.That(a.Shelved, Is.True, "손을 놓지 않아도 가까이 가져가면 오늘 자리가 끌어당긴다");
                Run(a, ref now, 1f, new HandFrame { right = PinchPose(hand, true) });
                Assert.That(Vector3.Distance(a.Jar.root.position, seat), Is.LessThan(0.005f), "오늘 자리 빛 위에 선다");

                float now2 = 1f;
                TimeRitual b = SealedShelfRitual("Depth", cfg, cats, glowMat, ref now2);
                Vector3 seatB = b.Shelf.TodaySeat;
                Vector3 radial = Vector3.ProjectOnPlane(seatB - b.Shelf.Eye, Vector3.up).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, radial);
                CarryJar(b, ref now2, seatB + side * 0.4f, true);
                Assert.That(b.Shelved, Is.False, "멀리서 놓으면 그 자리에 머문다");
                CarryJar(b, ref now2, seatB - radial * 0.3f, true);
                Assert.That(b.Shelved, Is.True, "앞뒤 깊이는 너그럽게 — 30cm 앞에서 놓아도 들어간다");
                Run(b, ref now2, 1f, default);
                Assert.That(Vector3.Distance(b.Jar.root.position, seatB), Is.LessThan(0.005f));
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        [Test]
        public void PokingTodaysJarOnTheShelfSwirlsItAndTheMoonOrbEndsTheDay()
        {
            Material glowMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ritual/RitualGlow.mat");
            TimeCategoryAsset[] cats = ShippedCategories();
            var cfg = Object.Instantiate(_config);
            cfg.shelfDemo = true;
            try
            {
                float now = 1f;
                TimeRitual r = SealedShelfRitual("Ending", cfg, cats, glowMat, ref now);
                RitualShelf.ShelfJar past = r.Shelf.Jars[0];
                Assert.That(past.vessel, Is.Not.Null, "지난 병도 완성된 입자 병");
                Assert.That(past.vessel.TotalUnits, Is.EqualTo(past.record.TotalUnits));
                Assert.That(past.vessel.Layers.Count, Is.EqualTo(past.record.layers.Count), "지난 병에도 고운 모래를 채우지 않는다");
                Assert.That(past.vessel.TotalGrains, Is.GreaterThan(500));
                float before = past.level;
                CarryJar(r, ref now, r.Shelf.TodaySeat + new Vector3(0f, 0.05f, 0f), false);
                Assert.That(r.Shelved, Is.True);
                Run(r, ref now, 1f, new HandFrame { right = PinchPose(r.Jar.BodyCenter + new Vector3(0.3f, 0.2f, 0f), false) });
                Assert.That(r.Shelf.Flash, Is.GreaterThan(0.5f), "오늘 병이 놓이는 순간");
                Assert.That(past.level, Is.GreaterThan(before * 1.3f), "다른 병들이 같이 밝아진다");
                Assert.That(r.Ending, Is.Not.Null);
                Assert.That(r.Ending.State, Is.EqualTo(RitualEnding.Phase.Waiting), "놓은 뒤 잠시 기다린다");
                Assert.That(r.TodaySwirl.Count, Is.GreaterThan(0));

                Vector3 away = r.Jar.BodyCenter + new Vector3(0.12f, 0f, -0.1f);
                Vector3 into = r.Jar.BodyCenter;
                Run(r, ref now, 4 * Dt, new HandFrame { right = PinchPose(away - new Vector3(0.055f, 0f, 0f), false) });
                Run(r, ref now, 4 * Dt, new HandFrame { right = PinchPose(into - new Vector3(0.055f, 0f, 0f), false) });
                Run(r, ref now, 10 * Dt, new HandFrame { right = PinchPose(away - new Vector3(0.055f, 0f, 0f), false) });
                Assert.That(r.TodaySwirl.Energy, Is.GreaterThan(10f), "오늘 병도 톡 건드리면 소용돌이");
                Assert.That(r.Shelf.NoteText, Does.Contain(cats[1].displayEn), "오늘의 기록");

                Run(r, ref now, cfg.endOrbDelay + 1.4f, new HandFrame { right = PinchPose(away, false) });
                Assert.That(r.Ending.State, Is.EqualTo(RitualEnding.Phase.Ready), "달빛 구슬이 떠오른다");
                Vector3 orb = r.Ending.Position;
                Assert.That(orb.y, Is.LessThan(r.Shelf.SurfaceY), "선반 아래쪽");
                Assert.That(r.Ending.QuitRequested, Is.False);

                for (int i = 0; i < 12; i++)
                    Run(r, ref now, Dt, new HandFrame { right = PinchPose(orb + new Vector3(-0.055f, 0.08f - i * 0.012f, 0.06f), false) });
                Assert.That(r.Ending.State, Is.EqualTo(RitualEnding.Phase.Snuffing), "쓸어내리면 촛불처럼 꺼진다");
                Assert.That(r.Ending.Trigger, Is.EqualTo("swept").Or.EqualTo("cupped"));
                Run(r, ref now, RitualEnding.SnuffSeconds, default);
                Assert.That(r.Ending.Mood, Is.LessThan(0.5f), "병들이 무드등으로 잦아든다");
                Run(r, ref now, cfg.endFadeSeconds + 0.7f, default);
                Assert.That(r.Ending.Fade, Is.EqualTo(1f).Within(1e-3f), "화면이 부드럽게 어두워진다");
                Assert.That(r.Ending.QuitRequested, Is.True, "앱을 닫는다");
                Assert.That(r.QuitIssued, Is.True);
                Assert.That(r.PassthroughLight, Is.LessThan(0.2f));
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

    }
}
