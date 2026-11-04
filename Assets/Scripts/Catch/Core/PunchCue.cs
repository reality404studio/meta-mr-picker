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
            if (st.lever != CueLine.Hidden) st.leverPose = Lever(r, head, p, s);

            st.blade = st.lever;
            st.bladeProgress = p;
            float h = hole.size * 0.5f;
            Vector3 y = hole.Up * (h - p * hole.size);
            st.bladeA = hole.center + y - hole.Right * h;
            st.bladeB = hole.center + y + hole.Right * h;
            return st;
        }

        public static LeverPose Lever(HandPose hand, Pose head, float progress, CatchSettings s)
        {
            Vector3 anchor = hand.palm;
            Vector3 view = anchor - head.position;
            if (view.sqrMagnitude < 1e-6f) view = head.rotation * Vector3.forward;
            Vector3 viewDir = view.normalized;
            Vector3 u = Vector3.ProjectOnPlane(hand.indexTip - hand.palm, viewDir);
            if (u.sqrMagnitude < 1e-6f) u = Quaternion.LookRotation(viewDir, Vector3.up) * Vector3.right;
            u.Normalize();
            Vector3 up = Vector3.Cross(viewDir, u).normalized;
            if (Vector3.Dot(up, Vector3.up) < 0f) up = -up;

            float reach = Mathf.Max(Vector3.Dot(hand.indexTip - anchor, u), Vector3.Dot(hand.thumbTip - anchor, u));
            float back = Mathf.Max(0f, -Mathf.Min(Vector3.Dot(hand.thumbTip - anchor, u), 0f));
            Vector3 start = anchor - u * (back + s.leverTipMargin);
            float length = Mathf.Max(s.leverLength, reach + back + 2f * s.leverTipMargin);
            float ph = s.leverPlateHeight * 0.5f;
            Vector3 plateMid = start + u * (length * 0.5f) - up * (s.leverPinchClearance + ph);
            float half = length * 0.5f;
            var plate = new[]
            {
                plateMid - u * half - up * ph,
                plateMid + u * half - up * ph,
                plateMid + u * half + up * ph,
                plateMid - u * half + up * ph,
            };
            Vector3 hinge = plate[3] + up * s.leverHingeGap;
            float angle = s.leverOpenDegrees * (1f - Mathf.Clamp01(progress));
            float a = angle * Mathf.Deg2Rad;
            Vector3 tip = hinge + (u * Mathf.Cos(a) + up * Mathf.Sin(a)) * length;
            return new LeverPose { anchor = anchor, hinge = hinge, armTip = tip, right = u, up = up, progress = Mathf.Clamp01(progress), angleDegrees = angle, plate = plate };
        }

        public static float Coverage(LeverPose lp, Vector3 thumbTip, Vector3 indexTip, int samples = 50)
        {
            Vector3 a = lp.plate[0], b = lp.plate[1];
            float len = Vector3.Distance(a, b);
            if (len < 1e-6f) return 0f;
            Vector3 u = (b - a) / len;
            int inside = 0;
            for (int i = 0; i <= samples; i++)
            {
                Vector3 p = Vector3.Lerp(thumbTip, indexTip, i / (float)samples);
                float x = Vector3.Dot(p - a, u);
                if (x >= 0f && x <= len) inside++;
            }
            return inside / (float)(samples + 1);
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
