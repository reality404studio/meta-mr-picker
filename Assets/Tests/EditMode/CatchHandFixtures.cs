using System;
using System.Collections.Generic;
using MoodPicker.Catch;
using MoodPicker.Hands;
using UnityEngine;

namespace MoodPicker.Tests
{
    public static class CatchHandFixtures
    {
        public const float Dt = 1f / 72f;
        public static readonly Pose Head = new Pose(new Vector3(0f, 1.2f, 0f), Quaternion.identity);

        public static HandPose Hand(Vector3 p, bool pinch, bool tracked = true, float gap = -1f, Vector3? palmNormal = null)
        {
            float g = gap >= 0f ? gap : (pinch ? 0.005f : 0.06f);
            return new HandPose
            {
                tracked = tracked,
                thumbTip = p + new Vector3(0f, -g * 0.5f, 0f),
                indexTip = p + new Vector3(0f, g * 0.5f, 0f),
                middleTip = p + new Vector3(0f, 0.03f, 0.02f),
                ringTip = p + new Vector3(0f, 0.03f, 0.01f),
                palm = p + new Vector3(0f, -0.02f, -0.05f),
                palmNormal = palmNormal ?? Vector3.down,
            };
        }

        public static Vector3 InFront(Pose head, float x, float y, float z) => head.position + head.rotation * new Vector3(x, y, z);

        public static Vector3 FacingHead(Vector3 p, Pose head) => (head.position - p).normalized;

        public class Run
        {
            public readonly CatchPunch punch;
            public readonly List<CatchShot> caught = new List<CatchShot>();
            public readonly List<(CatchMode mode, float t)> toggles = new List<(CatchMode, float)>();
            public readonly List<float> holdStarts = new List<float>();
            public IPinchGate gate;
            public Action<HandFrame> before;
            public float t;

            public Run(CatchSettings s = null)
            {
                punch = new CatchPunch(s ?? CatchSettings.Defaults());
                punch.Caught += x => caught.Add(x);
                punch.ModeChanged += (m, x) => toggles.Add((m, x));
                punch.HoldStarted += x => holdStarts.Add(x);
            }

            public Run For(float seconds, Func<float, (HandPose l, HandPose r)> hands, Pose? head = null)
            {
                float end = t + seconds;
                while (t < end - 1e-6f)
                {
                    var (l, r) = hands(t);
                    var f = new HandFrame { t = t, left = l, right = r };
                    before?.Invoke(f);
                    punch.Update(PinchGate.Apply(f, gate), head ?? Head);
                    t += Dt;
                }
                return this;
            }

            public static Vector3 LeftRest(Pose h) => InFront(h, -0.15f, -0.3f, 0.3f);
            public static Vector3 RightRest(Pose h) => InFront(h, 0.15f, -0.3f, 0.3f);

            public Run Idle(float seconds, Pose? head = null)
            {
                Pose h = head ?? Head;
                return For(seconds, _ => (Hand(LeftRest(h), false), Hand(RightRest(h), false)), head);
            }

            public Run RightPinch(float seconds, Vector3? palmNormal = null, bool tracked = true, float gap = -1f)
            {
                return For(seconds, _ => (Hand(LeftRest(Head), false), Hand(RightRest(Head), true, tracked, gap, palmNormal)));
            }

            public Run LeftDrag(Vector3 delta, float seconds, Pose? head = null, Vector3? palmNormal = null)
            {
                Pose h = head ?? Head;
                Vector3 start = LeftRest(h);
                For(0.1f, _ => (Hand(start, true, palmNormal: palmNormal), Hand(RightRest(h), false)), head);
                float t1 = t;
                return For(seconds, x => (Hand(start + delta * Mathf.Clamp01((x - t1) / (seconds - Dt)), true, palmNormal: palmNormal), Hand(RightRest(h), false)), head);
            }
        }
    }
}
