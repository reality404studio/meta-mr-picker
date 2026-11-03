using System;
using System.Collections.Generic;
using MoodPicker.Hands;
using UnityEngine;

namespace MoodPicker.DeskKeyboard
{
    public enum RectReject { None, NotTracked, TooEarly, Tilted, TooNarrow, TooWide, Moving }

    public class RectCalibrator
    {
        public float holdSeconds = 1.0f;
        public float stillRadius = 0.010f;
        public float maxHeightSpread = 0.03f;
        public float minWidth = 0.06f;
        public float maxWidth = 0.70f;
        public float ignoreFirstSeconds = 1.0f;

        readonly Queue<(float t, Vector3[] c)> _window = new Queue<(float, Vector3[])>();
        float? _t0;

        public bool Done { get; private set; }
        public DeskPlane Plane { get; private set; }
        public Vector3[] Corners { get; private set; } = new Vector3[4];
        public RectReject State { get; private set; } = RectReject.NotTracked;
        public float Progress { get; private set; }

        public event Action<DeskPlane, Vector3[]> Completed;

        public void Reset() { Done = false; _window.Clear(); Progress = 0f; }

        public static Vector3[] CornersOf(HandFrame f) =>
            new[] { f.left.indexTip, f.left.thumbTip, f.right.thumbTip, f.right.indexTip };

        public void Update(HandFrame f)
        {
            if (Done) return;
            if (!_t0.HasValue) _t0 = f.t;
            State = Evaluate(f, out Vector3[] c);
            Corners = c;
            if (State != RectReject.None) { _window.Clear(); Progress = 0f; return; }

            _window.Enqueue((f.t, c));
            while (f.t - _window.Peek().t > holdSeconds) _window.Dequeue();

            var mean = Mean();
            Vector3 meanCenter = Center(mean);
            foreach (var s in _window)
                if (Vector3.Distance(Center(s.c), meanCenter) > stillRadius)
                {
                    State = RectReject.Moving;
                    while (_window.Count > 1) _window.Dequeue();
                    Progress = 0f;
                    return;
                }

            Progress = Mathf.Clamp01((f.t - _window.Peek().t) / holdSeconds);
            if (f.t - _window.Peek().t < holdSeconds - 0.02f) return;

            Vector3 center = (mean[0] + mean[1] + mean[2] + mean[3]) * 0.25f;
            Vector3 leftMid = (mean[0] + mean[1]) * 0.5f, rightMid = (mean[2] + mean[3]) * 0.5f;
            Plane = DeskPlane.HorizontalAt(center, rightMid - leftMid);
            Corners = mean;
            Done = true;
            Progress = 1f;
            Completed?.Invoke(Plane, mean);
        }

        RectReject Evaluate(HandFrame f, out Vector3[] c)
        {
            c = CornersOf(f);
            if (!f.left.tracked || !f.right.tracked) return RectReject.NotTracked;
            if (f.t - _t0.Value < ignoreFirstSeconds) return RectReject.TooEarly;
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var p in c) { lo = Mathf.Min(lo, p.y); hi = Mathf.Max(hi, p.y); }
            if (hi - lo > maxHeightSpread) return RectReject.Tilted;
            Vector3 d = (c[2] + c[3]) * 0.5f - (c[0] + c[1]) * 0.5f;
            float w = new Vector2(d.x, d.z).magnitude;
            if (w < minWidth) return RectReject.TooNarrow;
            if (w > maxWidth) return RectReject.TooWide;
            return RectReject.None;
        }

        static Vector3 Center(Vector3[] c) => (c[0] + c[1] + c[2] + c[3]) * 0.25f;

        Vector3[] Mean()
        {
            var m = new Vector3[4];
            foreach (var s in _window) for (int i = 0; i < 4; i++) m[i] += s.c[i];
            for (int i = 0; i < 4; i++) m[i] /= _window.Count;
            return m;
        }
    }
}
