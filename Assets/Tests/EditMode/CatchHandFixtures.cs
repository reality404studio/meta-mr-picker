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
            public readonly CatchSquare square;
            public readonly List<float> shown = new List<float>();
            public readonly List<float> recordStarted = new List<float>();
            public readonly List<CatchShot> caught = new List<CatchShot>();
            public readonly List<string> cancelled = new List<string>();
            public readonly List<(CatchMode mode, float t)> toggles = new List<(CatchMode, float)>();
            public float t;

            public Run(CatchSettings s = null)
            {
                square = new CatchSquare(s ?? CatchSettings.Defaults());
                square.SquareShown += x => shown.Add(x);
                square.RecordStarted += x => recordStarted.Add(x);
                square.Caught += x => caught.Add(x);
                square.Cancelled += x => cancelled.Add(x);
                square.ModeChanged += (m, x) => toggles.Add((m, x));
            }

            public Run For(float seconds, Func<float, (HandPose l, HandPose r)> hands, Pose? head = null)
            {
                float end = t + seconds;
                while (t < end - 1e-6f)
                {
                    var (l, r) = hands(t);
                    square.Update(new HandFrame { t = t, left = l, right = r }, head ?? Head);
                    t += Dt;
                }
                return this;
            }

            public Run Idle(float seconds, Pose? head = null)
            {
                Pose h = head ?? Head;
                return For(seconds, _ => (Hand(InFront(h, -0.15f, -0.25f, 0.3f), false), Hand(InFront(h, 0.15f, -0.25f, 0.3f), false)), head);
            }

            public Run BothPinch(float seconds, Pose? head = null, float half = 0.08f, float z = 0.4f)
            {
                Pose h = head ?? Head;
                return For(seconds, _ => (Hand(InFront(h, -half, -half, z), true), Hand(InFront(h, half, half, z), true)), head);
            }

            public Run TapLeft(float hold, Vector3? palmNormal = null)
            {
                Vector3 p = InFront(Head, -0.15f, -0.25f, 0.3f);
                return For(hold, _ => (Hand(p, true, palmNormal: palmNormal), Hand(InFront(Head, 0.15f, -0.25f, 0.3f), false)));
            }
        }
    }
}
