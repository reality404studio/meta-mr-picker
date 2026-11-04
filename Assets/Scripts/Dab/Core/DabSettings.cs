using UnityEngine;

namespace MoodPicker.Dab
{
    [CreateAssetMenu(menuName = "Mood Picker/Dab Settings", fileName = "DabSettings")]
    public class DabSettings : ScriptableObject
    {
        public const int MaxMaskResolution = 256;

        [Header("Mask (baseline)")]
        public int maskResolution = 128;
        public float spreadSeconds = 0.8f;
        public float tapRadius = 0.08f;
        public float pressRadius = 0.18f;
        public float noiseAmount = 0.35f;
        public float noiseScale = 9f;
        public float edgeSoftness = 0.25f;

        [Header("Judge (baseline)")]
        public float contactDepth = 0.003f;
        public float releaseHeight = 0.006f;
        public float minApproachSpeed = 0.08f;
        public float pressDepth = 0.012f;
        public float pressHoldSeconds = 0.25f;
        public float decideSeconds = 0.25f;
        public float maxContactSeconds = 2f;

        void OnValidate() => maskResolution = ClampResolution(maskResolution);

        public static int ClampResolution(int r) => Mathf.Clamp(Mathf.ClosestPowerOfTwo(Mathf.Max(16, r)), 16, MaxMaskResolution);

        public static DabSettings Defaults() => CreateInstance<DabSettings>();
    }
}
