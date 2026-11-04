using System;
using System.Collections.Generic;
using System.Linq;
using MoodPicker.Catch;
using MoodPicker.Hands;
using MoodPicker.Portal;
using NUnit.Framework;
using UnityEngine;
using static MoodPicker.Tests.CatchHandFixtures;

namespace MoodPicker.Tests
{
    public class CatchPunchHoleTests
    {
        [Test]
        public void Hole_StaysLevelUnderHeadRollAndSitsOnHeadRay()
        {
            var s = CatchSettings.Defaults();
            var rnd = new System.Random(3);
            for (int i = 0; i < 24; i++)
            {
                var rot = Quaternion.Euler((float)(rnd.NextDouble() * 60 - 30), (float)(rnd.NextDouble() * 360), (float)(rnd.NextDouble() * 80 - 40));
                var head = new Pose(new Vector3(0f, 1.2f + (float)rnd.NextDouble() * 0.4f, 0f), rot);
                var run = new Run().Idle(0.2f, head);
                CatchSquareView h = run.punch.Hole;
                Vector3[] c = h.Corners();
                Assert.That(c[1].y - c[0].y, Is.EqualTo(0f).Within(1e-4f), $"pose {i}: 아래 변 수평");
                Assert.That(c[2].y - c[3].y, Is.EqualTo(0f).Within(1e-4f), $"pose {i}: 위 변 수평");
                Assert.That(h.size, Is.EqualTo(s.punchSize));
                Vector3 fwd = rot * Vector3.forward, off = h.center - head.position;
                Assert.That((off - fwd * Vector3.Dot(off, fwd)).magnitude, Is.LessThanOrEqualTo(0.001f), $"pose {i}: 머리 레이 위");
                Assert.That(Vector3.Dot(off, fwd), Is.EqualTo(s.punchDistance).Within(0.001f), $"pose {i}: 고정 거리");
            }
        }

        [Test]
        public void Hole_GazeAloneNeverCaptures()
        {
            var run = new Run();
            for (int i = 0; i < 30; i++)
            {
                var head = new Pose(Head.position, Quaternion.Euler(0f, i * 12f, i % 5 * 6f));
                run.Idle(1f, head);
                run.For(0.5f, _ => (default, default), head);
            }
            Assert.That(run.caught, Is.Empty);
            Assert.That(run.toggles, Is.Empty);
        }
    }

    public class CatchPunchDragTests
    {
        [Test]
        public void Drag_LeftPinchMovesHoleByHandDeltaAndStopsOnRelease()
        {
            var run = new Run().Idle(0.3f);
            Vector3 before = run.punch.Hole.center;
            run.LeftDrag(new Vector3(0.10f, 0f, 0f), 0.6f);
            Assert.That(run.punch.Offset.x, Is.EqualTo(0.10f).Within(0.005f));
            Assert.That(run.punch.Offset.y, Is.EqualTo(0f).Within(0.005f));
            Assert.That((run.punch.Hole.center - before).x, Is.EqualTo(0.10f).Within(0.005f));
            Vector2 held = run.punch.Offset;
            Vector3 p = Run.LeftRest(Head) + new Vector3(0.10f, 0f, 0f);
            run.For(0.5f, x => (Hand(p + new Vector3(0f, 0.1f * (x % 1f), 0f), false), Hand(Run.RightRest(Head), false)));
            Assert.That(run.punch.Offset.magnitude, Is.LessThan(0.005f), "놓으면 시선 중심으로 돌아감 (L2)");
            Assert.That(run.caught, Is.Empty);
            Assert.That(run.toggles, Is.Empty);
        }

        [Test]
        public void Drag_FollowsHoleAxesWhenHeadTurns()
        {
            var head = new Pose(Head.position, Quaternion.Euler(10f, 90f, 25f));
            Quaternion lvl = CatchPunch.LevelRotation(head);
            var run = new Run().Idle(0.3f, head).LeftDrag(lvl * new Vector3(0f, 0.08f, 0f), 0.6f, head);
            Assert.That(run.punch.Offset.y, Is.EqualTo(0.08f).Within(0.005f));
            Assert.That(run.punch.Offset.x, Is.EqualTo(0f).Within(0.005f));
        }

        [Test]
        public void Drag_IgnoredWhilePortalOpenOrPalmFacesHead()
        {
            var portal = new PalettePortal(PortalSettings.Defaults());
            var run = new Run { gate = portal };
            run.before = f => portal.Update(f);
            Vector3 palm = Run.LeftRest(Head);
            var leftUp = Hand(palm, false, palmNormal: Vector3.up);
            run.For(0.4f, _ => (leftUp, Hand(Run.RightRest(Head), false)));
            Assume.That(portal.IsOpen);
            run.LeftDrag(new Vector3(0.10f, 0f, 0f), 0.6f, palmNormal: Vector3.up);
            Assert.That(run.punch.Offset, Is.EqualTo(Vector2.zero), "포탈 열린 동안 드래그 무시");

            var r2 = new Run().Idle(0.3f).LeftDrag(new Vector3(0.10f, 0f, 0f), 0.6f, palmNormal: FacingHead(Run.LeftRest(Head), Head));
            Assert.That(r2.punch.Offset, Is.EqualTo(Vector2.zero), "손바닥 얼굴 쪽 핀치(시스템 메뉴)는 무시");
        }

        [Test]
        public void Drag_OffsetIsClamped()
        {
            var run = new Run().Idle(0.3f).LeftDrag(new Vector3(0.5f, 0f, 0f), 0.6f);
            float lim = CatchSettings.Defaults().maxOffset;
            Assert.That(Mathf.Abs(run.punch.Offset.x), Is.LessThanOrEqualTo(lim + 1e-4f));
            Assert.That(Mathf.Abs(run.punch.Offset.y), Is.LessThanOrEqualTo(lim + 1e-4f));
            Assert.That(lim, Is.EqualTo(0.1f).Within(1e-4f), "L2 한도 ±10cm");
        }
    }

    public class CatchPunchL2Tests
    {
        [Test]
        public void L2_LeftPinchMoveFollowsImmediatelyWithinTenCentimetres()
        {
            var run = new Run().Idle(0.3f);
            Vector3 start = Run.LeftRest(Head);
            run.For(0.1f, _ => (Hand(start, true), Hand(Run.RightRest(Head), false)));
            float moveT = run.t;
            float reached = -1f;
            Vector3 moved = start + new Vector3(0.05f, 0f, 0f);
            for (int i = 0; i < 30; i++)
            {
                run.For(Dt, _ => (Hand(moved, true), Hand(Run.RightRest(Head), false)));
                if (reached < 0f && Mathf.Abs(run.punch.Offset.x - 0.05f) < 0.0025f) reached = run.t;
            }
            Assert.That(reached, Is.GreaterThan(0f), "5cm 이동 → 오프셋 5cm");
            Assert.That(reached - moveT, Is.LessThanOrEqualTo(0.1f), "반응 지연 ≤ 0.1초");
            Assert.That(run.punch.Offset.y, Is.EqualTo(0f).Within(0.003f), "같은 방향");
            run.For(0.3f, _ => (Hand(start + new Vector3(0.3f, -0.3f, 0f), true), Hand(Run.RightRest(Head), false)));
            Assert.That(run.punch.Offset.x, Is.EqualTo(0.1f).Within(1e-4f), "한도 +10cm");
            Assert.That(run.punch.Offset.y, Is.EqualTo(-0.1f).Within(1e-4f), "한도 -10cm");
        }

        [Test]
        public void L2_ReleaseReturnsToGazeCenterWithin300ms()
        {
            var run = new Run().Idle(0.3f).LeftDrag(new Vector3(0.08f, 0.05f, 0f), 0.4f);
            Assume.That(run.punch.Offset.magnitude, Is.GreaterThan(0.08f));
            run.Idle(0.3f);
            Vector3 gazeCenter = CatchPunch.PlaceHole(Head, CatchSettings.Defaults().punchDistance, 0.1f, Vector2.zero).center;
            Assert.That((run.punch.Hole.center - gazeCenter).magnitude, Is.LessThan(0.005f), "0.3초 안에 시선 중심 복귀");
        }

        [Test]
        public void L2_HeadJitterShakesHoleLessThanRawGaze()
        {
            var s = CatchSettings.Defaults();
            var run = new Run();
            var rnd = new System.Random(5);
            var smooth = new List<Vector3>();
            var raw = new List<Vector3>();
            for (int i = 0; i < 288; i++)
            {
                var jitter = Quaternion.Euler((float)(rnd.NextDouble() * 3 - 1.5), (float)(rnd.NextDouble() * 3 - 1.5), 0f);
                var head = new Pose(Head.position, jitter);
                run.Idle(Dt, head);
                if (i < 36) continue;
                smooth.Add(run.punch.Hole.center);
                raw.Add(CatchPunch.PlaceHole(head, s.punchDistance, s.punchSize, Vector2.zero).center);
            }
            float Shake(List<Vector3> v) { float sum = 0f; for (int i = 1; i < v.Count; i++) sum += (v[i] - v[i - 1]).magnitude; return sum / (v.Count - 1); }
            float sm = Shake(smooth), rw = Shake(raw);
            Assert.That(sm, Is.LessThan(rw * 0.6f), $"프레임당 흔들림 {sm * 1000f:0.00}mm vs 기존 {rw * 1000f:0.00}mm");
        }
    }

    public class PunchLeverSizeTests
    {
        static readonly CatchSettings S = CatchSettings.Defaults();

        static HandPose OpenHand(float scale)
        {
            Vector3 palm = Run.RightRest(Head) + new Vector3(0f, -0.02f, -0.05f);
            return new HandPose
            {
                tracked = true,
                palm = palm,
                palmNormal = Vector3.down,
                indexTip = palm + new Vector3(-0.02f, 0.03f, 0.10f) * scale,
                thumbTip = palm + new Vector3(-0.04f, -0.01f, 0.055f) * scale,
                middleTip = palm + new Vector3(0f, 0.02f, 0.105f) * scale,
                ringTip = palm + new Vector3(0.015f, 0.015f, 0.095f) * scale,
            };
        }

        [TestCase(0.8f)]
        [TestCase(1.0f)]
        [TestCase(1.25f)]
        public void Lever_CoversThumbTipToIndexTip(float scale)
        {
            HandPose h = OpenHand(scale);
            foreach (float progress in new[] { 0f, 0.5f, 1f })
            {
                LeverPose lp = PunchCue.Lever(h, Head, progress, S);
                float cov = PunchCue.Coverage(lp, h.thumbTip, h.indexTip);
                Assert.That(cov, Is.GreaterThanOrEqualTo(0.9f), $"손 {scale} 진행 {progress}: 덮음 {cov:P0}");
            }
            HandPose pinched = h;
            pinched.thumbTip = pinched.indexTip;
            float lenOpen = Vector3.Distance(PunchCue.Lever(h, Head, 0f, S).hinge, PunchCue.Lever(h, Head, 0f, S).armTip);
            float lenPinch = Vector3.Distance(PunchCue.Lever(pinched, Head, 0f, S).hinge, PunchCue.Lever(pinched, Head, 0f, S).armTip);
            Assert.That(lenPinch, Is.GreaterThanOrEqualTo(lenOpen * 0.85f), "핀치로 손가락이 모여도 레버가 줄어들지 않음");
        }

        [Test]
        public void Lever_GrowsWithHand()
        {
            float small = Vector3.Distance(PunchCue.Lever(OpenHand(0.8f), Head, 0f, S).hinge, PunchCue.Lever(OpenHand(0.8f), Head, 0f, S).armTip);
            float large = Vector3.Distance(PunchCue.Lever(OpenHand(1.25f), Head, 0f, S).hinge, PunchCue.Lever(OpenHand(1.25f), Head, 0f, S).armTip);
            Assert.That(large, Is.GreaterThan(small));
            Assert.That(small, Is.GreaterThanOrEqualTo(S.leverLength - 1e-4f));
        }
    }

    public class CatchPunchHoldTests
    {
        [Test]
        public void Hold_ThreeSecondsSnapsOnceWithVisibleProgress()
        {
            var run = new Run().Idle(0.5f);
            float start = run.t;
            run.RightPinch(1.5f);
            Assert.That(run.punch.HoldProgress, Is.InRange(0.4f, 0.6f), "유지 진행이 보임");
            run.RightPinch(3.5f);
            Assert.That(run.caught.Count, Is.EqualTo(1));
            Assert.That(run.caught[0].t - start, Is.EqualTo(3f).Within(0.1f));
            Assert.That(run.caught[0].square.size, Is.EqualTo(CatchSettings.Defaults().punchSize));
            Assert.That(run.toggles, Is.Empty);
        }

        [Test]
        public void Hold_ReleaseAt2_9sSavesNothing()
        {
            var run = new Run().Idle(0.5f).RightPinch(2.9f).Idle(2f);
            Assert.That(run.caught, Is.Empty);
            Assert.That(run.toggles, Is.Empty);
            Assert.That(run.punch.HoldProgress, Is.EqualTo(0f));
        }

        [Test]
        public void Hold_TapOnlyToggles()
        {
            var run = new Run().Idle(0.5f).RightPinch(0.15f).Idle(0.5f);
            Assert.That(run.caught, Is.Empty);
            Assert.That(run.toggles.Count, Is.EqualTo(1));
            Assert.That(run.punch.Mode, Is.EqualTo(CatchMode.Loop));
            run.RightPinch(0.15f).Idle(0.5f);
            Assert.That(run.punch.Mode, Is.EqualTo(CatchMode.Still));
        }

        static IEnumerable<(string name, Action<Run> act)> FalseSnaps()
        {
            yield return ("left hold 4s", r => r.LeftDrag(Vector3.zero, 4f).Idle(0.3f));
            yield return ("right taps x5", r => { for (int i = 0; i < 5; i++) r.RightPinch(0.2f).Idle(0.3f); });
            yield return ("right hold 2.9s", r => r.RightPinch(2.9f).Idle(0.5f));
            yield return ("right flicker", r => { r.RightPinch(Dt); r.Idle(0.5f); });
            yield return ("right palm faces head 4s", r => r.RightPinch(4f, FacingHead(Run.RightRest(Head), Head)).Idle(0.3f));
            yield return ("right untracked 4s", r => r.RightPinch(4f, tracked: false).Idle(0.3f));
            yield return ("right almost pinch 4s", r => r.RightPinch(4f, gap: 0.025f).Idle(0.3f));
            yield return ("tracking loss mid-hold", r => { r.RightPinch(1.5f); r.For(0.1f, _ => (default, default)); r.RightPinch(2f).Idle(0.3f); });
            yield return ("hold 1s release", r => r.RightPinch(1f).Idle(0.5f));
            yield return ("portal open hold 4s", r =>
            {
                var portal = new PalettePortal(PortalSettings.Defaults());
                r.gate = portal;
                r.before = f => portal.Update(f);
                r.For(4.5f, _ => (Hand(Run.LeftRest(Head), false, palmNormal: Vector3.up), Hand(Run.RightRest(Head), true)));
            });
        }

        [Test]
        public void Hold_FalseFixturesSnapAtMostOneInTen()
        {
            var fired = new List<string>();
            var all = FalseSnaps().ToList();
            Assert.That(all.Count, Is.EqualTo(10));
            foreach (var (name, act) in all)
            {
                var run = new Run().Idle(0.5f);
                act(run);
                if (run.caught.Count > 0) fired.Add(name);
            }
            Assert.That(fired.Count, Is.LessThanOrEqualTo(1), "false snaps: " + string.Join(", ", fired));
        }

        [Test]
        public void Hold_LongPressAndLeftTapDoNotToggle()
        {
            var run = new Run().Idle(0.5f).RightPinch(1f).Idle(0.5f).LeftDrag(Vector3.zero, 0.1f).Idle(0.5f);
            Assert.That(run.toggles, Is.Empty);
        }

        [TestCase(0f, 0f)]
        [TestCase(0.08f, -0.05f)]
        public void Hold_CropRectMatchesHoleWithinFivePercent(float ox, float oy)
        {
            var s = CatchSettings.Defaults();
            var res = new Vector2Int(1280, 960);
            const float f = 870f;
            Vector3 lens = Head.position + new Vector3(0.032f, -0.02f, 0.07f);
            Vector2 Cam(Vector3 w) { Vector3 p = w - lens; return new Vector2((p.x / p.z * f + res.x * 0.5f) / res.x, (p.y / p.z * f + res.y * 0.5f) / res.y); }

            var hole = CatchPunch.PlaceHole(Head, s.punchDistance, s.punchSize, new Vector2(ox, oy));
            var pts = CatchProjection.ThroughSquare(Head.position, hole.Corners(), s.sceneDistance).Select(Cam).ToArray();
            Assert.That(CropMath.TrySquare(pts, res, s.overlapFrac, s.minCropPixels, out CropRect c));
            float eye = s.punchSize / s.punchDistance * f;
            Assert.That(c.side, Is.EqualTo(eye).Within(eye * 0.05f));
            Vector2 centerEye = new Vector2(res.x * 0.5f + ox / s.punchDistance * f, res.y * 0.5f + oy / s.punchDistance * f);
            Assert.That(c.Still.center.x, Is.EqualTo(centerEye.x).Within(eye * 0.05f));
            Assert.That(c.Still.center.y, Is.EqualTo(centerEye.y).Within(eye * 0.05f));
        }
    }
}
