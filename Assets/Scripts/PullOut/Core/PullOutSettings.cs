using System;

namespace MoodPicker.PullOut
{
    [Serializable]
    public sealed class PullOutSettings
    {
        public const int LetterCount = 5;
        public const float LetterDelaySeconds = 0.15f;

        public float EnterDepthMeters = 0.005f;
        public float ExitDepthMeters = 0.012f;
        public float ArmDepthMeters = 0.03f;
        public float MaxPenetrationMeters = 0.015f;
        public float HoldSeconds = 0.06f;
        public float MaxSampleGapSeconds = 0.1f;
        public float MinDownSpeed = 0.15f;
        public float MinContactSpeed = 0.06f;
        public float PredictLeadSeconds = 0.04f;
        public float VelocitySmoothing = 0.5f;
        public float DurationSeconds = 0.45f;
        public float HoverNormalOffsetMeters = 0.20f;
        public float HoverLetterSpacingMeters = 0.08f;
        public float HoverHeightMeters = 0.15f;
        public float ScreenWidthFraction = 0.8f;
        public float ScreenBaselineFraction = -0.25f;
        public float ModelScale = 0.5f;

        public bool IsValid => Positive(EnterDepthMeters) && Finite(ExitDepthMeters) &&
            ExitDepthMeters > EnterDepthMeters && Finite(ArmDepthMeters) && ArmDepthMeters > ExitDepthMeters &&
            Positive(MaxPenetrationMeters) && Positive(HoldSeconds) && Positive(MaxSampleGapSeconds) &&
            Positive(MinDownSpeed) && Positive(MinContactSpeed) && Positive(PredictLeadSeconds) &&
            Finite(VelocitySmoothing) && VelocitySmoothing >= 0f && VelocitySmoothing < 1f &&
            Positive(DurationSeconds) && Positive(HoverNormalOffsetMeters) && Positive(HoverLetterSpacingMeters) &&
            Finite(HoverHeightMeters) && Positive(ScreenWidthFraction) && ScreenWidthFraction <= 1f &&
            Finite(ScreenBaselineFraction) && Math.Abs(ScreenBaselineFraction) <= 0.5f && Positive(ModelScale);

        internal PullOutSettings Snapshot()
        {
            if (!IsValid) throw new ArgumentException("Invalid PullOut settings.");
            return (PullOutSettings)MemberwiseClone();
        }

        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Positive(float value) => Finite(value) && value > 0f;
    }
}
