using System;
using System.Collections.Generic;

namespace Pigment
{
    public struct LoopResult
    {
        public int Start;
        public int End;
        public float Score;
        public float Baseline;
        public bool Forced;
        public int Length { get { return End - Start; } }
    }

    public static class LoopFinder
    {
        public static float[] DiffMatrix(IReadOnlyList<float[]> gray)
        {
            int n = gray.Count, p = gray[0].Length;
            var d = new float[n * n];
            for (int i = 0; i < n; i++)
            {
                float[] a = gray[i];
                for (int j = i + 1; j < n; j++)
                {
                    float[] b = gray[j];
                    double s = 0;
                    for (int k = 0; k < p; k++) { float x = a[k] - b[k]; s += x < 0 ? -x : x; }
                    float v = (float)(s / p);
                    d[i * n + j] = v; d[j * n + i] = v;
                }
            }
            return d;
        }

        public static float Baseline(float[] d, int n)
        {
            var c = new List<float>(Math.Max(0, n - 1));
            for (int k = 0; k < n - 1; k++)
            {
                float v = d[k * n + k + 1];
                if (v > 0f) c.Add(v);
            }
            if (c.Count == 0) return 1e-4f;
            c.Sort();
            float med = (c.Count % 2 == 1) ? c[c.Count / 2] : 0.5f * (c[c.Count / 2 - 1] + c[c.Count / 2]);
            return Math.Max(1e-4f, med);
        }

        public static float Cost(float[] d, int n, int i, int j, int fade)
        {
            int f = Math.Max(fade, 1);
            double s = 0, c = 0;
            for (int t = -1; t <= f; t++)
            {
                int a = i + t, b = j + t;
                if (a < 0 || b >= n) continue;
                double w = (t >= 0 && t < f) ? 1.0 : 0.5;
                s += d[a * n + b] * w; c += w;
            }
            return (float)(s / c);
        }

        public static LoopResult Find(float[] d, int n, float fps, float minSec, float maxSec, int fade)
        {
            float bl = Baseline(d, n);
            int f = Math.Max(fade, 1);
            int lo = Math.Max(Math.Max(RoundHalfEven(minSec * fps), fade + 2), 6);
            int hi = Math.Max(lo, RoundHalfEven(maxSec * fps));
            int bi = -1, bj = -1; double best = double.MaxValue;
            for (int i = 1; i < n; i++)
            {
                for (int len = lo; len <= hi; len++)
                {
                    int j = i + len;
                    if (j + f > n - 1) break;
                    double c = Cost(d, n, i, j, fade) / bl - 0.02 * (len / (double)fps);
                    if (c < best) { best = c; bi = i; bj = j; }
                }
            }
            var r = new LoopResult { Baseline = bl };
            if (bi < 0)
            {
                int j = n - 1 - f, i = Math.Max(0, j - lo);
                r.Start = i; r.End = Math.Max(i + 2, j); r.Forced = true;
            }
            else { r.Start = bi; r.End = bj; }
            r.Score = Cost(d, n, r.Start, r.End, fade) / bl;
            return r;
        }

        public static float CrossfadeWeight(int t, int fade)
        {
            if (t >= fade) return 1f;
            float x = (t + 0.5f) / fade;
            return x * x * (3f - 2f * x);
        }

        static int RoundHalfEven(double v) { return (int)Math.Round(v, MidpointRounding.ToEven); }
    }
}
