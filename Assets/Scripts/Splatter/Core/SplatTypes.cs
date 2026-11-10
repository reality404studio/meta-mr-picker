using System;

namespace MoodPicker.Splatter
{
    public enum SplatShaderMode
    {
        Raw = 0,
        TwoToneCel = 1,
        Glint = 2,
        Chrome = 3,
        Caustic = 4
    }

    public struct SplatSample
    {
        public float CenterX, CenterY, CenterZ;
        public float NormalX, NormalY, NormalZ;
        public float ScaleTangentU, ScaleTangentV, ScaleNormal;
        public float RotX, RotY, RotZ, RotW;
        public int IsOutline;
    }

    public static class LetterPoseMath
    {
        public const float PhaseStep = 0.9f;
        public const float OutlinePushFactor = 0.018f;
        public const float NormalThicknessRatio = 0.08f;

        public static float Phase(int letterIndex)
        {
            return letterIndex * PhaseStep;
        }

        public static float RotationYRadians(float timeSeconds, int letterIndex)
        {
            return (float)(Math.Sin(0.5 * timeSeconds + Phase(letterIndex)) * 0.55);
        }

        public static float BobOffsetY(float timeSeconds, int letterIndex, float letterSize)
        {
            return (float)(Math.Sin(1.3 * timeSeconds + Phase(letterIndex)) * 0.04 * letterSize);
        }

        public static int SlotFrame(float timeSeconds, float fps, int frameCount)
        {
            if (frameCount <= 0 || fps <= 0f) return 0;
            int raw = (int)Math.Floor(timeSeconds * fps);
            int mod = raw % frameCount;
            return mod < 0 ? mod + frameCount : mod;
        }

        public static float SlotFrameContinuous(float timeSeconds, float fps, int frameCount)
        {
            if (frameCount <= 0 || fps <= 0f) return 0f;
            double raw = timeSeconds * (double)fps;
            double mod = raw % frameCount;
            if (mod < 0.0) mod += frameCount;
            return (float)mod;
        }
    }
}
