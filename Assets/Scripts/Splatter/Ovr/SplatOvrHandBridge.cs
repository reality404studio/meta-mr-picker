using MoodPicker.Hands;
using UnityEngine;

namespace MoodPicker.Splatter
{
    [DefaultExecutionOrder(-10)]
    public sealed class SplatOvrHandBridge : MonoBehaviour
    {
        const string Tag = "[MP]";

        [SerializeField] SplatWordController wordController;
        [SerializeField] SplatShelf shelf;
        [SerializeField] SplatTuning tuning;
        [SerializeField] OVRPassthroughLayer passthroughLayer;
        [SerializeField] MonoBehaviour handInputSource;
        [SerializeField] MonoBehaviour microgestureSource;
        [SerializeField] Transform trackingSpace;
        [SerializeField] float microRepeatGuardSeconds = 0.22f;
        [SerializeField] float afterPinchQuietSeconds = 0.35f;

        readonly PinchDetector _pinch = new PinchDetector();
        readonly bool[] _was = new bool[2];
        readonly bool[] _holding = new bool[2];
        readonly bool[] _consumed = new bool[2];
        readonly bool[] _otherDuring = new bool[2];
        readonly float[] _pinchStart = new float[2];
        readonly float[] _pinchChange = { -10f, -10f };
        IHandInput _input;
        IMicrogestureInput _micro;
        Microgesture _lastMicro;
        float _lastMicroTime = -10f;
        Vector3 _appliedPassthrough = new Vector3(float.NaN, 0f, 0f);

        void Awake()
        {
            _input = handInputSource as IHandInput;
            _micro = microgestureSource as IMicrogestureInput;
        }

        void Update()
        {
            ApplyPassthrough();
            if (wordController == null || _input == null || trackingSpace == null) return;
            if (!_input.TryGetFrame(out HandFrame frame)) return;
            if (tuning != null)
            {
                _pinch.startDistance = tuning.pinchStart;
                _pinch.endDistance = tuning.pinchEnd;
            }
            _pinch.Update(frame);
            SplatHandPoints left = ToWorld(frame.left);
            SplatHandPoints right = ToWorld(frame.right);
            bool lp = left.tracked && _pinch.IsPinching(Handedness.Left);
            bool rp = right.tracked && _pinch.IsPinching(Handedness.Right);
            HandlePinch(0, lp, rp, left);
            HandlePinch(1, rp, lp, right);
            left.pinching = lp && !_consumed[0];
            right.pinching = rp && !_consumed[1];
            wordController.UpdateHands(left, right, Time.time, Time.deltaTime);
            HandleMicrogestures();
        }

        void HandlePinch(int h, bool now, bool other, in SplatHandPoints p)
        {
            if (now != _was[h]) _pinchChange[h] = Time.time;
            if (now && !_was[h])
            {
                _pinchStart[h] = Time.time;
                _otherDuring[h] = other;
                _holding[h] = shelf != null && shelf.TryGrab(h, p.Pinch);
                _consumed[h] = _holding[h];
            }
            else if (now)
            {
                _otherDuring[h] |= other;
                if (_holding[h]) _holding[h] = shelf.UpdateHeld(h, p.Pinch, p.palm);
            }
            else if (_was[h])
            {
                if (_holding[h]) shelf.Release(h);
                _holding[h] = false;
                _consumed[h] = false;
            }
            _was[h] = now;
        }

        void HandleMicrogestures()
        {
            if (_micro == null) return;
            Microgesture g = _micro.Current(Handedness.Left);
            bool fresh = g != Microgesture.None && (g != _lastMicro || Time.unscaledTime - _lastMicroTime >= microRepeatGuardSeconds);
            _lastMicro = g;
            if (!fresh) return;
            _lastMicroTime = Time.unscaledTime;
            bool allowed = SplatFreezeFlow.LeftGestureAllowed(Time.time, _pinchChange[0], _was[0], _holding[0], afterPinchQuietSeconds);
            Debug.Log($"{Tag} left micro {g} allowed={allowed}");
            if (!allowed) return;
            switch (g)
            {
                case Microgesture.SwipeLeft:
                    wordController.CycleShape(-1);
                    break;
                case Microgesture.SwipeRight:
                    wordController.CycleShape(1);
                    break;
                case Microgesture.SwipeForward:
                    wordController.StepForward();
                    break;
                case Microgesture.SwipeBackward:
                    wordController.StepBackward();
                    break;
            }
        }

        SplatHandPoints ToWorld(in HandPose pose)
        {
            var points = new SplatHandPoints();
            if (!pose.tracked) return points;
            points.tracked = true;
            points.thumb = trackingSpace.TransformPoint(pose.thumbTip);
            points.index = trackingSpace.TransformPoint(pose.indexTip);
            points.middle = trackingSpace.TransformPoint(pose.middleTip);
            points.ring = trackingSpace.TransformPoint(pose.ringTip);
            points.little = trackingSpace.TransformPoint(pose.littleTip);
            points.palm = trackingSpace.TransformPoint(pose.palm);
            return points;
        }

        void ApplyPassthrough()
        {
            if (tuning == null || passthroughLayer == null) return;
            var wanted = new Vector3(tuning.passthroughBrightness, tuning.passthroughContrast, tuning.passthroughSaturation);
            if (wanted == _appliedPassthrough) return;
            _appliedPassthrough = wanted;
            passthroughLayer.SetBrightnessContrastSaturation(wanted.x, wanted.y, wanted.z);
        }
    }
}
