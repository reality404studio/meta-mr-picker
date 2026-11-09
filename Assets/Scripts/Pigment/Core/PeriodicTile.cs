using System;

namespace Pigment
{
    public static class PeriodicTile
    {
        public static void MakePeriodicRGBA(byte[] rgba, int n)
        {
            int n2 = n * n;
            var r = new double[n2]; var g = new double[n2]; var b = new double[n2];
            for (int i = 0; i < n2; i++) { r[i] = rgba[i * 4]; g[i] = rgba[i * 4 + 1]; b[i] = rgba[i * 4 + 2]; }
            double[] sr, sg, sb, unused;
            SmoothPair(r, g, n, out sr, out sg);
            SmoothPair(b, null, n, out sb, out unused);
            for (int i = 0; i < n2; i++)
            {
                rgba[i * 4]     = ToByte(r[i] - sr[i]);
                rgba[i * 4 + 1] = ToByte(g[i] - sg[i]);
                rgba[i * 4 + 2] = ToByte(b[i] - sb[i]);
            }
        }

        public static void MakePeriodic(float[] plane, int n)
        {
            var u = new double[n * n];
            for (int i = 0; i < u.Length; i++) u[i] = plane[i];
            double[] s, unused;
            SmoothPair(u, null, n, out s, out unused);
            for (int i = 0; i < u.Length; i++) plane[i] = (float)(u[i] - s[i]);
        }

        static void SmoothPair(double[] a, double[] b, int n, out double[] sa, out double[] sb)
        {
            var re = new double[n * n]; var im = new double[n * n];
            AddBoundary(a, re, n);
            if (b != null) AddBoundary(b, im, n);
            Fft.Forward2D(re, im, n);
            double[] den = Denominator(n);
            for (int i = 0; i < re.Length; i++) { re[i] *= den[i]; im[i] *= den[i]; }
            Fft.Inverse2D(re, im, n);
            sa = re; sb = im;
        }

        static void AddBoundary(double[] u, double[] v, int n)
        {
            for (int y = 0; y < n; y++) { double d = u[y * n + n - 1] - u[y * n]; v[y * n] += d; v[y * n + n - 1] -= d; }
            for (int x = 0; x < n; x++) { double d = u[(n - 1) * n + x] - u[x]; v[x] += d; v[(n - 1) * n + x] -= d; }
        }

        static double[] _den; static int _denN;
        static double[] Denominator(int n)
        {
            if (_den != null && _denN == n) return _den;
            var d = new double[n * n];
            for (int q = 0; q < n; q++)
                for (int r = 0; r < n; r++)
                {
                    double v = 2 * Math.Cos(2 * Math.PI * q / n) + 2 * Math.Cos(2 * Math.PI * r / n) - 4;
                    d[q * n + r] = (q == 0 && r == 0) ? 0 : 1.0 / v;
                }
            _den = d; _denN = n; return d;
        }

        static byte ToByte(double v) { return (byte)(v < 0 ? 0 : v > 255 ? 255 : Math.Round(v)); }

        public static float SeamError(byte[] rgba, int n)
        {
            double e = 0, bsum = 0;
            for (int y = 0; y < n; y++)
                for (int ch = 0; ch < 3; ch++)
                {
                    e += Math.Abs(rgba[(y * n + n - 1) * 4 + ch] - rgba[(y * n) * 4 + ch]);
                    bsum += Math.Abs(rgba[(y * n + n / 2) * 4 + ch] - rgba[(y * n + n / 2 - 1) * 4 + ch]);
                }
            for (int x = 0; x < n; x++)
                for (int ch = 0; ch < 3; ch++)
                {
                    e += Math.Abs(rgba[((n - 1) * n + x) * 4 + ch] - rgba[x * 4 + ch]);
                    bsum += Math.Abs(rgba[((n / 2) * n + x) * 4 + ch] - rgba[((n / 2 - 1) * n + x) * 4 + ch]);
                }
            return (float)(e / Math.Max(bsum, 1e-6));
        }
    }

    public static class Fft
    {
        public static void Forward2D(double[] re, double[] im, int n) { Run2D(re, im, n, false); }
        public static void Inverse2D(double[] re, double[] im, int n)
        {
            Run2D(re, im, n, true);
            double k = 1.0 / ((double)n * n);
            for (int i = 0; i < re.Length; i++) { re[i] *= k; im[i] *= k; }
        }

        static void Run2D(double[] re, double[] im, int n, bool inv)
        {
            if ((n & (n - 1)) != 0) throw new ArgumentException("n must be a power of two");
            var tr = new double[n]; var ti = new double[n];
            int[] rev = BitReverse(n);
            for (int y = 0; y < n; y++) Run1D(re, im, y * n, 1, n, inv, rev, tr, ti);
            for (int x = 0; x < n; x++) Run1D(re, im, x, n, n, inv, rev, tr, ti);
        }

        static void Run1D(double[] re, double[] im, int off, int stride, int n, bool inv, int[] rev, double[] tr, double[] ti)
        {
            for (int i = 0; i < n; i++) { int j = rev[i]; tr[j] = re[off + i * stride]; ti[j] = im[off + i * stride]; }
            for (int size = 2; size <= n; size <<= 1)
            {
                int half = size >> 1;
                double ang = (inv ? 2 : -2) * Math.PI / size;
                for (int k = 0; k < half; k++)
                {
                    double c = Math.Cos(ang * k), s = Math.Sin(ang * k);
                    for (int st = 0; st < n; st += size)
                    {
                        int a = st + k, b = a + half;
                        double xr = tr[b] * c - ti[b] * s, xi = tr[b] * s + ti[b] * c;
                        tr[b] = tr[a] - xr; ti[b] = ti[a] - xi; tr[a] += xr; ti[a] += xi;
                    }
                }
            }
            for (int i = 0; i < n; i++) { re[off + i * stride] = tr[i]; im[off + i * stride] = ti[i]; }
        }

        static int[] _rev; static int _revN;
        static int[] BitReverse(int n)
        {
            if (_rev != null && _revN == n) return _rev;
            int bits = 0; while ((1 << bits) < n) bits++;
            var r = new int[n];
            for (int i = 0; i < n; i++) { int v = 0; for (int b = 0; b < bits; b++) v |= ((i >> b) & 1) << (bits - 1 - b); r[i] = v; }
            _rev = r; _revN = n; return r;
        }
    }
}
