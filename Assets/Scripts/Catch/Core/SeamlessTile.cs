using System;
using UnityEngine;

namespace MoodPicker.Catch
{
    public static class SeamlessTile
    {
        public static Color32[] FromBig(Color32[] big, CropRect c) => FromBig(big, c.BigSize, c.side, c.overlap);

        public static Color32[] FromBig(Color32[] big, int bigSize, int side, int overlap)
        {
            if (big == null || big.Length != bigSize * bigSize) throw new ArgumentException("big size");
            if (side + overlap != bigSize || overlap < 1 || overlap > side) throw new ArgumentException("side + overlap");
            int B = bigSize, s = side, b = overlap;
            var a = new double[b];
            for (int i = 0; i < b; i++) { double t = (double)i / b; a[i] = t * t * (3 - 2 * t); }

            var xs = new double[B, s, 3];
            for (int yT = 0; yT < B; yT++)
            {
                int row = (B - 1 - yT) * B;
                for (int x = 0; x < s; x++)
                {
                    Color32 p = big[row + x];
                    if (x < b)
                    {
                        Color32 q = big[row + s + x];
                        double w = a[x];
                        xs[yT, x, 0] = p.r * w + q.r * (1 - w);
                        xs[yT, x, 1] = p.g * w + q.g * (1 - w);
                        xs[yT, x, 2] = p.b * w + q.b * (1 - w);
                    }
                    else { xs[yT, x, 0] = p.r; xs[yT, x, 1] = p.g; xs[yT, x, 2] = p.b; }
                }
            }

            var dst = new Color32[s * s];
            for (int yT = 0; yT < s; yT++)
            {
                int row = (s - 1 - yT) * s;
                for (int x = 0; x < s; x++)
                {
                    double r = xs[yT, x, 0], g = xs[yT, x, 1], bl = xs[yT, x, 2];
                    if (yT < b)
                    {
                        double w = a[yT];
                        r = r * w + xs[s + yT, x, 0] * (1 - w);
                        g = g * w + xs[s + yT, x, 1] * (1 - w);
                        bl = bl * w + xs[s + yT, x, 2] * (1 - w);
                    }
                    dst[row + x] = new Color32(To8(r), To8(g), To8(bl), 255);
                }
            }
            return dst;
        }

        static byte To8(double v) => (byte)Math.Max(0, Math.Min(255, Math.Round(v)));

        public static double SeamRatio(Color32[] px, int size)
        {
            double interior = 0, wrap = 0;
            long ni = 0, nw = 0;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Color32 p = px[y * size + x];
                    if (x + 1 < size) { interior += Diff(p, px[y * size + x + 1]); ni++; }
                    if (y + 1 < size) { interior += Diff(p, px[(y + 1) * size + x]); ni++; }
                    if (x == size - 1) { wrap += Diff(p, px[y * size]); nw++; }
                    if (y == size - 1) { wrap += Diff(p, px[x]); nw++; }
                }
            return (wrap / nw) / Math.Max(interior / ni, 1e-9);
        }

        static double Diff(Color32 a, Color32 b) => (Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b)) / 3.0;
    }
}
