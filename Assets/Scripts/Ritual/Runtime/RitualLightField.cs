using System.Collections.Generic;
using UnityEngine;

namespace MoodPicker.Ritual
{
    public sealed class RitualLightField
    {
        public struct Source
        {
            public Vector3 center;
            public Vector3 axis;
            public float length;
            public float radius;
            public Color color;
            public float weight;
            public bool halo;
        }

        const int MaxSplats = 160;
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int RevealId = Shader.PropertyToID("_Reveal");
        static readonly int BreathId = Shader.PropertyToID("_Breath");
        static readonly int SwirlId = Shader.PropertyToID("_Swirl");
        static readonly int FlatId = Shader.PropertyToID("_Flat");

        readonly TimeRitualConfig _config;
        readonly Material _material;
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        readonly List<Source> _sources = new List<Source>(32);
        readonly List<Vector3> _pos = new List<Vector3>(MaxSplats * 4);
        readonly List<Vector2> _quad = new List<Vector2>(MaxSplats * 4);
        readonly List<Vector4> _a = new List<Vector4>(MaxSplats * 4);
        readonly List<Vector4> _b = new List<Vector4>(MaxSplats * 4);
        readonly List<Vector4> _c = new List<Vector4>(MaxSplats * 4);
        readonly List<int> _idx = new List<int>(MaxSplats * 6);
        GameObject _go;
        Mesh _mesh;
        MeshRenderer _renderer;
        int _count;
        int _halos;
        int _handGlows;

        public int SplatCount => _count;
        public int HaloSplats => _halos;
        public int HandSplats => _handGlows;
        public bool Visible => _renderer != null && _renderer.enabled;
        public IReadOnlyList<Source> Sources => _sources;

        public RitualLightField(TimeRitualConfig config, Material material)
        {
            _config = config;
            _material = material;
            if (_material == null) return;
            _go = new GameObject("RitualLightField");
            _go.hideFlags = HideFlags.DontSave;
            _mesh = new Mesh { name = "RitualLightField", hideFlags = HideFlags.DontSave };
            _mesh.MarkDynamic();
            _go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = _go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            RitualUtil.Quiet(_renderer);
            _renderer.enabled = false;
        }

        public void BeginFrame()
        {
            _sources.Clear();
        }

        public void AddSource(Vector3 center, Vector3 axis, float length, float radius, Color color, float weight, bool halo = true)
        {
            if (weight <= 0.001f || radius <= 1e-4f) return;
            _sources.Add(new Source { center = center, axis = axis, length = length, radius = radius, color = color, weight = weight, halo = halo });
        }

        public static float DistanceTo(in Source s, Vector3 p)
        {
            if (s.length <= 1e-4f) return Mathf.Max(0f, Vector3.Distance(p, s.center) - s.radius);
            Vector3 a = s.center - s.axis * (s.length * 0.5f);
            float t = Mathf.Clamp(Vector3.Dot(p - a, s.axis), 0f, s.length);
            return Mathf.Max(0f, Vector3.Distance(p, a + s.axis * t) - s.radius);
        }

        public void EndFrame(Vector3 eye, RitualHands hands)
        {
            if (_renderer == null) return;
            _pos.Clear();
            _quad.Clear();
            _a.Clear();
            _b.Clear();
            _c.Clear();
            _idx.Clear();
            _count = 0;
            _halos = 0;
            _handGlows = 0;

            if (_config.halos && _config.haloIntensity > 0.001f)
            {
                for (int i = 0; i < _sources.Count && _count < MaxSplats - 24; i++)
                {
                    Source s = _sources[i];
                    if (!s.halo) continue;
                    Vector3 away = s.center - eye;
                    Vector3 back = away.sqrMagnitude > 1e-6f ? away.normalized * (s.radius * _config.haloBehind) : Vector3.zero;
                    int steps = s.length > 1e-4f ? Mathf.Clamp(Mathf.CeilToInt(s.length / Mathf.Max(0.02f, s.radius * 2.5f)), 1, 6) : 1;
                    for (int k = 0; k < steps; k++)
                    {
                        float u = steps == 1 ? 0.5f : k / (float)(steps - 1);
                        Vector3 at = s.length > 1e-4f ? s.center + s.axis * ((u - 0.5f) * s.length) : s.center;
                        float share = s.weight / Mathf.Sqrt(steps);
                        Add(at + back, s.radius * _config.haloCore, s.color * (0.55f * share));
                        Add(at + back * 1.4f, s.radius * _config.haloWide, s.color * (0.2f * share));
                        _halos += 2;
                    }
                }
            }

            if (_config.handLight && hands != null && _config.handLightIntensity > 0.001f)
            {
                for (int h = 0; h < 2; h++)
                {
                    ref RitualHand hand = ref hands[h];
                    if (!hand.tracked) continue;
                    int best = -1;
                    float bestD = _config.handLightRange;
                    for (int i = 0; i < _sources.Count; i++)
                    {
                        float d = DistanceTo(_sources[i], hand.palm);
                        d = Mathf.Min(d, DistanceTo(_sources[i], hand.index));
                        d = Mathf.Min(d, DistanceTo(_sources[i], hand.thumb));
                        if (d < bestD)
                        {
                            bestD = d;
                            best = i;
                        }
                    }
                    if (best < 0) continue;
                    Source s = _sources[best];
                    float near = 1f - Mathf.Clamp01(bestD / Mathf.Max(1e-3f, _config.handLightRange));
                    near *= near;
                    Color c = Color.Lerp(s.color, Color.white, 0.18f) * (near * s.weight);
                    HandGlow(hand.thumb, s, eye, c, 1f);
                    HandGlow(hand.index, s, eye, c, 1f);
                    HandGlow(hand.middle, s, eye, c, 0.8f);
                    HandGlow(hand.ring, s, eye, c, 0.6f);
                    HandGlow(hand.little, s, eye, c, 0.5f);
                    HandGlow(hand.palm, s, eye, c * 0.7f, 1.8f);
                }
            }

            bool show = _count > 0;
            if (_renderer.enabled != show) _renderer.enabled = show;
            if (!show) return;
            _mesh.Clear(false);
            _mesh.SetVertices(_pos);
            _mesh.SetUVs(0, _quad);
            _mesh.SetUVs(1, _a);
            _mesh.SetUVs(2, _b);
            _mesh.SetUVs(3, _c);
            _mesh.SetTriangles(_idx, 0, false);
            _mesh.bounds = new Bounds(eye, Vector3.one * 6f);
            _block.SetFloat(IntensityId, _config.haloIntensity);
            _block.SetFloat(RevealId, 10f);
            _block.SetFloat(BreathId, 0.08f);
            _block.SetFloat(SwirlId, 0f);
            _block.SetFloat(FlatId, 0f);
            _renderer.SetPropertyBlock(_block);
        }

        void HandGlow(Vector3 joint, in Source s, Vector3 eye, Color c, float size)
        {
            if (_count >= MaxSplats) return;
            Vector3 toward = s.center - joint;
            Vector3 lit = toward.sqrMagnitude > 1e-8f ? toward.normalized * 0.004f : Vector3.zero;
            Vector3 front = eye - joint;
            Vector3 lift = front.sqrMagnitude > 1e-8f ? front.normalized * 0.006f : Vector3.zero;
            float gain = _config.handLightIntensity / Mathf.Max(0.05f, _config.haloIntensity);
            Add(joint + lit + lift, _config.handLightSize * size, c * gain);
            _handGlows++;
        }

        void Add(Vector3 center, float radius, Color color)
        {
            if (_count >= MaxSplats) return;
            int v = _pos.Count;
            var dataA = new Vector4(1f, 0f, radius, radius);
            var dataB = new Vector4(color.r, color.g, color.b, (_count * 0.61803f) % 1f * 6.28f);
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
            _count++;
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
