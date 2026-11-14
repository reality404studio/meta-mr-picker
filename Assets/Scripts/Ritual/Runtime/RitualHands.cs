using MoodPicker.Hands;
using UnityEngine;

namespace MoodPicker.Ritual
{
    public struct RitualHand
    {
        public bool tracked;
        public bool pinching;
        public bool pinchStarted;
        public bool pinchEnded;
        public Vector3 pinch;
        public Vector3 thumb;
        public Vector3 index;
        public Vector3 middle;
        public Vector3 ring;
        public Vector3 little;
        public Vector3 palm;
        public Vector3 palmNormal;
        public Vector3 palmVelocity;
        public Quaternion rotation;
        public float rollSpeed;
        public float openness;
    }

    public sealed class RitualHands
    {
        readonly PinchDetector _pinch = new PinchDetector();
        readonly RitualHand[] _hands = new RitualHand[2];
        readonly bool[] _was = new bool[2];
        readonly bool[] _seen = new bool[2];
        readonly Vector3[] _smoothPinch = new Vector3[2];
        readonly Vector3[] _lastPalm = new Vector3[2];
        readonly Quaternion[] _lastRotation = { Quaternion.identity, Quaternion.identity };

        public ref RitualHand this[int h] => ref _hands[h];

        public void Update(in HandFrame frame, Transform trackingSpace, TimeRitualConfig config, float dt)
        {
            _pinch.startDistance = config.pinchStart;
            _pinch.endDistance = config.pinchEnd;
            _pinch.Update(frame);
            Read(0, frame.left, _pinch.IsPinching(Handedness.Left), trackingSpace, dt);
            Read(1, frame.right, _pinch.IsPinching(Handedness.Right), trackingSpace, dt);
        }

        void Read(int h, in HandPose pose, bool pinching, Transform space, float dt)
        {
            ref RitualHand hand = ref _hands[h];
            hand.tracked = pose.tracked;
            hand.pinchStarted = pose.tracked && pinching && !_was[h];
            hand.pinchEnded = _was[h] && !(pose.tracked && pinching);
            hand.pinching = pose.tracked && pinching;
            _was[h] = hand.pinching;
            if (!pose.tracked)
            {
                _seen[h] = false;
                hand.palmVelocity = Vector3.zero;
                hand.rollSpeed = 0f;
                return;
            }
            hand.thumb = World(space, pose.thumbTip);
            hand.index = World(space, pose.indexTip);
            hand.middle = World(space, pose.middleTip);
            hand.ring = World(space, pose.ringTip);
            hand.little = World(space, pose.littleTip);
            hand.palm = World(space, pose.palm);
            hand.palmNormal = space != null ? space.TransformDirection(pose.palmNormal) : pose.palmNormal;
            Vector3 raw = (hand.thumb + hand.index) * 0.5f;
            Vector3 forward = raw - hand.palm;
            Vector3 up = hand.palmNormal.sqrMagnitude > 1e-6f ? -hand.palmNormal : Vector3.up;
            Quaternion rotation = forward.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(forward.normalized, up) : Quaternion.identity;
            float safe = Mathf.Max(dt, 1e-4f);
            if (_seen[h])
            {
                float blend = 1f - Mathf.Exp(-dt * 45f);
                _smoothPinch[h] = Vector3.Lerp(_smoothPinch[h], raw, blend);
                Vector3 v = (hand.palm - _lastPalm[h]) / safe;
                hand.palmVelocity = Vector3.Lerp(hand.palmVelocity, v, 1f - Mathf.Exp(-dt * 18f));
                float angle = Quaternion.Angle(_lastRotation[h], rotation) * Mathf.Deg2Rad / safe;
                hand.rollSpeed = Mathf.Lerp(hand.rollSpeed, angle, 1f - Mathf.Exp(-dt * 12f));
            }
            else
            {
                _smoothPinch[h] = raw;
                hand.palmVelocity = Vector3.zero;
                hand.rollSpeed = 0f;
                _seen[h] = true;
            }
            hand.pinch = _smoothPinch[h];
            hand.rotation = rotation;
            _lastPalm[h] = hand.palm;
            _lastRotation[h] = rotation;
            float open = Vector3.Distance(hand.index, hand.palm);
            open = Mathf.Min(open, Vector3.Distance(hand.middle, hand.palm));
            open = Mathf.Min(open, Vector3.Distance(hand.ring, hand.palm));
            open = Mathf.Min(open, Vector3.Distance(hand.little, hand.palm));
            hand.openness = open;
        }

        static Vector3 World(Transform space, Vector3 p) => space != null ? space.TransformPoint(p) : p;
    }
}
