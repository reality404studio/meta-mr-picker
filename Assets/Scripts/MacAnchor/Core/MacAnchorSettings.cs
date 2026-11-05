namespace MoodPicker.MacAnchoring
{
    public sealed class MacAnchorSettings
    {
        public float MinSizeMeters { get; set; } = 0.05f;
        public float MaxSizeMeters { get; set; } = 0.60f;
        public float MaxVerticalTiltDegrees { get; set; } = 30f;
        public float RectangleToleranceMeters { get; set; } = 0.01f;
        public float BasisTolerance { get; set; } = 0.0001f;
        public float HoldSeconds { get; set; } = 1f;
        public float IgnoreFirstSeconds { get; set; } = 1f;
        public float StillRadiusMeters { get; set; } = 0.01f;
        public const string FileName = "mac-anchor.json";
        public const int FormatVersion = 1;

        public bool IsValid => Finite(MinSizeMeters) && MinSizeMeters > 0f &&
            Finite(MaxSizeMeters) && MaxSizeMeters >= MinSizeMeters &&
            Finite(MaxVerticalTiltDegrees) && MaxVerticalTiltDegrees >= 0f && MaxVerticalTiltDegrees < 90f &&
            Finite(RectangleToleranceMeters) && RectangleToleranceMeters > 0f &&
            Finite(BasisTolerance) && BasisTolerance > 0f && BasisTolerance < 1f &&
            Finite(HoldSeconds) && HoldSeconds > 0f &&
            Finite(IgnoreFirstSeconds) && IgnoreFirstSeconds >= 0f &&
            Finite(StillRadiusMeters) && StillRadiusMeters > 0f;

        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
