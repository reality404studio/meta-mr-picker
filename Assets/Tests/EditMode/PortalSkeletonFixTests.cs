using System;
using System.Collections.Generic;
using MoodPicker.Catch;
using MoodPicker.Hands;
using MoodPicker.Portal;
using NUnit.Framework;
using UnityEngine;
using static MoodPicker.Tests.CatchHandFixtures;

namespace MoodPicker.Tests
{
    public class PortalFlickerTests
    {
        static Vector3 Wrist(float deg) => Quaternion.AngleAxis(deg, Vector3.forward) * Vector3.up;

        static HandPose LeftPinching(Vector3 palm, Vector3 normal)
        {
            var h = PortalRun.Left(palm, normal);
            h.thumbTip = h.indexTip = palm + new Vector3(0.02f, 0.03f, 0.06f);
            return h;
        }

        [Test]
        public void Flicker_LeftPinchDragWithWristRollNeverOpens()
        {
            var run = new PortalRun().PalmDown(0.3f);
            run.For(3f, x =>
            {
                Vector3 palm = PortalRun.Palm + new Vector3(0.1f * Mathf.Sin(x * 2f), 0.05f * Mathf.Sin(x * 3f), 0f);
                return (LeftPinching(palm, Wrist(60f * Mathf.Sin(x * 4f))), PortalRun.RightIdle);
            });
            run.For(1.5f, _ => (LeftPinching(PortalRun.Palm, Vector3.up), PortalRun.RightIdle));
            Assert.That(run.opened, Is.Empty, "왼손 핀치 드래그 중엔 열리지 않음");
            Assert.That(run.portal.SuppressesPinch(run.t), Is.False, "드래그 중 왼손 핀치를 게이트가 가리지 않음");
        }

        [Test]
        public void Flicker_OpensOnlyAfterHoldingPalmUpWithoutPinch()
        {
            var shortHold = new PortalRun().PalmDown(0.3f).PalmUp(0.2f).PalmDown(0.5f);
            Assert.That(shortHold.opened, Is.Empty, "0.2초는 안 열림");
            var hold = new PortalRun().PalmDown(0.3f);
            float start = hold.t;
            hold.PalmUp(0.35f);
            Assert.That(hold.opened.Count, Is.EqualTo(1), "0.3초 유지면 열림");
            Assert.That(hold.opened[0] - start, Is.LessThanOrEqualTo(0.3f));
            var afterPinch = new PortalRun().PalmDown(0.3f);
            afterPinch.For(0.5f, _ => (LeftPinching(PortalRun.Palm, Vector3.up), PortalRun.RightIdle));
            float released = afterPinch.t;
            afterPinch.PalmUp(0.2f);
            Assert.That(afterPinch.opened, Is.Empty, "핀치 놓자마자는 안 열림");
            afterPinch.PalmUp(0.6f);
            Assert.That(afterPinch.opened.Count, Is.EqualTo(1));
            Assert.That(afterPinch.opened[0] - released, Is.GreaterThanOrEqualTo(0.27f));
        }

        [TestCase(0.8f)]
        [TestCase(0.5f)]
        public void Flicker_BoundaryAngleOscillationTogglesAtMostOnce(float dot)
        {
            float baseDeg = Mathf.Acos(dot) * Mathf.Rad2Deg;
            var run = new PortalRun().PalmDown(0.3f);
            if (dot < 0.6f) { run.PalmUp(0.5f); Assume.That(run.portal.IsOpen); }
            int before = run.opened.Count + run.closed.Count;
            run.For(4f, x => (PortalRun.Left(PortalRun.Palm, Wrist(baseDeg + 6f * Mathf.Sin(x * 25f))), PortalRun.RightIdle));
            int toggles = run.opened.Count + run.closed.Count - before;
            Assert.That(toggles, Is.LessThanOrEqualTo(1), $"경계 {dot} 왕복에서 깜빡임");
        }
    }

    public class PortalHeightTests
    {
        [Test]
        public void Height_ShippedAssetAndDefaultAre7cm()
        {
            var s = UnityEditor.AssetDatabase.LoadAssetAtPath<PortalSettings>("Assets/Settings/PortalSettings.asset");
            Assert.That(s, Is.Not.Null);
            Assert.That(s.heightAbovePalm, Is.EqualTo(0.07f).Within(0.005f));
            Assert.That(PortalSettings.Defaults().heightAbovePalm, Is.EqualTo(0.07f).Within(0.005f));
            var run = new PortalRun().PalmDown(0.3f).PalmUp(0.5f);
            Assert.That(Vector3.Dot(run.portal.Pose.center - PortalRun.Palm, Vector3.up), Is.EqualTo(0.07f).Within(0.005f));
        }
    }

    public class PortalWheelSizeTests
    {
        static readonly Vector2[] Offsets =
        {
            new Vector2(0.02f, 0f), new Vector2(-0.02f, 0f), new Vector2(0f, 0.02f), new Vector2(0f, -0.019f), new Vector2(0.014f, 0.014f),
            new Vector2(-0.014f, 0.014f), new Vector2(0.014f, -0.013f), new Vector2(-0.013f, -0.013f), new Vector2(0.008f, -0.005f), new Vector2(0f, 0f),
        };

        static void WobblyDip(PortalRun run, Vector2 target, float seed)
        {
            Vector2 side = new Vector2(-target.y, target.x).normalized;
            run.PalmUp(0.25f, x => PortalRun.RightTip(run.Surface(target + side * 0.01f * Mathf.Sin(x * 17f + seed), 0.05f)));
            float t0 = run.t;
            run.PalmUp(0.3f, x => PortalRun.RightTip(run.Surface(target + side * 0.012f * Mathf.Sin(x * 23f + seed), Mathf.Lerp(0.04f, 0.015f, (x - t0) / 0.3f))));
            run.PalmUp(0.12f, x => PortalRun.RightTip(run.Surface(target + side * 0.004f * Mathf.Sin(x * 31f), -0.02f)));
            run.PalmUp(0.4f, _ => PortalRun.RightTip(run.Surface(target, 0.05f)));
        }

        [Test]
        public void Wheel_WobblyFrontDipsPickNineOfTenWithoutSweepOrWash()
        {
            var run = new PortalRun(6).PalmUp(0.4f);
            Assume.That(run.portal.IsOpen);
            var rot = new List<int>();
            var washed = new List<float>();
            run.portal.Rotated += (i, _) => rot.Add(i);
            run.portal.Washed += t => washed.Add(t);
            for (int i = 0; i < Offsets.Length; i++) WobblyDip(run, run.portal.FrontSlot + Offsets[i], i);
            Assert.That(run.dips.Count, Is.GreaterThanOrEqualTo(9), "앞 칸 ±2cm dip 10회 중 9회 이상");
            Assert.That(rot, Is.Empty, "쓸기 오작동 0");
            Assert.That(washed, Is.Empty, "우물 혼동 0");
        }

        [Test]
        public void Wheel_WobblyWellDipsWashWithoutPick()
        {
            var run = new PortalRun(6).PalmUp(0.4f);
            var washed = new List<float>();
            var rot = new List<int>();
            run.portal.Washed += t => washed.Add(t);
            run.portal.Rotated += (i, _) => rot.Add(i);
            for (int i = 0; i < 5; i++) WobblyDip(run, Offsets[i] * 0.4f, i);
            Assert.That(washed.Count, Is.EqualTo(5));
            Assert.That(run.dips, Is.Empty);
            Assert.That(rot, Is.Empty);
        }
    }
}
