using UnityEngine;

namespace MoodPicker.Ritual
{
    public static class RitualVortex
    {
        public static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        public static Vector3 Spread(Vector3 from, Vector3 center, Vector3 outDir, float outRadius, float s, float sign, float hash)
        {
            Vector3 dest = center + outDir * outRadius;
            Vector3 rel = dest - center;
            float swirl = sign * s * 0.9f;
            float c = Mathf.Cos(swirl), sn = Mathf.Sin(swirl);
            rel = new Vector3(rel.x * c - rel.z * sn, rel.y, rel.x * sn + rel.z * c);
            Vector3 p = Vector3.Lerp(from, center + rel, s);
            p.y += 0.05f * Mathf.Sin(s * Mathf.PI) * (0.5f + hash);
            return p;
        }

        public static Vector3 OutPoint(Vector3 center, Vector3 outDir, float outRadius, float sign)
        {
            Vector3 rel = outDir * outRadius;
            float swirl = sign * 0.9f;
            float c = Mathf.Cos(swirl), sn = Mathf.Sin(swirl);
            return center + new Vector3(rel.x * c - rel.z * sn, rel.y, rel.x * sn + rel.z * c);
        }

        public static Vector3 Drift(Vector3 p, float t, float hash, float amount)
        {
            return p + new Vector3(
                Mathf.Sin(t * 0.7f + hash * 11f),
                0.6f * Mathf.Sin(t * 0.53f + hash * 7f),
                Mathf.Cos(t * 0.61f + hash * 5f)) * amount;
        }

        public static Vector3 Spiral(Vector3 from, Vector3 to, float q, float sign, float turns, float hash)
        {
            Vector3 rel = from - to;
            float r0 = Mathf.Sqrt(rel.x * rel.x + rel.z * rel.z);
            float a0 = Mathf.Atan2(rel.z, rel.x);
            float a = a0 + sign * q * q * turns * 6.2831853f;
            float r = r0 * Mathf.Pow(1f - q, 1.6f);
            float y = rel.y * Mathf.Pow(1f - q, 1.1f) + 0.06f * Mathf.Sin(q * Mathf.PI) * (0.4f + hash);
            float wisp = 0.03f * (1f - q) * Mathf.Sin(q * 9f + hash * 6.2831853f);
            return to + new Vector3(Mathf.Cos(a) * (r + wisp), y, Mathf.Sin(a) * (r + wisp));
        }
    }
}
