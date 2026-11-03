using System;
using UnityEngine;

namespace MoodPicker.Hands
{
    public struct PinchEvent
    {
        public Handedness hand;
        public float t;
        public Vector3 point;
    }

    /// 엄지-검지 거리 히스테리시스로 핀치 시작·끝. 추적이 끊기면 진행 중인 핀치는 끝으로 친다.
    public class PinchDetector
    {
        public float startDistance = 0.015f;
        public float endDistance = 0.030f;
        public float minHoldSeconds = 0.05f;

        public event Action<PinchEvent> PinchStarted;
        public event Action<PinchEvent> PinchEnded;

        readonly bool[] _pinching = new bool[2];
        readonly float[] _belowSince = { -1f, -1f };

        public bool IsPinching(Handedness h) => _pinching[(int)h];

        public void Update(HandFrame f)
        {
            Step(Handedness.Left, f.left, f.t);
            Step(Handedness.Right, f.right, f.t);
        }

        void Step(Handedness h, HandPose p, float t)
        {
            int i = (int)h;
            if (!p.tracked)
            {
                _belowSince[i] = -1f;
                if (_pinching[i]) End(h, p, t);
                return;
            }
            float d = p.PinchDistance;
            if (!_pinching[i])
            {
                if (d < startDistance)
                {
                    if (_belowSince[i] < 0f) _belowSince[i] = t;
                    if (t - _belowSince[i] >= minHoldSeconds)
                    {
                        _pinching[i] = true;
                        PinchStarted?.Invoke(new PinchEvent { hand = h, t = t, point = p.PinchPoint });
                    }
                }
                else _belowSince[i] = -1f;
            }
            else if (d > endDistance) End(h, p, t);
        }

        void End(Handedness h, HandPose p, float t)
        {
            int i = (int)h;
            _pinching[i] = false;
            _belowSince[i] = -1f;
            PinchEnded?.Invoke(new PinchEvent { hand = h, t = t, point = p.PinchPoint });
        }
    }
}
