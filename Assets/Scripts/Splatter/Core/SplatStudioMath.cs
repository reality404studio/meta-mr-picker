using System;

namespace MoodPicker.Splatter
{
    public static class SplatApplyWave
    {
        public static float Newness(float distance, float radius, float band)
        {
            float b = Math.Max(band, 1e-5f);
            float t = Clamp01((distance - (radius - b)) / b);
            return 1f - t * t * (3f - 2f * t);
        }

        public static float Front(float distance, float radius, float band)
        {
            float x = (distance - radius) / Math.Max(band, 1e-5f);
            return (float)Math.Exp(-x * x);
        }

        public static bool Done(float radius, float maxDistance, float band)
        {
            return radius - Math.Max(band, 0f) * 2f > maxDistance;
        }

        static float Clamp01(float v)
        {
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }
    }

    public static class SplatShelfLayout
    {
        public static float SlotX(int index, int count, float spacing)
        {
            return (index - (count - 1) * 0.5f) * spacing;
        }
    }

    public static class SplatShapeCycle
    {
        public static int Step(int index, int direction, int count)
        {
            if (count <= 0) return 0;
            return ((index + direction) % count + count) % count;
        }
    }

    public static class SplatPinchTap
    {
        public static bool IsTap(float heldSeconds, bool otherHandPinched, bool consumed, float maxSeconds)
        {
            return !consumed && !otherHandPinched && heldSeconds <= maxSeconds;
        }
    }

    public static class SplatStillNames
    {
        public static string File(DateTime t)
        {
            return "splat-" + t.ToString("yyyyMMdd-HHmmss") + ".png";
        }

        public static string Label(DateTime t)
        {
            return t.ToString("MM.dd  HH:mm");
        }
    }

    public static class SplatDensity
    {
        public static double MeshArea(float[] positionsXYZ, int[] triangles)
        {
            double total = 0.0;
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                int i0 = triangles[t] * 3, i1 = triangles[t + 1] * 3, i2 = triangles[t + 2] * 3;
                double ax = positionsXYZ[i1] - positionsXYZ[i0], ay = positionsXYZ[i1 + 1] - positionsXYZ[i0 + 1], az = positionsXYZ[i1 + 2] - positionsXYZ[i0 + 2];
                double bx = positionsXYZ[i2] - positionsXYZ[i0], by = positionsXYZ[i2 + 1] - positionsXYZ[i0 + 1], bz = positionsXYZ[i2 + 2] - positionsXYZ[i0 + 2];
                double cx = ay * bz - az * by, cy = az * bx - ax * bz, cz = ax * by - ay * bx;
                total += 0.5 * Math.Sqrt(cx * cx + cy * cy + cz * cz);
            }
            return total;
        }

        public static int CountFor(double area, double perSquareMeter, int min, int max)
        {
            return (int)Math.Max(min, Math.Min(max, Math.Round(area * perSquareMeter)));
        }
    }
}
