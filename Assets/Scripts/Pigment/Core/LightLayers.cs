using System;

namespace Pigment
{
    public sealed class LightLayersResult
    {
        public byte[] CalmRgba;
        public byte[] SparkRgba;
        public float SparkCover;
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
