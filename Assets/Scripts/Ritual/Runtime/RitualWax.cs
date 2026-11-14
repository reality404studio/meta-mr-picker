using System;
using UnityEngine;

namespace MoodPicker.Ritual
{
    public enum WaxDraw { None, Drawing, Falling, Returning }

    public sealed class RitualWax
    {
        const float GoldenAngle = 2.39996323f;
        const float CoverTop = 0.0012f;
        const float HoldShare = 0.7f;
        const int Runs = 5;
        const float DomeUp = 0.85f;
        const float DomeDown = 0.45f;
        const float DomeWide = 1.08f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int DeepId = Shader.PropertyToID("_Deep");
        static readonly int LightId = Shader.PropertyToID("_Light");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int SheenId = Shader.PropertyToID("_Sheen");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int PressId = Shader.PropertyToID("_Press");
        static readonly int StampId = Shader.PropertyToID("_Stamp");
        static readonly int StampDepthId = Shader.PropertyToID("_StampDepth");
        static readonly int AppearId = Shader.PropertyToID("_Appear");
        static readonly int OriginId = Shader.PropertyToID("_Origin");
        static readonly int BobId = Shader.PropertyToID("_Bob");
        static readonly int GrowId = Shader.PropertyToID("_GrowWithPress");

        public readonly Transform root;
        public readonly Transform stickRoot;
        public readonly Transform dropRoot;
        public event Action<Vector3> Landed;

        readonly TimeRitualConfig _config;
        readonly JarShape _shape;
        readonly MaterialPropertyBlock _waxBlock = new MaterialPropertyBlock();
        readonly MaterialPropertyBlock _coverBlock = new MaterialPropertyBlock();
        readonly MaterialPropertyBlock _twineBlock = new MaterialPropertyBlock();
        readonly MaterialPropertyBlock _stickBlock = new MaterialPropertyBlock();
        readonly MaterialPropertyBlock _dropBlock = new MaterialPropertyBlock();
        MeshRenderer _wax;
        MeshRenderer _cover;
        MeshRenderer _twine;
        MeshRenderer _stick;
        MeshRenderer _drop;
        MeshRenderer _sparks;
        Mesh _waxMesh;
        Mesh _coverMesh;
        Mesh _twineMesh;
        Mesh _stickMesh;
        Mesh _dropMesh;
        Mesh _sparksMesh;
        float _waxRadius;
        float _coverRadius;
        float _stickSplat;
        float _dropSplat;
        float _sparkSplat;

        WaxDraw _draw;
        Vector3 _drawFrom;
        Vector3 _dropPos;
        float _dropSize;
        float _fallSpeed;
        bool _dripped;
        float _stickAppear;
        float _stickTarget;
        bool _stickDirty = true;
        float _coverAppear;
        float _coverTarget;
        float _dropShown = -1f;
        float _sparkTime = -1f;

        Color _color = new Color(0.76f, 0.28f, 0.18f, 1f);
        readonly MaterialPropertyBlock _sparkBlock = new MaterialPropertyBlock();
        float _appear;
        float _appearTarget;
        float _hold;
        float _press;
        float _stamp;
        float _glow;
        float _sealedFor = -1f;
        bool _dirty = true;

        public bool Shown => _appearTarget > 0.5f;
        public bool Ready => _dripped && _appear > 0.85f && _sealedFor < 0f;
        public bool Sealed => _sealedFor >= 0f;
        public float Press => _press;
        public Vector3 DropCenter => root.TransformPoint(new Vector3(0f, _config.waxHover + _config.waxRadius * DomeDown, 0f));
        public bool StickShown => _stickTarget > 0.5f && _stickAppear > 0.5f;
        public bool Dripped => _dripped;
        public WaxDraw DrawState => _draw;
        public bool Loaded => _draw == WaxDraw.Drawing && _dropSize > 0.95f;
        public float DropSize => _dropSize;
        public Vector3 DropPosition => _dropPos;
        public Color Color => _color;

        public RitualWax(Transform jarRoot, TimeRitualConfig config, Material waxMaterial, Material glowMaterial = null)
        {
            _config = config;
            _shape = config.jar;
            var go = new GameObject("Wax");
            root = go.transform;
            root.SetParent(jarRoot, false);
            root.localPosition = new Vector3(0f, _shape.height, 0f);
            var stickGo = new GameObject("WaxStick");
            stickRoot = stickGo.transform;
            stickRoot.SetParent(jarRoot, false);
            stickRoot.localPosition = config.waxStickOffset + Vector3.up * config.waxStickRadius;
            stickRoot.localRotation = Quaternion.Euler(0f, config.waxStickYaw, 0f);
            var dropGo = new GameObject("WaxDrop");
            dropRoot = dropGo.transform;
            dropRoot.SetParent(jarRoot, false);
            if (waxMaterial == null) return;
            BuildWax(waxMaterial);
            BuildCover(waxMaterial);
            BuildTwine(waxMaterial);
            BuildStick(waxMaterial);
            BuildDrop(waxMaterial);
            BuildSparks(waxMaterial);
            Push();
        }

        void BuildSparks(Material material)
        {
            const int n = 160;
            var b = RitualStaticSplats.Builder.Create(n);
            float rs = _config.sealRadius;
            for (int i = 0; i < n; i++)
            {
                float phi = i * GoldenAngle;
                float rnd = RitualUtil.Hash(i * 19 + 7);
                float rnd2 = RitualUtil.Hash(i * 29 + 11);
                float r0 = rs * (0.25f + 0.75f * rnd);
                Vector3 a = new Vector3(Mathf.Cos(phi) * r0, CoverTop + 0.004f, Mathf.Sin(phi) * r0);
                float r1 = rs * (1.15f + 1.35f * rnd2);
                float y1 = CoverTop + 0.008f + 0.036f * rnd;
                Vector3 end = new Vector3(Mathf.Cos(phi + (rnd - 0.5f) * 0.4f) * r1, y1, Mathf.Sin(phi + (rnd - 0.5f) * 0.4f) * r1);
                Vector3 nrm = (end - a).normalized;
                float delay = 0.25f * RitualUtil.Hash(i * 13 + 3);
                b.Add(a, nrm, new Vector4(end.x, end.y, end.z, delay), new Vector4(nrm.x, nrm.y, nrm.z, 0f), new Vector4(nrm.x, nrm.y, nrm.z, rnd));
            }
            _sparkSplat = 0.0012f;
            _sparksMesh = b.ToMesh("SealSparks", 0.25f);
            _sparks = RitualStaticSplats.Spawn("SealSparks", root, _sparksMesh, material);
            _sparks.enabled = false;
        }

        float OuterAt(float below)
        {
            return _shape.OuterRadius(_shape.height - below);
        }

        static float Blob(float phi)
        {
            return 1f + 0.075f * Mathf.Sin(3f * phi + 0.7f) + 0.05f * Mathf.Sin(5f * phi + 2.1f) + 0.03f * Mathf.Sin(8f * phi + 4.2f);
        }

        void BuildWax(Material material)
        {
            int n = Mathf.Max(600, _config.waxSplats);
            float rd = _config.waxRadius;
            float rs = _config.sealRadius;
            float thick = _config.sealThickness;
            float yc = _config.waxHover + rd * DomeDown;
            int discCount = Mathf.RoundToInt(n * 0.74f);
            int runCount = n - discCount;
            var b = RitualStaticSplats.Builder.Create(n);

            for (int i = 0; i < discCount; i++)
            {
                float t = (i + 0.5f) / discCount;
                float phi = i * GoldenAngle;
                float polar = Mathf.Acos(1f - 2f * t);
                float sp = Mathf.Sin(polar), cp = Mathf.Cos(polar);
                Vector3 dir = new Vector3(sp * Mathf.Cos(phi), cp, sp * Mathf.Sin(phi));
                Vector3 a = new Vector3(dir.x * rd * DomeWide, yc + dir.y * rd * (dir.y > 0f ? DomeUp : DomeDown), dir.z * rd * DomeWide);
                float rnd = RitualUtil.Hash(i * 7 + 11);
                float edge = rs * Blob(phi);
                Vector3 radial = new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi));
                Vector3 pos, normal, stamped;
                float relief = 0f, delay;
                if (t < 0.86f)
                {
                    float rn = Mathf.Sqrt(t / 0.86f);
                    float r = edge * rn;
                    float bulge = RitualUtil.Smooth((rn - 0.8f) / 0.13f);
                    float roll = rn > 0.93f ? Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((rn - 0.93f) / 0.07f, 2f))) : 1f;
                    float h = thick * (0.72f + 0.28f * bulge) * roll;
                    pos = radial * r + Vector3.up * (CoverTop + h);
                    float tilt = RitualUtil.Smooth((rn - 0.9f) / 0.1f) * 1.15f - 0.45f * bulge * (1f - RitualUtil.Smooth((rn - 0.9f) / 0.05f));
                    normal = (Vector3.up * Mathf.Cos(tilt) + radial * Mathf.Sin(tilt)).normalized;
                    float u = pos.x / rs, v = pos.z / rs;
                    relief = 1f - SealEmblem.Height(u, v);
                    const float e = 0.02f;
                    float gx = (SealEmblem.Height(u + e, v) - SealEmblem.Height(u - e, v)) / (2f * e);
                    float gz = (SealEmblem.Height(u, v + e) - SealEmblem.Height(u, v - e)) / (2f * e);
                    float gain = _config.stampDepth / rs * 1.6f;
                    stamped = Vector3.Lerp(normal, new Vector3(-gx * gain, 1f, -gz * gain).normalized, Mathf.Clamp01(1f - bulge * 2f)).normalized;
                    delay = rn * 0.45f;
                }
                else
                {
                    float q = (t - 0.86f) / 0.14f;
                    pos = radial * (edge * 1.01f) + Vector3.up * (CoverTop + thick * 0.85f * (1f - q));
                    normal = (radial + Vector3.up * 0.25f).normalized;
                    stamped = normal;
                    delay = 0.5f;
                }
                b.Add(a, dir, new Vector4(pos.x, pos.y, pos.z, delay), new Vector4(normal.x, normal.y, normal.z, relief), new Vector4(stamped.x, stamped.y, stamped.z, rnd));
            }

            float lipR = _shape.NeckOuter + _shape.lipBulge + 0.0018f;
            float deepest = _shape.height - _shape.bodyTop - 0.004f;
            for (int i = 0; i < runCount; i++)
            {
                int k = i % Runs;
                float phi = k * (2f * Mathf.PI / Runs) + 0.5f + (RitualUtil.Hash(k * 3 + 1) - 0.5f) * 0.5f;
                float drop = Mathf.Lerp(0.012f, deepest, RitualUtil.Hash(k * 5 + 2));
                float s = RitualUtil.Hash(i * 13 + 3);
                float w = RitualUtil.Hash(i * 17 + 4) * 2f - 1f;
                float rnd = RitualUtil.Hash(i * 19 + 5);
                Vector3 radial = new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi));
                Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
                float r1 = rs * Blob(phi) * 0.9f;
                float top = Mathf.Max(0.001f, lipR - r1);
                float along = s * (top + drop);
                float bulb = RitualUtil.Smooth((s - 0.78f) / 0.22f);
                float half = 0.003f * (0.75f + 0.55f * bulb) * Mathf.Min(1f, 0.45f + s * 4f);
                float rise = 0.0016f * (0.7f + 0.9f * bulb) * Mathf.Sqrt(Mathf.Max(0f, 1f - w * w));
                Vector3 pos, normal;
                if (along < top)
                {
                    pos = radial * (r1 + along) + Vector3.up * (CoverTop + 0.0006f + rise);
                    normal = Vector3.up;
                }
                else
                {
                    float below = along - top;
                    float lift = below < 0.022f ? 0.0024f : 0.001f;
                    pos = radial * (OuterAt(below) + lift + rise) + Vector3.down * below;
                    normal = radial;
                }
                pos += tangent * (w * half);
                normal = (normal + tangent * (w * 0.9f)).normalized;
                Vector3 start = radial * r1 + Vector3.up * (CoverTop + _config.sealThickness * 0.5f);
                Vector3 via = along < top ? Vector3.Lerp(start, pos, 0.5f) : radial * lipR + Vector3.up * (CoverTop + 0.0012f) + tangent * (w * half);
                float delay = 0.75f + 0.25f * s;
                b.Add(start, normal, new Vector4(pos.x, pos.y, pos.z, delay), new Vector4(normal.x, normal.y, normal.z, -1f), new Vector4(via.x, via.y, via.z, rnd));
            }

            float area = 4f * Mathf.PI * rd * rd;
            _waxRadius = Mathf.Sqrt(area / discCount) * 1.15f;
            _waxMesh = b.ToMesh("WaxSeal", 0.2f);
            _wax = RitualStaticSplats.Spawn("WaxSeal", root, _waxMesh, material);
        }

        void BuildCover(Material material)
        {
            int n = Mathf.Max(200, _config.coverSplats);
            float rc = _shape.NeckOuter + _shape.lipBulge + 0.001f;
            var b = RitualStaticSplats.Builder.Create(n);
            int topCount = Mathf.RoundToInt(n * 0.5f);
            for (int i = 0; i < n; i++)
            {
                float phi = i * GoldenAngle;
                Vector3 radial = new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi));
                Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
                float rnd = RitualUtil.Hash(i * 7 + 2);
                Vector3 pos, normal;
                float delay;
                if (i < topCount)
                {
                    float q = Mathf.Sqrt((i + 0.5f) / topCount);
                    float r = rc * q;
                    pos = radial * r + Vector3.up * (CoverTop * 0.6f - 0.001f * (1f - q * q));
                    normal = Vector3.up;
                    delay = q * 0.3f;
                }
                else
                {
                    float q = (i - topCount + 0.5f) / (n - topCount);
                    float hem = 0.02f + 0.004f * Mathf.Sin(5f * phi + 1f) + 0.003f * Mathf.Sin(9f * phi + 2.4f);
                    float d = q * hem;
                    float fold = Mathf.Sin(16f * phi + 0.6f * Mathf.Sin(3f * phi));
                    float open = RitualUtil.Smooth(d / 0.008f);
                    float tie = Mathf.Exp(-Mathf.Pow((d - 0.0158f) / 0.0022f, 2f));
                    float r = OuterAt(d) + 0.0012f + 0.0022f * fold * open * (1f - 0.8f * tie) - 0.0011f * tie;
                    pos = radial * r + Vector3.down * d;
                    normal = (radial - tangent * (Mathf.Cos(16f * phi) * 0.7f * open) + Vector3.up * 0.15f).normalized;
                    delay = 0.3f + 0.6f * q;
                }
                Vector3 a = new Vector3(pos.x * 0.25f, 0.012f + pos.y * 0.3f, pos.z * 0.25f);
                b.Add(a, normal, new Vector4(pos.x, pos.y, pos.z, delay), new Vector4(normal.x, normal.y, normal.z, 0f), new Vector4(normal.x, normal.y, normal.z, rnd));
            }
            float area = Mathf.PI * rc * rc + 2f * Mathf.PI * rc * 0.02f;
            _coverRadius = Mathf.Sqrt(area / n) * 1.05f;
            _coverMesh = b.ToMesh("WaxCover", 0.2f);
            _cover = RitualStaticSplats.Spawn("Cover", root, _coverMesh, material);
        }

        void BuildTwine(Material material)
        {
            int n = Mathf.Max(60, _config.twineSplats);
            var b = RitualStaticSplats.Builder.Create(n);
            int knot = n / 6;
            int tails = n / 6;
            int wrap = n - knot - tails;
            float front = -Mathf.PI * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float rnd = RitualUtil.Hash(i * 5 + 3);
                Vector3 jitter = new Vector3(RitualUtil.Hash(i * 3 + 1) - 0.5f, RitualUtil.Hash(i * 3 + 2) - 0.5f, RitualUtil.Hash(i * 3 + 3) - 0.5f);
                Vector3 pos, normal;
                if (i < wrap)
                {
                    float q = (i + 0.5f) / wrap;
                    float phi = front + q * 2.2f * 2f * Mathf.PI;
                    float d = 0.0146f + 0.0026f * q;
                    Vector3 radial = new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi));
                    pos = radial * (OuterAt(d) + 0.0032f) + Vector3.down * d + jitter * 0.0012f;
                    normal = (radial + jitter * 0.8f).normalized;
                }
                else if (i < wrap + knot)
                {
                    Vector3 radial = new Vector3(Mathf.Cos(front), 0f, Mathf.Sin(front));
                    pos = radial * (OuterAt(0.016f) + 0.0046f) + Vector3.down * 0.016f + jitter * 0.0052f;
                    normal = (radial + jitter).normalized;
                }
                else
                {
                    int tail = (i - wrap - knot) & 1;
                    float q = RitualUtil.Hash(i * 11 + 7);
                    float phi = front + (tail == 0 ? -0.09f : 0.1f) * (1f + q);
                    float d = 0.017f + q * (tail == 0 ? 0.013f : 0.01f);
                    Vector3 radial = new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi));
                    pos = radial * (OuterAt(d) + 0.0042f - 0.001f * q) + Vector3.down * d + jitter * 0.001f;
                    normal = (radial + jitter * 0.6f).normalized;
                }
                Vector3 a = new Vector3(pos.x * 0.25f, 0.012f + pos.y * 0.3f, pos.z * 0.25f);
                float delay = 0.75f + 0.25f * RitualUtil.Hash(i * 13 + 9);
                b.Add(a, normal, new Vector4(pos.x, pos.y, pos.z, delay), new Vector4(normal.x, normal.y, normal.z, 0f), new Vector4(normal.x, normal.y, normal.z, rnd));
            }
            _twineMesh = b.ToMesh("WaxTwine", 0.2f);
            _twine = RitualStaticSplats.Spawn("Twine", root, _twineMesh, material);
        }

        void BuildStick(Material material)
        {
            int n = Mathf.Max(200, _config.waxStickSplats);
            float r = _config.waxStickRadius;
            float half = _config.waxStickLength * 0.5f;
            int capN = n / 8;
            int body = n - 2 * capN;
            var b = RitualStaticSplats.Builder.Create(n);
            for (int i = 0; i < n; i++)
            {
                float rnd = RitualUtil.Hash(i * 7 + 3);
                Vector3 pos, normal;
                if (i < body)
                {
                    float t = (i + 0.5f) / body;
                    float z = -half + t * 2f * half;
                    float phi = i * GoldenAngle;
                    float wobble = 1f + 0.07f * Mathf.Sin(3f * phi + z * 90f) + 0.04f * Mathf.Sin(7f * phi + 1.3f);
                    normal = new Vector3(Mathf.Cos(phi), Mathf.Sin(phi), 0f);
                    pos = new Vector3(normal.x * r * wobble, normal.y * r * wobble, z);
                }
                else
                {
                    int k = i - body;
                    float side = k < capN ? -1f : 1f;
                    int j = k % capN;
                    float t = (j + 0.5f) / capN;
                    float polar = Mathf.Acos(1f - t);
                    float az = j * GoldenAngle;
                    float sp = Mathf.Sin(polar), cp = Mathf.Cos(polar);
                    normal = new Vector3(sp * Mathf.Cos(az), sp * Mathf.Sin(az), cp * side);
                    pos = new Vector3(normal.x * r, normal.y * r, side * half + normal.z * r * 0.55f);
                }
                b.Add(pos, normal, new Vector4(pos.x, pos.y, pos.z, 0f), new Vector4(normal.x, normal.y, normal.z, 0f), new Vector4(normal.x, normal.y, normal.z, rnd));
            }
            float area = 2f * Mathf.PI * r * 2f * half + 2f * Mathf.PI * r * r;
            _stickSplat = Mathf.Sqrt(area / n) * 1.2f;
            _stickMesh = b.ToMesh("WaxStick", 0.2f);
            _stick = RitualStaticSplats.Spawn("Stick", stickRoot, _stickMesh, material);
        }

        void BuildDrop(Material material)
        {
            const int n = 520;
            float r = _config.waxDropRadius;
            var b = RitualStaticSplats.Builder.Create(n);
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n;
                float phi = i * GoldenAngle;
                float polar = Mathf.Acos(1f - 2f * t);
                float sp = Mathf.Sin(polar), cp = Mathf.Cos(polar);
                Vector3 dir = new Vector3(sp * Mathf.Cos(phi), cp, sp * Mathf.Sin(phi));
                Vector3 pos = new Vector3(dir.x * r, dir.y * r * (dir.y < 0f ? 1.3f : 0.9f), dir.z * r);
                float rnd = RitualUtil.Hash(i * 11 + 5);
                b.Add(pos, dir, new Vector4(pos.x, pos.y, pos.z, 0f), new Vector4(dir.x, dir.y, dir.z, 0f), new Vector4(dir.x, dir.y, dir.z, rnd));
            }
            _dropSplat = Mathf.Sqrt(4f * Mathf.PI * r * r / n) * 1.25f;
            _dropMesh = b.ToMesh("WaxDrop", 0.2f);
            _drop = RitualStaticSplats.Spawn("Drop", dropRoot, _dropMesh, material);
        }

        public void ShowStick(Color color)
        {
            if (Sealed) return;
            if (_stickTarget < 0.5f || color != _color) _stickDirty = true;
            if (_coverTarget < 0.5f) _dirty = true;
            if (!_dripped) _color = color;
            _stickTarget = 1f;
            _coverTarget = 1f;
        }

        public void HideStick()
        {
            _stickTarget = 0f;
            if (_draw == WaxDraw.Drawing) _draw = WaxDraw.Returning;
        }

        void StickEnds(out Vector3 a, out Vector3 b)
        {
            float half = _config.waxStickLength * 0.5f;
            a = stickRoot.TransformPoint(new Vector3(0f, 0f, -half));
            b = stickRoot.TransformPoint(new Vector3(0f, 0f, half));
        }

        Vector3 ClosestOnStick(Vector3 world)
        {
            StickEnds(out Vector3 a, out Vector3 b);
            Vector3 ab = b - a;
            float s = Mathf.Clamp01(Vector3.Dot(world - a, ab) / Mathf.Max(1e-10f, ab.sqrMagnitude));
            return a + ab * s;
        }

        public float StickDistance(Vector3 world)
        {
            if (!StickShown || Sealed || _dripped || _draw != WaxDraw.None) return float.MaxValue;
            return Vector3.Distance(world, ClosestOnStick(world)) - _config.waxStickRadius;
        }

        public bool BeginDraw(Vector3 pinch)
        {
            if (StickDistance(pinch) == float.MaxValue) return false;
            _draw = WaxDraw.Drawing;
            _drawFrom = ClosestOnStick(pinch);
            _dropPos = pinch;
            _dropSize = 0f;
            return true;
        }

        public void DrawTo(Vector3 pinch, float dt)
        {
            if (_draw != WaxDraw.Drawing) return;
            _dropPos = pinch;
            float want = Mathf.Clamp01(Vector3.Distance(pinch, _drawFrom) / Mathf.Max(1e-3f, _config.waxDrawLength));
            _dropSize = Mathf.Max(_dropSize, Mathf.MoveTowards(_dropSize, want, dt * 5f));
        }

        public bool OverMouth(Vector3 world)
        {
            Vector3 p = root.InverseTransformPoint(world);
            if (p.y < -0.005f || p.y > _config.waxDripHeight) return false;
            return p.x * p.x + p.z * p.z < _config.pressRadius * _config.pressRadius;
        }

        public bool EndDraw(bool allowDrip)
        {
            if (_draw != WaxDraw.Drawing) return false;
            if (allowDrip && Loaded && OverMouth(_dropPos))
            {
                _draw = WaxDraw.Falling;
                _fallSpeed = 0f;
                return true;
            }
            _draw = WaxDraw.Returning;
            return false;
        }

        void StepDraw(float dt)
        {
            if (_draw == WaxDraw.Falling)
            {
                _fallSpeed += 9.8f * dt;
                Vector3 local = root.InverseTransformPoint(_dropPos);
                float pull = 1f - Mathf.Exp(-dt * 10f);
                local.x -= local.x * pull;
                local.z -= local.z * pull;
                local.y -= _fallSpeed * dt;
                float rest = _config.waxHover + _config.waxRadius * DomeDown;
                if (local.y <= rest)
                {
                    local.y = rest;
                    _draw = WaxDraw.None;
                    _dropSize = 0f;
                    _dripped = true;
                    _dropPos = root.TransformPoint(local);
                    Show(_color);
                    Landed?.Invoke(_dropPos);
                    return;
                }
                _dropPos = root.TransformPoint(local);
            }
            else if (_draw == WaxDraw.Returning)
            {
                _dropSize = Mathf.MoveTowards(_dropSize, 0f, dt * 3f);
                _dropPos = Vector3.Lerp(_dropPos, _drawFrom, 1f - Mathf.Exp(-dt * 8f));
                if (_dropSize <= 0f) _draw = WaxDraw.None;
            }
        }

        public void Show(Color color)
        {
            if (Sealed) return;
            if (!Shown || color != _color) _dirty = true;
            _color = color;
            _appearTarget = 1f;
            _coverTarget = 1f;
        }

        public void Hide()
        {
            if (Sealed) return;
            _appearTarget = 0f;
            _coverTarget = 0f;
            _hold = 0f;
            _dripped = false;
        }

        public float TopY(float press)
        {
            float high = _config.waxHover + _config.waxRadius * (DomeDown + DomeUp);
            float low = CoverTop + _config.sealThickness;
            return Mathf.Lerp(high, low, RitualUtil.Smooth(press));
        }

        public float Depth(in RitualHand hand)
        {
            if (!hand.tracked || !Ready) return 0f;
            float top = TopY(_hold);
            float best = 0f;
            best = Mathf.Max(best, Probe(hand.thumb, top));
            best = Mathf.Max(best, Probe(hand.index, top));
            best = Mathf.Max(best, Probe(hand.middle, top));
            best = Mathf.Max(best, Probe(hand.palm, top));
            return best;
        }

        float Probe(Vector3 world, float top)
        {
            Vector3 p = root.InverseTransformPoint(world);
            if (p.y < -0.02f) return 0f;
            if (p.x * p.x + p.z * p.z > _config.pressRadius * _config.pressRadius) return 0f;
            return Mathf.Max(0f, top - p.y);
        }

        public void SetHold(float progress)
        {
            if (Sealed) return;
            _hold = Mathf.Clamp01(progress);
        }

        public void Seal()
        {
            if (Sealed) return;
            _hold = 1f;
            _sealedFor = 0f;
            _sparkTime = 0f;
            _appearTarget = 1f;
            _coverTarget = 1f;
            HideStick();
        }

        public void Tick(float dt)
        {
            StepDraw(dt);
            float stickAppear = Mathf.MoveTowards(_stickAppear, _stickTarget, dt / 0.7f);
            bool stickChanged = _stickDirty || Mathf.Abs(stickAppear - _stickAppear) > 1e-5f;
            _stickAppear = stickAppear;
            if (_wax == null)
            {
                _appear = Mathf.MoveTowards(_appear, _appearTarget, dt / 0.7f);
                _coverAppear = Mathf.MoveTowards(_coverAppear, _coverTarget, dt / 0.55f);
                if (Sealed) _sealedFor += dt;
                return;
            }
            PushStickAndDrop(stickChanged);
            float appear = Mathf.MoveTowards(_appear, _appearTarget, dt / 0.7f);
            float coverAppear = Mathf.MoveTowards(_coverAppear, _coverTarget, dt / 0.55f);
            float pressGoal = Sealed ? 1f : _hold * HoldShare;
            float press = Sealed ? Mathf.MoveTowards(_press, 1f, dt * (1f - HoldShare) / 1.5f) : Mathf.Lerp(_press, pressGoal, 1f - Mathf.Exp(-dt * 14f));
            float stampGoal = Sealed ? 1f : _hold * 0.78f;
            float stamp = Sealed ? Mathf.MoveTowards(_stamp, 1f, dt / Mathf.Max(0.05f, _config.stampSeconds)) : Mathf.Lerp(_stamp, stampGoal, 1f - Mathf.Exp(-dt * 12f));
            float glowGoal = _config.waxGlow * (1f + 0.55f * _hold);
            if (Sealed)
            {
                _sealedFor += dt;
                glowGoal = Mathf.Lerp(_config.waxGlow * 1.65f, 0.16f, RitualUtil.Smooth(_sealedFor / 3.2f));
            }
            float glow = Mathf.Lerp(_glow, glowGoal, 1f - Mathf.Exp(-dt * 6f));
            bool changed = _dirty || Mathf.Abs(appear - _appear) > 1e-5f || Mathf.Abs(coverAppear - _coverAppear) > 1e-5f || Mathf.Abs(press - _press) > 1e-5f || Mathf.Abs(stamp - _stamp) > 1e-5f || Mathf.Abs(glow - _glow) > 1e-4f;
            _appear = appear;
            _coverAppear = coverAppear;
            _press = press;
            _stamp = stamp;
            _glow = glow;
            bool waxVisible = _appear > 0.002f;
            bool coverVisible = _coverAppear > 0.002f;
            if (_wax.enabled != waxVisible) _wax.enabled = waxVisible;
            if (_cover.enabled != coverVisible) _cover.enabled = coverVisible;
            if (_twine.enabled != coverVisible) _twine.enabled = coverVisible;
            if (changed && (waxVisible || coverVisible)) Push();
            PushSparks(dt);
        }

        void PushSparks(float dt)
        {
            if (_sparks == null) return;
            if (_sparkTime < 0f || _sparkTime > 1.7f)
            {
                if (_sparks.enabled) _sparks.enabled = false;
                return;
            }
            _sparkTime += dt;
            float u = Mathf.Clamp01(_sparkTime / 1.65f);
            float appear = Mathf.Sin(u * Mathf.PI);
            if (!_sparks.enabled && appear > 0.01f) _sparks.enabled = true;
            if (!_sparks.enabled) return;
            float spread = 1f - (1f - u) * (1f - u);
            Color gold = new Color(1.0f, 0.88f, 0.44f, 1f);
            Color deep = new Color(0.85f, 0.52f, 0.16f, 1f);
            Color hi = new Color(1.0f, 0.98f, 0.85f, 1f);
            Fill(_sparkBlock, gold, deep, hi, 1.85f * (1f - 0.5f * u), 0.7f, _sparkSplat * (1.25f - 0.45f * u), spread, 0f, appear, Vector4.zero, 0f, 0f);
            _sparks.SetPropertyBlock(_sparkBlock);
        }

        void PushStickAndDrop(bool stickChanged)
        {
            bool stickVisible = _stickAppear > 0.002f;
            if (_stick.enabled != stickVisible) _stick.enabled = stickVisible;
            if (stickVisible && stickChanged)
            {
                _stickDirty = false;
                Color deep = new Color(_color.r * 0.4f, _color.g * 0.36f, _color.b * 0.36f, 1f);
                Fill(_stickBlock, _color * 0.92f, deep, new Color(1f, 0.93f, 0.84f, 1f), 0.12f, 0.45f, _stickSplat, 0f, 0f, _stickAppear, Vector4.zero, 0f, 0f);
                _stick.SetPropertyBlock(_stickBlock);
            }
            bool dropVisible = _draw != WaxDraw.None && _dropSize > 0.002f;
            if (_drop.enabled != dropVisible) _drop.enabled = dropVisible;
            if (!dropVisible) return;
            dropRoot.position = _dropPos;
            if (Mathf.Abs(_dropSize - _dropShown) < 1e-4f) return;
            _dropShown = _dropSize;
            Color dd = new Color(_color.r * 0.4f, _color.g * 0.36f, _color.b * 0.36f, 1f);
            Fill(_dropBlock, _color, dd, new Color(1f, 0.93f, 0.84f, 1f), _config.waxGlow * 0.6f, 0.6f, _dropSplat, 0f, 0f, _dropSize, Vector4.zero, 0f, 0f);
            _drop.SetPropertyBlock(_dropBlock);
        }

        void Push()
        {
            _dirty = false;
            Vector4 origin = new Vector4(0f, 0f, 0f, 0f);
            Color deep = new Color(_color.r * 0.42f, _color.g * 0.34f, _color.b * 0.32f, 1f);
            Fill(_waxBlock, _color, deep, new Color(1f, 0.94f, 0.85f, 1f), _glow, 0.55f, _waxRadius, _press, _stamp, _appear, origin, 0f, 0f);
            _wax.SetPropertyBlock(_waxBlock);
            float cinch = Mathf.Lerp(0.75f, 1f, Mathf.Max(_appear, _press));
            Fill(_coverBlock, new Color(0.78f, 0.72f, 0.61f, 1f), new Color(0.35f, 0.30f, 0.24f, 1f), new Color(0.92f, 0.88f, 0.80f, 1f), 0f, 0.04f, _coverRadius, cinch, 0f, _coverAppear, origin, 0f, 0f);
            _cover.SetPropertyBlock(_coverBlock);
            Fill(_twineBlock, new Color(0.56f, 0.44f, 0.29f, 1f), new Color(0.26f, 0.20f, 0.12f, 1f), new Color(0.84f, 0.76f, 0.64f, 1f), 0f, 0.08f, 0.00075f, cinch, 0f, _coverAppear, origin, 0f, 0f);
            _twine.SetPropertyBlock(_twineBlock);
        }

        void Fill(MaterialPropertyBlock block, Color color, Color deep, Color light, float glow, float sheen, float radius, float press, float stamp, float appear, Vector4 origin, float bob, float grow)
        {
            block.SetColor(ColorId, color);
            block.SetColor(DeepId, deep);
            block.SetColor(LightId, light);
            block.SetFloat(GlowId, glow);
            block.SetFloat(SheenId, sheen);
            block.SetFloat(RadiusId, radius);
            block.SetFloat(PressId, press);
            block.SetFloat(StampId, stamp);
            block.SetFloat(StampDepthId, _config.stampDepth);
            block.SetFloat(AppearId, appear);
            block.SetVector(OriginId, origin);
            block.SetFloat(BobId, bob);
            block.SetFloat(GrowId, grow);
        }

        public void Dispose()
        {
            RitualUtil.Kill(_waxMesh);
            RitualUtil.Kill(_coverMesh);
            RitualUtil.Kill(_twineMesh);
            RitualUtil.Kill(_stickMesh);
            RitualUtil.Kill(_dropMesh);
            RitualUtil.Kill(_sparksMesh);
            if (root != null) RitualUtil.Kill(root.gameObject);
            if (stickRoot != null) RitualUtil.Kill(stickRoot.gameObject);
            if (dropRoot != null) RitualUtil.Kill(dropRoot.gameObject);
        }
    }
}
