// PeriodicTile — 이음매 없는 타일 (찰칵 모드, 루프 프레임 모두).
// Moisan, "Periodic plus smooth image decomposition" (2011) 의 주기 성분 p = u - s 만 남긴다.
// 가장자리 점프를 부드러운 저주파 s 로 흡수하므로 무늬는 그대로, 랩 경계만 사라진다.
// 기준 구현: tools/texlab.py (periodic). UnityEngine 의존 없음. n 은 2의 거듭제곱 (256, 512 …).
using System;

namespace Pigment
{
    public static class PeriodicTile
    {
        /// RGBA8 (행 우선, n×n×4) 를 제자리에서 주기화. 알파는 건드리지 않는다.
        /// R,G 는 한 번의 복소 FFT 에 실수/허수로 함께 넣는다 (분모가 실수·대칭이라 섞이지 않음).
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

        /// 한 채널(float, 0~1 이든 0~255 든) 을 제자리에서 주기화.
        public static void MakePeriodic(float[] plane, int n)
        {
            var u = new double[n * n];
            for (int i = 0; i < u.Length; i++) u[i] = plane[i];
            double[] s, unused;
            SmoothPair(u, null, n, out s, out unused);
            for (int i = 0; i < u.Length; i++) plane[i] = (float)(u[i] - s[i]);
        }

        /// 평활 성분 s 를 구한다. a, b 두 실수 채널을 한 번에 (b 는 null 가능).
        static void SmoothPair(double[] a, double[] b, int n, out double[] sa, out double[] sb)
        {
            var re = new double[n * n]; var im = new double[n * n];
            AddBoundary(a, re, n);
            if (b != null) AddBoundary(b, im, n);
            Fft.Forward2D(re, im, n);
            double[] den = Denominator(n);
            for (int i = 0; i < re.Length; i++) { re[i] *= den[i]; im[i] *= den[i]; }
            Fft.Inverse2D(re, im, n);   // 1/(n*n) 정규화 포함
            sa = re; sb = im;
        }

        // v: 마주 보는 가장자리의 차이 (Moisan 식의 경계 이미지)
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

        /// 진단: 랩 경계의 평균 RGB 점프 / 내부 이웃 평균 차이. 1 근처 = 경계가 내부와 구분 안 됨.
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

    /// 반지름 2 FFT (복소, 제자리). 256² 한 장에 수 ms 수준 — 무거우면 Burst Job 이나 컴퓨트로.
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
