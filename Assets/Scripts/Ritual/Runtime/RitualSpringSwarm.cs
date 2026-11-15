using MoodPicker.Splatter;
using Unity.Mathematics;
using UnityEngine;

namespace MoodPicker.Ritual
{
    public sealed class RitualSpringSwarm
    {
        readonly Transform _owner;
        readonly TimeRitualConfig _config;
        readonly RitualSplatCloud _cloud;
        readonly RitualSurface _surface;
        readonly Vector3[] _u;
        readonly Vector3[] _v;
        readonly float[] _radius;
        readonly float[] _omega;
        readonly float[] _phase;
        readonly float[] _hash;
        Vector3 _lean;
        float _maxReach;

        public int Count => _cloud != null ? _cloud.Count : 0;
        public float MaxReach => _maxReach;
        public RitualSplatCloud Cloud => _cloud;

        public RitualSpringSwarm(Transform owner, TimeRitualConfig config, SplatTuning tuning, Material material, SplatSurfaceMaterial look, TimeCategoryAsset category)
        {
            _owner = owner;
            _config = config;
            int n = Mathf.Clamp(config.springSplats, 32, 4000);
            _cloud = new RitualSplatCloud("Swarm " + (category != null ? category.id : ""), owner, material, n, 0.2f);
            _u = new Vector3[n];
            _v = new Vector3[n];
            _radius = new float[n];
            _omega = new float[n];
            _phase = new float[n];
            _hash = new float[n];
            float half = Mathf.Max(0.0008f, config.springGrain);
            for (int i = 0; i < n; i++)
            {
                float h0 = RitualUtil.Hash(i * 31 + 11);
                float h1 = RitualUtil.Hash(i * 37 + 5);
                float h2 = RitualUtil.Hash(i * 41 + 17);
                float h3 = RitualUtil.Hash(i * 43 + 23);
                float z = 1f - 2f * h0;
                float rr = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                float az = h1 * 6.2831853f;
                Vector3 axis = new Vector3(rr * Mathf.Cos(az), z, rr * Mathf.Sin(az));
                Vector3 refAxis = Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right;
                _u[i] = Vector3.Cross(axis, refAxis).normalized;
                _v[i] = Vector3.Cross(axis, _u[i]).normalized;
                _radius[i] = 0.3f + 0.7f * Mathf.Sqrt(h2);
                _omega[i] = (0.55f + 0.9f * h3) * (h0 > 0.35f ? 1f : -1f);
                _phase[i] = h1 * 6.2831853f + h2 * 3.1f;
                _hash[i] = h3;
                int s = _cloud.Add(float3.zero, (float3)axis, half * Mathf.Lerp(0.7f, 1.25f, h2));
                if (s >= 0)
                {
                    _cloud.scale[s] = 1f;
                    _cloud.glow[s] = 0f;
                }
            }
            _surface = new RitualSurface
            {
                material = look,
                look = TimeRitualLook.Sand(tuning, category, true, 1f),
                sizeMeters = config.sandTextureSize,
                triplanarScale = 1f,
                intensity = category != null ? category.sandIntensity * 1.1f : 1.2f,
                aspectMax = config.scatterAspectMax
            };
        }

        public void Tick(float now, float dt, float presence, float pulse, float hover, float spinRate, Vector3 leanWS, float invite)
        {
            if (_cloud == null || _cloud.Disposed) return;
            bool visible = presence > 0.002f;
            if (_cloud.renderer.enabled != visible) _cloud.renderer.enabled = visible;
            if (!visible) return;
            float baseR = _config.heapDiameter * 0.5f * _config.springLoose;
            float boost = 1f + Mathf.Min(3f, Mathf.Abs(spinRate) * 0.15f) + pulse * 0.6f;
            float spin = _config.springSpin * boost;
            float breathe = 0.5f + 0.5f * Mathf.Sin(now * 2.2f);
            _lean = Vector3.Lerp(_lean, leanWS * (invite * (0.55f + 0.45f * breathe)), 1f - Mathf.Exp(-dt * 4f));
            float contract = 1f - 0.18f * hover - 0.1f * invite * breathe;
            float glowBase = 0.12f + 0.35f * pulse + 0.25f * hover + 0.45f * invite * breathe;
            float3[] disp = _cloud.disp;
            float3[] vel = _cloud.vel;
            float[] glow = _cloud.glow;
            float reach = 0f;
            int n = _cloud.Count;
            for (int i = 0; i < n; i++)
            {
                float w = _omega[i] * spin;
                _phase[i] += w * dt;
                float ph = _phase[i];
                float wobble = 1f + 0.14f * Mathf.Sin(now * (0.6f + _hash[i]) + _hash[i] * 6.2831853f);
                float r = _radius[i] * baseR * wobble * contract;
                float c = Mathf.Cos(ph), s = Mathf.Sin(ph);
                Vector3 local = (_u[i] * c + _v[i] * s) * r;
                local.y += 0.18f * r * Mathf.Sin(ph * 2f + _hash[i] * 4f);
                Vector3 world = _owner.TransformVector(local) + _lean * (0.4f + 0.6f * _radius[i]);
                Vector3 tangent = _owner.TransformVector((_v[i] * c - _u[i] * s) * (r * w));
                disp[i] = (float3)world;
                vel[i] = (float3)tangent;
                glow[i] = Mathf.Min(1f, glowBase * (0.6f + 0.8f * _hash[i]));
                float m = world.magnitude;
                if (m > reach) reach = m;
            }
            _maxReach = reach;
            _cloud.MarkSim();
            _cloud.Flush();
            _surface.Apply(_cloud.renderer, _cloud.SimTexture, now, 1f + pulse * 0.4f + invite * 0.35f * breathe);
        }

        public void Dispose()
        {
            _cloud?.Dispose();
        }
    }
}
