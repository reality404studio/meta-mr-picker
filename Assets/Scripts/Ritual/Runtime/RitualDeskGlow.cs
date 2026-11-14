using System.Collections.Generic;
using UnityEngine;

namespace MoodPicker.Ritual
{
    public sealed class RitualDeskGlow
    {
        const float GoldenAngle = 2.39996323f;
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int RevealId = Shader.PropertyToID("_Reveal");
        static readonly int BreathId = Shader.PropertyToID("_Breath");

        public readonly Transform root;

        readonly TimeRitualConfig _config;
        readonly Material _material;
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        MeshRenderer _renderer;
        Mesh _mesh;
        float _reveal;
        float _lift;
        float _pushedReveal = -1f;
        float _pushedIntensity = -1f;
        bool _blooming;

        public bool Blooming => _blooming;
        public float Reveal => _reveal;

        public RitualDeskGlow(Transform parent, TimeRitualConfig config, Material material)
        {
            _config = config;
            _material = material;
            var go = new GameObject("DeskGlow");
            root = go.transform;
            root.SetParent(parent, false);
        }

        public void Bloom(Vector3 deskPoint, Quaternion yaw, IReadOnlyList<Color> colors, IReadOnlyList<int> units, Color dominant)
        {
            if (_material == null || colors == null || colors.Count == 0) return;
            root.SetPositionAndRotation(deskPoint + Vector3.up * 0.0015f, yaw);
            if (_renderer != null) RitualUtil.Kill(_renderer.gameObject);
            RitualUtil.Kill(_mesh);

            int total = 0;
            for (int i = 0; i < units.Count; i++) total += Mathf.Max(1, units[i]);
            bool seep = _config.glowStyle == GlowStyle.Seep;
            int petals = seep ? Mathf.Max(8, _config.glowSeepSplats) : Mathf.Max(8, _config.glowPetals);
            int motes = Mathf.Max(0, seep ? _config.glowSeepMotes : _config.glowMotes);
            float reach = _config.glowRadius;
            float foot = _config.jar.bodyRadius;
            var b = RitualStaticSplats.Builder.Create(petals + motes + 4);

            Color warm = Color.Lerp(dominant, new Color(1f, 0.86f, 0.66f, 1f), 0.45f);
            AddSplat(ref b, Vector2.zero, new Vector2(1f, 0f), reach * 0.95f, reach * 0.95f, warm * 0.32f, 0.3f, 0f, 0f, 0.55f, 0f);
            AddSplat(ref b, new Vector2(0.02f, -0.015f), new Vector2(1f, 0f), reach * 0.6f, reach * 0.52f, dominant * 0.38f, 1.9f, 0f, 0f, 0.4f, 0f);
            AddSplat(ref b, Vector2.zero, new Vector2(1f, 0f), foot * 1.9f, foot * 1.9f, warm * 0.5f, 3.1f, 0f, 0f, 0.7f, 0f);

            if (seep) AddSeep(ref b, colors, units, total, petals, foot, reach);
            else AddPetals(ref b, colors, units, total, petals, foot, reach);

            for (int i = 0; i < motes; i++)
            {
                float angle = i * GoldenAngle * 1.7f;
                float dist = foot * 1.05f + (reach * 0.85f - foot) * Mathf.Sqrt(RitualUtil.Hash(i * 31 + 8));
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                int layer = Mathf.Min(colors.Count - 1, (int)(RitualUtil.Hash(i * 37 + 9) * colors.Count));
                Color c = Color.Lerp(colors[layer], Color.white, 0.35f) * 0.9f;
                float size = Mathf.Lerp(0.0025f, 0.006f, RitualUtil.Hash(i * 41 + 10));
                AddSplat(ref b, dir * dist, dir, size, size, c, RitualUtil.Hash(i * 43 + 11) * 6.28f, dist, 2f, Mathf.Lerp(0.5f, 1.3f, RitualUtil.Hash(i * 47 + 12)), 0.4f);
            }
            _mesh = b.ToMesh("DeskGlow", reach * 1.5f);
            _renderer = RitualStaticSplats.Spawn("Glow", root, _mesh, _material);
            _reveal = 0f;
            _pushedReveal = -1f;
            _blooming = true;
        }

        void AddSeep(ref RitualStaticSplats.Builder b, IReadOnlyList<Color> colors, IReadOnlyList<int> units, int total, int count, float foot, float reach)
        {
            float inner = foot * 1.05f;
            for (int i = 0; i < count; i++)
            {
                float pick = (i + 0.5f) / count * total;
                int layer = units.Count - 1;
                float below = 0f;
                float acc = 0f;
                for (int k = 0; k < units.Count; k++)
                {
                    float u = Mathf.Max(1, units[k]);
                    if (pick <= acc + u)
                    {
                        layer = k;
                        below = acc;
                        break;
                    }
                    acc += u;
                }
                float height = (below + Mathf.Max(1, units[layer]) * 0.5f) / total;
                float far = Mathf.Clamp01((0.12f + 0.88f * height) * Mathf.Lerp(0.88f, 1.12f, RitualUtil.Hash(i * 11 + 2)));
                float dist = inner + (reach - inner) * far;
                float angle = i * GoldenAngle + RitualUtil.Hash(i * 7 + 1) * 0.6f;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 along = new Vector2(-dir.y, dir.x);
                float length = Mathf.Lerp(0.04f, 0.07f, RitualUtil.Hash(i * 13 + 3)) * (0.8f + 0.45f * far);
                float width = length * Mathf.Lerp(0.65f, 0.95f, RitualUtil.Hash(i * 17 + 4));
                Color c = colors[layer] * (_config.glowSeepGain * Mathf.Lerp(0.75f, 1.05f, RitualUtil.Hash(i * 19 + 5)) * (1.2f - 0.85f * far));
                float swirl = (i & 1) == 0 ? 0.6f : -0.45f;
                AddSplat(ref b, dir * dist, along, length, width, c, RitualUtil.Hash(i * 23 + 6) * 6.28f, dist * 0.85f, 1f, Mathf.Lerp(0.25f, 0.6f, RitualUtil.Hash(i * 29 + 7)), swirl);
            }
        }

        void AddPetals(ref RitualStaticSplats.Builder b, IReadOnlyList<Color> colors, IReadOnlyList<int> units, int total, int petals, float foot, float reach)
        {
            for (int i = 0; i < petals; i++)
            {
                float pick = (i + 0.5f) / petals * total;
                int layer = 0;
                float acc = 0f;
                for (int k = 0; k < units.Count; k++)
                {
                    acc += Mathf.Max(1, units[k]);
                    if (pick <= acc)
                    {
                        layer = k;
                        break;
                    }
                }
                float angle = i * GoldenAngle + RitualUtil.Hash(i * 7 + 1) * 0.5f;
                float far = Mathf.Lerp(0.25f, 1f, Mathf.Pow(RitualUtil.Hash(i * 11 + 2), 0.7f));
                float dist = foot * 0.9f + (reach - foot) * far * 0.8f;
                float length = Mathf.Lerp(0.018f, 0.055f, RitualUtil.Hash(i * 13 + 3)) * (1.2f - 0.5f * far);
                float width = length * Mathf.Lerp(0.16f, 0.34f, RitualUtil.Hash(i * 17 + 4));
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Color c = colors[layer] * Mathf.Lerp(0.5f, 1.05f, RitualUtil.Hash(i * 19 + 5)) * (1.1f - 0.6f * far);
                float swirl = (i & 1) == 0 ? 1f : -0.7f;
                AddSplat(ref b, dir * dist, dir, length, width, c, RitualUtil.Hash(i * 23 + 6) * 6.28f, dist, 1f, Mathf.Lerp(0.35f, 0.9f, RitualUtil.Hash(i * 29 + 7)), swirl);
            }
        }

        static void AddSplat(ref RitualStaticSplats.Builder b, Vector2 at, Vector2 axis, float halfLength, float halfWidth, Color color, float phase, float dist, float kind, float speed, float swirl)
        {
            b.Add(new Vector3(at.x, 0f, at.y), Vector3.up, new Vector4(axis.x, axis.y, halfLength, halfWidth), new Vector4(color.r, color.g, color.b, phase), new Vector4(dist, kind, speed, swirl));
        }

        public void SetLift(float metres)
        {
            _lift = Mathf.Max(0f, metres);
        }

        public void Tick(float dt)
        {
            if (!_blooming || _renderer == null) return;
            float reach = _config.glowRadius * 1.15f;
            _reveal = Mathf.MoveTowards(_reveal, reach, dt * reach / Mathf.Max(0.1f, _config.glowBloomSeconds));
            float intensity = _config.glowIntensity * Mathf.Clamp01(1f - _lift / 0.12f);
            if (Mathf.Abs(_reveal - _pushedReveal) < 1e-5f && Mathf.Abs(intensity - _pushedIntensity) < 1e-4f) return;
            _pushedReveal = _reveal;
            _pushedIntensity = intensity;
            _block.SetFloat(RevealId, _reveal);
            _block.SetFloat(IntensityId, intensity);
            _block.SetFloat(BreathId, 0.14f);
            _renderer.SetPropertyBlock(_block);
        }

        public void Clear()
        {
            _blooming = false;
            if (_renderer != null) RitualUtil.Kill(_renderer.gameObject);
            _renderer = null;
            RitualUtil.Kill(_mesh);
            _mesh = null;
        }

        public void Dispose()
        {
            Clear();
            if (root != null) RitualUtil.Kill(root.gameObject);
        }
    }
}
