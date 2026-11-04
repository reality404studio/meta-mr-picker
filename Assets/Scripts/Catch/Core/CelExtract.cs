using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MoodPicker.Catch
{
    public struct CelResult
    {
        public Color[] tones;
        public Vector3 lightDirView;

        public Color Light => tones[tones.Length - 1];
        public Color Shadow => tones[0];
        public Color Mid => tones[tones.Length / 2];
    }

    public static class CelExtract
    {
        public static double Luminance(double r, double g, double b) => r * 0.2126 + g * 0.7152 + b * 0.0722;

        public static CelResult Run(IReadOnlyList<Color32> pixels, int width, int height, CatchSettings s) =>
            Run(pixels, width, height, s.sampleGrid, s.tones, s.kmeansIterations, s.lightGain);

        public static CelResult Run(IReadOnlyList<Color32> pixels, int width, int height, int grid, int k, int iterations, float lightGain)
        {
            if (pixels == null || pixels.Count != width * height) throw new ArgumentException("pixel count");
            if (k < 2 || k > 3) throw new ArgumentOutOfRangeException(nameof(k));
            int n = grid * grid;
            var px = new double[n, 3];
            for (int yi = 0; yi < grid; yi++)
            {
                int yTop = (int)((yi + 0.5) * height / grid);
                int row = height - 1 - yTop;
                for (int xi = 0; xi < grid; xi++)
                {
                    int x = (int)((xi + 0.5) * width / grid);
                    Color32 c = pixels[row * width + x];
                    int i = yi * grid + xi;
                    px[i, 0] = c.r / 255.0; px[i, 1] = c.g / 255.0; px[i, 2] = c.b / 255.0;
                }
            }

            var lum = new double[n];
            for (int i = 0; i < n; i++) lum[i] = Luminance(px[i, 0], px[i, 1], px[i, 2]);
            int[] order = Enumerable.Range(0, n).OrderBy(i => lum[i]).ToArray();
            double[] quant = k == 3 ? new[] { 0.1, 0.5, 0.9 } : new[] { 0.1, 0.9 };
            var centers = new double[k, 3];
            for (int c = 0; c < k; c++)
            {
                int src = order[(int)(quant[c] * (n - 1))];
                for (int ch = 0; ch < 3; ch++) centers[c, ch] = px[src, ch];
            }

            var labels = new int[n];
            var sums = new double[k, 3];
            var counts = new int[k];
            for (int it = 0; it < iterations; it++)
            {
                Array.Clear(sums, 0, sums.Length);
                Array.Clear(counts, 0, counts.Length);
                for (int i = 0; i < n; i++)
                {
                    int best = 0;
                    double bd = double.MaxValue;
                    for (int c = 0; c < k; c++)
                    {
                        double dr = px[i, 0] - centers[c, 0], dg = px[i, 1] - centers[c, 1], db = px[i, 2] - centers[c, 2];
                        double d = dr * dr + dg * dg + db * db;
                        if (d < bd) { bd = d; best = c; }
                    }
                    labels[i] = best;
                    counts[best]++;
                    for (int ch = 0; ch < 3; ch++) sums[best, ch] += px[i, ch];
                }
                for (int c = 0; c < k; c++)
                    if (counts[c] > 0)
                        for (int ch = 0; ch < 3; ch++) centers[c, ch] = sums[c, ch] / counts[c];
            }

            int[] rank = Enumerable.Range(0, k).OrderBy(c => Luminance(centers[c, 0], centers[c, 1], centers[c, 2])).ToArray();
            var tones = new Color[k];
            for (int r = 0; r < k; r++)
                tones[r] = new Color((float)centers[rank[r], 0], (float)centers[rank[r], 1], (float)centers[rank[r], 2], 1f);

            int brightest = rank[k - 1];
            double cx = 0, cy = 0;
            int cnt = 0;
            for (int i = 0; i < n; i++)
            {
                if (labels[i] != brightest) continue;
                cx += (i % grid + 0.5) / grid * 2.0 - 1.0;
                cy += (i / grid + 0.5) / grid * 2.0 - 1.0;
                cnt++;
            }
            Vector3 dir = Vector3.back;
            if (cnt > 0)
            {
                cx /= cnt; cy /= cnt;
                dir = new Vector3((float)(cx * lightGain), (float)(-cy * lightGain), -1f).normalized;
            }
            return new CelResult { tones = tones, lightDirView = dir };
        }

        public static string Hex(Color c)
        {
            int r = (int)Math.Round(c.r * 255.0), g = (int)Math.Round(c.g * 255.0), b = (int)Math.Round(c.b * 255.0);
            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }
}
