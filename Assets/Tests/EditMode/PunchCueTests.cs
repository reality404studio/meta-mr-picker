using System.Collections.Generic;
using MoodPicker.Catch;
using MoodPicker.Hands;
using MoodPicker.Portal;
using NUnit.Framework;
using UnityEngine;
using static MoodPicker.Tests.CatchHandFixtures;

namespace MoodPicker.Tests
{
    public class PunchCueTests
    {
        static readonly CatchSettings S = CatchSettings.Defaults();

        class CueRun
        {
            public readonly Run run = new Run(S);
            HandFrame _last;
            public CueRun() { run.before = f => _last = PinchGate.Apply(f, run.gate); }
            public PunchCueState Cue => PunchCue.Compute(run.punch, _last, Head, S);
            public HandFrame Last => _last;
        }

        [Test]
        public void Rest_LeaderLeverAndBladeAreDotted()
        {
            var c = new CueRun();
            c.run.Idle(0.5f);
            PunchCueState st = c.Cue;
            Assert.That(st.leader, Is.EqualTo(CueLine.Dotted));
            Assert.That(st.lever, Is.EqualTo(CueLine.Dotted));
            Assert.That(st.blade, Is.EqualTo(CueLine.Dotted));
            Assert.That(st.bladeProgress, Is.EqualTo(0f));
            Assert.That(st.leaderFrom, Is.EqualTo(c.Last.left.PinchPoint));
            Assert.That(st.leaderTo, Is.EqualTo(c.run.punch.Hole.Corners()[PunchCue.LeaderCorner]));
            Assert.That(st.leverPose.angleDegrees, Is.EqualTo(S.leverOpenDegrees).Within(1e-3f));
            var segs = new List<(Vector3, Vector3)>();
            PunchCue.Segments(st.leaderFrom, st.leaderTo, st.leader, S, segs);
            Assert.That(segs.Count, Is.GreaterThan(5), "점선 = 여러 조각");
        }

        [Test]
        public void LeftPinch_LeaderGoesSolidAndDragsPunchLikeRigidRod()
        {
            var c = new CueRun();
            c.run.Idle(0.3f);
            Vector3 delta = new Vector3(0.08f, 0.04f, 0f);
            Vector3 start = Run.LeftRest(Head);
            c.run.For(0.1f, _ => (Hand(start, true), Hand(Run.RightRest(Head), false)));
            PunchCueState a = c.Cue;
            Assert.That(a.leader, Is.EqualTo(CueLine.Solid));
            Vector3 rodA = a.leaderTo - a.leaderFrom;
            float t1 = c.run.t;
            c.run.For(0.6f, x => (Hand(start + delta * Mathf.Clamp01((x - t1) / (0.6f - Dt)), true), Hand(Run.RightRest(Head), false)));
            PunchCueState b = c.Cue;
            Assert.That(b.leader, Is.EqualTo(CueLine.Solid));
            Assert.That(Vector3.Distance(b.leaderFrom, c.Last.left.PinchPoint), Is.LessThan(1e-4f), "손끝에 붙음");
            Assert.That(Vector3.Distance(b.leaderTo, c.run.punch.Hole.Corners()[PunchCue.LeaderCorner]), Is.LessThan(1e-4f), "펀치 모서리에 붙음");
            Assert.That(Vector3.Distance(b.leaderFrom, a.leaderFrom), Is.EqualTo(delta.magnitude).Within(0.005f), "손이 움직임");
            Assert.That(Vector3.Distance(b.leaderTo - b.leaderFrom, rodA), Is.LessThan(0.005f), "막대처럼 길이·방향 유지");
            c.run.Idle(0.3f);
            Assert.That(c.Cue.leader, Is.EqualTo(CueLine.Dotted), "놓으면 다시 점선");
        }

        [TestCase(0.6f)]
        [TestCase(1.5f)]
        [TestCase(2.4f)]
        public void RightHold_LeverAndBladeTrackHoldOverThreeSeconds(float t)
        {
            var c = new CueRun();
            c.run.Idle(0.5f);
            c.run.RightPinch(0.1f);
            Assume.That(c.run.punch.Holding);
            float held0 = c.run.t - c.run.holdStarts[0];
            c.run.RightPinch(t - held0);
            float held = c.run.t - Dt - c.run.holdStarts[0];
            float want = held / S.holdSeconds;
            Assert.That(held, Is.EqualTo(t).Within(0.03f));
            PunchCueState st = c.Cue;
            Assert.That(st.lever, Is.EqualTo(CueLine.Solid));
            Assert.That(st.blade, Is.EqualTo(CueLine.Solid));
            Assert.That(st.leverPose.progress, Is.EqualTo(want).Within(0.05f));
            Assert.That(st.leverPose.angleDegrees, Is.EqualTo(S.leverOpenDegrees * (1f - want)).Within(S.leverOpenDegrees * 0.05f));
            CatchSquareView hole = c.run.punch.Hole;
            float top = hole.size * 0.5f;
            float bladeY = Vector3.Dot(st.bladeA - hole.center, hole.Up);
            Assert.That((top - bladeY) / hole.size, Is.EqualTo(want).Within(0.05f), "칼날 하강 = 유지 진행");
            Assert.That(c.run.caught, Is.Empty);
        }

        [Test]
        public void RightHold_FiresAtThreeSecondsWithBladeAtBottom()
        {
            var c = new CueRun();
            c.run.Idle(0.5f).RightPinch(3.3f);
            Assert.That(c.run.caught.Count, Is.EqualTo(1));
            PunchCueState st = c.Cue;
            Assert.That(st.leverPose.progress, Is.EqualTo(1f));
            Assert.That(st.leverPose.angleDegrees, Is.EqualTo(0f).Within(1e-3f));
            CatchSquareView hole = c.run.punch.Hole;
            Assert.That(Vector3.Dot(st.bladeA - hole.center, hole.Up), Is.EqualTo(-hole.size * 0.5f).Within(1e-4f));
        }

        [Test]
        public void Cue_HiddenForSystemGestureUntrackedOrPortalOpen()
        {
            var c = new CueRun();
            Vector3 l = Run.LeftRest(Head), r = Run.RightRest(Head);
            c.run.For(0.3f, _ => (Hand(l, false, palmNormal: FacingHead(l, Head)), Hand(r, false, tracked: false)));
            Assert.That(c.Cue.leader, Is.EqualTo(CueLine.Hidden));
            Assert.That(c.Cue.lever, Is.EqualTo(CueLine.Hidden));
            Assert.That(c.Cue.blade, Is.EqualTo(CueLine.Hidden));

            var portal = new PalettePortal(PortalSettings.Defaults());
            var p = new CueRun();
            p.run.gate = portal;
            var before = p.run.before;
            p.run.before = f => { portal.Update(f); before(f); };
            p.run.For(0.5f, _ => (Hand(l, false, palmNormal: Vector3.up), Hand(r, true)));
            Assume.That(portal.IsOpen);
            Assert.That(p.Cue.leader, Is.EqualTo(CueLine.Hidden));
            Assert.That(p.Cue.lever, Is.EqualTo(CueLine.Hidden));
        }

        [Test]
        public void Lever_IsLegibleAtArmLength()
        {
            HandPose hand = Hand(Run.RightRest(Head), false);
            Vector3 anchor = hand.palm;
            LeverPose lp = PunchCue.Lever(hand, Head, 0f, S);
            float dist = Vector3.Distance(anchor, Head.position);
            float len = Vector3.Distance(lp.armTip, lp.hinge);
            float armDeg = 2f * Mathf.Atan(len * 0.5f / dist) * Mathf.Rad2Deg;
            float plateDeg = 2f * Mathf.Atan(S.leverPlateHeight * 0.5f / dist) * Mathf.Rad2Deg;
            Assert.That(armDeg, Is.GreaterThanOrEqualTo(8f), "레버 길이 시야각 ≥ 8°");
            Assert.That(plateDeg, Is.GreaterThanOrEqualTo(1.5f), "받침 두께 시야각 ≥ 1.5°");
            Assert.That(len, Is.GreaterThanOrEqualTo(S.leverLength - 1e-4f), "레버 길이 하한");
            float open = Vector3.Distance(lp.armTip, lp.plate[2]);
            Assert.That(open, Is.GreaterThan(0.02f), "열린 레버 끝이 받침에서 2cm 넘게 떨어져 보임");
        }
    }
}
