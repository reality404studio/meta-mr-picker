using System;
using MoodPicker.Hands;
using UnityEngine;

namespace MoodPicker.DeskKeyboard
{
    public class KeyboardGrab
    {
        public float grabRadius = 0.04f;
        public Handedness hand = Handedness.Left;

        readonly PinchDetector _pinch = new PinchDetector();
        readonly Vector3 _handleOffset;
        Vector3 _startPinch, _startOrigin;
        HandFrame _frame;

        public Vector3 Origin { get; private set; }
        public Vector3 Handle => Origin + _handleOffset;
        public bool Grabbing { get; private set; }

        public event Action<Vector3> Grabbed;
        public event Action<Vector3> Released;

        public KeyboardGrab(Vector3 origin, Vector3 handleOffset)
        {
            Origin = origin;
            _handleOffset = handleOffset;
            _pinch.PinchStarted += OnStart;
            _pinch.PinchEnded += OnEnd;
        }

        public void Update(HandFrame f)
        {
            _frame = f;
            _pinch.Update(f);
            if (Grabbing) Origin = _startOrigin + (f[hand].PinchPoint - _startPinch);
        }

        void OnStart(PinchEvent e)
        {
            if (e.hand != hand || Vector3.Distance(e.point, Handle) > grabRadius) return;
            Grabbing = true;
            _startPinch = e.point;
            _startOrigin = Origin;
            Grabbed?.Invoke(Origin);
        }

        void OnEnd(PinchEvent e)
        {
            if (e.hand != hand || !Grabbing) return;
            Grabbing = false;
            Released?.Invoke(Origin);
        }
    }
}
