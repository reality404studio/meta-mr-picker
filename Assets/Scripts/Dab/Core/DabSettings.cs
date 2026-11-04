using UnityEngine;

namespace MoodPicker.Dab
{
    [CreateAssetMenu(menuName = "Mood Picker/Dab Settings", fileName = "DabSettings")]
    public class DabSettings : ScriptableObject
    {
        [Header("Bucket (baseline)")]
        public float fillSeconds = 0.2f;

        [Header("Judge (baseline)")]
        public float contactDepth = 0.003f;
        public float releaseHeight = 0.006f;
        public float minApproachSpeed = 0.08f;
        public float pressDepth = 0.012f;
        public float pressHoldSeconds = 0.25f;
        public float decideSeconds = 0.25f;
        public float maxContactSeconds = 2f;

        public static DabSettings Defaults() => CreateInstance<DabSettings>();
    }
}
