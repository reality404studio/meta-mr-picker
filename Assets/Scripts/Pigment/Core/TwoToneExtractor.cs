using System;

namespace Pigment
{
    public struct TwoTonePalette
    {
        public float LightR, LightG, LightB;
        public float ShadowR, ShadowG, ShadowB;
        public float Threshold;
    }

    public static class TwoToneExtractor
    {
        public static float LumaSrgb(float r, float g, float b)
        {
            return 0.2126f * r + 0.7152f * g + 0.0722f * b;
        }

        public static TwoTonePalette Extract(byte[] rgba, int maxSamples = 6000, int iterations = 10)
        {
            int totalPixels = rgba.Length / 4;
            if (totalPixels <= 0) throw new ArgumentException("Empty pixel buffer");
            int step = Math.Max(1, totalPixels / Math.Max(1, maxSamples));
            int count = (totalPixels + step - 1) / step;
            var sr = new float[count];
            var sg = new float[count];
            var sb = new float[count];
            var luma = new float[count];
            var order = new int[count];
            int idx = 0;
            for (int p = 0; p < totalPixels && idx < count; p += step, idx++)
            {
                float r = rgba[p * 4] / 255f;
                float g = rgba[p * 4 + 1] / 255f;
                float b = rgba[p * 4 + 2] / 255f;
                sr[idx] = r; sg[idx] = g; sb[idx] = b;
                luma[idx] = LumaSrgb(r, g, b);
                order[idx] = idx;
            }
            Array.Sort(luma, order, 0, idx);
            int iShadow = order[Math.Min(idx - 1, (int)(idx * 0.2f))];
            int iLight = order[Math.Min(idx - 1, (int)(idx * 0.8f))];
            float c0r = sr[iShadow], c0g = sg[iShadow], c0b = sb[iShadow];
            float c1r = sr[iLight], c1g = sg[iLight], c1b = sb[iLight];

            int iters = Math.Max(1, iterations);
            for (int it = 0; it < iters; it++)
            {
                double s0r = 0, s0g = 0, s0b = 0; int n0 = 0;
                double s1r = 0, s1g = 0, s1b = 0; int n1 = 0;
                for (int k = 0; k < idx; k++)
                {
                    float r = sr[k], g = sg[k], b = sb[k];
                    float d0 = (r - c0r) * (r - c0r) + (g - c0g) * (g - c0g) + (b - c0b) * (b - c0b);
                    float d1 = (r - c1r) * (r - c1r) + (g - c1g) * (g - c1g) + (b - c1b) * (b - c1b);
                    if (d0 <= d1) { s0r += r; s0g += g; s0b += b; n0++; }
                    else { s1r += r; s1g += g; s1b += b; n1++; }
                }
                if (n0 > 0) { c0r = (float)(s0r / n0); c0g = (float)(s0g / n0); c0b = (float)(s0b / n0); }
                if (n1 > 0) { c1r = (float)(s1r / n1); c1g = (float)(s1g / n1); c1b = (float)(s1b / n1); }
            }

            float l0 = LumaSrgb(c0r, c0g, c0b);
            float l1 = LumaSrgb(c1r, c1g, c1b);
            if (l0 > l1)
            {
                float tr = c0r, tg = c0g, tb = c0b;
                c0r = c1r; c0g = c1g; c0b = c1b;
                c1r = tr; c1g = tg; c1b = tb;
                float tl = l0; l0 = l1; l1 = tl;
            }
            return new TwoTonePalette
            {
                ShadowR = c0r, ShadowG = c0g, ShadowB = c0b,
                LightR = c1r, LightG = c1g, LightB = c1b,
                Threshold = 0.5f * (l0 + l1)
            };
        }
    }
}
