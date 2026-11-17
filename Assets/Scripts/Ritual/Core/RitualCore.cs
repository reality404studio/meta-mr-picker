using System;

namespace MoodPicker.Ritual
{
    [Serializable]
    public struct JarShape
    {
        public float height;
        public float bodyRadius;
        public float mouthRadius;
        public float glass;
        public float floor;
        public float bodyTop;
        public float neckBottom;
        public float lipBottom;
        public float lipBulge;
        public float cornerRadius;

        public static JarShape Default => new JarShape
        {
            height = 0.2f,
            bodyRadius = 0.052f,
            mouthRadius = 0.04f,
            glass = 0.0025f,
            floor = 0.006f,
            bodyTop = 0.152f,
            neckBottom = 0.165f,
            lipBottom = 0.183f,
            lipBulge = 0.0035f,
            cornerRadius = 0.007f
        };

        public float NeckOuter => mouthRadius + glass;
        public float FillCeiling => lipBottom - 0.004f;

        public float OuterRadius(float y)
        {
            if (y <= 0f) return bodyRadius - cornerRadius;
            if (y < cornerRadius)
            {
                float d = cornerRadius - y;
                return bodyRadius - cornerRadius + (float)Math.Sqrt(Math.Max(0f, cornerRadius * cornerRadius - d * d));
            }
            if (y <= bodyTop) return bodyRadius;
            if (y < neckBottom)
            {
                float t = (y - bodyTop) / Math.Max(1e-5f, neckBottom - bodyTop);
                float s = t * t * (3f - 2f * t);
                return bodyRadius + (NeckOuter - bodyRadius) * s;
            }
            if (y <= lipBottom) return NeckOuter;
            if (y <= height)
            {
                float t = (y - lipBottom) / Math.Max(1e-5f, height - lipBottom);
                return NeckOuter + lipBulge * (float)Math.Sin(Math.PI * t);
            }
            return NeckOuter;
        }

        public float InnerRadius(float y)
        {
            if (y >= lipBottom) return mouthRadius;
            return Math.Max(0f, OuterRadius(y) - glass);
        }

        public float Volume(float y)
        {
            float top = Math.Min(Math.Max(y, floor), height);
            const int steps = 96;
            float dy = (top - floor) / steps;
            if (dy <= 0f) return 0f;
            double sum = 0.0;
            for (int i = 0; i < steps; i++)
            {
                float r = InnerRadius(floor + (i + 0.5f) * dy);
                sum += Math.PI * r * r * dy;
            }
            return (float)sum;
        }

        public float BodyVolume => Volume(bodyTop);

        public float HeightForVolume(float volume)
        {
            if (volume <= 0f) return floor;
            float lo = floor, hi = height;
            if (Volume(hi) <= volume) return hi;
            for (int i = 0; i < 28; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (Volume(mid) < volume) lo = mid;
                else hi = mid;
            }
            return 0.5f * (lo + hi);
        }
    }

    public static class RitualFill
    {
        public const int UnitsPerHour = 6;
        public const int DayUnits = 24 * UnitsPerHour;

        public static float VolumePerUnit(in JarShape jar, float hoursWhenFull)
        {
            return jar.BodyVolume / Math.Max(1f, hoursWhenFull * UnitsPerHour);
        }

        public static float HeightAfter(in JarShape jar, float hoursWhenFull, int totalUnits)
        {
            return jar.HeightForVolume(totalUnits * VolumePerUnit(jar, hoursWhenFull));
        }

        public static int Capacity(in JarShape jar, float hoursWhenFull)
        {
            return Math.Min(DayUnits, (int)Math.Floor(jar.Volume(jar.FillCeiling) / VolumePerUnit(jar, hoursWhenFull)));
        }
    }

    public static class Strata
    {
        public const int Samples = 64;

        public static float[] Flat(float y)
        {
            var b = new float[Samples];
            for (int i = 0; i < Samples; i++) b[i] = y;
            return b;
        }

        public static void Raise(float[] previous, float[] next, float meanRise, float maxBump, int seed)
        {
            float amp = meanRise > 1e-6f ? Math.Min(0.3f, maxBump / meanRise) : 0f;
            float p1 = Hash01(seed * 3 + 1) * 6.2831853f;
            float p2 = Hash01(seed * 3 + 2) * 6.2831853f;
            float p3 = Hash01(seed * 3 + 3) * 6.2831853f;
            for (int i = 0; i < Samples; i++)
            {
                float a = i * 6.2831853f / Samples;
                float n = (0.6f * (float)Math.Sin(a + p1) + 0.3f * (float)Math.Sin(2f * a + p2) + 0.2f * (float)Math.Sin(3f * a + p3)) / 1.1f;
                next[i] = previous[i] + meanRise * (1f + amp * n);
            }
        }

        public static float At(float[] boundary, float angle)
        {
            float x = angle / 6.2831853f;
            x -= (float)Math.Floor(x);
            float f = x * Samples;
            int i0 = (int)f % Samples;
            int i1 = (i0 + 1) % Samples;
            float t = f - (float)Math.Floor(f);
            return boundary[i0] + (boundary[i1] - boundary[i0]) * t;
        }

        public static float Mean(float[] boundary)
        {
            float s = 0f;
            for (int i = 0; i < boundary.Length; i++) s += boundary[i];
            return s / boundary.Length;
        }

        public static float Hash01(int n)
        {
            unchecked
            {
                uint x = (uint)n * 747796405u + 2891336453u;
                x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
                x = (x >> 22) ^ x;
                return (x & 0xFFFFFF) / 16777216f;
            }
        }
    }

    public struct SealPress
    {
        public float progress;
        public bool pressing;
        public bool sealedShut;
        public bool full;

        public void Step(float depth, float dt, float pressDepth, float holdSeconds, float recoverSeconds, bool sealOnRelease = false)
        {
            if (sealedShut) return;
            pressing = pressing ? depth >= pressDepth * 0.5f : depth >= pressDepth;
            if (pressing)
            {
                progress += dt / Math.Max(1e-3f, holdSeconds);
                if (progress >= 1f)
                {
                    progress = 1f;
                    full = true;
                    if (!sealOnRelease)
                    {
                        sealedShut = true;
                        pressing = false;
                    }
                }
            }
            else if (full && sealOnRelease)
            {
                sealedShut = true;
            }
            else
            {
                progress = Math.Max(0f, progress - dt / Math.Max(1e-3f, recoverSeconds));
            }
        }
    }

    public static class RitualTally
    {
        public static int Dominant(int[] unitsByCategory)
        {
            int best = -1, bestUnits = 0;
            for (int i = 0; i < unitsByCategory.Length; i++)
            {
                if (unitsByCategory[i] > bestUnits)
                {
                    best = i;
                    bestUnits = unitsByCategory[i];
                }
            }
            return best;
        }

        public static int Total(int[] unitsByCategory)
        {
            int s = 0;
            for (int i = 0; i < unitsByCategory.Length; i++) s += unitsByCategory[i];
            return s;
        }
    }

    public enum GovernorVerdict { Calm, Hold, Over }

    public static class RitualGovernor
    {
        public const float SlowShare = 1.12f;
        public const float BusyShare = 0.85f;

        public static float ToMilliseconds(float raw)
        {
            if (float.IsNaN(raw) || raw <= 0f) return float.NaN;
            return raw < 0.1f ? raw * 1000f : raw;
        }

        public static float Work(float gpuMs, float cpuMs)
        {
            if (float.IsNaN(gpuMs)) return cpuMs;
            if (float.IsNaN(cpuMs)) return gpuMs;
            return Math.Max(gpuMs, cpuMs);
        }

        public static GovernorVerdict Judge(float frameMs, float workMs, float budgetMs)
        {
            bool slow = frameMs > budgetMs * SlowShare;
            if (float.IsNaN(workMs)) return slow ? GovernorVerdict.Hold : GovernorVerdict.Calm;
            bool busy = workMs > budgetMs * BusyShare;
            if (slow && busy) return GovernorVerdict.Over;
            return busy ? GovernorVerdict.Hold : GovernorVerdict.Calm;
        }
    }

    public enum EmblemStyle { Sun = 0, Whorl = 1 }

    public static class SealEmblem
    {
        public const float FieldRadius = 0.86f;

        static float Smooth(float a, float b, float x)
        {
            float t = Math.Min(1f, Math.Max(0f, (x - a) / (b - a)));
            return t * t * (3f - 2f * t);
        }

        static float Band(float distance, float halfWidth, float soft)
        {
            return 1f - Smooth(halfWidth, halfWidth + soft, Math.Abs(distance));
        }

        public static float Motif(float x, float y)
        {
            float r = (float)Math.Sqrt(x * x + y * y);
            float ring = Band(r - 0.7f, 0.036f, 0.028f);
            float ticks = 0f;
            if (r > 0.59f && r < 0.72f)
            {
                float ang = (float)Math.Atan2(y, x);
                float spoke = (float)Math.Abs(Math.Sin(ang * 6.0));
                ticks = (1f - Smooth(0.18f, 0.34f, spoke)) * (1f - Smooth(0.055f, 0.085f, Math.Abs(r - 0.645f)));
            }
            float inside = 1f - Smooth(0.54f, 0.60f, r);
            float waves = 0f;
            for (int k = 0; k < 3; k++)
            {
                float y0 = -0.36f + k * 0.2f;
                float w = y0 + 0.055f * (float)Math.Sin(4.2f * x + k * 1.9f);
                waves = Math.Max(waves, Band(y - w, 0.038f, 0.03f));
            }
            float dx = x, dy = y - 0.3f;
            float sr = (float)Math.Sqrt(dx * dx + dy * dy);
            float sunAng = (float)Math.Atan2(dy, dx);
            float rays = (float)Math.Cos(sunAng * 6.0 + Math.PI * 0.5) * 0.5f + 0.5f;
            float starRadius = 0.095f + 0.065f * rays * rays;
            float sun = 1f - Smooth(starRadius, starRadius + 0.03f, sr);
            return Math.Max(Math.Max(ring, ticks), Math.Max(waves * inside, sun));
        }

        public static float Height(float x, float y)
        {
            float r = (float)Math.Sqrt(x * x + y * y);
            float field = 1f - Smooth(FieldRadius - 0.05f, FieldRadius + 0.03f, r);
            return 1f - field * (1f - Motif(x, y));
        }

        public static float Whorl(float x, float y)
        {
            float ex = x, ey = y * 1.12f;
            float r = (float)Math.Sqrt(ex * ex + ey * ey);
            float a = (float)Math.Atan2(ey, ex);
            float warp = 0.035f * (float)Math.Sin(3f * a + r * 7f) + 0.02f * (float)Math.Sin(5f * a - r * 4f);
            float phase = (r + warp) / 0.074f + a / 6.2831853f;
            float ridge = 0.5f + 0.5f * (float)Math.Cos(phase * 6.2831853f);
            float lines = Smooth(0.52f, 0.78f, ridge) * Smooth(0.05f, 0.11f, r);
            float core = 1f - Smooth(0.035f, 0.06f, r);
            float border = Band(r - 0.79f, 0.026f, 0.022f);
            return Math.Max(Math.Max(lines * (1f - Smooth(0.72f, 0.76f, r)), core), border);
        }

        public static float HeightOf(EmblemStyle style, float x, float y)
        {
            if (style != EmblemStyle.Whorl) return Height(x, y);
            float r = (float)Math.Sqrt(x * x + y * y);
            float field = 1f - Smooth(FieldRadius - 0.05f, FieldRadius + 0.03f, r);
            return 1f - field * (1f - Whorl(x, y));
        }
    }
}
