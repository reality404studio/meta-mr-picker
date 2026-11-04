using System;
using MoodPicker.Hands;
using UnityEngine;

namespace MoodPicker.Catch
{
    public struct CatchShot
    {
        public CatchMode mode;
        public float t;
        public float holdSeconds;
        public CatchSquareView square;
        public Pose head;
    }

    public class CatchPunch
    {
        readonly CatchSettings _s;
        readonly PinchDetector _pinch = new PinchDetector();

        public CatchMode Mode { get; private set; } = CatchMode.Still;
        public Vector2 Offset { get; private set; }
        public CatchSquareView Hole { get; private set; }
        public bool Dragging { get; private set; }
        public bool Holding { get; private set; }
        public float HoldProgress => Holding ? Mathf.Clamp01((_t - _holdStart) / _s.holdSeconds) : 0f;

        public event Action<CatchShot> Caught;
        public event Action<CatchMode, float> ModeChanged;
        public event Action<float> HoldStarted;
        public event Action<float, float> HoldCancelled;

        Pose _head;
        HandFrame _frame;
        float _t, _holdStart;
        bool _holdFired;
        Vector3 _dragLast;

        public CatchPunch(CatchSettings settings)
        {
            _s = settings;
            _pinch.PinchStarted += OnPinchStarted;
            _pinch.PinchEnded += OnPinchEnded;
        }

        public static Quaternion LevelRotation(Pose head)
        {
            Vector3 fwd = head.rotation * Vector3.forward;
            if (Mathf.Abs(Vector3.Dot(fwd, Vector3.up)) > 0.999f) fwd = Vector3.ProjectOnPlane(head.rotation * Vector3.up, Vector3.up);
            return Quaternion.LookRotation(fwd, Vector3.up);
        }

        public static CatchSquareView PlaceHole(Pose head, float distance, float size, Vector2 offset)
        {
            Quaternion rot = LevelRotation(head);
            Vector3 c = CatchRay.Center(head, distance) + rot * new Vector3(offset.x, offset.y, 0f);
            return new CatchSquareView { center = c, rotation = rot, size = size };
        }

        public void Update(HandFrame f, Pose head)
        {
            _frame = f;
            _head = head;
            _t = f.t;
            _pinch.Update(f);

            if (Dragging)
            {
                if (!f.left.tracked || !_pinch.IsPinching(Handedness.Left)) Dragging = false;
                else
                {
                    Vector3 p = f.left.PinchPoint;
                    Vector3 d = p - _dragLast;
                    _dragLast = p;
                    Quaternion rot = LevelRotation(head);
                    Vector2 step = new Vector2(Vector3.Dot(d, rot * Vector3.right), Vector3.Dot(d, rot * Vector3.up)) * _s.dragGain;
                    Offset = Vector2.ClampMagnitude(Offset + step, _s.maxOffset);
                }
            }

            Hole = PlaceHole(head, _s.punchDistance, _s.punchSize, Offset);

            if (Holding && !_holdFired && f.t - _holdStart >= _s.holdSeconds)
            {
                _holdFired = true;
                Caught?.Invoke(new CatchShot { mode = Mode, t = f.t, holdSeconds = f.t - _holdStart, square = Hole, head = head });
            }
        }

        void OnPinchStarted(PinchEvent e)
        {
            HandPose p = _frame[e.hand];
            if (PalmFacesHead(p)) return;
            if (e.hand == Handedness.Left)
            {
                Dragging = true;
                _dragLast = p.PinchPoint;
                return;
            }
            Holding = true;
            _holdFired = false;
            _holdStart = e.t;
            HoldStarted?.Invoke(e.t);
        }

        void OnPinchEnded(PinchEvent e)
        {
            if (e.hand == Handedness.Left) { Dragging = false; return; }
            if (!Holding) return;
            Holding = false;
            float held = e.t - _holdStart;
            if (_holdFired) return;
            HoldCancelled?.Invoke(held, e.t);
            if (held <= _s.toggleTapMaxSeconds && !_pinch.IsPinching(Handedness.Left))
            {
                Mode = Mode == CatchMode.Still ? CatchMode.Loop : CatchMode.Still;
                ModeChanged?.Invoke(Mode, e.t);
            }
        }

        bool PalmFacesHead(HandPose p)
        {
            Vector3 toHead = _head.position - p.palm;
            if (toHead.sqrMagnitude < 1e-6f || p.palmNormal.sqrMagnitude < 1e-6f) return false;
            return Vector3.Dot(p.palmNormal.normalized, toHead.normalized) > _s.palmFacingHeadDot;
        }

        public void ResetOffset() => Offset = Vector2.zero;
    }
}
