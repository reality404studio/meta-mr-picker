using System;
using UnityEngine;

namespace MoodPicker.Hands
{
    public enum Handedness { Left = 0, Right = 1 }

    [Serializable]
    public struct HandPose
    {
        public bool tracked;
        public Vector3 thumbTip;
        public Vector3 indexTip;
        public Vector3 middleTip;
        public Vector3 ringTip;
        public Vector3 palm;
        public Vector3 palmNormal;

        public float PinchDistance => Vector3.Distance(thumbTip, indexTip);
        public Vector3 PinchPoint => (thumbTip + indexTip) * 0.5f;
    }

    [Serializable]
    public struct HandFrame
    {
        public float t;
        public HandPose left;
        public HandPose right;

        public HandPose this[Handedness h] => h == Handedness.Left ? left : right;
    }

    public interface IHandInput
    {
        bool TryGetFrame(out HandFrame frame);
    }
}
