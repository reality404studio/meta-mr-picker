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
    public class PortalRun
    {
        public readonly PalettePortal portal;
        public readonly CatchHandFixtures.Run catchRun;
        public readonly List<float> opened = new List<float>(), closed = new List<float>();
        public readonly List<DipEvent> dips = new List<DipEvent>();
        public float t;

        public static readonly Vector3 Palm = new Vector3(-0.1f, 0.95f, 0.3f);

        public PortalRun(int chips = 6)
        {
            portal = new PalettePortal(PortalSettings.Defaults()) { ChipCount = chips };
            portal.Opened += x => opened.Add(x);
            portal.Closed += x => closed.Add(x);
            portal.Dipped += x => dips.Add(x);
            catchRun = new CatchHandFixtures.Run();
        }

        public static HandPose Left(Vector3 palm, Vector3 normal, bool tracked = true)
        {
            var h = Hand(palm + new Vector3(0f, 0.02f, 0.05f), false, tracked, palmNormal: normal);
            h.palm = palm;
            h.middleTip = palm + new Vector3(0f, 0f, 0.09f);
            return h;
        }

        public static HandPose RightTip(Vector3 tip, bool pinch = false)
        {
            var h = Hand(tip, pinch);
            h.indexTip = tip;
            if (pinch) h.thumbTip = tip + new Vector3(0f, -0.005f, 0f);
            else h.thumbTip = tip + new Vector3(-0.03f, -0.04f, 0f);
            return h;
        }

        public static HandPose RightIdle => RightTip(new Vector3(0.2f, 0.8f, 0.3f));

        public PortalRun For(float seconds, Func<float, (HandPose l, HandPose r)> hands)
        {
            float end = t + seconds;
            while (t < end - 1e-6f)
            {
                var (l, r) = hands(t);
                var f = new HandFrame { t = t, left = l, right = r };
                portal.Update(f);
                catchRun.punch.Update(PinchGate.Apply(f, portal), Head);
                t += Dt;
            }
            catchRun.t = t;
            return this;
        }

        public PortalRun PalmUp(float seconds, Func<float, HandPose> right = null) =>
            For(seconds, x => (Left(Palm, Vector3.up), right != null ? right(x) : RightIdle));

        public PortalRun PalmDown(float seconds) => For(seconds, _ => (Left(Palm, Vector3.down), RightIdle));

        public Vector3 Surface(Vector2 xy, float h) => portal.Pose.ToWorld(xy) + portal.Pose.normal * h;
    }

    public class PortalPalmTests
    {
        [Test]
        public void Portal_SkyPalmOpensWithin300msAboveThePalm()
        {
            var run = new PortalRun().PalmDown(0.5f);
            float start = run.t;
            run.PalmUp(0.5f);
            Assert.That(run.opened.Count, Is.EqualTo(1));
            Assert.That(run.opened[0] - start, Is.LessThanOrEqualTo(0.3f));
            float h = Vector3.Dot(run.portal.Pose.center - PortalRun.Palm, Vector3.up);
            Assert.That(h, Is.EqualTo(0.14f).Within(0.01f), "포탈은 손바닥 위 약 14cm");
        }

        [Test]
        public void Portal_CenterSits14cmAlongTiltedPalmNormal()
        {
            Vector3 n = Quaternion.AngleAxis(20f, Vector3.right) * Vector3.up;
            var run = new PortalRun().PalmDown(0.3f).For(1.0f, _ => (PortalRun.Left(PortalRun.Palm, n), PortalRun.RightIdle));
            Assume.That(run.portal.IsOpen);
            Vector3 d = run.portal.Pose.center - PortalRun.Palm;
            Assert.That(Vector3.Dot(d, n), Is.EqualTo(0.14f).Within(0.01f), "손바닥 법선 방향 0.14m");
            Assert.That(Vector3.ProjectOnPlane(d, n).magnitude, Is.LessThan(0.01f), "법선 위");
        }

        [Test]
        public void Portal_ShippedSettingsAssetUses14cm()
        {
            var s = UnityEditor.AssetDatabase.LoadAssetAtPath<PortalSettings>("Assets/Settings/PortalSettings.asset");
            Assert.That(s, Is.Not.Null);
            Assert.That(s.heightAbovePalm, Is.EqualTo(0.14f).Within(0.01f));
        }

        [Test]
        public void Portal_ClosesOnPalmDownOrTrackingLoss()
        {
            var a = new PortalRun().PalmUp(0.5f).PalmDown(0.6f);
            Assert.That(a.closed.Count, Is.EqualTo(1));
            Assert.That(a.portal.IsOpen, Is.False);
            var b = new PortalRun().PalmUp(0.5f).For(0.8f, _ => (default, PortalRun.RightIdle));
            Assert.That(b.closed.Count, Is.EqualTo(1));
            var c = new PortalRun().PalmUp(0.5f).For(0.2f, _ => (default, PortalRun.RightIdle)).PalmUp(0.5f);
            Assert.That(c.closed, Is.Empty, "짧은 추적 끊김으론 안 닫힘");
        }

        static Vector3 Tilt(float deg) => Quaternion.AngleAxis(deg, Vector3.right) * Vector3.up;

        static IEnumerable<(string name, Action<PortalRun> act)> FalseOpens()
        {
            Vector3 P = PortalRun.Palm;
            Vector3 toFace = (Head.position - P).normalized;
            yield return ("palm faces face", r => r.For(1f, _ => (PortalRun.Left(P, toFace), PortalRun.RightIdle)));
            yield return ("palm down", r => r.PalmDown(1f));
            yield return ("palm sideways", r => r.For(1f, _ => (PortalRun.Left(P, Vector3.right), PortalRun.RightIdle)));
            yield return ("hand shake flip", r => r.For(1.2f, x => (PortalRun.Left(P, ((int)(x / 0.07f)) % 2 == 0 ? Vector3.up : Vector3.down), PortalRun.RightIdle)));
            yield return ("fast swipe palm up", r => r.For(0.6f, x => (PortalRun.Left(P + new Vector3(Mathf.Sin(x * 20f) * 0.15f, 0f, 0f), Vector3.up), PortalRun.RightIdle)));
            yield return ("right palm up only", r => r.For(1f, _ => (PortalRun.Left(P, Vector3.down), new Func<HandPose>(() => { var h = PortalRun.RightIdle; h.palmNormal = Vector3.up; return h; })())));
            yield return ("left untracked", r => r.For(1f, _ => (PortalRun.Left(P, Vector3.up, tracked: false), PortalRun.RightIdle)));
            yield return ("palm up blip", r => { r.For(0.06f, _ => (PortalRun.Left(P, Vector3.up), PortalRun.RightIdle)); r.PalmDown(0.5f); });
            yield return ("tilted 50deg", r => r.For(1f, _ => (PortalRun.Left(P, Tilt(50f)), PortalRun.RightIdle)));
            yield return ("waving palm", r => r.For(1.2f, x => (PortalRun.Left(P + new Vector3(0f, Mathf.Sin(x * 15f) * 0.06f, 0f), Tilt(Mathf.Sin(x * 15f) * 60f)), PortalRun.RightIdle)));
        }

        [Test]
        public void Portal_FaceAndShakeFixturesFalseOpenAtMostOneInTen()
        {
            var fired = new List<string>();
            var all = FalseOpens().ToList();
            Assert.That(all.Count, Is.EqualTo(10));
            foreach (var (name, act) in all)
            {
                var run = new PortalRun().PalmDown(0.3f);
                act(run);
                if (run.opened.Count > 0) fired.Add(name);
            }
            Assert.That(fired.Count, Is.LessThanOrEqualTo(1), "false opens: " + string.Join(", ", fired));
        }
    }

    public class PortalDipTests
    {
        static void Dip(PortalRun run, Vector2 xy, out float exitT)
        {
            run.PalmUp(0.2f, _ => PortalRun.RightTip(run.Surface(xy, 0.04f)));
            run.PalmUp(0.15f, _ => PortalRun.RightTip(run.Surface(xy, -0.02f)));
            exitT = run.t;
            run.PalmUp(0.4f, _ => PortalRun.RightTip(run.Surface(xy, 0.04f)));
        }

        [Test]
        public void Dip_InAndOutPaintsFingertipWithin300ms()
        {
            var run = new PortalRun(6).PalmUp(0.4f);
            Assume.That(run.portal.IsOpen);
            Vector2 slot = run.portal.ChipSlot(4);
            Dip(run, slot, out float exit);
            Assert.That(run.dips.Count, Is.EqualTo(1));
            Assert.That(run.dips[0].chip, Is.EqualTo(4));
            Assert.That(run.dips[0].t - exit, Is.LessThanOrEqualTo(0.3f));
        }

        [Test]
        public void Dip_HoverOrSideEntryDoesNotPaint()
        {
            var run = new PortalRun(6).PalmUp(0.4f);
            Vector2 xy = run.portal.ChipSlot(0);
            run.PalmUp(0.5f, _ => PortalRun.RightTip(run.Surface(xy, 0.004f)));
            run.PalmUp(0.3f, _ => PortalRun.RightTip(run.Surface(xy, 0.04f)));
            Assert.That(run.dips, Is.Empty, "표면 위 호버는 아님");
            run.PalmUp(0.3f, _ => PortalRun.RightTip(run.Surface(new Vector2(0.15f, 0f), -0.03f)));
            run.PalmUp(0.3f, _ => PortalRun.RightTip(run.Surface(xy, -0.03f)));
            run.PalmUp(0.3f, _ => PortalRun.RightTip(run.Surface(xy, 0.04f)));
            Assert.That(run.dips, Is.Empty, "옆에서 밑으로 들어온 건 아님");
        }

        [Test]
        public void Dip_NoChipsNoPaint()
        {
            var run = new PortalRun(0).PalmUp(0.4f);
            Dip(run, Vector2.zero, out _);
            Assert.That(run.dips, Is.Empty);
        }

        [Test]
        public void Gate_PinchesWhilePortalOpenNeverReachCatchOrToggle()
        {
            var run = new PortalRun(6).PalmUp(0.4f);
            Assume.That(run.portal.IsOpen);
            Vector3 L = PortalRun.Palm;
            run.For(0.2f, _ => (PortalRun.Left(L, Vector3.up), PortalRun.RightTip(new Vector3(0.1f, 1.0f, 0.4f), pinch: true)));
            run.PalmUp(0.4f);
            run.For(0.5f, _ =>
            {
                var l = PortalRun.Left(L, Vector3.up);
                l.thumbTip = l.indexTip = InFront(Head, -0.08f, -0.08f, 0.4f);
                return (l, PortalRun.RightTip(InFront(Head, 0.08f, 0.08f, 0.4f), pinch: true));
            });
            run.PalmUp(4f);
            Assert.That(run.catchRun.holdStarts, Is.Empty, "포탈 열린 동안 오른손 핀치 → 찰칵 유지가 시작 안 됨");
            Assert.That(run.catchRun.toggles, Is.Empty, "포탈 열린 동안 한 손 핀치 → 토글 안 됨");
            Assert.That(run.catchRun.caught, Is.Empty);
            Assert.That(run.catchRun.punch.Offset, Is.EqualTo(Vector2.zero), "포탈 열린 동안 왼손 핀치 → 펀치 안 움직임");
        }

        [Test]
        public void Gate_ReleasesAfterCloseAndCooldown()
        {
            var run = new PortalRun(6).PalmUp(0.4f).PalmDown(0.5f);
            Assume.That(run.portal.IsOpen, Is.False);
            run.PalmDown(0.6f);
            run.For(0.5f, _ =>
            {
                var l = PortalRun.Left(PortalRun.Palm, Vector3.down);
                l.thumbTip = l.indexTip = InFront(Head, -0.08f, -0.08f, 0.4f);
                return (l, PortalRun.RightTip(InFront(Head, 0.08f, 0.08f, 0.4f), pinch: true));
            });
            Assert.That(run.catchRun.holdStarts.Count, Is.EqualTo(1), "닫힌 뒤엔 찰칵 유지가 다시 시작됨");
        }
    }
}
