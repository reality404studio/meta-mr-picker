using System;
using MoodPicker.Hands;
using UnityEngine;

namespace MoodPicker.Catch
{
    public enum CatchPhase { Idle, Sizing, Recording }

    public struct CatchShot
    {
        public CatchMode mode;
        public float t;
        public float recordSeconds;
        public CatchSquareView square;
        public Pose head;
    }

    public class CatchSquare
    {
        readonly CatchSettings _s;
        readonly PinchDetector _pinch = new PinchDetector();

        public CatchPhase Phase { get; private set; } = CatchPhase.Idle;
        public CatchMode Mode { get; private set; } = CatchMode.Still;
        public float Elapsed { get; private set; }
        public float Duration => _s.RecordSeconds(Mode);
        public float Progress => Phase == CatchPhase.Recording ? Mathf.Clamp01(Elapsed / Duration) : 0f;
        public CatchSquareView Square { get; private set; }

        public event Action<float> SquareShown;
        public event Action<float> RecordStarted;
        public event Action<CatchShot> Caught;
        public event Action<string> Cancelled;
        public event Action<CatchMode, float> ModeChanged;

        float _distance, _size, _recordStart, _lostSince = -1f;
        Pose _head;
        HandFrame _frame;
        int _tapHand = -1;
        float _tapStart;

        public CatchSquare(CatchSettings settings)
        {
            _s = settings;
            _pinch.PinchStarted += OnPinchStarted;
            _pinch.PinchEnded += OnPinchEnded;
        }

        public void Update(HandFrame f, Pose head)
        {
            _frame = f;
            _head = head;
            _pinch.Update(f);
            bool both = _pinch.IsPinching(Handedness.Left) && _pinch.IsPinching(Handedness.Right);
            bool anyPinch = _pinch.IsPinching(Handedness.Left) || _pinch.IsPinching(Handedness.Right);
            bool tracked = f.left.tracked && f.right.tracked;

            switch (Phase)
            {
                case CatchPhase.Idle:
                    if (both && TryMeasure(f, out _distance, out _size))
                    {
                        _tapHand = -1;
                        Phase = CatchPhase.Sizing;
                        Square = CatchRay.Square(head, _distance, _size);
                        SquareShown?.Invoke(f.t);
                    }
                    break;

                case CatchPhase.Sizing:
                    if (!tracked) { Cancel("tracking lost while sizing"); break; }
                    if (both && TryMeasure(f, out float d, out float s)) { _distance = d; _size = s; }
                    Square = CatchRay.Square(head, _distance, _size);
                    if (!anyPinch)
                    {
                        Phase = CatchPhase.Recording;
                        _recordStart = f.t;
                        _lostSince = -1f;
                        Elapsed = 0f;
                        RecordStarted?.Invoke(f.t);
                    }
                    break;

                case CatchPhase.Recording:
                    Square = CatchRay.Square(head, _distance, _size);
                    Elapsed = f.t - _recordStart;
                    if (!tracked)
                    {
                        if (_lostSince < 0f) _lostSince = f.t;
                        if (f.t - _lostSince > _s.trackingLossGraceSeconds) { Cancel("tracking lost while recording"); break; }
                    }
                    else _lostSince = -1f;
                    if (Elapsed >= Duration)
                    {
                        var shot = new CatchShot { mode = Mode, t = f.t, recordSeconds = Elapsed, square = Square, head = head };
                        Phase = CatchPhase.Idle;
                        Caught?.Invoke(shot);
                    }
                    break;
            }
        }

        bool TryMeasure(HandFrame f, out float distance, out float size)
        {
            Vector3 a = f.left.PinchPoint, b = f.right.PinchPoint;
            distance = (CatchRay.DistanceAlong(_head, a) + CatchRay.DistanceAlong(_head, b)) * 0.5f;
            size = CatchRay.SideFromDiagonal(_head, a, b);
            if (size < _s.minSquare || distance < _s.minHeadDistance || distance > _s.maxHeadDistance) return false;
            size = Mathf.Min(size, _s.maxSquare);
            return true;
        }

        void Cancel(string why)
        {
            Phase = CatchPhase.Idle;
            Elapsed = 0f;
            Cancelled?.Invoke(why);
        }

        void OnPinchStarted(PinchEvent e)
        {
            int other = 1 - (int)e.hand;
            if (Phase != CatchPhase.Idle) { _tapHand = -1; return; }
            if (_tapHand >= 0 && _tapHand != (int)e.hand) { _tapHand = -1; return; }
            if (_pinch.IsPinching((Handedness)other)) { _tapHand = -1; return; }
            if (PalmFacesHead(_frame[e.hand])) { _tapHand = -1; return; }
            _tapHand = (int)e.hand;
            _tapStart = e.t;
        }

        void OnPinchEnded(PinchEvent e)
        {
            if (_tapHand != (int)e.hand) return;
            _tapHand = -1;
            if (Phase != CatchPhase.Idle) return;
            if (_pinch.IsPinching((Handedness)(1 - (int)e.hand))) return;
            if (e.t - _tapStart > _s.toggleTapMaxSeconds) return;
            Mode = Mode == CatchMode.Still ? CatchMode.Loop : CatchMode.Still;
            ModeChanged?.Invoke(Mode, e.t);
        }

        bool PalmFacesHead(HandPose p)
        {
            Vector3 toHead = _head.position - p.palm;
            if (toHead.sqrMagnitude < 1e-6f || p.palmNormal.sqrMagnitude < 1e-6f) return false;
            return Vector3.Dot(p.palmNormal.normalized, toHead.normalized) > _s.palmFacingHeadDot;
        }
    }
}
