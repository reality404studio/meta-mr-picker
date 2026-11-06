using MoodPicker.Tokens;
using UnityEngine;

namespace MoodPicker.Portal
{
    [CreateAssetMenu(menuName = "Mood Picker/Portal Settings", fileName = "PortalSettings")]
    public class PortalSettings : ScriptableObject
    {
        [Header("Open / close (baseline)")]
        public float openUpDot = OrionTokens.HysteresisPalmUpEnterDot;
        public float closeUpDot = OrionTokens.HysteresisPalmUpExitDot;
        public float openHoldSeconds = OrionTokens.HysteresisPalmUpHoldSeconds;
        public float closeHoldSeconds = OrionTokens.HysteresisPalmUpReleaseSeconds;
        public float lostCloseSeconds = 0.5f;
        public float maxPalmSpeed = 0.6f;
        public bool invertPalmNormal;

        [Header("Placement (baseline)")]
        [Range(0.05f, 0.10f)] public float heightAbovePalm = OrionTokens.ZoneHandFolioAboveMeters;
        public float radius = 0.09f;
        public float normalSmoothing = 12f;

        [Header("Dip (baseline)")]
        public float dipDepth = 0.01f;
        public float exitHeight = 0.005f;
        public float dipMaxSeconds = 1.5f;

        [Header("Wheel (baseline)")]
        public float wheelRadius = 0.055f;
        public int wheelMinSlots = 8;
        public float frontAngleDegrees = -90f;
        public float slotHitRadius = 0.025f;
        public float rimInner = 0.035f;
        public float rimOuter = 0.1f;
        public float rimBelow = 0.005f;
        public float rimAbove = 0.03f;
        public float sweepEngageDegrees = 10f;
        public float sweepMaxVerticalSpeed = 0.15f;

        [Header("Well (baseline)")]
        public float wellRadius = 0.02f;

        [Header("Pinch gate")]
        public float pinchCooldownSeconds = 0.5f;

        [Header("Left pinch block (baseline)")]
        public float leftPinchDistance = 0.03f;
        public float leftPinchReleaseSeconds = 0.3f;

        public static PortalSettings Defaults() => CreateInstance<PortalSettings>();
    }
}
