using System.Collections.Generic;
using UnityEngine;

namespace MoodPicker.Ritual
{
    public static class RitualClockFont
    {
        public const float Width = 0.62f;
        public const float ColonWidth = 0.26f;

        static readonly Dictionary<char, List<Vector2[]>> Cache = new Dictionary<char, List<Vector2[]>>();

        static Vector2[] Arc(Vector2 c, float rx, float ry, float fromDeg, float toDeg, int steps)
        {
            var pts = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, i / (float)steps) * Mathf.Deg2Rad;
                pts[i] = new Vector2(c.x + Mathf.Cos(a) * rx, c.y + Mathf.Sin(a) * ry);
            }
            return pts;
        }

        static Vector2[] Line(params float[] xy)
        {
            var pts = new Vector2[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return pts;
        }

        public static List<Vector2[]> Strokes(char c)
        {
            if (Cache.TryGetValue(c, out List<Vector2[]> hit)) return hit;
            var s = new List<Vector2[]>();
            switch (c)
            {
                case '0':
                    s.Add(Arc(new Vector2(0.31f, 0.5f), 0.27f, 0.48f, 90f, 450f, 36));
                    break;
                case '1':
                    s.Add(Line(0.16f, 0.82f, 0.34f, 1f, 0.34f, 0f));
                    break;
                case '2':
                    s.Add(Arc(new Vector2(0.31f, 0.72f), 0.26f, 0.27f, 165f, -30f, 18));
                    s.Add(Line(0.535f, 0.585f, 0.05f, 0.02f, 0.05f, 0f, 0.6f, 0f));
                    break;
                case '3':
                    s.Add(Arc(new Vector2(0.3f, 0.76f), 0.24f, 0.24f, 155f, -90f, 18));
                    s.Add(Arc(new Vector2(0.3f, 0.27f), 0.28f, 0.27f, 90f, -155f, 20));
                    break;
                case '4':
                    s.Add(Line(0.46f, 0f, 0.46f, 1f, 0.03f, 0.32f, 0.62f, 0.32f));
                    break;
                case '5':
                    s.Add(Line(0.56f, 1f, 0.11f, 1f, 0.07f, 0.56f));
                    s.Add(Arc(new Vector2(0.31f, 0.32f), 0.28f, 0.31f, 128f, -150f, 22));
                    break;
                case '6':
                    s.Add(Arc(new Vector2(0.36f, 0.52f), 0.31f, 0.48f, 60f, 185f, 14));
                    s.Add(Arc(new Vector2(0.31f, 0.3f), 0.27f, 0.3f, 180f, 540f, 28));
                    break;
                case '7':
                    s.Add(Line(0.03f, 1f, 0.6f, 1f, 0.22f, 0f));
                    break;
                case '8':
                    s.Add(Arc(new Vector2(0.31f, 0.76f), 0.22f, 0.24f, -90f, 270f, 24));
                    s.Add(Arc(new Vector2(0.31f, 0.27f), 0.27f, 0.27f, 90f, 450f, 26));
                    break;
                case '9':
                    s.Add(Arc(new Vector2(0.31f, 0.7f), 0.27f, 0.3f, 0f, 360f, 28));
                    s.Add(Arc(new Vector2(0.26f, 0.48f), 0.31f, 0.48f, -5f, -120f, 14));
                    break;
                case ':':
                    s.Add(Arc(new Vector2(0.13f, 0.27f), 0.045f, 0.045f, 0f, 360f, 8));
                    s.Add(Arc(new Vector2(0.13f, 0.73f), 0.045f, 0.045f, 0f, 360f, 8));
                    break;
                case '.':
                    s.Add(Arc(new Vector2(0.13f, 0.05f), 0.05f, 0.05f, 0f, 360f, 8));
                    break;
            }
            Cache[c] = s;
            return s;
        }

        public static float Advance(char c) => c == ':' || c == '.' ? ColonWidth : Width + 0.12f;

        public static float Length(List<Vector2[]> strokes)
        {
            float total = 0f;
            foreach (Vector2[] st in strokes)
                for (int i = 1; i < st.Length; i++) total += Vector2.Distance(st[i - 1], st[i]);
            return total;
        }

        public static void Sample(char c, int count, Vector2[] into, int offset, float jitter, int seed)
        {
            List<Vector2[]> strokes = Strokes(c);
            float total = Length(strokes);
            if (strokes.Count == 0 || total <= 0f)
            {
                for (int i = 0; i < count; i++) into[offset + i] = new Vector2(Width * 0.5f, 0.5f);
                return;
            }
            for (int i = 0; i < count; i++)
            {
                float want = (i + 0.5f) / count * total;
                float acc = 0f;
                Vector2 p = strokes[0][0];
                Vector2 dir = Vector2.up;
                bool found = false;
                foreach (Vector2[] st in strokes)
                {
                    for (int k = 1; k < st.Length && !found; k++)
                    {
                        float seg = Vector2.Distance(st[k - 1], st[k]);
                        if (acc + seg >= want)
                        {
                            float u = seg > 1e-6f ? (want - acc) / seg : 0f;
                            p = Vector2.Lerp(st[k - 1], st[k], u);
                            dir = seg > 1e-6f ? (st[k] - st[k - 1]) / seg : Vector2.up;
                            found = true;
                        }
                        acc += seg;
                    }
                    if (found) break;
                }
                Vector2 side = new Vector2(-dir.y, dir.x);
                float j = (RitualUtil.Hash(seed + i * 17) - 0.5f) * 2f * jitter;
                into[offset + i] = p + side * j;
            }
        }
    }
}
