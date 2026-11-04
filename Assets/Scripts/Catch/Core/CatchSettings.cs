using UnityEngine;

namespace MoodPicker.Catch
{
    public enum CatchMode { Still = 0, Loop = 1 }

    [CreateAssetMenu(menuName = "Mood Picker/Catch Settings", fileName = "CatchSettings")]
    public class CatchSettings : ScriptableObject
    {
        [Header("Record (baseline)")]
        public float stillRecordSeconds = 3f;
        public float loopRecordSeconds = 4f;
        public float trackingLossGraceSeconds = 0.2f;

        [Header("Square (baseline)")]
        public float minSquare = 0.06f;
        public float maxSquare = 0.6f;
        public float minHeadDistance = 0.15f;
        public float maxHeadDistance = 1.0f;

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

        public float RecordSeconds(CatchMode m) => m == CatchMode.Still ? stillRecordSeconds : loopRecordSeconds;

        public static CatchSettings Defaults() => CreateInstance<CatchSettings>();
    }
}
