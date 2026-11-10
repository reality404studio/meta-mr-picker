using UnityEngine;

namespace MoodPicker.Splatter
{
    [DefaultExecutionOrder(-10)]
    public sealed class SplatOvrHandBridge : MonoBehaviour
    {
        [SerializeField] SplatWordController wordController;
        [SerializeField] OVRSkeleton leftSkeleton;
        [SerializeField] OVRSkeleton rightSkeleton;
        [SerializeField] OVRHand leftHand;
        [SerializeField] OVRHand rightHand;

        readonly int[] _thumbIdx = { -1, -1 };
        readonly int[] _indexIdx = { -1, -1 };
        readonly int[] _middleIdx = { -1, -1 };
        readonly int[] _palmIdx = { -1, -1 };
        readonly bool[] _wasPinching = { false, false };

        void Update()
        {
            if (wordController == null) return;

            Vector3 targetPos = wordController.transform.position;
            bool lTracked = ReadHand(leftSkeleton, leftHand, 0, targetPos, out Vector3 lEffector, out Vector3 lPinch, out bool lPinching);
            bool rTracked = ReadHand(rightSkeleton, rightHand, 1, targetPos, out Vector3 rEffector, out Vector3 rPinch, out bool rPinching);

            wordController.UpdateHandInteraction(
                lTracked,
                lEffector,
                lPinch,
                lPinching,
                rTracked,
                rEffector,
                rPinch,
                rPinching,
                Time.time,
                Time.deltaTime);
        }

        bool ReadHand(
            OVRSkeleton skel,
            OVRHand hand,
            int h,
            Vector3 targetWorldPos,
            out Vector3 effectorWS,
            out Vector3 pinchWS,
            out bool isPinching)
        {
            effectorWS = Vector3.zero;
            pinchWS = Vector3.zero;
            isPinching = false;

            if (skel == null || !skel.IsInitialized || !skel.IsDataValid || skel.Bones == null)
            {
                _wasPinching[h] = false;
                return false;
            }

            if (hand == null) hand = skel.GetComponent<OVRHand>();
            if (hand != null && !hand.IsTracked)
            {
                _wasPinching[h] = false;
                return false;
            }

            if (_indexIdx[h] < 0 || _thumbIdx[h] < 0)
            {
                for (int i = 0; i < skel.Bones.Count; i++)
                {
                    var id = skel.Bones[i].Id;
                    if (id == OVRSkeleton.BoneId.XRHand_ThumbTip) _thumbIdx[h] = i;
                    else if (id == OVRSkeleton.BoneId.XRHand_IndexTip) _indexIdx[h] = i;
                    else if (id == OVRSkeleton.BoneId.XRHand_MiddleTip) _middleIdx[h] = i;
                    else if (id == OVRSkeleton.BoneId.XRHand_Palm) _palmIdx[h] = i;
                }
                if (_indexIdx[h] < 0 || _thumbIdx[h] < 0)
                {
                    _wasPinching[h] = false;
                    return false;
                }
            }

            Vector3 indexTip = skel.Bones[_indexIdx[h]].Transform.position;
            Vector3 thumbTip = skel.Bones[_thumbIdx[h]].Transform.position;
            pinchWS = 0.5f * (indexTip + thumbTip);

            effectorWS = indexTip;
            float bestDistSq = (indexTip - targetWorldPos).sqrMagnitude;

            if (_middleIdx[h] >= 0 && _middleIdx[h] < skel.Bones.Count)
            {
                Vector3 middleTip = skel.Bones[_middleIdx[h]].Transform.position;
                float dSq = (middleTip - targetWorldPos).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    effectorWS = middleTip;
                }
            }

            if (_palmIdx[h] >= 0 && _palmIdx[h] < skel.Bones.Count)
            {
                Vector3 palmPos = skel.Bones[_palmIdx[h]].Transform.position;
                float dSq = (palmPos - targetWorldPos).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    effectorWS = palmPos;
                }
            }

            float pinchDist = Vector3.Distance(indexTip, thumbTip);
            float distThreshold = _wasPinching[h] ? 0.038f : 0.026f;
            bool distPinch = pinchDist <= distThreshold;
            bool ovrPinch = false;
            if (hand != null)
            {
                float strengthThreshold = _wasPinching[h] ? 0.55f : 0.72f;
                ovrPinch = hand.GetFingerIsPinching(OVRHand.HandFinger.Index)
                        && hand.GetFingerPinchStrength(OVRHand.HandFinger.Index) >= strengthThreshold;
            }

            isPinching = hand != null ? (distPinch && ovrPinch) : distPinch;
            _wasPinching[h] = isPinching;
            return true;
        }
    }
}
