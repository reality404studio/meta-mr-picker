using UnityEngine;

namespace MoodPicker.Portal
{
    [CreateAssetMenu(menuName = "Mood Picker/Portal Settings", fileName = "PortalSettings")]
    public class PortalSettings : ScriptableObject
    {
        [Header("Open / close (baseline)")]
        public float openUpDot = 0.8f;
        public float closeUpDot = 0.5f;
        public float openHoldSeconds = 0.12f;
        public float closeHoldSeconds = 0.3f;
        public float lostCloseSeconds = 0.5f;
        public float maxPalmSpeed = 0.6f;
        public bool invertPalmNormal;

        [Header("Placement (baseline)")]
        [Range(0.05f, 0.20f)] public float heightAbovePalm = 0.14f;
        public float radius = 0.06f;
        public float normalSmoothing = 12f;

        [Header("Dip (baseline)")]
        public float dipDepth = 0.01f;
        public float exitHeight = 0.005f;
        public float dipMaxSeconds = 1.5f;
        public int chipCols = 3;
        public float chipPitch = 0.032f;

        [Header("Pinch gate")]
        public float pinchCooldownSeconds = 0.5f;

        public static PortalSettings Defaults() => CreateInstance<PortalSettings>();
    }
}
