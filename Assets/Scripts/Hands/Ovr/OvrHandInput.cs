using UnityEngine;

namespace MoodPicker.Hands.Ovr
{
    public class OvrHandInput : MonoBehaviour, IHandInput
    {
        [SerializeField] OVRCameraRig rig;
        [SerializeField] OVRSkeleton leftSkeleton;
        [SerializeField] OVRSkeleton rightSkeleton;

        readonly int[] _thumb = { -1, -1 }, _index = { -1, -1 }, _middle = { -1, -1 }, _ring = { -1, -1 }, _little = { -1, -1 }, _palm = { -1, -1 };

        public bool TryGetFrame(out HandFrame frame)
        {
            frame = new HandFrame
            {
                t = Time.time,
                left = Read(leftSkeleton, 0),
                right = Read(rightSkeleton, 1),
            };
            return true;
        }

        HandPose Read(OVRSkeleton s, int h)
        {
            if (s == null || !s.IsInitialized || !s.IsDataValid || s.Bones == null) return default;
            var hand = s.GetComponent<OVRHand>();
            if (hand == null || !hand.IsTracked || !hand.IsDataHighConfidence) return default;
            if (_thumb[h] < 0)
            {
                for (int i = 0; i < s.Bones.Count; i++)
                {
                    var id = s.Bones[i].Id;
                    if (id == OVRSkeleton.BoneId.XRHand_ThumbTip) _thumb[h] = i;
                    else if (id == OVRSkeleton.BoneId.XRHand_IndexTip) _index[h] = i;
                    else if (id == OVRSkeleton.BoneId.XRHand_MiddleTip) _middle[h] = i;
                    else if (id == OVRSkeleton.BoneId.XRHand_RingTip) _ring[h] = i;
                    else if (id == OVRSkeleton.BoneId.XRHand_LittleTip) _little[h] = i;
                    else if (id == OVRSkeleton.BoneId.XRHand_Palm) _palm[h] = i;
                }
                if (_thumb[h] < 0 || _index[h] < 0 || _middle[h] < 0 || _ring[h] < 0 || _little[h] < 0 || _palm[h] < 0) { _thumb[h] = -1; return default; }
            }
            Transform space = rig.trackingSpace;
            Transform palm = s.Bones[_palm[h]].Transform;
            return new HandPose
            {
                tracked = true,
                thumbTip = space.InverseTransformPoint(s.Bones[_thumb[h]].Transform.position),
                indexTip = space.InverseTransformPoint(s.Bones[_index[h]].Transform.position),
                middleTip = space.InverseTransformPoint(s.Bones[_middle[h]].Transform.position),
                ringTip = space.InverseTransformPoint(s.Bones[_ring[h]].Transform.position),
                littleTip = space.InverseTransformPoint(s.Bones[_little[h]].Transform.position),
                palm = space.InverseTransformPoint(palm.position),
                palmNormal = space.InverseTransformDirection(-palm.up),
            };
        }
    }
}
