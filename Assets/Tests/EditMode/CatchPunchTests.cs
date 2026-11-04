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
            Assert.That(run.punch.Offset, Is.EqualTo(held), "놓으면 멈춤");
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
            Assert.That(run.punch.Offset.magnitude, Is.LessThanOrEqualTo(CatchSettings.Defaults().maxOffset + 1e-4f));
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
