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
        static readonly int SwirlId = Shader.PropertyToID("_Swirl");

        public readonly Transform root;

        readonly TimeRitualConfig _config;
        readonly Material _material;
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        MeshRenderer _renderer;
        Mesh _mesh;
        float _reveal;
        float _spinBoost;
        float _boost;
        float _pushedReveal = -1f;
        float _pushedIntensity = -1f;
        float _pushedSwirl = -1f;
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

        public void Bloom(Vector3 jarPos, Quaternion jarRot, IReadOnlyList<Color> colors, IReadOnlyList<int> units, Color dominant)
        {
            if (_material == null || colors == null || colors.Count == 0) return;
            root.SetPositionAndRotation(jarPos, jarRot);
            if (_renderer != null) RitualUtil.Kill(_renderer.gameObject);
            RitualUtil.Kill(_mesh);

            int total = 0;
            for (int i = 0; i < units.Count; i++) total += Mathf.Max(1, units[i]);
            bool seep = _config.glowStyle == GlowStyle.Seep;
            int shells = seep ? Mathf.Max(8, _config.glowSeepSplats) : Mathf.Max(8, _config.glowPetals);
            int motes = Mathf.Max(12, seep ? _config.glowSeepMotes + 12 : _config.glowMotes);
            float reach = _config.glowRadius;
            float foot = _config.jar.bodyRadius;
            var b = RitualStaticSplats.Builder.Create(shells + motes + 4);

            Color warm = Color.Lerp(dominant, new Color(1f, 0.86f, 0.66f, 1f), 0.45f);
            AddSplat3D(ref b, new Vector3(0f, 0.045f, 0f), new Vector2(1f, 0f), foot * 1.15f, foot * 1.05f, warm * 0.22f, 0.4f, 0f, 0f, 0.45f, 0f);
            AddSplat3D(ref b, new Vector3(0f, 0.092f, 0f), new Vector2(1f, 0f), foot * 1.18f, foot * 1.08f, dominant * 0.24f, 1.9f, 0f, 0f, 0.40f, 0f);
            AddSplat3D(ref b, new Vector3(0f, 0.138f, 0f), new Vector2(1f, 0f), foot * 0.98f, foot * 0.92f, warm * 0.20f, 3.3f, 0f, 0f, 0.50f, 0f);

            AddAuraCloud(ref b, colors, units, total, shells);

            float maxShell = 0.054f;
            for (int i = 0; i < motes; i++)
            {
                float hRand = (i + 0.5f) / motes;
                float y = Mathf.Lerp(0.008f, _config.jar.height + 0.026f, hRand);
                float wallR = _config.jar.OuterRadius(Mathf.Clamp(y, 0f, _config.jar.height));
                float shell = Mathf.Lerp(0.012f, maxShell, Mathf.Sqrt(RitualUtil.Hash(i * 31 + 8)));
                float r = wallR + shell;
                float angle = i * GoldenAngle * 1.7f;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                int layer = LayerAt(units, hRand * total, out float _);
                layer = Mathf.Clamp(layer, 0, colors.Count - 1);
                Color c = Color.Lerp(colors[layer], Color.white, 0.38f) * 0.95f;
                if (pos.z < -wallR * 0.55f && y > 0.025f && y < 0.145f) c *= 0.35f;

                float size = Mathf.Lerp(0.0022f, 0.0052f, RitualUtil.Hash(i * 41 + 10));
                float swirl = ((i & 1) == 0 ? 1f : -0.85f) * Mathf.Lerp(0.8f, 1.45f, RitualUtil.Hash(i * 43 + 11));
                AddSplat3D(ref b, pos, dir, size, size, c, RitualUtil.Hash(i * 43 + 11) * 6.28f, shell * 3.5f, 2f, Mathf.Lerp(0.55f, 1.35f, RitualUtil.Hash(i * 47 + 12)), swirl);
            }

            _mesh = b.ToMesh("VesselAura", reach * 1.5f);
            _renderer = RitualStaticSplats.Spawn("Aura", root, _mesh, _material);
            _reveal = 0f;
            _pushedReveal = -1f;
            _pushedSwirl = -1f;
            _spinBoost = 0f;
            _blooming = true;
        }

        void AddAuraCloud(ref RitualStaticSplats.Builder b, IReadOnlyList<Color> colors, IReadOnlyList<int> units, int total, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float pick = (i + 0.5f) / count * total;
                int layer = LayerAt(units, pick, out float below);
                float height = (below + Mathf.Max(1, units[layer]) * 0.5f) / total;
                float y = Mathf.Lerp(_config.jar.floor + 0.008f, _config.jar.bodyTop + 0.014f, height)
                    + (RitualUtil.Hash(i * 31 + 4) - 0.5f) * 0.018f;
                float wallR = _config.jar.OuterRadius(Mathf.Clamp(y, 0f, _config.jar.height));
                float shell = Mathf.Lerp(0.008f, 0.038f, RitualUtil.Hash(i * 11 + 2));
                float r = wallR + shell;
                float angle = i * GoldenAngle + RitualUtil.Hash(i * 7 + 1) * 0.55f;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
                Vector2 tangent = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));

                float length = Mathf.Lerp(0.018f, 0.034f, RitualUtil.Hash(i * 13 + 3));
                float width = length * Mathf.Lerp(0.75f, 0.98f, RitualUtil.Hash(i * 17 + 4));
                float gain = _config.glowStyle == GlowStyle.Seep ? _config.glowSeepGain * 1.35f : 0.42f;
                Color c = colors[layer] * (gain * Mathf.Lerp(0.75f, 1.08f, RitualUtil.Hash(i * 19 + 5)));
                if (pos.z < -wallR * 0.55f && y > 0.025f && y < 0.145f) c *= 0.25f;

                float swirl = (i & 1) == 0 ? 0.75f : -0.55f;
                AddSplat3D(ref b, pos, tangent, length, width, c, RitualUtil.Hash(i * 23 + 6) * 6.28f, shell * 3.5f, 1f, Mathf.Lerp(0.28f, 0.65f, RitualUtil.Hash(i * 29 + 7)), swirl);
            }
        }

        static int LayerAt(IReadOnlyList<int> units, float pick, out float below)
        {
            int layer = units.Count - 1;
            below = 0f;
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
            return layer;
        }

        static void AddSplat3D(ref RitualStaticSplats.Builder b, Vector3 pos, Vector2 axis, float halfLength, float halfWidth, Color color, float phase, float dist, float kind, float speed, float swirl)
        {
            b.Add(pos, Vector3.up, new Vector4(axis.x, axis.y, halfLength, halfWidth), new Vector4(color.r, color.g, color.b, phase), new Vector4(dist, kind, speed, swirl));
        }

        public void SetLift(float metres)
        {
        }

        public void SetBoost(float boost)
        {
            _boost = Mathf.Max(0f, boost);
        }

        public void TrackVessel(Vector3 jarPos, Quaternion jarRot, float angularSpeed, float dt)
        {
            if (!_blooming) return;
            root.position = jarPos;
            root.rotation = Quaternion.Slerp(root.rotation, jarRot, 1f - Mathf.Exp(-dt * 8f));
            _spinBoost = Mathf.MoveTowards(_spinBoost, Mathf.Clamp01(angularSpeed / 3.0f), dt * 3.5f);
        }

        public void Tick(float dt)
        {
            if (!_blooming || _renderer == null) return;
            float reach = _config.glowRadius * 1.15f;
            _reveal = Mathf.MoveTowards(_reveal, reach, dt * reach / Mathf.Max(0.1f, _config.glowBloomSeconds));
            float intensity = _config.glowIntensity * (1f + 0.38f * _spinBoost) * (1f + 1.6f * _boost);
            float swirl = 0.035f + 0.16f * _spinBoost;
            if (Mathf.Abs(_reveal - _pushedReveal) < 1e-5f
                && Mathf.Abs(intensity - _pushedIntensity) < 1e-4f
                && Mathf.Abs(swirl - _pushedSwirl) < 1e-4f) return;
            _pushedReveal = _reveal;
            _pushedIntensity = intensity;
            _pushedSwirl = swirl;
            _block.SetFloat(RevealId, _reveal);
            _block.SetFloat(IntensityId, intensity);
            _block.SetFloat(BreathId, 0.14f);
            _block.SetFloat(SwirlId, swirl);
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
