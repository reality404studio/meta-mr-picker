using MoodPicker.Hands;
using UnityEngine;

namespace MoodPicker.Splatter
{
    [DefaultExecutionOrder(-10)]
    public sealed class SplatOvrHandBridge : MonoBehaviour
    {
        [SerializeField] SplatWordController wordController;
        [SerializeField] SplatTuning tuning;
        [SerializeField] OVRPassthroughLayer passthroughLayer;
        [SerializeField] MonoBehaviour handInputSource;
        [SerializeField] Transform trackingSpace;

        readonly PinchDetector _pinch = new PinchDetector();
        IHandInput _input;
        Vector3 _appliedPassthrough = new Vector3(float.NaN, 0f, 0f);

        void Awake()
        {
            _input = handInputSource as IHandInput;
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
            SplatHandPoints left = ToWorld(frame.left, _pinch.IsPinching(Handedness.Left));
            SplatHandPoints right = ToWorld(frame.right, _pinch.IsPinching(Handedness.Right));
            wordController.UpdateHands(left, right, Time.time, Time.deltaTime);
        }

        SplatHandPoints ToWorld(in HandPose pose, bool pinching)
        {
            var points = new SplatHandPoints();
            if (!pose.tracked) return points;
            points.tracked = true;
            points.pinching = pinching;
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
