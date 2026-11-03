using System;
using MoodPicker.Hands;
using UnityEngine;

namespace MoodPicker.DeskKeyboard
{
    public enum Finger { LeftIndex, LeftMiddle, LeftRing, RightIndex, RightMiddle, RightRing }

    public struct TouchEvent
    {
        public Finger finger;
        public float t;
        public Vector3 local;
        public float height;
        public float speed;
        public bool late;
    }

    public class TouchDetector
    {
        public DeskPlane plane;
        public float enterHeight = 0.005f;
        public float exitHeight = 0.012f;
        public float armHeight = 0.03f;
        public float minDownSpeed = 0.15f;
        public float minContactSpeed = 0.06f;
        public float predictLead = 0.04f;
        public float velocitySmoothing = 0.5f;

        public event Action<TouchEvent> Predicted;
        public event Action<TouchEvent> Contact;
        public event Action<TouchEvent> Released;
        public event Action<TouchEvent> Rested;

        const int N = 6;
        readonly float[] _h = new float[N], _v = new float[N], _t = new float[N];
        readonly bool[] _has = new bool[N], _down = new bool[N], _predicted = new bool[N];

        public TouchDetector(DeskPlane plane) { this.plane = plane; }

        public bool IsDown(Finger f) => _down[(int)f];

        public void Update(HandFrame f)
        {
            Step(Finger.LeftIndex, f.left.tracked, f.left.indexTip, f.t);
            Step(Finger.LeftMiddle, f.left.tracked, f.left.middleTip, f.t);
            Step(Finger.LeftRing, f.left.tracked, f.left.ringTip, f.t);
            Step(Finger.RightIndex, f.right.tracked, f.right.indexTip, f.t);
            Step(Finger.RightMiddle, f.right.tracked, f.right.middleTip, f.t);
            Step(Finger.RightRing, f.right.tracked, f.right.ringTip, f.t);
        }

        void Step(Finger finger, bool tracked, Vector3 p, float t)
        {
            int i = (int)finger;
            if (!tracked)
            {
                _has[i] = false;
                _predicted[i] = false;
                if (_down[i]) { _down[i] = false; Released?.Invoke(Make(finger, t, p, _h[i], 0f, false)); }
                return;
            }
            float h = plane.Height(p);
            if (_has[i])
            {
                float dt = Mathf.Max(t - _t[i], 1e-4f);
                float raw = (h - _h[i]) / dt;
                _v[i] = Mathf.Lerp(raw, _v[i], velocitySmoothing);
            }
            else _v[i] = 0f;
            _h[i] = h; _t[i] = t; _has[i] = true;

            if (_down[i])
            {
                if (h > exitHeight) { _down[i] = false; _predicted[i] = false; Released?.Invoke(Make(finger, t, p, h, _v[i], false)); }
                return;
            }

            float down = -_v[i];
            if (!_predicted[i] && h < armHeight && down > minDownSpeed && (h - enterHeight) / down <= predictLead)
            {
                _predicted[i] = true;
                Predicted?.Invoke(Make(finger, t, p, h, down, false));
            }
            if (h < enterHeight)
            {
                if (!_predicted[i] && down < minContactSpeed)
                {
                    _down[i] = true;
                    Rested?.Invoke(Make(finger, t, p, h, down, false));
                    return;
                }
                bool late = !_predicted[i];
                if (late) Predicted?.Invoke(Make(finger, t, p, h, down, true));
                _down[i] = true;
                Contact?.Invoke(Make(finger, t, p, h, down, late));
            }
            else if (_predicted[i] && h > armHeight) _predicted[i] = false;
        }

        TouchEvent Make(Finger f, float t, Vector3 p, float h, float speed, bool late) =>
            new TouchEvent { finger = f, t = t, local = plane.ToLocal(p), height = h, speed = speed, late = late };
    }
}
