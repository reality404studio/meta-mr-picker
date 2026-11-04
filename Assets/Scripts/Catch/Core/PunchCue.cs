using System.Collections.Generic;
using MoodPicker.Hands;
using UnityEngine;

namespace MoodPicker.Catch
{
    public enum CueLine { Hidden = 0, Dotted = 1, Solid = 2 }

    public struct LeverPose
    {
        public Vector3 anchor;
        public Vector3 hinge;
        public Vector3 armTip;
        public Vector3 right;
        public Vector3 up;
        public float progress;
        public float angleDegrees;
        public Vector3[] plate;
    }

    public struct PunchCueState
    {
        public CueLine leader;
        public Vector3 leaderFrom;
        public Vector3 leaderTo;
        public CueLine lever;
        public LeverPose leverPose;
        public CueLine blade;
        public float bladeProgress;
        public Vector3 bladeA;
        public Vector3 bladeB;
    }

    public static class PunchCue
    {
        public static int LeaderCorner => 0;

        public static PunchCueState Compute(CatchPunch punch, HandFrame gated, Pose head, CatchSettings s)
        {
            var st = new PunchCueState();
            CatchSquareView hole = punch.Hole;
            Vector3[] c = hole.Corners();

            HandPose l = gated.left;
            bool leftReady = l.tracked && !punch.PalmFacesHead(l);
            st.leader = punch.Dragging ? CueLine.Solid : leftReady ? CueLine.Dotted : CueLine.Hidden;
            if (st.leader != CueLine.Hidden)
            {
                st.leaderFrom = l.PinchPoint;
                st.leaderTo = c[LeaderCorner];
            }

            HandPose r = gated.right;
            bool rightReady = r.tracked && !punch.PalmFacesHead(r);
            st.lever = punch.Holding ? CueLine.Solid : rightReady ? CueLine.Dotted : CueLine.Hidden;
            float p = punch.HoldProgress;
            if (st.lever != CueLine.Hidden) st.leverPose = Lever(r.PinchPoint, head, p, s);

            st.blade = st.lever;
            st.bladeProgress = p;
            float h = hole.size * 0.5f;
            Vector3 y = hole.Up * (h - p * hole.size);
            st.bladeA = hole.center + y - hole.Right * h;
            st.bladeB = hole.center + y + hole.Right * h;
            return st;
        }

        public static LeverPose Lever(Vector3 anchor, Pose head, float progress, CatchSettings s)
        {
            Vector3 view = anchor - head.position;
            if (view.sqrMagnitude < 1e-6f) view = head.rotation * Vector3.forward;
            Quaternion q = Quaternion.LookRotation(view, Vector3.up);
            Vector3 right = q * Vector3.right, up = q * Vector3.up;
            float half = s.leverLength * 0.5f, ph = s.leverPlateHeight * 0.5f;
            Vector3 plateTop = anchor - up * s.leverPinchClearance;
            Vector3 plateMid = plateTop - up * ph;
            var plate = new[]
            {
                plateMid - right * half - up * ph,
                plateMid + right * half - up * ph,
                plateMid + right * half + up * ph,
                plateMid - right * half + up * ph,
            };
            Vector3 hinge = plateTop + right * half + up * s.leverHingeGap;
            float angle = s.leverOpenDegrees * (1f - Mathf.Clamp01(progress));
            float a = angle * Mathf.Deg2Rad;
            Vector3 tip = hinge + (-right * Mathf.Cos(a) + up * Mathf.Sin(a)) * s.leverLength;
            return new LeverPose { anchor = anchor, hinge = hinge, armTip = tip, right = right, up = up, progress = Mathf.Clamp01(progress), angleDegrees = angle, plate = plate };
        }

        public static void Dash(Vector3 a, Vector3 b, float dash, float gap, List<(Vector3, Vector3)> into)
        {
            float len = Vector3.Distance(a, b);
            if (len < 1e-5f) return;
            Vector3 d = (b - a) / len;
            float step = Mathf.Max(1e-4f, dash + gap);
            for (float x = 0f; x < len; x += step) into.Add((a + d * x, a + d * Mathf.Min(len, x + dash)));
        }

        public static void Segments(Vector3 a, Vector3 b, CueLine style, CatchSettings s, List<(Vector3, Vector3)> into)
        {
            if (style == CueLine.Solid) into.Add((a, b));
            else if (style == CueLine.Dotted) Dash(a, b, s.cueDashLength, s.cueDashGap, into);
        }

        public static void Polygon(Vector3[] pts, CueLine style, CatchSettings s, List<(Vector3, Vector3)> into)
        {
            for (int i = 0; i < pts.Length; i++) Segments(pts[i], pts[(i + 1) % pts.Length], style, s, into);
        }
    }
}
