using System;

namespace Pigment
{
    public sealed class LightLayersResult
    {
        public byte[] CalmRgba;
        public byte[] SparkRgba;
        public float SparkCover;
        public bool Synthetic;
        public double CalmSigma;
    }

    public static class LightLayers
    {
        public static LightLayersResult Build(byte[] atlasRgba, FlipbookLayout layout, double calmSigma = 7.0, int haloRadius = 3)
        {
            int n = layout.Tile, n2 = n * n, width = layout.Width, frames = Math.Max(1, layout.Frames);
            var r = new double[n2];
            var g = new double[n2];
            var b = new double[n2];
            var mean = new float[n2];
            for (int k = 0; k < frames; k++)
            {
                layout.PixelRectUnity(k, out int ox, out int oy);
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        int o = ((oy + y) * width + ox + x) * 4, i = y * n + x;
                        float cr = atlasRgba[o] / 255f, cg = atlasRgba[o + 1] / 255f, cb = atlasRgba[o + 2] / 255f;
                        r[i] += cr;
                        g[i] += cg;
                        b[i] += cb;
                        mean[i] += cr * 0.2126f + cg * 0.7152f + cb * 0.0722f;
                    }
                }
            }
            for (int i = 0; i < n2; i++)
            {
                r[i] /= frames;
                g[i] /= frames;
                b[i] /= frames;
                mean[i] /= frames;
            }
            BlurPeriodic(r, g, n, calmSigma);
            var zero = new double[n2];
            BlurPeriodic(b, zero, n, calmSigma);

            var calm = new byte[n2 * 4];
            for (int i = 0; i < n2; i++)
            {
                calm[i * 4] = ToByte(r[i]);
                calm[i * 4 + 1] = ToByte(g[i]);
                calm[i * 4 + 2] = ToByte(b[i]);
                calm[i * 4 + 3] = 255;
            }

            var spark = new byte[atlasRgba.Length];
            var sp = new float[n2];
            long lit = 0;
            for (int k = 0; k < frames; k++)
            {
                layout.PixelRectUnity(k, out int ox, out int oy);
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        int o = ((oy + y) * width + ox + x) * 4, i = y * n + x;
                        float cr = atlasRgba[o] / 255f, cg = atlasRgba[o + 1] / 255f, cb = atlasRgba[o + 2] / 255f;
                        float l = cr * 0.2126f + cg * 0.7152f + cb * 0.0722f;
                        float whiteMin = Math.Min(cr, Math.Min(cg, cb));
                        sp[i] = Clamp01((l - mean[i] - 0.06f) / 0.18f) * Clamp01((whiteMin - 0.55f) / 0.35f);
                        if (sp[i] > 0.3f) lit++;
                    }
                }
                float[] halo = BoxBlurWrap(BoxBlurWrap(sp, n, haloRadius), n, haloRadius);
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        int o = ((oy + y) * width + ox + x) * 4, i = y * n + x;
                        spark[o] = ToByte(sp[i]);
                        spark[o + 1] = ToByte(Math.Min(1f, halo[i] * 2.2f));
                        spark[o + 3] = 255;
                    }
                }
            }
            return new LightLayersResult { CalmRgba = calm, SparkRgba = spark, SparkCover = lit / (float)((long)n2 * frames) };
        }

        public static LightLayersResult BuildAdaptive(byte[] atlasRgba, FlipbookLayout layout, int haloRadius = 3, int seed = 1)
        {
            int n = layout.Tile, n2 = n * n, width = layout.Width, frames = Math.Max(1, layout.Frames);
            var r = new double[n2];
            var g = new double[n2];
            var b = new double[n2];
            var lm = new double[n2];
            var lAll = new float[n2 * frames];
            var vAll = new float[n2 * frames];
            var wAll = new float[n2 * frames];
            for (int k = 0; k < frames; k++)
            {
                layout.PixelRectUnity(k, out int ox, out int oy);
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        int o = ((oy + y) * width + ox + x) * 4, i = y * n + x, j = k * n2 + i;
                        float cr = atlasRgba[o] / 255f, cg = atlasRgba[o + 1] / 255f, cb = atlasRgba[o + 2] / 255f;
                        float l = cr * 0.2126f + cg * 0.7152f + cb * 0.0722f;
                        r[i] += cr;
                        g[i] += cg;
                        b[i] += cb;
                        lm[i] += l;
                        lAll[j] = l;
                        vAll[j] = Math.Max(cr, Math.Max(cg, cb));
                        wAll[j] = Math.Min(cr, Math.Min(cg, cb));
                    }
                }
            }
            for (int i = 0; i < n2; i++)
            {
                r[i] /= frames;
                g[i] /= frames;
                b[i] /= frames;
                lm[i] /= frames;
            }

            var lmB = (double[])lm.Clone();
            BlurPeriodic(lmB, new double[n2], n, 4.0);
            double hf = 0;
            for (int i = 0; i < n2; i++) hf += Math.Abs(lm[i] - lmB[i]);
            hf /= n2;
            double sigma = Clamp01((float)((hf - 0.01) / 0.03)) * 6.0 + 1.5;
            BlurPeriodic(r, g, n, sigma);
            BlurPeriodic(b, new double[n2], n, sigma);
            var calm = new byte[n2 * 4];
            for (int i = 0; i < n2; i++)
            {
                calm[i * 4] = ToByte(r[i]);
                calm[i * 4 + 1] = ToByte(g[i]);
                calm[i * 4 + 2] = ToByte(b[i]);
                calm[i * 4 + 3] = 255;
            }

            float v60 = Percentile(vAll, 0.60f);
            var cand = new float[lAll.Length];
            for (int j = 0; j < cand.Length; j++)
                cand[j] = (lAll[j] - (float)lm[j % n2]) * Clamp01((vAll[j] - v60) / 0.15f);
            float thr = Math.Min(0.10f, Math.Max(0.02f, Percentile(cand, 0.97f)));
            var spk = new float[lAll.Length];
            long lit = 0;
            for (int j = 0; j < spk.Length; j++)
            {
                float dL = lAll[j] - (float)lm[j % n2];
                float white = Clamp01((dL - 0.06f) / 0.18f) * Clamp01((wAll[j] - 0.55f) / 0.35f);
                float colour = Clamp01((cand[j] - thr) / Math.Max(thr, 0.03f));
                spk[j] = Math.Max(white, colour);
                if (spk[j] > 0.3f) lit++;
            }
            float cover = lit / (float)spk.Length;
            bool synthetic = cover < 0.01f;
            if (synthetic) AddSyntheticTwinkles(spk, lm, lmB, n, frames, seed);

            var spark = new byte[atlasRgba.Length];
            var sp = new float[n2];
            for (int k = 0; k < frames; k++)
            {
                Array.Copy(spk, k * n2, sp, 0, n2);
                float[] halo = BoxBlurWrap(BoxBlurWrap(sp, n, haloRadius), n, haloRadius);
                layout.PixelRectUnity(k, out int ox, out int oy);
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        int o = ((oy + y) * width + ox + x) * 4, i = y * n + x;
                        spark[o] = ToByte(sp[i]);
                        spark[o + 1] = ToByte(Math.Min(1f, halo[i] * 2.2f));
                        spark[o + 3] = 255;
                    }
                }
            }
            return new LightLayersResult { CalmRgba = calm, SparkRgba = spark, SparkCover = cover, Synthetic = synthetic, CalmSigma = sigma };
        }

        static void AddSyntheticTwinkles(float[] spk, double[] lm, double[] lmB, int n, int frames, int seed)
        {
            int n2 = n * n;
            var rng = new Random(seed);
            var sm = (double[])lm.Clone();
            BlurPeriodic(sm, new double[n2], n, 1.5);
            var edge = new float[n2];
            var hi = new float[n2];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i = y * n + x;
                    double gx = (sm[y * n + (x + 1) % n] - sm[y * n + (x - 1 + n) % n]) / 2;
                    double gy = (sm[((y + 1) % n) * n + x] - sm[((y - 1 + n) % n) * n + x]) / 2;
                    edge[i] = (float)Math.Sqrt(gx * gx + gy * gy);
                    hi[i] = edge[i] * Clamp01((float)((lm[i] - 0.45) / 0.25)) + 0.5f * (float)Math.Max(0, lm[i] - lmB[i]);
                }
            }
            float h88 = Percentile(hi, 0.88f);
            float e99 = Percentile(edge, 0.99f);
            if (e99 <= 0f) e99 = 1e-6f;
            var peaks = new System.Collections.Generic.List<(int x, int y, double ph, int kk)>();
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float v = hi[y * n + x];
                    if (v <= h88) continue;
                    bool isMax = true;
                    for (int dy = -4; dy <= 4 && isMax; dy++)
                        for (int dx = -4; dx <= 4; dx++)
                            if (hi[((y + dy + n) % n) * n + (x + dx + n) % n] > v) { isMax = false; break; }
                    if (isMax) peaks.Add((x, y, rng.NextDouble(), 1 + rng.Next(3)));
                }
            }
            var glow = new double[n2];
            for (int i = 0; i < n2; i++) glow[i] = Math.Min(1f, edge[i] / e99) * Clamp01((float)((lm[i] - 0.45) / 0.25));
            BlurPeriodic(glow, new double[n2], n, 2.5);
            const double s2 = 2 * 1.3 * 1.3;
            for (int k = 0; k < frames; k++)
            {
                double breathe = 0.75 + 0.25 * Math.Sin(2 * Math.PI * k / frames);
                int baseJ = k * n2;
                for (int i = 0; i < n2; i++)
                    spk[baseJ + i] = Math.Max(spk[baseJ + i], (float)(Math.Min(0.28, glow[i] * 0.3) * breathe));
                foreach (var p in peaks)
                {
                    double amp = Math.Pow(Math.Max(0, Math.Sin(2 * Math.PI * ((double)k / frames * p.kk + p.ph))), 4);
                    if (amp < 0.01) continue;
                    for (int dy = -4; dy <= 4; dy++)
                    {
                        for (int dx = -4; dx <= 4; dx++)
                        {
                            int j = baseJ + ((p.y + dy + n) % n) * n + (p.x + dx + n) % n;
                            spk[j] = (float)Math.Min(1, Math.Max(spk[j], amp * Math.Exp(-(dx * dx + dy * dy) / s2)));
                        }
                    }
                }
            }
        }

        static float Percentile(float[] arr, float q)
        {
            int step = Math.Max(1, arr.Length / 60000);
            var smp = new float[(arr.Length + step - 1) / step];
            for (int i = 0, c = 0; i < arr.Length; i += step) smp[c++] = arr[i];
            Array.Sort(smp);
            return smp[Math.Min(smp.Length - 1, (int)(q * smp.Length))];
        }

        static void BlurPeriodic(double[] a, double[] b, int n, double sigma)
        {
            var re = (double[])a.Clone();
            var im = (double[])b.Clone();
            Fft.Forward2D(re, im, n);
            for (int y = 0; y < n; y++)
            {
                double fy = (y <= n / 2 ? y : y - n) / (double)n;
                for (int x = 0; x < n; x++)
                {
                    double fx = (x <= n / 2 ? x : x - n) / (double)n;
                    double k = Math.Exp(-2.0 * Math.PI * Math.PI * sigma * sigma * (fx * fx + fy * fy));
                    re[y * n + x] *= k;
                    im[y * n + x] *= k;
                }
            }
            Fft.Inverse2D(re, im, n);
            Array.Copy(re, a, a.Length);
            Array.Copy(im, b, b.Length);
        }

        public static float[] BoxBlurWrap(float[] src, int n, int radius)
        {
            var tmp = new float[n * n];
            var dst = new float[n * n];
            int w = 2 * radius + 1;
            for (int y = 0; y < n; y++)
            {
                float acc = 0f;
                for (int x = -radius; x <= radius; x++) acc += src[y * n + (x + n) % n];
                for (int x = 0; x < n; x++)
                {
                    tmp[y * n + x] = acc / w;
                    acc += src[y * n + (x + radius + 1) % n] - src[y * n + (x - radius + n) % n];
                }
            }
            for (int x = 0; x < n; x++)
            {
                float acc = 0f;
                for (int y = -radius; y <= radius; y++) acc += tmp[((y + n) % n) * n + x];
                for (int y = 0; y < n; y++)
                {
                    dst[y * n + x] = acc / w;
                    acc += tmp[((y + radius + 1) % n) * n + x] - tmp[((y - radius + n) % n) * n + x];
                }
            }
            return dst;
        }

        static float Clamp01(float v)
        {
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }

        static byte ToByte(double v)
        {
            return (byte)Math.Round(Math.Max(0.0, Math.Min(1.0, v)) * 255.0);
        }
    }
}
