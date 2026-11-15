using System.Collections.Generic;
using UnityEngine;

namespace MoodPicker.Ritual
{
    public sealed class RitualSealWave
    {
        const int RingSplats = 220;
        const int ShellSplats = 440;
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int RevealId = Shader.PropertyToID("_Reveal");
        static readonly int BreathId = Shader.PropertyToID("_Breath");
        static readonly int SwirlId = Shader.PropertyToID("_Swirl");
        static readonly int FlatId = Shader.PropertyToID("_Flat");

        readonly TimeRitualConfig _config;
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        readonly List<Vector3> _pos = new List<Vector3>(2048);
        readonly List<Vector2> _quad = new List<Vector2>(2048);
        readonly List<Vector4> _a = new List<Vector4>(2048);
        readonly List<Vector4> _b = new List<Vector4>(2048);
        readonly List<Vector4> _c = new List<Vector4>(2048);
        readonly List<int> _idx = new List<int>(3072);
        readonly List<Color> _palette = new List<Color>(8);
        GameObject _go;
        Mesh _mesh;
        MeshRenderer _renderer;
        Vector3 _center;
        Color _color;
        ClimaxStyle _style;
        float _age = -1f;

        public bool Active => _age >= 0f && _age <= Duration;
        public float Radius { get; private set; }
        public float Strength { get; private set; }
        public float JarGlow { get; private set; }
        public float MoteSpread { get; private set; }
        public int FogCount { get; private set; }
        public ClimaxStyle Style => _style;
        public int SplatCount { get; private set; }
        public float Duration => _style == ClimaxStyle.Bloom ? Mathf.Max(0.5f, _config.bloomSeconds) : Mathf.Max(0.2f, _config.climaxSeconds);

        public RitualSealWave(TimeRitualConfig config, Material material)
        {
            _config = config;
            if (material == null) return;
            _go = new GameObject("RitualSealWave");
            _go.hideFlags = HideFlags.DontSave;
            _mesh = new Mesh { name = "RitualSealWave", hideFlags = HideFlags.DontSave };
            _mesh.MarkDynamic();
            _go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = _go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            RitualUtil.Quiet(_renderer);
            _renderer.enabled = false;
        }

        public void Begin(Vector3 center, Color color, IReadOnlyList<Color> palette = null)
        {
            _center = center;
            _color = Color.Lerp(color, Color.white, 0.35f);
            _palette.Clear();
            if (palette != null)
                for (int i = 0; i < palette.Count; i++) _palette.Add(palette[i]);
            if (_palette.Count == 0) _palette.Add(color);
            _style = _config.climaxStyle;
            _age = 0f;
            Radius = 0f;
            Strength = 0f;
            JarGlow = 0f;
            MoteSpread = 0f;
        }

        public void Tick(float dt, Vector3 eye)
        {
            if (_age < 0f) return;
            _age += dt;
            float dur = Duration;
            if (_age > dur)
            {
                _age = -1f;
                Radius = 0f;
                Strength = 0f;
                JarGlow = 0f;
                MoteSpread = 0f;
                SplatCount = 0;
                FogCount = 0;
                if (_renderer != null) _renderer.enabled = false;
                return;
            }
            float u = _age / dur;
            _pos.Clear();
            _quad.Clear();
            _a.Clear();
            _b.Clear();
            _c.Clear();
            _idx.Clear();
            if (_style == ClimaxStyle.Bloom) Bloom(u, eye);
            else Wave(u);
            SplatCount = _pos.Count / 4;
            if (_renderer == null) return;
            bool show = Strength > 0.003f && SplatCount > 0;
            if (_renderer.enabled != show) _renderer.enabled = show;
            if (!show) return;
            _mesh.Clear(false);
            _mesh.SetVertices(_pos);
            _mesh.SetUVs(0, _quad);
            _mesh.SetUVs(1, _a);
            _mesh.SetUVs(2, _b);
            _mesh.SetUVs(3, _c);
            _mesh.SetTriangles(_idx, 0, false);
            _mesh.bounds = new Bounds(_center, Vector3.one * (Mathf.Max(Radius, _config.climaxReach) * 2f + 1f));
            _block.SetFloat(IntensityId, _style == ClimaxStyle.Bloom ? 1f : Strength);
            _block.SetFloat(RevealId, 10f);
            _block.SetFloat(BreathId, 0f);
            _block.SetFloat(SwirlId, 0f);
            _block.SetFloat(FlatId, 0f);
            _renderer.SetPropertyBlock(_block);
        }

        void Wave(float u)
        {
            float ease = 1f - (1f - u) * (1f - u) * (1f - u);
            Radius = Mathf.Max(0.05f, _config.climaxReach * ease);
            Strength = _config.climaxIntensity * Mathf.Pow(1f - u, 1.4f) * Mathf.Clamp01(u / 0.06f);
            JarGlow = Mathf.Pow(1f - u, 2f);
            float size = 0.014f + Radius * 0.03f;
            if (_style == ClimaxStyle.Ring)
            {
                for (int row = 0; row < 3; row++)
                {
                    float rr = Radius * (1f - 0.035f * row);
                    float gain = row == 0 ? 1f : (row == 1 ? 0.55f : 0.28f);
                    int n = RingSplats / 3;
                    for (int i = 0; i < n; i++)
                    {
                        float a = (i + 0.5f * row) / n * 6.2831853f;
                        Vector3 p = _center + new Vector3(Mathf.Cos(a) * rr, (row - 1) * size * 0.4f, Mathf.Sin(a) * rr);
                        Add(p, size * (1f + 0.25f * row), _color * gain, i);
                    }
                }
                return;
            }
            for (int i = 0; i < ShellSplats; i++)
            {
                float t = (i + 0.5f) / ShellSplats;
                float z = 1f - 2f * t;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                float phi = i * 2.39996323f;
                Vector3 dir = new Vector3(r * Mathf.Cos(phi), z, r * Mathf.Sin(phi));
                Add(_center + dir * Radius, size, _color * 0.8f, i);
            }
        }

        static Vector3 Dir(int i, int n, float lift)
        {
            float t = (i + 0.5f) / n;
            float z = 1f - 2f * t;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            float phi = i * 2.39996323f;
            return new Vector3(r * Mathf.Cos(phi), z * 0.6f + lift, r * Mathf.Sin(phi)).normalized;
        }

        void Bloom(float u, Vector3 eye)
        {
            float t = _age;
            float reach = _config.climaxReach;
            float rise = RitualVortex.Smooth01(u / 0.06f);
            float fall = 1f - RitualVortex.Smooth01((u - 0.55f) / 0.4f);
            Strength = _config.climaxIntensity * rise * Mathf.Max(0.15f, fall);
            JarGlow = RitualVortex.Smooth01(u / 0.05f) * (1f - RitualVortex.Smooth01((u - 0.25f) / 0.6f)) + 0.35f * RitualVortex.Smooth01((u - 0.7f) / 0.2f) * (1f - RitualVortex.Smooth01((u - 0.9f) / 0.1f));

            float haloSize = Mathf.Lerp(0.1f, 0.3f, RitualVortex.Smooth01(u / 0.25f));
            Color warm = Color.Lerp(_color, Color.white, 0.2f);
            AddEyeSafe(_center, haloSize, warm * (0.55f * JarGlow), 0, eye);
            AddEyeSafe(_center, haloSize * 1.6f, warm * (0.22f * JarGlow), 1, eye);

            int fog = Mathf.Max(0, _config.bloomFog);
            float fogEnv = RitualVortex.Smooth01(u / 0.18f) * (1f - RitualVortex.Smooth01((u - 0.62f) / 0.36f));
            FogCount = 0;
            for (int i = 0; i < fog; i++)
            {
                float h = RitualUtil.Hash(i * 53 + 7);
                Vector3 dir = Dir(i, fog, 0.15f);
                float far = reach * Mathf.Lerp(0.35f, 0.8f, h);
                float outU = RitualVortex.Smooth01(u / 0.4f);
                float inU = RitualVortex.Smooth01((u - 0.6f) / 0.4f);
                float dist = Mathf.Lerp(Mathf.Lerp(0.08f, far, outU), 0.15f, inU);
                float swirl = (h > 0.5f ? 1f : -1f) * (0.4f * outU + 1.6f * inU * inU);
                float c = Mathf.Cos(swirl), s = Mathf.Sin(swirl);
                Vector3 d = new Vector3(dir.x * c - dir.z * s, dir.y, dir.x * s + dir.z * c);
                Vector3 p = RitualVortex.Drift(_center + d * dist, t, h, 0.06f);
                float size = Mathf.Lerp(0.08f, Mathf.Lerp(0.18f, 0.34f, h), outU) * Mathf.Lerp(1f, 0.5f, inU);
                Color col = Color.Lerp(_palette[i % _palette.Count], Color.white, 0.25f);
                float gain = _config.bloomFogIntensity * fogEnv;
                if (AddEyeSafe(p, size, col * gain, i + 2, eye)) FogCount++;
            }

            int motes = Mathf.Max(0, _config.bloomMotes);
            double spread = 0;
            float far2 = 0f;
            for (int i = 0; i < motes; i++)
            {
                float h = RitualUtil.Hash(i * 37 + 3);
                float h2 = RitualUtil.Hash(i * 41 + 9);
                float sign = h2 > 0.2f ? 1f : -1f;
                Vector3 dir = Dir(i, motes, 0.12f);
                float outR = reach * Mathf.Lerp(0.2f, 1f, Mathf.Sqrt(h));
                Vector3 start = _center + dir * 0.04f;
                float s = RitualVortex.Smooth01((u - 0.02f * h2) / 0.3f);
                float startIn = 0.5f + 0.18f * h;
                float q = RitualVortex.Smooth01((u - startIn) / (1f - startIn));
                Vector3 p;
                if (q <= 0f)
                {
                    p = RitualVortex.Spread(start, _center, dir, outR, s, sign, h);
                    p = RitualVortex.Drift(p, t, h, 0.05f * s);
                }
                else
                {
                    Vector3 outPos = RitualVortex.Drift(RitualVortex.OutPoint(_center, dir, outR, sign), t, h, 0.05f * (1f - q));
                    p = RitualVortex.Spiral(outPos, _center, q, sign, 1.25f, h);
                }
                float dist = Vector3.Distance(p, _center);
                spread += dist;
                if (dist > far2) far2 = dist;
                float twinkle = 0.7f + 0.3f * Mathf.Sin(t * (3f + 4f * h) + h * 20f);
                float env = RitualVortex.Smooth01(u / 0.04f) * (1f - 0.75f * q * q);
                Color col = Color.Lerp(_palette[i % _palette.Count], Color.white, 0.3f + 0.2f * h2);
                float size = Mathf.Lerp(0.004f, 0.011f, h2) * (1f + 0.6f * s * (1f - q));
                AddEyeSafe(p, size, col * (_config.climaxIntensity * 0.9f * env * twinkle), i + 50, eye);
            }
            MoteSpread = motes > 0 ? (float)(spread / motes) : 0f;
            Radius = far2;
        }

        bool AddEyeSafe(Vector3 center, float radius, Color color, int i, Vector3 eye)
        {
            float d = Vector3.Distance(center, eye);
            float keep = Mathf.Clamp01((d - radius * 1.5f - 0.1f) / Mathf.Max(0.05f, radius));
            if (keep <= 0.01f || color.maxColorComponent * keep < 0.002f) return false;
            Add(center, radius, color * keep, i);
            return true;
        }

        void Add(Vector3 center, float radius, Color color, int i)
        {
            int v = _pos.Count;
            var dataA = new Vector4(1f, 0f, radius, radius);
            var dataB = new Vector4(color.r, color.g, color.b, i * 0.61803f % 1f * 6.28f);
            var dataC = new Vector4(0f, 0f, 0.6f, 0f);
            for (int k = 0; k < 4; k++)
            {
                _pos.Add(center);
                _quad.Add(new Vector2(k == 1 || k == 2 ? 1f : -1f, k >= 2 ? 1f : -1f));
                _a.Add(dataA);
                _b.Add(dataB);
                _c.Add(dataC);
            }
            _idx.Add(v);
            _idx.Add(v + 2);
            _idx.Add(v + 1);
            _idx.Add(v);
            _idx.Add(v + 3);
            _idx.Add(v + 2);
        }

        public void Dispose()
        {
            if (_go != null) RitualUtil.Kill(_go);
            RitualUtil.Kill(_mesh);
            _go = null;
            _mesh = null;
            _renderer = null;
        }
    }
}
