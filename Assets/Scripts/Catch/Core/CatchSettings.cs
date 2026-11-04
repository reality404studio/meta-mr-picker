using UnityEngine;

namespace MoodPicker.Catch
{
    public enum CatchMode { Still = 0, Loop = 1 }

    [CreateAssetMenu(menuName = "Mood Picker/Catch Settings", fileName = "CatchSettings")]
    public class CatchSettings : ScriptableObject
    {
        [Header("Punch (baseline)")]
        public float punchDistance = 0.45f;
        public float punchSize = 0.12f;
        public float holdSeconds = 3f;
        public float loopRecordSeconds = 4f;

        [Header("Left drag (baseline)")]
        public float dragGain = 1f;
        public float maxOffset = 0.2f;

        [Header("Toggle (baseline)")]
        public float toggleTapMaxSeconds = 0.5f;
        public float palmFacingHeadDot = 0.5f;

        [Header("Crop / tile")]
        [Range(0.02f, 0.3f)] public float overlapFrac = 0.125f;
        public float sceneDistance = 3f;
        public int minCropPixels = 32;
        public bool flipCameraY;

        [Header("CelExtract")]
        public int sampleGrid = 64;
        [Range(2, 3)] public int tones = 3;
        public int kmeansIterations = 20;
        public float lightGain = 2f;

        public static CatchSettings Defaults() => CreateInstance<CatchSettings>();
    }
}
