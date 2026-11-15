using MoodPicker.Ritual;
using MoodPicker.Splatter;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace MoodPicker.Tests
{
    public class RitualGestureTests
    {
        const float Dt = 1f / 72f;
        GameObject _root;
        TimeRitualConfig _config;
        TimeCategoryAsset _sleep;
        SplatSurfaceMaterial _look;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("RitualGestureRoot");
            _config = ScriptableObject.CreateInstance<TimeRitualConfig>();
            _config.showMinutesLabel = false;
            _sleep = ScriptableObject.CreateInstance<TimeCategoryAsset>();
            _sleep.id = "sleep";
            _look = SplatSurfaceMaterial.Clay("test");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_sleep);
        }

        TimePieceView Pulled(int units, out float now)
        {
            var piece = new TimePieceView(_root.transform, _sleep, 0, _look, null, _config, null, null, null);
            piece.BeginPull(Vector3.zero, Vector3.forward);
            float length = units * _config.unitLength + 0.001f;
            int current = 0;
            now = 0f;
            for (int i = 1; i <= 60; i++)
            {
                float raw = length * i / 60f;
                current = TimeUnits.Step(raw, current, _config.unitLength, _config.hysteresis, _config.maxUnits);
                piece.UpdatePull(Vector3.forward * raw, current, raw, Dt);
                now += Dt;
                piece.Tick(now, Dt);
            }
            piece.ReleasePull();
            return piece;
        }

        [Test]
        public void SqueezingARodBetweenTwoHandsCrumplesItIntoABlobWithTheSameTime()
        {
            TimePieceView piece = Pulled(12, out float now);
            piece.Hold();
            for (int i = 0; i <= 30; i++)
            {
                piece.SetSqueeze(i / 30f);
                now += Dt;
                piece.Tick(now, Dt);
            }
            Assert.That(piece.shape, Is.EqualTo(PieceShape.Rod), "쥐어짜는 동안은 아직 막대");
            Assert.That(piece.Units, Is.EqualTo(12));

            Assert.That(piece.Crumple(), Is.True);
            Assert.That(piece.shape, Is.EqualTo(PieceShape.Blob), "꾸깃하면 덩어리");
            Assert.That(piece.Units, Is.EqualTo(12), "구겨도 시간은 그대로");
            Assert.That(piece.data.mergedCount, Is.EqualTo(0), "구기기는 합치기가 아니다");
            Assert.That(piece.SplatCount, Is.EqualTo(2 * _config.capSplats + 12 * _config.SplatsPerUnit), "점이 사라지지 않는다");
            Assert.That(piece.Squeeze, Is.EqualTo(0f));
            Assert.That(piece.Lump, Is.GreaterThan(0f), "구긴 덩어리는 울퉁불퉁하다");
            Assert.That(piece.Crumple(), Is.False, "덩어리는 다시 구겨지지 않는다");

            piece.Drop();
            for (int i = 0; i < 200; i++)
            {
                now += Dt;
                piece.Tick(now, Dt);
            }
            Assert.That(piece.Awake, Is.False, "구긴 뒤에는 계산이 잠든다");
            piece.Kill();
        }

        [Test]
        public void ACrumpledBlobKeepsGettingSmallerAndMoreWrinkledDownToAFloor()
        {
            TimePieceView piece = Pulled(12, out float now);
            Assert.That(piece.Compact(), Is.False, "막대는 주무르기 전에 먼저 구겨야 한다");
            Assert.That(piece.Crumple(), Is.True);
            float full = piece.BlobRadiusNow;
            float lump = piece.Lump;
            int splats = piece.SplatCount;
            Assert.That(piece.Compact(), Is.True);
            Assert.That(piece.BlobRadiusNow, Is.EqualTo(full * _config.compactStep).Within(1e-6f), "한 번 주무르면 작아진다");
            Assert.That(piece.Lump, Is.GreaterThan(lump), "주름이 깊어진다");
            int more = 1;
            while (piece.Compact() && more < 50) more++;
            Assert.That(more, Is.LessThan(50));
            Assert.That(piece.BlobRadiusNow, Is.EqualTo(full * _config.compactMin).Within(1e-5f), "더 작아지지 않는 바닥이 있다");
            Assert.That(piece.Lump, Is.LessThanOrEqualTo(_config.compactLumpMax + 1e-6f));
            Assert.That(piece.Units, Is.EqualTo(12), "주물러도 시간은 그대로");
            Assert.That(piece.SplatCount, Is.EqualTo(splats), "점이 사라지지 않는다");
            for (int i = 0; i < 200; i++)
            {
                now += Dt;
                piece.Tick(now, Dt);
            }
            Assert.That(piece.Awake, Is.False);
            piece.Kill();
        }

        [Test]
        public void TheThreadSagsSlightlyBendsWhenTheHandSwingsAndStraightensAfterRelease()
        {
            var piece = new TimePieceView(_root.transform, _sleep, 0, _look, null, _config, null, null, null);
            piece.BeginPull(Vector3.zero, Vector3.forward);
            int current = 0;
            float now = 0f;
            Vector3 tip = Vector3.zero;
            for (int i = 1; i <= 40; i++)
            {
                tip = Vector3.forward * (0.15f * i / 40f);
                current = TimeUnits.Step(tip.magnitude, current, _config.unitLength, _config.hysteresis, _config.maxUnits);
                piece.UpdatePull(tip, current, tip.magnitude, Dt);
                now += Dt;
                piece.Tick(now, Dt);
            }
            Assert.That(Mathf.Abs(piece.Bend.x), Is.LessThan(1e-4f), "곧게 당기면 옆으로는 곧다");
            Assert.That(piece.Bend.y, Is.EqualTo(-_config.threadSag * tip.magnitude).Within(1e-3f), "중력에 살짝 처질 뿐");

            for (int i = 1; i <= 10; i++)
            {
                tip = Vector3.forward * 0.15f + Vector3.right * (0.1f * i / 10f);
                current = TimeUnits.Step(tip.magnitude, current, _config.unitLength, _config.hysteresis, _config.maxUnits);
                piece.UpdatePull(tip, current, tip.magnitude, Dt);
                now += Dt;
                piece.Tick(now, Dt);
            }
            Assert.That(piece.Bend.magnitude, Is.GreaterThan(0.005f), "손을 옆으로 흔들면 실이 휜다");
            Assert.That(Vector3.Dot(piece.Bend, Vector3.right), Is.LessThan(0f), "휜 실은 손보다 늦게 따라온다");
            Assert.That(piece.Bend.magnitude, Is.LessThanOrEqualTo(_config.threadBendMax * tip.magnitude + 1e-5f));

            piece.ReleasePull();
            for (int i = 0; i < 150; i++)
            {
                now += Dt;
                piece.Tick(now, Dt);
            }
            Assert.That(piece.Bend.magnitude, Is.LessThan(1e-3f), "놓으면 곧게 돌아온다");
            piece.Kill();
        }

        RitualWax Wax(out Transform jar)
        {
            jar = new GameObject("Jar").transform;
            jar.SetParent(_root.transform, false);
            return new RitualWax(jar, _config, null);
        }

        static void Run(RitualWax wax, float seconds)
        {
            for (int i = 0; i < Mathf.RoundToInt(seconds / Dt); i++) wax.Tick(Dt);
        }

        [Test]
        public void WaxIsDrawnFromTheStickAndOnlyDripsOntoTheMouth()
        {
            RitualWax wax = Wax(out _);
            Vector3 onStick = wax.stickRoot.position + Vector3.up * 0.004f;
            Assert.That(wax.BeginDraw(onStick), Is.False, "막대가 보이기 전에는 집을 수 없다");
            Assert.That(wax.Shown, Is.False, "병 위에 떠 있는 밀랍 방울은 없다");

            wax.ShowStick(Color.red);
            Run(wax, 1f);
            Assert.That(wax.StickShown, Is.True);
            Assert.That(wax.Shown, Is.False, "막대가 나와도 병 위 밀랍은 아직 없다");

            Assert.That(wax.BeginDraw(onStick), Is.True);
            for (int i = 0; i < 20; i++) wax.DrawTo(onStick + Vector3.up * 0.01f, Dt);
            Assert.That(wax.Loaded, Is.False, "조금만 당기면 방울이 덜 맺힌다");
            Assert.That(wax.EndDraw(true), Is.False);
            Run(wax, 1f);
            Assert.That(wax.DrawState, Is.EqualTo(WaxDraw.None), "덜 맺힌 방울은 막대로 돌아간다");
            Assert.That(wax.Dripped, Is.False);

            Assert.That(wax.BeginDraw(onStick), Is.True);
            for (int i = 0; i < 40; i++) wax.DrawTo(onStick + Vector3.up * 0.06f, Dt);
            Assert.That(wax.Loaded, Is.True, "충분히 당기면 손끝에 방울이 맺힌다");
            Vector3 far = wax.root.position + new Vector3(0.2f, 0.05f, 0f);
            wax.DrawTo(far, Dt);
            Assert.That(wax.EndDraw(true), Is.False, "입구가 아닌 곳에서 놓으면 떨어지지 않는다");
            Run(wax, 1f);
            Assert.That(wax.Dripped, Is.False);

            Assert.That(wax.BeginDraw(onStick), Is.True);
            for (int i = 0; i < 40; i++) wax.DrawTo(onStick + Vector3.up * 0.06f, Dt);
            Vector3 above = wax.root.position + Vector3.up * 0.06f;
            wax.DrawTo(above, Dt);
            Assert.That(wax.EndDraw(true), Is.True, "입구 위에서 놓으면 떨어진다");
            Run(wax, 1.5f);
            Assert.That(wax.Dripped, Is.True);
            Assert.That(wax.Ready, Is.True, "떨어진 밀랍은 누를 수 있다");
            Assert.That(wax.StickDistance(onStick), Is.EqualTo(float.MaxValue), "이미 떨군 뒤에는 다시 뽑지 않는다");

            wax.Hide();
            Assert.That(wax.Dripped, Is.False, "더 부으면 밀랍을 다시 떨궈야 한다");
            wax.Dispose();
        }

        [Test]
        public void NoDripWhileTheJarIsPouring()
        {
            RitualWax wax = Wax(out _);
            wax.ShowStick(Color.red);
            Run(wax, 1f);
            Vector3 onStick = wax.stickRoot.position + Vector3.up * 0.004f;
            Assert.That(wax.BeginDraw(onStick), Is.True);
            for (int i = 0; i < 40; i++) wax.DrawTo(onStick + Vector3.up * 0.06f, Dt);
            wax.DrawTo(wax.root.position + Vector3.up * 0.06f, Dt);
            Assert.That(wax.EndDraw(false), Is.False);
            Run(wax, 1.5f);
            Assert.That(wax.Dripped, Is.False);
            wax.Dispose();
        }

        [Test]
        public void TheHeapUnspoolsTowardThePullingHandAndCoastsAfter()
        {
            var go = new GameObject("Heap");
            go.transform.SetParent(_root.transform, false);
            TimeHeap heap = go.AddComponent<TimeHeap>();
            Vector3 dir = Vector3.forward;
            heap.Spin(heap.Radius * 0.8f, dir, Dt);
            Assert.That(Vector3.Dot(go.transform.up, dir), Is.GreaterThan(0.5f), "실을 당기는 쪽으로 윗면이 굴러온다");
            Assert.That(heap.SpinRate, Is.GreaterThan(0f));
            Quaternion before = go.transform.rotation;
            heap.Tick(0f, Dt);
            heap.Tick(Dt, Dt);
            Assert.That(Quaternion.Angle(before, go.transform.rotation), Is.GreaterThan(0.01f), "놓아도 조금 더 돈다");
            for (int i = 0; i < 72 * 6; i++) heap.Tick(i * Dt, Dt);
            Assert.That(heap.SpinRate, Is.EqualTo(0f), "곧 멈춘다");
        }

        [Test]
        public void TheJarKnowsHowMuchTimeStillFits()
        {
            var jar = new RitualJar(_root.transform, _config, null, null, null, c => _look);
            Assert.That(jar.Room, Is.EqualTo(jar.Capacity));
            Assert.That(jar.Fits(jar.Capacity), Is.True);
            Assert.That(jar.Fits(jar.Capacity + 1), Is.False, "넘치는 조각은 받지 않는다");
            Assert.That(jar.Fits(0), Is.False);
            jar.Dispose();
        }

        [Test]
        public void PieceFeelStaysFixedAndRodRespondsToDirectTouch()
        {
            TimePieceView piece = Pulled(12, out float now);
            PieceFeel start = piece.feel;
            Vector3 probe = piece.Center;
            float contact = piece.Touch(probe, new Vector3(0.4f, 0.1f, 0f), 0.036f, Dt);
            Assert.That(piece.shape, Is.EqualTo(PieceShape.Rod));
            Assert.That(contact, Is.GreaterThan(0f), "타래 상태에서도 직접 만지면 반응한다");
            Assert.That(piece.Awake, Is.True);
            now += Dt;
            piece.Tick(now, Dt);
            Assert.That(piece.feel, Is.EqualTo(start));

            Assert.That(piece.Crumple(), Is.True);
            Assert.That(piece.Compact(), Is.True);
            Assert.That(piece.feel, Is.EqualTo(start), "주물러도 카테고리의 고정 질감은 순환하지 않는다");
            Assert.That(piece.Compact(), Is.True);
            Assert.That(piece.feel, Is.EqualTo(start));
            piece.Kill();
        }

        [Test]
        public void AllBeadShapesProduceFinitePositionsAndNormals()
        {
            var shapes = (BeadShape[])System.Enum.GetValues(typeof(BeadShape));
            var dirs = new[]
            {
                new float3(0f, 1f, 0f),
                new float3(0f, -1f, 0f),
                new float3(1f, 0f, 0f),
                math.normalize(new float3(0.6f, 0.5f, 0.6f)),
                math.normalize(new float3(-0.7f, -0.4f, 0.5f)),
            };
            foreach (BeadShape s in shapes)
            {
                foreach (float3 d in dirs)
                {
                    float3 p = TimePieceView.ShapePoint(s, d, 0.02f, out float3 n);
                    Assert.That(math.all(math.isfinite(p)), Is.True, $"non-finite position for {s}");
                    Assert.That(math.all(math.isfinite(n)), Is.True, $"non-finite normal for {s}");
                    Assert.That(math.length(p), Is.GreaterThan(1e-4f), $"degenerate point for {s}");
                    Assert.That(math.length(n), Is.EqualTo(1f).Within(1e-2f), $"unnormalized normal for {s}");
                }
            }
        }
    }
}
