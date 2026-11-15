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
        readonly SwarmMotion _motion;
        readonly Vector3[] _u;
        readonly Vector3[] _v;
        readonly Vector3[] _cu;
        readonly Vector3[] _cv;
        readonly float[] _radius;
        readonly float[] _omega;
        readonly float[] _phase;
        readonly float[] _phase0;
        readonly float[] _hash;
        readonly float[] _hash2;
        readonly Vector3[] _freq;
        Vector3 _lean;
        float _clock;
        float _maxReach;
        bool _primed;

        public int Count => _cloud != null ? _cloud.Count : 0;
        public float MaxReach => _maxReach;
        public RitualSplatCloud Cloud => _cloud;
        public SwarmMotion Motion => _motion;

        public RitualSpringSwarm(Transform owner, TimeRitualConfig config, SplatTuning tuning, Material material, SplatSurfaceMaterial look, TimeCategoryAsset category)
        {
            _owner = owner;
            _config = config;
            _motion = category != null ? category.swarmMotion : SwarmMotion.Orbit;
            int n = Mathf.Clamp(config.springSplats, 32, 4000);
            _cloud = new RitualSplatCloud("Swarm " + (category != null ? category.id : ""), owner, material, n, 0.2f);
            _u = new Vector3[n];
            _v = new Vector3[n];
            _cu = new Vector3[n];
            _cv = new Vector3[n];
            _radius = new float[n];
            _omega = new float[n];
            _phase = new float[n];
            _phase0 = new float[n];
            _hash = new float[n];
            _hash2 = new float[n];
            _freq = new Vector3[n];
            float half = Mathf.Max(0.0008f, config.springGrain);
            Vector3 common = new Vector3(0.22f, 1f, 0.12f).normalized;
            for (int i = 0; i < n; i++)
            {
                float h0 = RitualUtil.Hash(i * 31 + 11);
                float h1 = RitualUtil.Hash(i * 37 + 5);
                float h2 = RitualUtil.Hash(i * 41 + 17);
                float h3 = RitualUtil.Hash(i * 43 + 23);
                float h4 = RitualUtil.Hash(i * 47 + 29);
                float z = 1f - 2f * h0;
                float rr = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                float az = h1 * 6.2831853f;
                Vector3 axis = new Vector3(rr * Mathf.Cos(az), z, rr * Mathf.Sin(az));
                Vector3 refAxis = Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right;
                _u[i] = Vector3.Cross(axis, refAxis).normalized;
                _v[i] = Vector3.Cross(axis, _u[i]).normalized;
                Vector3 tilted = (common + (axis - common * Vector3.Dot(axis, common)) * 0.22f).normalized;
                Vector3 cref = Mathf.Abs(tilted.y) < 0.9f ? Vector3.up : Vector3.right;
                _cu[i] = Vector3.Cross(tilted, cref).normalized;
                _cv[i] = Vector3.Cross(tilted, _cu[i]).normalized;
                _radius[i] = 0.3f + 0.7f * Mathf.Sqrt(h2);
                _omega[i] = (0.55f + 0.9f * h3) * (h0 > 0.35f ? 1f : -1f);
                _phase[i] = h1 * 6.2831853f + h2 * 3.1f;
                _phase0[i] = _phase[i];
                _hash[i] = h3;
                _hash2[i] = h4;
                _freq[i] = new Vector3(0.22f + 0.4f * h1, 0.18f + 0.36f * h3, 0.2f + 0.38f * h4);
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

        static float Frac(float x) => x - Mathf.Floor(x);

        static float Fade(float u)
        {
            float a = Mathf.Clamp01(u / 0.14f);
            float b = Mathf.Clamp01((1f - u) / 0.14f);
            return a * a * (3f - 2f * a) * b * b * (3f - 2f * b);
        }

        Vector3 Shaped(int i, float R, float t, out float fade)
        {
            fade = 1f;
            float h = _hash[i];
            switch (_motion)
            {
                case SwarmMotion.Orbit:
                {
                    float r = _radius[i] * R * 0.82f;
                    float a = _phase0[i] + t * 1.9f * (0.8f + 0.4f * h);
                    return (_cu[i] * Mathf.Cos(a) + _cv[i] * Mathf.Sin(a)) * r;
                }
                case SwarmMotion.Settle:
                {
                    float fall = Frac(h + t * 0.06f);
                    float y = R * (0.92f - 1.84f * fall);
                    float ring = R * _radius[i] * Mathf.Sqrt(Mathf.Max(0f, 1f - (y / R) * (y / R)));
                    float a = _phase0[i] + t * 0.22f * Mathf.Sign(_omega[i]);
                    fade = Fade(fall);
                    return new Vector3(Mathf.Cos(a) * ring, y, Mathf.Sin(a) * ring);
                }
                case SwarmMotion.Ripple:
                {
                    float y = R * (_hash2[i] * 1.6f - 0.8f) * 0.62f;
                    float maxR = R * Mathf.Sqrt(Mathf.Max(0.05f, 1f - (y / R) * (y / R)));
                    float grow = Frac(h + t * 0.3f);
                    float ring = maxR * (0.12f + 0.88f * grow);
                    float a = _phase0[i] + 0.15f * Mathf.Sin(t * 0.5f + h * 6.2831853f);
                    fade = Mathf.Sin(Mathf.PI * grow);
                    return new Vector3(Mathf.Cos(a) * ring, y + R * 0.07f * Mathf.Sin(grow * 12.566371f), Mathf.Sin(a) * ring);
                }
                case SwarmMotion.Wander:
                {
                    Vector3 f = _freq[i];
                    float p1 = _phase0[i], p2 = _hash2[i] * 6.2831853f, p3 = h * 6.2831853f;
                    Vector3 w = new Vector3(
                        Mathf.Sin(t * f.x + p1) * Mathf.Cos(t * f.y * 0.7f + p2),
                        Mathf.Sin(t * f.y + p3) * 0.85f,
                        Mathf.Cos(t * f.z + p2) * Mathf.Sin(t * f.x * 0.6f + p3));
                    if (w.sqrMagnitude > 1f) w.Normalize();
                    return w * (R * 0.9f);
                }
                case SwarmMotion.Bounce:
                {
                    float r = _radius[i] * R * 0.9f;
                    float a = _phase0[i] + t * 1.1f * Mathf.Sign(_omega[i]);
                    float hop = Mathf.Abs(Mathf.Sin(t * 2.6f + _phase0[i] * 1.7f));
                    float y = R * (0.85f * hop - 0.42f);
                    float ring = r * Mathf.Sqrt(Mathf.Max(0.1f, 1f - (y / R) * (y / R)));
                    return new Vector3(Mathf.Cos(a) * ring, y, Mathf.Sin(a) * ring);
                }
                default:
                    return Vector3.zero;
            }
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
            _clock += dt * boost * (_config.springSpin / 0.9f);
            float character = Mathf.Clamp01(_config.springMotion);
            float breathe = 0.5f + 0.5f * Mathf.Sin(now * 2.2f);
            _lean = Vector3.Lerp(_lean, leanWS * (invite * (0.55f + 0.45f * breathe)), 1f - Mathf.Exp(-dt * 4f));
            float contract = 1f - 0.18f * hover - 0.1f * invite * breathe;
            float glowBase = 0.12f + 0.35f * pulse + 0.25f * hover + 0.45f * invite * breathe;
            float invDt = 1f / Mathf.Max(dt, 1e-4f);
            float3[] disp = _cloud.disp;
            float3[] vel = _cloud.vel;
            float[] glow = _cloud.glow;
            float[] scale = _cloud.scale;
            float reach = 0f;
            int n = _cloud.Count;
            for (int i = 0; i < n; i++)
            {
                float w = _omega[i] * spin;
                _phase[i] += w * dt;
                float ph = _phase[i];
                float wobble = 1f + 0.14f * Mathf.Sin(now * (0.6f + _hash[i]) + _hash[i] * 6.2831853f);
                float r = _radius[i] * baseR * wobble;
                float c = Mathf.Cos(ph), s = Mathf.Sin(ph);
                Vector3 local = (_u[i] * c + _v[i] * s) * r;
                local.y += 0.18f * r * Mathf.Sin(ph * 2f + _hash[i] * 4f);
                float fade = 1f;
                if (character > 0.001f)
                {
                    Vector3 shaped = Shaped(i, baseR * wobble, _clock, out float shapedFade);
                    local = Vector3.Lerp(local, shaped, character);
                    fade = Mathf.Lerp(1f, shapedFade, character);
                }
                local *= contract;
                Vector3 world = _owner.TransformVector(local) + _lean * (0.4f + 0.6f * _radius[i]);
                float3 next = (float3)world;
                vel[i] = _primed ? (float3)Vector3.ClampMagnitude((Vector3)(next - disp[i]) * invDt, 0.6f) : float3.zero;
                disp[i] = next;
                scale[i] = fade;
                glow[i] = Mathf.Min(1f, glowBase * (0.6f + 0.8f * _hash[i]));
                float m = world.magnitude;
                if (m > reach) reach = m;
            }
            _primed = true;
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
