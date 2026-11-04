using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoodPicker.Dab
{
    public struct DabStamp
    {
        public Vector2 uv;
        public float t;
        public float radius;
        public float strength;
    }

    public class PaintMask
    {
        readonly DabSettings _s;
        readonly List<DabStamp> _active = new List<DabStamp>();

        public int Size { get; }
        public float[] Values { get; }
        public bool WrapU { get; set; } = true;
        public bool Dirty { get; private set; }
        public int ActiveCount => _active.Count;

        public PaintMask(DabSettings settings)
        {
            _s = settings;
            Size = DabSettings.ClampResolution(settings.maskResolution);
            Values = new float[Size * Size];
        }

        public void Stamp(Vector2 uv, float strength, float t, bool press)
        {
            _active.Add(new DabStamp { uv = uv, t = t, radius = press ? _s.pressRadius : _s.tapRadius, strength = Mathf.Clamp01(strength) });
        }

        public float Radius(DabStamp s, float t)
        {
            float k = Mathf.Clamp01((t - s.t) / Mathf.Max(1e-3f, _s.spreadSeconds));
            k = 1f - (1f - k) * (1f - k) * (1f - k);
            return s.radius * Mathf.Lerp(0.15f, 1f, k);
        }

        public float ValueAt(DabStamp s, Vector2 uv, float t)
        {
            float dx = Mathf.Abs(uv.x - s.uv.x);
            if (WrapU) dx = Mathf.Min(dx, 1f - dx);
            float dy = uv.y - s.uv.y;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float r = Radius(s, t);
            float n = (Noise(uv * _s.noiseScale) - 0.5f) * 2f * _s.noiseAmount;
            float x = d / Mathf.Max(1e-4f, r) + n * Mathf.Clamp01(d / Mathf.Max(1e-4f, r));
            float edge = Mathf.Max(1e-3f, _s.edgeSoftness);
            float v = Mathf.Clamp01((1f - x) / edge);
            return v * s.strength;
        }

        public void Update(float t)
        {
            Dirty = false;
            for (int k = _active.Count - 1; k >= 0; k--)
            {
                DabStamp s = _active[k];
                float r = Radius(s, t) * (1f + _s.noiseAmount) + 1f / Size;
                int x0 = Mathf.FloorToInt((s.uv.x - r) * Size), x1 = Mathf.CeilToInt((s.uv.x + r) * Size);
                int y0 = Mathf.Max(0, Mathf.FloorToInt((s.uv.y - r) * Size)), y1 = Mathf.Min(Size - 1, Mathf.CeilToInt((s.uv.y + r) * Size));
                if (!WrapU) { x0 = Mathf.Max(0, x0); x1 = Mathf.Min(Size - 1, x1); }
                for (int y = y0; y <= y1; y++)
                    for (int xi = x0; xi <= x1; xi++)
                    {
                        int x = ((xi % Size) + Size) % Size;
                        var uv = new Vector2((x + 0.5f) / Size, (y + 0.5f) / Size);
                        float v = ValueAt(s, uv, t);
                        int i = y * Size + x;
                        if (v > Values[i]) { Values[i] = v; Dirty = true; }
                    }
                if (t - s.t >= _s.spreadSeconds) _active.RemoveAt(k);
            }
        }

        public float Sample(Vector2 uv)
        {
            int x = Mathf.Clamp((int)(uv.x * Size), 0, Size - 1), y = Mathf.Clamp((int)(uv.y * Size), 0, Size - 1);
            return Values[y * Size + x];
        }

        public void Clear()
        {
            Array.Clear(Values, 0, Values.Length);
            _active.Clear();
            Dirty = true;
        }

        public void CopyTo(byte[] r8)
        {
            for (int i = 0; i < Values.Length; i++) r8[i] = (byte)Mathf.RoundToInt(Values[i] * 255f);
        }

        static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        static float Noise(Vector2 p)
        {
            int xi = Mathf.FloorToInt(p.x), yi = Mathf.FloorToInt(p.y);
            float fx = p.x - xi, fy = p.y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
    }
}
