using UnityEngine;

namespace MoodPicker.Hands.Ovr
{
    [DefaultExecutionOrder(-20)]
    public class OvrMicrogestureInput : MonoBehaviour, IMicrogestureInput
    {
        [SerializeField] OVRHand leftHand;
        [SerializeField] OVRHand rightHand;

        readonly Microgesture[] _current = new Microgesture[2];

        public Microgesture Current(Handedness hand) => _current[(int)hand];

        void Update()
        {
            _current[0] = Read(leftHand);
            _current[1] = Read(rightHand);
        }

        static Microgesture Read(OVRHand hand)
        {
            if (hand == null || !hand.IsTracked) return Microgesture.None;
            switch (hand.GetMicrogestureType())
            {
                case OVRHand.MicrogestureType.SwipeLeft: return Microgesture.SwipeLeft;
                case OVRHand.MicrogestureType.SwipeRight: return Microgesture.SwipeRight;
                case OVRHand.MicrogestureType.SwipeForward: return Microgesture.SwipeForward;
                case OVRHand.MicrogestureType.SwipeBackward: return Microgesture.SwipeBackward;
                case OVRHand.MicrogestureType.ThumbTap: return Microgesture.ThumbTap;
                default: return Microgesture.None;
            }
        }
    }
}
