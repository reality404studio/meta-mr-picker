using System;
using System.Collections.Generic;
using System.Linq;
using MoodPicker.DeskKeyboard;
using MoodPicker.Hands;
using NUnit.Framework;
using UnityEngine;

namespace MoodPicker.Tests
{
    public class DeskKeyboardTests
    {
        const float Fps = 72f;
        const float Frame = 1f / Fps;
        static readonly Quaternion Tilt = Quaternion.Euler(2f, 0f, 1f);
        static readonly Vector3 DeskOrigin = new Vector3(-0.2f, 0.75f, 0.35f);

        static Vector3 OnDesk(float x, float h, float z) => DeskOrigin + Tilt * new Vector3(x, h, z);

        static HandPose Hand(Vector3 index, Vector3 middle, Vector3 ring) =>
            new HandPose { tracked = true, thumbTip = index + Vector3.left * 0.03f, indexTip = index, middleTip = middle, ringTip = ring };

        static HandPose Rest(float x) => Hand(OnDesk(x, 0.05f, 0.1f), OnDesk(x + 0.02f, 0.05f, 0.1f), OnDesk(x + 0.04f, 0.05f, 0.1f));

        static HandPose RectHand(Vector3 index, Vector3 thumb) =>
            new HandPose { tracked = true, indexTip = index, thumbTip = thumb, middleTip = index, ringTip = index };

        static List<HandFrame> RectFrames(float rightLift = 0f, bool rightTracked = true)
        {
            var frames = new List<HandFrame>();
            var rng = new System.Random(11);
            float t = 0f;
            for (int k = 0; k < (int)(1.2f * Fps); k++, t += Frame)
            {
                float s = Mathf.Sin(k * 0.3f) * 0.05f;
                frames.Add(new HandFrame { t = t, left = RectHand(new Vector3(-0.2f, 0.85f + s, 0.4f), new Vector3(-0.15f, 0.85f, 0.35f)),
                                                  right = RectHand(new Vector3(0.2f, 0.9f, 0.4f + s), new Vector3(0.15f, 0.88f, 0.35f)) });
            }
            for (int k = 0; k < (int)(1.4f * Fps); k++, t += Frame)
            {
                Vector3 J() => new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 0.003f;
                var right = RectHand(new Vector3(0.15f, 0.75f + rightLift, 0.40f) + J(), new Vector3(0.10f, 0.75f + rightLift, 0.32f) + J());
                right.tracked = rightTracked;
                frames.Add(new HandFrame { t = t, left = RectHand(new Vector3(-0.15f, 0.75f, 0.40f) + J(), new Vector3(-0.10f, 0.75f, 0.32f) + J()), right = right });
            }
            return frames;
        }

        static RectCalibrator RunRect(List<HandFrame> frames)
        {
            var cal = new RectCalibrator();
            var input = new RecordedHandInput(new HandTrajectory { frames = frames });
            while (input.TryGetFrame(out var f)) cal.Update(f);
            return cal;
        }

        static float TapHeight(float t, float start, float speed)
        {
            float top = 0.04f, fall = top / speed, hold = 0.08f;
            float u = t - start;
            if (u < 0f) return top;
            if (u < fall) return top - speed * u;
            if (u < fall + hold) return 0f;
            if (u < fall + hold + fall) return speed * (u - fall - hold);
            return top;
        }

        static float Hover(float t, float start)
        {
            float u = t - start;
            if (u < 0f || u > 0.5f) return 0.04f;
            return 0.015f + 0.025f * Mathf.Abs(u - 0.25f) / 0.25f;
        }

        static List<HandFrame> TapFrames(DeskPlane plane)
        {
            var frames = new List<HandFrame>();
            for (int k = 0; k < (int)(4f * Fps); k++)
            {
                float t = 10f + k * Frame;
                float ri = Mathf.Min(TapHeight(t, 10.2f, 0.3f), Mathf.Min(TapHeight(t, 11.0f, 0.3f), TapHeight(t, 11.8f, 0.3f)));
                float rm = Hover(t, 12.6f);
                float lm = TapHeight(t, 13.0f, 0.2f);
                Vector3 P(float x, float h) => plane.ToWorld(new Vector3(x, h, 0.15f));
                frames.Add(new HandFrame
                {
                    t = t,
                    right = Hand(P(0.25f, ri), P(0.27f, rm), P(0.29f, 0.04f)),
                    left = Hand(P(0.10f, 0.04f), P(0.12f, lm), P(0.14f, 0.04f)),
                });
            }
            return frames;
        }

        static DeskPlane Calibrate() => DeskPlane.FromThreePoints(OnDesk(0f, 0f, 0f), OnDesk(0.4f, 0f, 0f), OnDesk(0.2f, 0f, 0.3f));

        [Test]
        public void RectCalibration_SetsHorizontalPlaneAtRectangleHeight()
        {
            var cal = RunRect(RectFrames());
            Assert.That(cal.Done, "사각형을 1초 유지하면 끝나야 함 (state=" + cal.State + ")");
            Assert.That(cal.Plane.origin.y, Is.EqualTo(0.75f).Within(0.002f));
            Assert.That(cal.Plane.up, Is.EqualTo(Vector3.up));
            Assert.That(Vector3.Angle(cal.Plane.right, Vector3.right), Is.LessThan(1f));
            Assert.That(cal.Plane.origin.x, Is.EqualTo(0f).Within(0.003f));
        }

        [Test]
        public void RectCalibration_RejectsTiltedFrame()
        {
            var cal = RunRect(RectFrames(rightLift: 0.04f));
            Assert.That(cal.Done, Is.False);
            Assert.That(cal.State, Is.EqualTo(RectReject.Tilted));
        }

        [Test]
        public void RectCalibration_NeedsBothHands()
        {
            var cal = RunRect(RectFrames(rightTracked: false));
            Assert.That(cal.Done, Is.False);
            Assert.That(cal.State, Is.EqualTo(RectReject.NotTracked));
        }

        [Test]
        public void Taps_ContactCountsPerFinger_HoverDoesNotTouch()
        {
            DeskPlane plane = Calibrate();
            var det = new TouchDetector(plane);
            var contacts = new List<TouchEvent>();
            var releases = new List<TouchEvent>();
            det.Contact += contacts.Add;
            det.Released += releases.Add;
            var input = new RecordedHandInput(new HandTrajectory { frames = TapFrames(plane) });
            while (input.TryGetFrame(out var f)) det.Update(f);

            Assert.That(contacts.Count(e => e.finger == Finger.RightIndex), Is.EqualTo(3));
            Assert.That(contacts.Count(e => e.finger == Finger.LeftMiddle), Is.EqualTo(1));
            Assert.That(contacts.Count(e => e.finger == Finger.RightMiddle), Is.EqualTo(0), "15mm 호버는 닿음이 아님");
            Assert.That(contacts.Count, Is.EqualTo(4));
            Assert.That(releases.Count, Is.EqualTo(4));
        }

        [TestCase(0.04f)]
        [TestCase(0.06f)]
        public void Taps_PredictedFiresLeadBeforeContact(float lead)
        {
            DeskPlane plane = Calibrate();
            var det = new TouchDetector(plane) { predictLead = lead };
            var predicted = new Dictionary<Finger, float>();
            var leads = new List<float>();
            det.Predicted += e => { if (!e.late) predicted[e.finger] = e.t; };
            det.Contact += e =>
            {
                Assert.That(e.late, Is.False, e.finger + " 예측 없이 닿음");
                leads.Add(e.t - predicted[e.finger]);
            };
            var input = new RecordedHandInput(new HandTrajectory { frames = TapFrames(plane) });
            while (input.TryGetFrame(out var f)) det.Update(f);

            Assert.That(leads.Count, Is.EqualTo(4));
            foreach (float l in leads) Assert.That(l, Is.EqualTo(lead).Within(Frame + 0.002f), "lead=" + l);
        }

        static List<HandFrame> PinchDrag(Vector3 at, Vector3 delta)
        {
            var frames = new List<HandFrame>();
            HandPose L(Vector3 c, float gap) => new HandPose { tracked = true, thumbTip = c - Vector3.right * gap * 0.5f, indexTip = c + Vector3.right * gap * 0.5f };
            var rest = new HandPose { tracked = true, thumbTip = new Vector3(0.3f, 1f, 0.3f), indexTip = new Vector3(0.36f, 1f, 0.3f) };
            float t = 0f;
            for (int k = 0; k < 10; k++, t += Frame) frames.Add(new HandFrame { t = t, left = L(at, 0.05f), right = rest });
            for (int k = 0; k < 10; k++, t += Frame) frames.Add(new HandFrame { t = t, left = L(at, 0.005f), right = rest });
            for (int k = 1; k <= 36; k++, t += Frame) frames.Add(new HandFrame { t = t, left = L(at + delta * (k / 36f), 0.005f), right = rest });
            for (int k = 0; k < 10; k++, t += Frame) frames.Add(new HandFrame { t = t, left = L(at + delta, 0.05f), right = rest });
            return frames;
        }

        [Test]
        public void Grab_LeftPinchOnHandle_MovesKeyboardByDragDelta()
        {
            var origin = new Vector3(0f, 0.75f, 0.4f);
            var offset = new Vector3(-0.25f, 0.01f, 0f);
            var grab = new KeyboardGrab(origin, offset);
            int released = 0;
            grab.Released += _ => released++;
            var delta = new Vector3(0.05f, -0.02f, 0.03f);
            foreach (var f in PinchDrag(origin + offset, delta)) grab.Update(f);
            Assert.That(released, Is.EqualTo(1));
            Assert.That(grab.Grabbing, Is.False);
            Assert.That(Vector3.Distance(grab.Origin, origin + delta), Is.LessThan(0.003f), grab.Origin.ToString("F4"));
        }

        [Test]
        public void Grab_PinchAwayFromHandle_DoesNothing()
        {
            var origin = new Vector3(0f, 0.75f, 0.4f);
            var grab = new KeyboardGrab(origin, new Vector3(-0.25f, 0.01f, 0f));
            foreach (var f in PinchDrag(origin + new Vector3(0.1f, 0.05f, 0f), new Vector3(0.05f, 0f, 0f))) grab.Update(f);
            Assert.That(grab.Origin, Is.EqualTo(origin));
        }

        [Test]
        public void SlowLanding_IsRestNotTap()
        {
            DeskPlane plane = Calibrate();
            var det = new TouchDetector(plane);
            int contacts = 0, predicted = 0, rested = 0;
            det.Contact += _ => contacts++;
            det.Predicted += _ => predicted++;
            det.Rested += _ => rested++;
            for (int k = 0; k < (int)(2f * Fps); k++)
            {
                float t = k * Frame;
                float h = Mathf.Max(0f, 0.04f - 0.03f * t);
                Vector3 tip = plane.ToWorld(new Vector3(0.2f, h, 0.1f));
                Vector3 up = plane.ToWorld(new Vector3(0.3f, 0.05f, 0.1f));
                det.Update(new HandFrame { t = t, right = Hand(tip, up, up), left = Hand(up, up, up) });
            }
            Assert.That(contacts, Is.EqualTo(0), "초당 3cm 로 내려앉는 손가락은 탭이 아님");
            Assert.That(predicted, Is.EqualTo(0));
            Assert.That(rested, Is.EqualTo(1));
        }
    }
}
