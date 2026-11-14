using UnityEngine;

namespace MoodPicker.Ritual
{
    public sealed class RitualGuideTrails
    {
        public struct ArcSpec
        {
            public bool active;
            public Vector3 start;
            public Vector3 mid;
            public Vector3 end;
            public Color color;
            public float alpha;
            public float radius;
            public float speed;
        }

        struct StreamState
        {
            public bool init;
            public Vector3 start;
            public Vector3 mid;
            public Vector3 end;
            public Color color;
            public float alpha;
            public float radius;
            public float speed;
        }

        const int MotesPerStream = 48;
        const float GoldenRatio = 0.6180339887f;

        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int Arc0StartId = Shader.PropertyToID("_Arc0Start");
        static readonly int Arc0MidId = Shader.PropertyToID("_Arc0Mid");
        static readonly int Arc0EndId = Shader.PropertyToID("_Arc0End");
        static readonly int Arc0ColorId = Shader.PropertyToID("_Arc0Color");
        static readonly int Arc1StartId = Shader.PropertyToID("_Arc1Start");
        static readonly int Arc1MidId = Shader.PropertyToID("_Arc1Mid");
        static readonly int Arc1EndId = Shader.PropertyToID("_Arc1End");
        static readonly int Arc1ColorId = Shader.PropertyToID("_Arc1Color");

        public readonly Transform root;

        readonly TimeRitualConfig _config;
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        MeshRenderer _renderer;
        Mesh _mesh;
        StreamState _s0;
        StreamState _s1;
        float _masterAlpha;
        bool _active;

        public bool Visible => _active;
        public float MasterAlpha => _masterAlpha;
        public Vector3 Stream0Start => _s0.start;
        public Vector3 Stream0Mid => _s0.mid;
        public Vector3 Stream0End => _s0.end;
        public float Stream0Alpha => _s0.alpha * _masterAlpha;
        public Vector3 Stream1Start => _s1.start;
        public Vector3 Stream1Mid => _s1.mid;
        public Vector3 Stream1End => _s1.end;
        public float Stream1Alpha => _s1.alpha * _masterAlpha;

        public RitualGuideTrails(Transform parent, TimeRitualConfig config, Material glowMaterial)
        {
            _config = config;
            var go = new GameObject("GuideTrails");
            root = go.transform;
            root.SetParent(parent, false);
            _masterAlpha = config != null && config.guideTrails ? 1f : 0f;
            if (glowMaterial != null) BuildMesh(glowMaterial);
        }

        void BuildMesh(Material material)
        {
            var b = RitualStaticSplats.Builder.Create(MotesPerStream * 2);
            AddStreamSplats(ref b, 3f, 0);
            AddStreamSplats(ref b, 4f, 1009);
            _mesh = b.ToMesh("RitualGuideTrails", 2.5f);
            _renderer = RitualStaticSplats.Spawn("Streams", root, _mesh, material);
            _renderer.enabled = false;
        }

        static void AddStreamSplats(ref RitualStaticSplats.Builder b, float kind, int seedOffset)
        {
            for (int i = 0; i < MotesPerStream; i++)
            {
                int seed = seedOffset + i;
                float u0 = ((i + 0.5f) / MotesPerStream + RitualUtil.Hash(seed * 7 + 1) * (0.4f / MotesPerStream)) % 1f;
                float phase = ((i * GoldenRatio) % 1f) * 6.2831853f;
                bool streak = (i % 3) != 0;
                float rndA = RitualUtil.Hash(seed * 13 + 3);
                float rndB = RitualUtil.Hash(seed * 19 + 5);
                float halfLen = streak ? Mathf.Lerp(0.0085f, 0.015f, rndA) : Mathf.Lerp(0.0038f, 0.0065f, rndA);
                float halfWid = streak ? Mathf.Lerp(0.0018f, 0.0028f, rndB) : halfLen * 0.85f;
                float speedMul = Mathf.Lerp(0.78f, 1.24f, RitualUtil.Hash(seed * 23 + 7));
                float swirl = ((i & 1) == 0 ? 1f : -1f) * Mathf.Lerp(0.45f, 1.15f, rndB);
                float lum = Mathf.Lerp(0.68f, 1.16f, RitualUtil.Hash(seed * 29 + 11));
                b.Add(
                    Vector3.zero,
                    Vector3.up,
                    new Vector4(1f, 0f, halfLen, halfWid),
                    new Vector4(lum, 1f, 1f, phase),
                    new Vector4(u0, kind, speedMul, swirl));
            }
        }

        public void SetStreams(bool enabled, in ArcSpec primary, in ArcSpec secondary, float dt)
        {
            float masterGoal = enabled ? 1f : 0f;
            _masterAlpha = Mathf.MoveTowards(_masterAlpha, masterGoal, dt * 4.5f);

            StepStream(ref _s0, in primary, dt);
            StepStream(ref _s1, in secondary, dt);

            float peakAlpha = Mathf.Max(_s0.alpha, _s1.alpha) * _masterAlpha;
            _active = peakAlpha > 0.004f;

            if (_renderer == null) return;
            if (_renderer.enabled != _active) _renderer.enabled = _active;
            if (!_active) return;

            float gain = _config != null ? _config.guideIntensity : 0.85f;
            _block.SetFloat(IntensityId, gain);
            PushStream(Arc0StartId, Arc0MidId, Arc0EndId, Arc0ColorId, in _s0);
            PushStream(Arc1StartId, Arc1MidId, Arc1EndId, Arc1ColorId, in _s1);
            _renderer.SetPropertyBlock(_block);
        }

        static void StepStream(ref StreamState state, in ArcSpec spec, float dt)
        {
            float targetAlpha = spec.active ? Mathf.Clamp01(spec.alpha) : 0f;
            if (!spec.active)
            {
                state.alpha = Mathf.MoveTowards(state.alpha, 0f, dt * 5f);
                return;
            }

            bool snap = !state.init || state.alpha < 0.02f;
            state.init = true;
            if (snap)
            {
                state.start = spec.start;
                state.mid = spec.mid;
                state.end = spec.end;
                state.color = spec.color;
                state.radius = Mathf.Max(0.001f, spec.radius);
                state.speed = Mathf.Max(0.05f, spec.speed);
            }
            else
            {
                float k = 1f - Mathf.Exp(-dt * 14f);
                state.start = Vector3.Lerp(state.start, spec.start, k);
                state.mid = Vector3.Lerp(state.mid, spec.mid, k);
                state.end = Vector3.Lerp(state.end, spec.end, k);
                state.color = Color.Lerp(state.color, spec.color, k);
                state.radius = Mathf.Lerp(state.radius, Mathf.Max(0.001f, spec.radius), k);
                state.speed = Mathf.Lerp(state.speed, Mathf.Max(0.05f, spec.speed), k);
            }
            state.alpha = Mathf.MoveTowards(state.alpha, targetAlpha, dt * 4.5f);
        }

        void PushStream(int startId, int midId, int endId, int colId, in StreamState s)
        {
            Vector3 lStart = root.InverseTransformPoint(s.start);
            Vector3 lMid = root.InverseTransformPoint(s.mid);
            Vector3 lEnd = root.InverseTransformPoint(s.end);
            _block.SetVector(startId, new Vector4(lStart.x, lStart.y, lStart.z, s.alpha * _masterAlpha));
            _block.SetVector(midId, new Vector4(lMid.x, lMid.y, lMid.z, s.radius));
            _block.SetVector(endId, new Vector4(lEnd.x, lEnd.y, lEnd.z, s.speed));
            _block.SetVector(colId, new Vector4(s.color.r, s.color.g, s.color.b, 1f));
        }

        public void Dispose()
        {
            if (_renderer != null) RitualUtil.Kill(_renderer.gameObject);
            _renderer = null;
            RitualUtil.Kill(_mesh);
            _mesh = null;
            if (root != null) RitualUtil.Kill(root.gameObject);
        }
    }
}
