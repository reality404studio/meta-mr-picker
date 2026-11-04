using System;
using System.Collections.Generic;
using System.Linq;
using MoodPicker.Catch;
using MoodPicker.Hands;
using NUnit.Framework;
using UnityEngine;
using static MoodPicker.Tests.CatchHandFixtures;

namespace MoodPicker.Tests
{
    public class CatchRayTests
    {
        [Test]
        public void CatchRay_CenterLiesOnHeadForwardRay()
        {
            var rnd = new System.Random(7);
            for (int i = 0; i < 20; i++)
            {
                var rot = Quaternion.Euler((float)(rnd.NextDouble() * 60 - 30), (float)(rnd.NextDouble() * 360), (float)(rnd.NextDouble() * 20 - 10));
                var head = new Pose(new Vector3((float)rnd.NextDouble(), 1.1f + (float)rnd.NextDouble() * 0.5f, (float)rnd.NextDouble()), rot);
                var run = new Run().Idle(0.2f, head).BothPinch(0.3f, head, 0.05f + i * 0.004f, 0.3f + i * 0.02f);
                Assert.That(run.square.Phase, Is.EqualTo(CatchPhase.Sizing), $"pose {i}");
                Vector3 c = run.square.Square.center;
                Vector3 fwd = rot * Vector3.forward;
                Vector3 off = c - head.position;
                float err = (off - fwd * Vector3.Dot(off, fwd)).magnitude;
                Assert.That(err, Is.LessThanOrEqualTo(0.001f), $"pose {i}: {err * 1000f:0.000}mm off the head ray");
                Assert.That(Vector3.Dot(off, fwd), Is.GreaterThan(0f));
            }
        }

        [Test]
        public void CatchRay_GazeAloneNeverCaptures()
        {
            var run = new Run();
            for (int i = 0; i < 30; i++)
            {
                var head = new Pose(Head.position, Quaternion.Euler(0f, i * 12f, 0f));
                run.Idle(1f, head);
                run.For(0.5f, _ => (default, default), head);
            }
            Assert.That(run.shown, Is.Empty);
            Assert.That(run.caught, Is.Empty);
        }
    }

    public class CatchPinchTests
    {
        static CatchSettings Settings(float still = 3f)
        {
            var s = CatchSettings.Defaults();
            s.stillRecordSeconds = still;
            return s;
        }

        [Test]
        public void Pinch_BothHandsShowSquareWithin300ms()
        {
            var run = new Run().Idle(1f);
            float start = run.t;
            run.BothPinch(0.5f);
            Assert.That(run.shown.Count, Is.EqualTo(1));
            Assert.That(run.shown[0] - start, Is.LessThanOrEqualTo(0.3f));
            Assert.That(run.square.Square.size, Is.EqualTo(0.16f).Within(0.002f));
        }

        [Test]
        public void Pinch_ReleaseRecordsThenCatchesOnceAfterSettingsTime()
        {
            var run = new Run(Settings(1.5f)).Idle(0.5f).BothPinch(0.5f);
            float released = run.t;
            run.Idle(3f);
            Assert.That(run.recordStarted.Count, Is.EqualTo(1));
            Assert.That(run.caught.Count, Is.EqualTo(1));
            Assert.That(run.caught[0].t - released, Is.EqualTo(1.5f).Within(2 * Dt));
            Assert.That(run.caught[0].mode, Is.EqualTo(CatchMode.Still));
            Assert.That(run.caught[0].square.size, Is.EqualTo(0.16f).Within(0.002f));
            Assert.That(run.square.Phase, Is.EqualTo(CatchPhase.Idle));
        }

        [Test]
        public void Pinch_SquareKeepsSizeAfterRelease()
        {
            var run = new Run().Idle(0.5f).BothPinch(0.5f).Idle(1f);
            Assert.That(run.square.Phase, Is.EqualTo(CatchPhase.Recording));
            Assert.That(run.square.Square.size, Is.EqualTo(0.16f).Within(0.002f));
            Assert.That(run.square.Progress, Is.GreaterThan(0.2f).And.LessThan(0.5f));
        }

        [Test]
        public void Pinch_TrackingLossCancelsWithoutCapture()
        {
            var run = new Run(Settings(2f)).Idle(0.5f).BothPinch(0.5f).Idle(0.5f);
            run.For(0.6f, _ => (default, default)).Idle(3f);
            Assert.That(run.cancelled.Count, Is.EqualTo(1));
            Assert.That(run.caught, Is.Empty);
        }

        [Test]
        public void Pinch_ShortTrackingBlipStillCaptures()
        {
            var run = new Run(Settings(2f)).Idle(0.5f).BothPinch(0.5f).Idle(0.5f);
            run.For(0.1f, _ => (default, default)).Idle(2f);
            Assert.That(run.cancelled, Is.Empty);
            Assert.That(run.caught.Count, Is.EqualTo(1));
        }

        static IEnumerable<(string name, Action<Run> act)> NearMisses()
        {
            Vector3 L = InFront(Head, -0.08f, -0.08f, 0.4f), R = InFront(Head, 0.08f, 0.08f, 0.4f);
            yield return ("left only", r => r.For(1f, _ => (Hand(L, true), Hand(R, false))));
            yield return ("right only", r => r.For(1f, _ => (Hand(L, false), Hand(R, true))));
            yield return ("both almost pinching", r => r.For(1f, _ => (Hand(L, false, gap: 0.025f), Hand(R, false, gap: 0.025f))));
            yield return ("hands clasped", r => r.For(1f, _ => (Hand(InFront(Head, -0.01f, 0f, 0.35f), true), Hand(InFront(Head, 0.01f, 0.01f, 0.35f), true))));
            yield return ("pinching behind head", r => r.For(1f, _ => (Hand(InFront(Head, -0.08f, -0.08f, -0.2f), true), Hand(InFront(Head, 0.08f, 0.08f, -0.2f), true))));
            yield return ("pinching out of reach", r => r.For(1f, _ => (Hand(InFront(Head, -0.08f, -0.08f, 1.5f), true), Hand(InFront(Head, 0.08f, 0.08f, 1.5f), true))));
            yield return ("one untracked", r => r.For(1f, _ => (Hand(L, true), Hand(R, true, tracked: false))));
            yield return ("one-frame flicker", r => { r.For(Dt, _ => (Hand(L, true), Hand(R, true))); r.Idle(1f); });
            yield return ("both untracked", r => r.For(1f, _ => (Hand(L, true, tracked: false), Hand(R, true, tracked: false))));
            yield return ("alternating pinches", r => { r.For(0.3f, _ => (Hand(L, true), Hand(R, false))); r.For(0.3f, _ => (Hand(L, false), Hand(R, true))); r.Idle(0.5f); });
        }

        [Test]
        public void Pinch_NearMissFixturesFalsePositiveAtMostOneInTen()
        {
            var fired = new List<string>();
            var all = NearMisses().ToList();
            Assert.That(all.Count, Is.EqualTo(10));
            foreach (var (name, act) in all)
            {
                var run = new Run().Idle(0.5f);
                act(run);
                if (run.shown.Count > 0) fired.Add(name);
            }
            Assert.That(fired.Count, Is.LessThanOrEqualTo(1), "false positives: " + string.Join(", ", fired));
        }
    }

    public class CatchToggleTests
    {
        [Test]
        public void Toggle_SingleTapSwitchesModeWithin300ms()
        {
            var run = new Run().Idle(0.5f).TapLeft(0.15f);
            float released = run.t;
            run.Idle(0.5f);
            Assert.That(run.toggles.Count, Is.EqualTo(1));
            Assert.That(run.toggles[0].mode, Is.EqualTo(CatchMode.Loop));
            Assert.That(run.toggles[0].t - released, Is.LessThanOrEqualTo(0.3f));
            Assert.That(run.square.Duration, Is.EqualTo(CatchSettings.Defaults().loopRecordSeconds));
            run.TapLeft(0.15f).Idle(0.5f);
            Assert.That(run.square.Mode, Is.EqualTo(CatchMode.Still));
        }

        static IEnumerable<(string name, Action<Run> act)> FalseToggles()
        {
            Vector3 L = InFront(Head, -0.15f, -0.25f, 0.3f), R = InFront(Head, 0.15f, -0.25f, 0.3f);
            yield return ("two-hand sizing + release", r => r.BothPinch(0.4f).Idle(0.5f));
            yield return ("left palm faces head", r => r.TapLeft(0.15f, FacingHead(L, Head)).Idle(0.5f));
            yield return ("long hold", r => r.TapLeft(1.0f).Idle(0.5f));
            yield return ("tap while recording", r => { r.BothPinch(0.4f).Idle(0.3f); r.TapLeft(0.15f).Idle(0.3f); });
            yield return ("right taps while left holds", r => r.For(1.2f, t => (Hand(InFront(Head, -0.02f, 0f, 0.3f), true), Hand(InFront(Head, 0.0f, 0.0f, 0.3f), t > 0.4f && t < 0.6f))).Idle(0.5f));
            yield return ("flicker", r => { r.For(Dt, _ => (Hand(L, true), Hand(R, false))); r.Idle(0.5f); });
            yield return ("untracked pinch", r => r.For(0.15f, _ => (Hand(L, true, tracked: false), Hand(R, false))).Idle(0.5f));
            yield return ("almost pinch", r => r.For(0.15f, _ => (Hand(L, false, gap: 0.025f), Hand(R, false))).Idle(0.5f));
            yield return ("right palm faces head", r => r.For(0.15f, _ => (Hand(L, false), Hand(R, true, palmNormal: FacingHead(R, Head)))).Idle(0.5f));
            yield return ("both tap together", r => r.For(0.15f, _ => (Hand(InFront(Head, -0.01f, 0f, 0.3f), true), Hand(InFront(Head, 0.01f, 0f, 0.3f), true))).Idle(0.5f));
        }

        [Test]
        public void Toggle_FalseFixturesAtMostOneInTen()
        {
            var fired = new List<string>();
            var all = FalseToggles().ToList();
            Assert.That(all.Count, Is.EqualTo(10));
            foreach (var (name, act) in all)
            {
                var run = new Run().Idle(0.5f);
                act(run);
                if (run.toggles.Count > 0) fired.Add(name);
            }
            Assert.That(fired.Count, Is.LessThanOrEqualTo(1), "false toggles: " + string.Join(", ", fired));
        }
    }
}
