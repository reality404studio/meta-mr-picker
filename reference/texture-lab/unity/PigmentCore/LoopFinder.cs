// LoopFinder — 짧은 클립에서 이음매가 가장 덜 티 나는 루프 구간 (i, j) 을 찾는다.
// 기준 구현: tools/texlab.py (find_loop), web/pigment-lab (findLoop). 결과가 같아야 한다 (unity/tests).
// UnityEngine 의존 없음. Burst 로 옮길 땐 배열을 NativeArray 로 바꾸면 된다.
using System;
using System.Collections.Generic;

namespace Pigment
{
    public struct LoopResult
    {
        public int Start;        // i: 루프 첫 프레임
        public int End;          // j: 여기서 i 로 되돌아감 (루프 길이 = End - Start)
        public float Score;      // 이음매 비용 / 평소 프레임 간 차이. 1 ≈ 일반 전환, 물·불 같은 불규칙 장면은 2~3도 크로스페이드로 가려짐
        public float Baseline;   // 연속 프레임 차이 중앙값
        public bool Forced;      // 클립이 허용 길이보다 짧아서 가능한 최대로 잘랐음
        public int Length { get { return End - Start; } }
    }

    public static class LoopFinder
    {
        /// gray: 프레임마다 작은 흑백 이미지(예: 64×64 = 4096) 를 펼친 배열. 전부 같은 길이.
        /// 반환: N×N 평균 절대 차이 행렬 (행 우선).
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
            var c = new float[n - 1];
            for (int k = 0; k < n - 1; k++) c[k] = d[k * n + k + 1];
            Array.Sort(c);
            // numpy median 과 같게: 짝수 개면 가운데 두 값 평균
            float med = (c.Length % 2 == 1) ? c[c.Length / 2] : 0.5f * (c[c.Length / 2 - 1] + c[c.Length / 2]);
            return Math.Max(1e-4f, med);
        }

        /// 전환 (j-1 → i) 의 비용: 크로스페이드 F 프레임 동안 겹칠 프레임 쌍들의 평균 차이 (양 끝은 절반 가중).
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
                    // 같은 품질이면 긴 루프를 조금 더 선호 (texlab.py 와 같은 가중치)
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

        /// 루프의 t 번째 출력 프레임을 만들 때 쓰는 가중치.
        /// t < fade 이면  out = src[End+t] * (1-w) + src[Start+t] * w,  그 밖에는 src[Start+t] 그대로.
        public static float CrossfadeWeight(int t, int fade)
        {
            if (t >= fade) return 1f;
            float x = (t + 0.5f) / fade;
            return x * x * (3f - 2f * x);
        }

        // Python round() 와 같은 은행가 반올림 (기준 출력과 맞추려고)
        static int RoundHalfEven(double v) { return (int)Math.Round(v, MidpointRounding.ToEven); }
    }
}
