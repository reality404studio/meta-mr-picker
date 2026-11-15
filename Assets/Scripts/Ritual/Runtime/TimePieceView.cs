using System.Collections.Generic;
using MoodPicker.Splatter;
using MoodPicker.Splatter.Sim;
using Unity.Mathematics;
using UnityEngine;

namespace MoodPicker.Ritual
{
    public enum PieceShape { Rod, Blob }
    public enum PieceState { Pulling, Floating, Held, Pouring, Dissolving, Dead }

    public sealed class TimePieceView
    {
        const float GoldenAngle = 2.39996323f;

        struct Seam
        {
            public float3 axis;
            public float z0;
            public float seed;
            public bool fresh;
        }

        static readonly int SeamColorId = Shader.PropertyToID("_Color");
        static readonly int SeamDeepId = Shader.PropertyToID("_Deep");
        static readonly int SeamLightId = Shader.PropertyToID("_Light");
        static readonly int SeamGlowId = Shader.PropertyToID("_Glow");
        static readonly int SeamSheenId = Shader.PropertyToID("_Sheen");
        static readonly int SeamRadiusId = Shader.PropertyToID("_Radius");
        static readonly int SeamPressId = Shader.PropertyToID("_Press");
        static readonly int SeamStaggerId = Shader.PropertyToID("_Stagger");
        static readonly int SeamStampId = Shader.PropertyToID("_Stamp");
        static readonly int SeamAppearId = Shader.PropertyToID("_Appear");
        static readonly int SeamOriginId = Shader.PropertyToID("_Origin");
        static readonly int SeamBobId = Shader.PropertyToID("_Bob");
        static readonly int SeamGrowId = Shader.PropertyToID("_GrowWithPress");

        public readonly TimeCategoryAsset category;
        public readonly int categoryIndex;
        public TimePiece data;
        public PieceShape shape = PieceShape.Rod;
        public PieceState state = PieceState.Pulling;
        public PieceFeel feel = PieceFeel.Jelly;
        public Vector3 drift;
        public float nodePulse;
        public float sweptAt = -10f;

        readonly TimeRitualConfig _config;
        readonly SplatTuning _tuning;
        readonly SplatSurfaceMaterial _look;
        readonly Material _splatMaterial;
        readonly Material _glowMaterial;
        readonly Transform _parent;
        readonly Transform _head;
        readonly Font _font;

        RitualSplatCloud _cloud;
        readonly RitualSurface _surface = new RitualSurface();
        TextMesh _label;
        TextMesh _shadow;
        int _labelUnits = -1;
        PieceShape _labelShape;
        PieceFeel _labelFeel;
        int _labelMerged = -1;
        bool _labelCompact;
        bool _labelNumbers = true;
        float _labelAlpha = 0.96f;

        int _cap;
        int _per;
        int _ring;
        int _rings;
        float _radius;
        float _unitLength;
        float _half;
        float _blobRadius;
        float _baseBlobRadius;
        float _blobHalf;
        Vector3 _squeezeAxis = Vector3.forward;

        float[] _stiff;
        float[] _delay;
        float[] _order;
        float[] _depth;
        float[] _strength;

        int _allocatedUnits;
        int _targetUnits;
        float _visibleUnits;
        float _tension;
        Vector3 _tipWS;
        Vector3 _tipPull;
        Vector3 _axisWS = Vector3.forward;
        float _nodeAmount;
        float _nodeTarget;
        float _age;
        float _flash;
        float _energy = 1f;
        float _bobPhase;
        float _pourProgress;
        bool _pourFromTip;
        float _dissolve;
        bool _awake = true;
        bool _reformed;
        float _mergeWindow = 0.9f;
        Vector3 _floatTarget;
        bool _hasFloat;
        float _floatWait;
        float _floatAge;
        float _lump;
        float _squeeze;
        Vector3 _bend;
        Vector3 _bendMid;
        bool _bendInit;
        Vector3 _rest;
        Vector3 _dissolveTo;

        float[] _loose;
        float[] _soft;
        int _looseCount;
        int _lastScattered;
        Vector3 _bridgePoint;
        float _bridgeAmount;
        bool _bridgeHeld;

        readonly Material _seamMaterial;
        readonly List<Seam> _seams = new List<Seam>();
        MeshRenderer _seamRenderer;
        Mesh _seamMesh;
        MaterialPropertyBlock _seamBlock;
        float _seamAge = -1f;
        float _seamPresence = 1f;

        public int Units => data.units;
        public int LooseCount => _looseCount;
        public int LastScattered => _lastScattered;
        public float BridgeAmount => _bridgeAmount;
        public int SeamCount => _seams.Count;
        public bool SeamVisible => _seamRenderer != null && _seamRenderer.enabled;
        public float SeamPress => _seamAge < 0f ? 1f : Mathf.Clamp01((_seamAge - _config.seamDelay) / Mathf.Max(0.05f, _config.seamSeconds));
        public Transform Transform => _cloud != null ? _cloud.transform : null;
        public bool Alive => state != PieceState.Dead;
        public float Length => data.units * _unitLength;
        public float Radius => shape == PieceShape.Blob ? _blobRadius : _radius;
        public int SplatCount => _cloud != null ? _cloud.Count : 0;
        public bool Awake => _awake;
        public float NodeAmount => _nodeAmount;
        public float PourProgress => _pourProgress;
        public float Squeeze => _squeeze;
        public Vector3 Bend => _bend;
        public float Lump => _lump;
        public float BlobRadiusNow => _blobRadius;
        public float BlobRadiusFull => _baseBlobRadius;
        public string LabelText => _label != null && _label.gameObject.activeSelf ? _label.text : "";

        public Vector3 Center
        {
            get
            {
                if (_cloud == null) return _rest;
                return shape == PieceShape.Blob ? _cloud.transform.position : _cloud.transform.TransformPoint(new Vector3(0f, 0f, Length * 0.5f));
            }
        }

        public TimePieceView(Transform parent, TimeCategoryAsset category, int categoryIndex, SplatSurfaceMaterial look, Material splatMaterial,
            TimeRitualConfig config, SplatTuning tuning, Font font, Transform head, Material glowMaterial = null, Material seamMaterial = null)
        {
            _seamMaterial = seamMaterial;
            _parent = parent;
            this.category = category;
            this.categoryIndex = categoryIndex;
            _look = look;
            _splatMaterial = splatMaterial;
            _glowMaterial = glowMaterial;
            _config = config;
            _tuning = tuning;
            _font = font;
            _head = head;
            feel = category != null ? category.EffectiveFeel : PieceFeel.Jelly;
            data = new TimePiece { category = category != null ? category.id : "", units = 0, mergedCount = 0 };
            _cap = Mathf.Max(4, config.capSplats);
            _ring = Mathf.Max(6, config.splatsPerRing);
            _rings = Mathf.Max(1, config.ringsPerUnit);
            _per = _ring * _rings;
            _radius = config.barRadius;
            _unitLength = config.unitLength;
            float areaHalf = Mathf.Sqrt(4f * _radius * _radius / Mathf.Max(1, _per)) * 1.45f * config.splatOverlap;
            _half = Mathf.Max(0.0022f, areaHalf);
            _bobPhase = RitualUtil.Hash(GetHashCode()) * 6.28f;
        }

        public BeadShape ShapeKind => category != null ? category.EffectiveShape : BeadShape.Droplet;

        void Build(int unitCapacity, string name)
        {
            int capacity = 2 * _cap + unitCapacity * _per;
            _cloud = new RitualSplatCloud(name, _parent, _splatMaterial, capacity, 0.6f);
            _stiff = new float[capacity];
            _delay = new float[capacity];
            _order = new float[capacity];
            _loose = new float[capacity];
            _soft = new float[capacity];
            _looseCount = 0;
            for (int i = 0; i < capacity; i++)
            {
                _stiff[i] = 1f + (RitualUtil.Hash(i * 13 + 7) - 0.5f) * 2f * _config.stiffnessJitter;
                _loose[i] = -1f;
            }
            _depth = new float[unitCapacity + 2];
            _strength = new float[unitCapacity + 2];
            _depth[0] = _config.node10;
            _strength[0] = 0.6f;
            for (int b = 1; b < _depth.Length; b++)
            {
                _depth[b] = _config.NodeDepth(b);
                _strength[b] = 0.6f + 0.2f * TimeUnits.NodeStrength(b);
            }
            _surface.material = _look;
            _surface.look = TimeRitualLook.Piece(_tuning, feel);
            _surface.sizeMeters = _config.pieceTextureSize;
            _surface.triplanarScale = _tuning != null ? _tuning.triplanarScale : 1.15f;
            _surface.intensity = _config.pieceIntensity;
            _surface.aspectMax = _config.scatterAspectMax;
            _surface.MarkDirty();
            if (_font != null && _label == null) _label = MakeLabel(_font, out _shadow);
            if (_label != null) _label.transform.SetParent(_cloud.transform, false);
        }

        TextMesh MakeLabel(Font font, out TextMesh shadow)
        {
            var go = new GameObject("Minutes");
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 68;
            tm.characterSize = 0.00132f;
            tm.lineSpacing = 1.05f;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.98f, 0.96f, 0.92f, 0.96f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = font.material;
            RitualUtil.Quiet(r);

            var sgo = new GameObject("MinutesShadow");
            sgo.transform.SetParent(go.transform, false);
            sgo.transform.localPosition = new Vector3(0.0008f, -0.0008f, 0.0015f);
            shadow = sgo.AddComponent<TextMesh>();
            shadow.font = font;
            shadow.fontSize = 68;
            shadow.characterSize = 0.00132f;
            shadow.lineSpacing = 1.05f;
            shadow.anchor = TextAnchor.LowerCenter;
            shadow.alignment = TextAlignment.Center;
            shadow.color = new Color(0.02f, 0.02f, 0.03f, 0.85f);
            var sr = sgo.GetComponent<MeshRenderer>();
            sr.sharedMaterial = font.material;
            RitualUtil.Quiet(sr);
            return tm;
        }

        float3 CapHome(int index, bool tip, float z, out float3 n)
        {
            float t = (index + 0.5f) / _cap;
            float polar = math.acos(1f - t);
            float az = index * GoldenAngle;
            float sp = math.sin(polar), cp = math.cos(polar);
            float dir = tip ? 1f : -1f;
            n = new float3(sp * math.cos(az), sp * math.sin(az), cp * dir);
            return new float3(n.x * _radius * 0.42f, n.y * _radius * 0.42f, z + dir * cp * _radius * 0.35f);
        }

        static float3 EvalShape(BeadShape kind, float3 d, float radius)
        {
            switch (kind)
            {
                case BeadShape.Heart:
                {
                    float a = math.atan2(math.abs(d.x), d.y);
                    float topNotch = math.exp(-a * a * 7.5f) * 0.58f;
                    float da = math.abs(a - 0.85f);
                    float lobeBulge = math.exp(-da * da * 2.4f) * 0.36f;
                    float edge = math.max(0f, math.PI - a);
                    float botPoint = math.exp(-edge * edge * 3.8f) * 0.34f;
                    float x = d.x * (0.98f + 0.30f * math.saturate(d.y)) + math.sign(d.x) * (0.14f * lobeBulge);
                    float y = d.y * (1.02f - topNotch + botPoint) + 0.16f * lobeBulge;
                    float z = d.z * (0.54f + 0.10f * math.saturate(1f - math.abs(d.y)));
                    return new float3(x, y, z) * radius;
                }
                case BeadShape.Star:
                {
                    float rxy = math.sqrt(d.x * d.x + d.y * d.y);
                    float ang = math.atan2(d.x, d.y);
                    float wave = 0.5f + 0.5f * math.cos(5f * ang);
                    float ray = math.pow(math.max(0f, wave), 1.8f);
                    float xyScale = math.lerp(1f, 0.46f + 0.72f * ray, rxy);
                    float zScale = 0.52f * (1f - 0.32f * rxy);
                    return new float3(d.x * xyScale, d.y * xyScale, d.z * zScale) * radius;
                }
                case BeadShape.Diamond:
                {
                    float l1 = math.max(0.45f, math.abs(d.x) * 1.05f + math.abs(d.y) * 0.86f + math.abs(d.z) * 1.15f);
                    float facet = math.lerp(1f, 0.88f / l1, 0.78f);
                    return new float3(d.x * 0.88f, d.y * 1.16f, d.z * 0.52f) * (facet * radius);
                }
                case BeadShape.Feather:
                {
                    float t = math.saturate(0.5f * (d.y + 1f));
                    float u = t >= 0.42f ? (t - 0.42f) / 0.58f : (t - 0.42f) / 0.42f;
                    float vane = math.sqrt(math.max(0f, 1f - math.pow(math.abs(u), 2.4f)));
                    float quill = math.saturate((t - 0.05f) / 0.12f);
                    bool right = d.x >= 0f;
                    float side = right ? 1.15f : 0.82f;
                    float splitAt = right ? (t - 0.64f) / 0.022f : (t - 0.33f) / 0.026f;
                    float split = 1f - (right ? 0.6f : 0.5f) * math.exp(-splitAt * splitAt);
                    float w = (0.035f + 0.38f * vane * quill * split) * side;
                    float y = d.y * 1.45f;
                    float x = d.x * w + 0.3f * (t * t - t);
                    float across = math.saturate(math.abs(d.x) * 1.5f);
                    float barbs = 0.02f * math.sin(52f * (t + 0.4f * across)) * across;
                    float shaft = 0.04f * math.exp(-d.x * d.x / 0.003f);
                    float z = d.z * (0.06f + 0.03f * vane) + barbs + shaft + 0.05f * across * across;
                    return new float3(x, y, z) * radius;
                }
                case BeadShape.Crescent:
                {
                    float dy2 = d.y * d.y;
                    float arch = -0.46f * dy2 + 0.16f;
                    float halfThick = 0.34f * math.max(0.12f, 1f - 0.82f * dy2);
                    float x = arch + d.x * halfThick;
                    float y = d.y * 1.10f;
                    float z = d.z * (0.54f * math.max(0.25f, 1f - 0.55f * dy2));
                    return new float3(x, y, z) * radius;
                }
                default:
                {
                    float t = 0.5f * (1f - d.y);
                    float profile = 0.08f + 0.94f * math.pow(math.max(0f, t), 0.78f) * (1f - 0.18f * t);
                    return new float3(d.x * profile, d.y * 1.12f, d.z * profile * 0.56f) * radius;
                }
            }
        }

        public static float3 ShapePoint(BeadShape kind, float3 d, float radius, out float3 n)
        {
            d = math.normalizesafe(d, new float3(0f, 1f, 0f));
            float3 p = EvalShape(kind, d, radius);
            const float e = 0.045f;
            RitualUtil.Basis(d, out float3 tu, out float3 tv);
            float3 du = EvalShape(kind, math.normalize(d + tu * e), radius) - EvalShape(kind, math.normalize(d - tu * e), radius);
            float3 dv = EvalShape(kind, math.normalize(d + tv * e), radius) - EvalShape(kind, math.normalize(d - tv * e), radius);
            n = math.normalizesafe(math.cross(du, dv), d);
            if (math.dot(n, d) < 0f) n = -n;
            return p;
        }

        float3 RodHome(int unitSplat, out float3 n)
        {
            int k = unitSplat / _per;
            int rem = unitSplat - k * _per;
            float t = (rem + 0.5f) / _per;
            float dy = 1f - 2f * t;
            float r = math.sqrt(math.max(0f, 1f - dy * dy));
            float phi = rem * GoldenAngle;
            float3 d = new float3(r * math.cos(phi), dy, r * math.sin(phi));
            float beadRadius = _unitLength * 0.42f;
            float shell = (rem % 3 == 0) ? 0.78f : 1f;
            float3 p = ShapePoint(ShapeKind, d, beadRadius * shell, out float3 sn);
            float zCenter = (k + 0.5f) * _unitLength;
            n = math.normalizesafe(new float3(sn.z, sn.y, sn.x), new float3(1f, 0f, 0f));
            return new float3(p.z, p.y, zCenter + p.x);
        }

        public static float BlobRadius(float rodRadius, float length)
        {
            return Mathf.Max(0.022f, Mathf.Pow(2.2f * rodRadius * rodRadius * Mathf.Max(0.015f, length), 1f / 3f));
        }

        public static float Fold(float3 d)
        {
            float a = math.sin(6.1f * d.x + 1.3f) * math.sin(5.3f * d.y + 0.4f);
            float b = math.sin(7.7f * d.z + 2.2f) * math.sin(4.6f * d.x - 3.1f * d.y + 0.9f);
            float c = math.abs(math.sin(9.2f * d.y + 3.7f * d.z + 1.7f));
            return 0.55f * a + 0.45f * b + 0.6f * c - 0.3f;
        }

        float3 BlobHome(int unitSplat, int total, out float3 n)
        {
            float t = (unitSplat + 0.5f) / total;
            float z = 1f - 2f * t;
            float r = math.sqrt(math.max(0f, 1f - z * z));
            float phi = unitSplat * GoldenAngle;
            float3 d = new float3(r * math.cos(phi), r * math.sin(phi), z);
            int layer = unitSplat % 4;
            float shell = layer == 0 ? 0.72f : (layer == 1 ? 0.88f : 1.0f);
            return ShapePoint(ShapeKind, d, _blobRadius * shell, out n);
        }

        Vector3 StrandUp(Vector3 originWS, Vector3 dirWS)
        {
            Vector3 fallback = Mathf.Abs(dirWS.y) < 0.95f ? Vector3.up : Vector3.right;
            if (_head == null || _head.position.sqrMagnitude <= 1e-4f) return fallback;
            Vector3 toEye = _head.position - originWS;
            Vector3 perp = toEye - dirWS * Vector3.Dot(toEye, dirWS);
            if (perp.sqrMagnitude <= 1e-6f) return fallback;
            Vector3 faceUp = Vector3.Cross(dirWS, perp.normalized);
            if (faceUp.y < 0f) faceUp = -faceUp;
            return (Vector3.up * 0.45f + faceUp.normalized * 0.55f).normalized;
        }

        public void BeginPull(Vector3 originWS, Vector3 dirWS)
        {
            Build(Mathf.Max(1, _config.maxUnits), "Piece " + data.category);
            _axisWS = dirWS.sqrMagnitude > 1e-8f ? dirWS.normalized : Vector3.forward;
            _cloud.transform.SetPositionAndRotation(originWS, Quaternion.LookRotation(_axisWS, StrandUp(originWS, _axisWS)));
            for (int i = 0; i < _cap; i++)
            {
                float3 h = CapHome(i, false, 0f, out float3 n);
                int s = _cloud.Add(h, n, _half);
                if (s >= 0) _cloud.scale[s] = 0f;
            }
            for (int i = 0; i < _cap; i++)
            {
                float3 h = CapHome(i, true, 0f, out float3 n);
                int s = _cloud.Add(h, n, _half);
                if (s >= 0) _cloud.scale[s] = 0f;
            }
            _tipWS = originWS;
            _rest = originWS;
            AllocateUnit(0);
            _visibleUnits = 1f;
            _nodeAmount = 1f;
            _nodeTarget = 1f;
            state = PieceState.Pulling;
            shape = PieceShape.Rod;
            _awake = true;
        }

        void AllocateUnit(int k)
        {
            for (int q = 0; q < _per; q++)
            {
                float3 h = RodHome(k * _per + q, out float3 n);
                int s = _cloud.Add(h, n, _half);
                if (s < 0) return;
                _cloud.disp[s] = float3.zero;
                _cloud.vel[s] = float3.zero;
                _cloud.scale[s] = 1f;
                _cloud.glow[s] = _config.unitFlash * 0.5f;
            }
            _allocatedUnits = k + 1;
        }

        void MoveTipCap(int units)
        {
            float3x3 rot = new float3x3((quaternion)_cloud.transform.rotation);
            float z = units * _unitLength;
            for (int i = 0; i < _cap; i++)
            {
                int s = _cap + i;
                float3 old = _cloud.home[s];
                float3 h = CapHome(i, true, z, out float3 n);
                _cloud.disp[s] += math.mul(rot, old - h);
                _cloud.SetHome(s, h, n, _half);
            }
        }

        public void SetPose(Vector3 position, Quaternion rotation, float carry)
        {
            Transform t = _cloud.transform;
            if (carry < 0.999f && _cloud.Count > 0)
            {
                float3x3 ro = new float3x3((quaternion)t.rotation);
                float3x3 rn = new float3x3((quaternion)rotation);
                float3x3 dr = rn - ro;
                float3 dp = (float3)position - (float3)t.position;
                float keep = 1f - carry;
                float3[] disp = _cloud.disp;
                float3[] home = _cloud.home;
                int n = _cloud.Count;
                float[] loose = _loose;
                for (int i = 0; i < n; i++) disp[i] -= (math.mul(dr, home[i]) + dp) * (loose[i] >= 0f ? 1f : keep);
                _cloud.MarkSim();
                _awake = true;
            }
            t.SetPositionAndRotation(position, rotation);
        }

        public bool UpdatePull(Vector3 tipWS, int units, float rawLength, float dt)
        {
            Transform t = _cloud.transform;
            Vector3 origin = t.position;
            Vector3 to = tipWS - origin;
            if (to.sqrMagnitude > 1e-6f)
            {
                Vector3 dir = to.normalized;
                Quaternion rot = Quaternion.LookRotation(dir, StrandUp(origin, dir));
                _axisWS = dir;
                SetPose(origin, rot, 1f);
            }
            FollowThread(origin, tipWS, dt);
            _tipWS = tipWS;
            units = Mathf.Clamp(units, 0, _config.maxUnits);
            int allocTarget = Mathf.Max(1, units);
            while (_allocatedUnits < allocTarget) AllocateUnit(_allocatedUnits);
            bool changed = units != _targetUnits;
            if (changed)
            {
                bool grew = units > _targetUnits;
                _targetUnits = units;
                data.units = units;
                MoveTipCap(units);
                if (grew)
                {
                    _flash = 1f;
                    int n = _cloud.Count;
                    float add = _config.unitFlash * 0.35f;
                    for (int i = 0; i < n; i++) _cloud.glow[i] = Mathf.Max(_cloud.glow[i], add);
                }
            }
            _visibleUnits = allocTarget;
            _tension = 0f;
            _tipPull = Vector3.zero;
            _awake = true;
            return changed;
        }

        void FollowThread(Vector3 origin, Vector3 tipWS, float dt)
        {
            Vector3 straightMid = (origin + tipWS) * 0.5f;
            if (!_bendInit)
            {
                _bendMid = straightMid;
                _bendInit = true;
            }
            _bendMid = Vector3.Lerp(_bendMid, straightMid, 1f - Mathf.Exp(-dt * Mathf.Max(0.1f, _config.threadFollow)));
            Vector3 bend = (_bendMid - straightMid) * _config.threadBend;
            float span = Vector3.Distance(origin, tipWS);
            bend += Vector3.down * (_config.threadSag * span);
            bend -= _axisWS * Vector3.Dot(bend, _axisWS);
            float limit = Mathf.Max(_config.threadBendMax, _config.threadSag) * span;
            if (bend.sqrMagnitude > limit * limit) bend = bend.normalized * limit;
            _bend = bend;
        }

        public void SetSqueeze(float amount, Vector3 axisWS)
        {
            if (axisWS.sqrMagnitude > 1e-8f) _squeezeAxis = axisWS.normalized;
            SetSqueeze(amount);
        }

        public bool Compact()
        {
            if (shape != PieceShape.Blob || _cloud == null) return false;
            float min = _baseBlobRadius * _config.compactMin;
            if (_blobRadius <= min + 1e-6f)
            {
                _flash = 1f;
                _awake = true;
                return false;
            }
            _blobRadius = Mathf.Max(min, _blobRadius * _config.compactStep);
            _lump = Mathf.Min(_config.compactLumpMax, Mathf.Max(_lump, _config.crumpleLump) + _config.compactLumpStep);
            float3x3 rot = new float3x3((quaternion)_cloud.transform.rotation);
            int cap2 = 2 * _cap;
            int total = _cloud.Count - cap2;
            _reformed = false;
            for (int q = 0; q < total; q++)
            {
                int s = cap2 + q;
                float3 old = _cloud.home[s];
                float3 h = BlobHome(q, total, out float3 n);
                _cloud.disp[s] += math.mul(rot, old - h);
                _cloud.vel[s] = float3.zero;
                _delay[s] = 0f;
                _cloud.SetHome(s, h, n, _blobHalf);
                _cloud.glow[s] = Mathf.Max(_cloud.glow[s], 0.35f);
            }
            _flash = 1f;
            _awake = true;
            _surface.MarkDirty();
            if (_seams.Count > 0) BuildSeamMesh();
            return true;
        }

        static float3 HashDirection(int seed)
        {
            float z = RitualUtil.Hash(seed * 73 + 11) * 2f - 1f;
            float a = RitualUtil.Hash(seed * 197 + 29) * 6.2831853f;
            float r = math.sqrt(math.max(0f, 1f - z * z));
            return new float3(r * math.cos(a), r * math.sin(a), z);
        }

        static float3 ClampVec(float3 v, float maxLen)
        {
            if (!math.all(math.isfinite(v))) return float3.zero;
            float sq = math.lengthsq(v);
            return sq > maxLen * maxLen ? v * (maxLen / math.sqrt(sq)) : v;
        }

        public float Touch(Vector3 pointWS, Vector3 handVelWS, float radius, float dt)
        {
            if (_cloud == null || _cloud.Count <= 0) return 0f;
            if (state != PieceState.Floating && state != PieceState.Held) return 0f;
            float3 pWS = pointWS;
            float3 vWS = handVelWS;
            if (!math.all(math.isfinite(pWS)) || !math.all(math.isfinite(vWS))) return 0f;
            vWS = ClampVec(vWS, 2.5f);
            float speed = math.length(vWS);

            Transform tr = _cloud.transform;
            float3 pos = tr.position;
            float3x3 rot = new float3x3((quaternion)tr.rotation);
            float r = math.max(0.015f, radius);
            float shell = r * 2.2f;
            float stepDt = math.min(dt, 1f / 30f);

            float3[] home = _cloud.home;
            float3[] disp = _cloud.disp;
            float3[] vel = _cloud.vel;
            float[] scale = _cloud.scale;
            float[] glow = _cloud.glow;
            int n = _cloud.Count;
            float maxWeight = 0f;

            for (int i = 0; i < n; i++)
            {
                if (scale[i] <= 0.01f) continue;
                float3 wPos = pos + math.mul(rot, home[i]) + disp[i];
                float3 diff = wPos - pWS;
                float d = math.length(diff);
                if (d >= shell) continue;

                float3 dir = d > 1e-5f ? diff / d : new float3(0f, 1f, 0f);
                if (d < r)
                {
                    float w = 1f - d / r;
                    maxWeight = math.max(maxWeight, w);
                    if (feel == PieceFeel.Water)
                    {
                        float3 accel = dir * (60f * w) + (vWS - vel[i]) * (18f * w);
                        vel[i] = ClampVec(vel[i] + accel * stepDt, 1.4f);
                        disp[i] = ClampVec(disp[i] + dir * ((r - d) * 0.22f * w), 0.08f);
                        glow[i] = math.max(glow[i], math.min(1f, 0.65f + w * 0.5f));
                    }
                    else
                    {
                        float3 tangent = vWS - dir * math.dot(vWS, dir);
                        float3 accel = dir * (52f * w) + (vWS - vel[i]) * (24f * w) + tangent * (8f * w);
                        vel[i] = ClampVec(vel[i] + accel * stepDt, 1.4f);
                        disp[i] = ClampVec(disp[i] + dir * ((r - d) * 0.32f * w), 0.06f);
                        glow[i] = math.max(glow[i], math.min(1f, 0.55f + w * 0.45f));
                    }
                }
                else
                {
                    float s = 1f - (d - r) / math.max(1e-5f, shell - r);
                    maxWeight = math.max(maxWeight, s * 0.35f);
                    float drag = feel == PieceFeel.Water ? 3.6f : 4.8f;
                    float attract = feel == PieceFeel.Water ? 1.2f : 2.4f;
                    float3 accel = (vWS - vel[i]) * (drag * s) - dir * (attract * s);
                    vel[i] = ClampVec(vel[i] + accel * stepDt, 1.4f);
                    glow[i] = math.max(glow[i], s * 0.65f);
                }
            }

            if (maxWeight > 0f)
            {
                _awake = true;
                _flash = Mathf.Max(_flash, maxWeight * 0.55f);
                _cloud.MarkSim();
            }
            return maxWeight;
        }

        bool Gathering => _reformed && _age < _mergeWindow;

        public float Field(Vector3[] probes, float[] radii, int count, Vector3 handVelWS, bool scatter, float dt)
        {
            _lastScattered = 0;
            if (_cloud == null || _cloud.Count <= 0 || probes == null || radii == null) return 0f;
            if (state != PieceState.Floating && state != PieceState.Held) return 0f;
            if (Gathering) scatter = false;
            float3 vH = handVelWS;
            if (!math.all(math.isfinite(vH))) return 0f;
            vH = ClampVec(vH, 2.5f);
            count = Mathf.Min(count, Mathf.Min(probes.Length, radii.Length));
            if (count <= 0) return 0f;

            Transform tr = _cloud.transform;
            float3 pos = tr.position;
            float3x3 rot = new float3x3((quaternion)tr.rotation);
            float reachScale = scatter ? 1.6f : 1f;
            float part = _config.partStrength;
            float carry = _config.scatterCarry;
            float burst = _config.scatterBurst;
            float3[] home = _cloud.home;
            float3[] disp = _cloud.disp;
            float3[] vel = _cloud.vel;
            float[] scale = _cloud.scale;
            float[] glow = _cloud.glow;
            int n = _cloud.Count;
            int seedTime = (int)(_age * 60f);
            float contact = 0f;
            int scattered = 0;

            for (int i = 2 * _cap; i < n; i++)
            {
                if (scale[i] <= 0.01f) continue;
                float3 w = pos + math.mul(rot, home[i]) + disp[i];
                float best = 1f;
                float bestDist = 0f;
                float bestRadius = 0f;
                float3 bestDiff = float3.zero;
                bool hit = false;
                for (int k = 0; k < count; k++)
                {
                    float rr = radii[k] * reachScale;
                    if (rr <= 1e-5f) continue;
                    float3 diff = w - (float3)probes[k];
                    float d2 = math.lengthsq(diff);
                    if (d2 >= rr * rr) continue;
                    float d = math.sqrt(d2);
                    float share = d / rr;
                    if (share < best)
                    {
                        best = share;
                        bestDist = d;
                        bestRadius = radii[k];
                        bestDiff = diff;
                        hit = true;
                    }
                }
                if (!hit) continue;
                float3 dir = bestDist > 1e-5f ? bestDiff / bestDist : SplatSimKernel.HashDirection(RitualUtil.Hash(i * 29 + 5));
                float weight = 1f - best;
                contact = math.max(contact, weight);
                if (scatter)
                {
                    float3 jitter = SplatSimKernel.HashDirection(RitualUtil.Hash(i * 41 + seedTime)) * 0.06f;
                    float3 kick = vH * (carry * (0.5f + 0.5f * weight)) + dir * (burst * weight) + jitter;
                    vel[i] = ClampVec(vel[i] * 0.3f + kick, 1.8f);
                    _loose[i] = 0f;
                    glow[i] = math.max(glow[i], 0.5f + 0.4f * weight);
                    scattered++;
                }
                else if (bestDist < bestRadius)
                {
                    disp[i] = ClampVec(disp[i] + dir * ((bestRadius - bestDist) * part), 0.12f);
                    vel[i] *= 0.6f;
                    _soft[i] = 1f;
                    glow[i] = math.max(glow[i], 0.35f + 0.3f * weight);
                }
            }

            _lastScattered = scattered;
            if (contact > 0f)
            {
                _awake = true;
                _flash = Mathf.Max(_flash, contact * (scatter ? 0.6f : 0.35f));
                _cloud.MarkSim();
            }
            return contact;
        }

        public void SetBridge(Vector3 pointWS, float amount)
        {
            if (_cloud == null) return;
            _bridgePoint = pointWS;
            _bridgeAmount = Mathf.Clamp01(amount);
            _bridgeHeld = true;
            _awake = true;
        }

        public void Snap()
        {
            _bridgeAmount = 0f;
            _bridgeHeld = false;
            _flash = 1f;
            AddGlow(0.45f);
        }

        public float MaxDisplacement()
        {
            if (_cloud == null) return 0f;
            float best = 0f;
            float3[] disp = _cloud.disp;
            float[] scale = _cloud.scale;
            int n = _cloud.Count;
            for (int i = 2 * _cap; i < n; i++)
                if (scale[i] > 0.01f) best = math.max(best, math.lengthsq(disp[i]));
            return math.sqrt(best);
        }

        void ClearLoose()
        {
            if (_loose == null) return;
            for (int i = 0; i < _loose.Length; i++) _loose[i] = -1f;
            _looseCount = 0;
        }

        public void SetSqueeze(float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (Mathf.Abs(amount - _squeeze) < 1e-4f) return;
            _squeeze = amount;
            _awake = true;
        }

        public bool Crumple()
        {
            if (shape != PieceShape.Rod || data.units <= 0 || _cloud == null) return false;
            Reform(null, PieceShape.Blob);
            return true;
        }

        public void ReleasePull()
        {
            int keep = 2 * _cap + data.units * _per;
            _cloud.Truncate(keep);
            _allocatedUnits = data.units;
            _visibleUnits = data.units;
            _tension = 0f;
            _tipPull = Vector3.zero;
            _nodeTarget = _config.nodesAlwaysVisible ? 1f : 0f;
            state = PieceState.Floating;
            _rest = _cloud.transform.position;
            _awake = true;
        }

        public void Dissolve(Vector3 towardWS)
        {
            if (state == PieceState.Dead) return;
            state = PieceState.Dissolving;
            ClearLoose();
            _dissolve = 0f;
            _dissolveTo = towardWS;
            _awake = true;
        }

        public bool FloatingAway => _hasFloat;
        public Vector3 FloatTarget => _floatTarget;

        public void FloatTo(Vector3 target, float wait)
        {
            _floatTarget = target;
            _hasFloat = true;
            _floatWait = Mathf.Max(0f, wait);
            _floatAge = 0f;
            _awake = true;
        }

        public void Hold()
        {
            if (state == PieceState.Floating) state = PieceState.Held;
            _hasFloat = false;
            drift = Vector3.zero;
            _awake = true;
        }

        public void Drop()
        {
            if (state == PieceState.Held) state = PieceState.Floating;
            if (_cloud != null) _rest = _cloud.transform.position;
        }

        public void Segment(out Vector3 a, out Vector3 b)
        {
            Transform t = _cloud.transform;
            if (shape == PieceShape.Blob)
            {
                a = b = t.position;
                return;
            }
            a = t.position;
            b = t.TransformPoint(new Vector3(0f, 0f, Length));
        }

        public float SurfaceDistance(Vector3 point)
        {
            Segment(out Vector3 a, out Vector3 b);
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float s = len2 > 1e-10f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / len2) : 0f;
            return Vector3.Distance(point, a + ab * s) - Radius;
        }

        public float DistanceTo(TimePieceView other)
        {
            Segment(out Vector3 a, out Vector3 b);
            other.Segment(out Vector3 c, out Vector3 d);
            return RitualUtil.SegmentDistance(a, b, c, d, out _, out _) - Radius - other.Radius;
        }

        public void Nudge(Vector3 velocity)
        {
            drift += velocity;
            _awake = true;
        }

        public void AddGlow(float amount)
        {
            if (_cloud == null || amount <= 0f) return;
            float[] glow = _cloud.glow;
            int n = _cloud.Count;
            for (int i = 0; i < n; i++) glow[i] = Mathf.Min(1f, Mathf.Max(glow[i], amount * (0.5f + RitualUtil.Hash(i * 31 + (int)(_age * 30f)))));
            _cloud.MarkSim();
            _awake = true;
        }

        public void Absorb(TimePieceView other, MergedShape target)
        {
            Reform(other, target == MergedShape.Blob ? PieceShape.Blob : PieceShape.Rod);
        }

        void Reform(TimePieceView other, PieceShape target)
        {
            int ua = data.units, ub = other != null ? other.data.units : 0;
            bool flow = other == null && shape == PieceShape.Rod && _config.formStyle == FormStyle.Flow;
            Vector3 chainBase = _cloud != null ? _cloud.transform.position : Vector3.zero;
            Vector3 chainAxis = _cloud != null ? _cloud.transform.forward : Vector3.forward;
            float chainLength = Mathf.Max(1e-3f, Length);
            int na = ua * _per, nb = ub * _per;
            int total = na + nb;
            var world = new float3[total];
            var velocity = new float3[total];
            var glow = new float[total];
            Collect(this, world, velocity, glow, 0, na);
            if (other != null) Collect(other, world, velocity, glow, na, nb);
            Vector3 centerA = Center, centerB = other != null ? other.Center : Center;
            Quaternion oldRot = _cloud.transform.rotation;
            Vector3 oldPos = _cloud.transform.position;
            PieceShape oldShape = shape;
            float oldLength = Length;
            string name = _cloud.gameObject.name;
            var carried = new List<Seam>();
            CarrySeams(this, carried);
            if (other != null) CarrySeams(other, carried);
            DisposeSeam();

            if (_label != null) _label.transform.SetParent(null, false);
            _cloud.Dispose();
            if (other != null) other.Kill();

            data.units = ua + ub;
            if (other != null) data.mergedCount += other.data.mergedCount + 1;
            Build(data.units, name);
            _allocatedUnits = data.units;
            _targetUnits = data.units;
            _visibleUnits = data.units;
            shape = target;
            _lump = other == null && target == PieceShape.Blob ? _config.crumpleLump : 0f;
            _reformed = true;
            _squeeze = 0f;
            _bend = Vector3.zero;

            Vector3 pos;
            Quaternion rot;
            if (shape == PieceShape.Blob)
            {
                _blobRadius = Mathf.Max(_config.formMinRadius, BlobRadius(_radius, Length));
                _baseBlobRadius = _blobRadius;
                if (other == null)
                {
                    pos = _tipWS.sqrMagnitude > 1e-6f ? (flow ? _tipWS : Vector3.Lerp(centerA, _tipWS, 0.5f)) : centerA;
                    if (_head != null && _head.position.sqrMagnitude > 1e-4f)
                    {
                        Vector3 toEye = Vector3.ProjectOnPlane(_head.position - pos, Vector3.up);
                        rot = toEye.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(toEye.normalized, Vector3.up) : oldRot;
                    }
                    else
                    {
                        Vector3 flat = Vector3.ProjectOnPlane(_axisWS, Vector3.up);
                        rot = flat.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(flat.normalized, Vector3.up) : oldRot;
                    }
                }
                else
                {
                    Vector3 away = centerA - centerB;
                    rot = away.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(away.normalized, Mathf.Abs(away.normalized.y) < 0.95f ? Vector3.up : Vector3.right) : oldRot;
                    pos = centerA;
                }
            }
            else
            {
                rot = oldRot;
                pos = oldShape == PieceShape.Rod ? oldPos : centerA - (oldRot * Vector3.forward) * (Length * 0.5f);
            }
            _cloud.transform.SetPositionAndRotation(pos, rot);
            _axisWS = rot * Vector3.forward;
            _seams.Clear();
            _seamAge = -1f;
            if (shape == PieceShape.Blob)
            {
                Quaternion toLocal = Quaternion.Inverse(rot);
                for (int c = 0; c < carried.Count; c++)
                {
                    Seam s = carried[c];
                    s.axis = math.normalizesafe((float3)(toLocal * (Vector3)s.axis), new float3(0f, 0f, 1f));
                    s.fresh = false;
                    _seams.Add(s);
                }
                if (other != null && _config.seam)
                {
                    _seams.Add(new Seam
                    {
                        axis = new float3(0f, 0f, 1f),
                        z0 = SeamLevel(ub / (float)Mathf.Max(1, ua + ub)),
                        seed = RitualUtil.Hash(data.units * 31 + data.mergedCount * 7 + categoryIndex),
                        fresh = true
                    });
                    _seamAge = 0f;
                }
            }

            float3x3 r = new float3x3((quaternion)rot);
            float3 p = pos;
            for (int i = 0; i < _cap; i++)
            {
                float3 h = CapHome(i, false, 0f, out float3 n);
                int s = _cloud.Add(h, n, _half);
                _cloud.scale[s] = 0f;
            }
            for (int i = 0; i < _cap; i++)
            {
                float3 h = CapHome(i, true, Length, out float3 n);
                int s = _cloud.Add(h, n, _half);
                _cloud.scale[s] = 0f;
            }
            float blobHalf = shape == PieceShape.Blob ? Mathf.Max(0.0020f, Mathf.Sqrt(4f * Mathf.PI * _blobRadius * _blobRadius / Mathf.Max(1, total)) * 0.95f) : _half;
            _blobHalf = blobHalf;
            float pieceHalf = shape == PieceShape.Blob ? blobHalf : _half;
            var toHomes = new float3[total];
            var toNormals = new float3[total];
            var fromLocal = new float3[total];
            Quaternion invRot = Quaternion.Inverse(rot);
            for (int q = 0; q < total; q++)
            {
                toHomes[q] = shape == PieceShape.Blob ? BlobHome(q, total, out toNormals[q]) : RodHome(q, out toNormals[q]);
                fromLocal[q] = (float3)(invRot * ((Vector3)world[q] - (Vector3)p));
            }
            float[] hashes = SplatSimSetup.Hashes(toHomes);
            int[] map = SplatSimSetup.MapByShape(fromLocal, toHomes, hashes);
            float3[] burstVel = SplatSimSetup.BurstVelocities(toHomes, hashes, 0.24f);
            var arrivals = new float[total];
            SplatSimSetup.GatherArrivals(toHomes, hashes, arrivals, 0.28f);
            float burstWindow = flow ? 0f : 0.10f;
            float flowSeconds = Mathf.Max(0.05f, _config.formFlowSeconds);
            _mergeWindow = flow ? flowSeconds + 0.6f : 0.9f;
            for (int q = 0; q < total; q++)
            {
                float3 h = toHomes[q];
                int m = map[q];
                int s = _cloud.Add(h, toNormals[q], pieceHalf);
                _cloud.disp[s] = world[m] - (p + math.mul(r, h));
                _cloud.glow[s] = Mathf.Max(glow[m], 0.85f);
                if (flow)
                {
                    float along = Mathf.Clamp01(Vector3.Dot((Vector3)world[m] - chainBase, chainAxis) / chainLength);
                    _cloud.vel[s] = velocity[m] * 0.30f;
                    _delay[s] = flowSeconds * (1f - along) + hashes[q] * 0.06f;
                }
                else
                {
                    _cloud.vel[s] = velocity[m] * 0.30f + math.mul(r, burstVel[q]);
                    _delay[s] = burstWindow + arrivals[q];
                }
            }
            _age = 0f;
            _nodeAmount = 0f;
            _nodeTarget = _config.nodesAlwaysVisible ? 1f : 0f;
            nodePulse = 1f;
            _flash = 1f;
            _labelUnits = -1;
            _labelMerged = -1;
            _rest = pos;
            _awake = true;
            if (state == PieceState.Pulling) state = PieceState.Floating;
            _surface.MarkDirty();
            _seamPresence = 1f;
            BuildSeamMesh();
        }

        static void CarrySeams(TimePieceView piece, List<Seam> into)
        {
            if (piece == null || piece._cloud == null || piece.shape != PieceShape.Blob) return;
            Quaternion rot = piece._cloud.transform.rotation;
            for (int i = 0; i < piece._seams.Count; i++)
            {
                Seam s = piece._seams[i];
                s.axis = (float3)(rot * (Vector3)s.axis);
                into.Add(s);
            }
        }

        public static float SeamLevel(float share)
        {
            share = Mathf.Clamp01(share);
            float lo = 0f, hi = 2f;
            for (int it = 0; it < 24; it++)
            {
                float h = 0.5f * (lo + hi);
                if (h * h * (3f - h) * 0.25f < share) lo = h;
                else hi = h;
            }
            return 0.5f * (lo + hi) - 1f;
        }

        float3 SeamPoint(in Seam s, float3 tu, float3 tv, float phi, float radius, out float3 n)
        {
            float z = s.z0 + 0.07f * math.sin(3f * phi + s.seed * 6.2831853f) + 0.035f * math.sin(7f * phi + s.seed * 17.3f);
            z = math.clamp(z, -0.93f, 0.93f);
            float ring = math.sqrt(math.max(0f, 1f - z * z));
            float3 d = s.axis * z + (tu * math.cos(phi) + tv * math.sin(phi)) * ring;
            return ShapePoint(ShapeKind, d, radius, out n);
        }

        void BuildSeamMesh()
        {
            DisposeSeam();
            if (_seams.Count == 0 || _seamMaterial == null || _cloud == null || shape != PieceShape.Blob) return;
            int per = Mathf.Max(12, _config.seamSplats);
            float radius = _blobRadius * Mathf.Max(1f, _config.seamLift);
            var builder = RitualStaticSplats.Builder.Create(per * _seams.Count);
            Vector3 headLocal = Vector3.forward;
            if (_head != null && _head.position.sqrMagnitude > 1e-4f) headLocal = _cloud.transform.InverseTransformPoint(_head.position);
            for (int si = 0; si < _seams.Count; si++)
            {
                Seam s = _seams[si];
                RitualUtil.Basis(s.axis, out float3 tu, out float3 tv);
                float3 hl = headLocal;
                float phi0 = math.atan2(math.dot(hl, tv), math.dot(hl, tu));
                int seedInt = (int)(s.seed * 9973f);
                for (int j = 0; j < per; j++)
                {
                    float phi = 6.2831853f * j / per;
                    float3 p = SeamPoint(s, tu, tv, phi, radius, out float3 nrm);
                    float lane = (j % 3 == 1) ? 0.6f : (j % 3 == 2 ? -0.5f : 0f);
                    float3 side = math.normalizesafe(math.cross(nrm, tu * -math.sin(phi) + tv * math.cos(phi)), s.axis);
                    p += side * (lane * _config.seamRadius * 1.4f);
                    float rnd = RitualUtil.Hash(j * 13 + seedInt);
                    if (!s.fresh)
                    {
                        builder.Add((Vector3)p, (Vector3)nrm, new Vector4(p.x, p.y, p.z, 0f), new Vector4(nrm.x, nrm.y, nrm.z, 0f), new Vector4(nrm.x, nrm.y, nrm.z, rnd));
                        continue;
                    }
                    float dphi = phi - phi0;
                    dphi -= 6.2831853f * math.floor((dphi + math.PI) / 6.2831853f);
                    float delay = math.abs(dphi) / math.PI;
                    float3 start = SeamPoint(s, tu, tv, phi0, radius, out _);
                    float3 via = SeamPoint(s, tu, tv, phi0 + dphi * 0.5f, radius, out _);
                    builder.Add((Vector3)start, (Vector3)nrm, new Vector4(p.x, p.y, p.z, delay), new Vector4(nrm.x, nrm.y, nrm.z, -1f), new Vector4(via.x, via.y, via.z, rnd));
                }
            }
            _seamMesh = builder.ToMesh("PieceSeam", radius * 1.6f + 0.02f);
            _seamRenderer = RitualStaticSplats.Spawn("Seam", _cloud.transform, _seamMesh, _seamMaterial);
            _seamBlock ??= new MaterialPropertyBlock();
            ApplySeam(0f);
        }

        void ApplySeam(float dt)
        {
            if (_seamRenderer == null) return;
            if (_seamAge >= 0f)
            {
                _seamAge += dt;
                if (_seamAge > _config.seamDelay + _config.seamSeconds + 0.5f) _seamAge = -1f;
            }
            float press = SeamPress;
            float flowing = _seamAge >= 0f && press < 1f ? Mathf.Sin(press * Mathf.PI) : 0f;
            float target;
            if (state == PieceState.Pouring) target = 1f - Mathf.Clamp01(_pourProgress * 2.5f);
            else if (state == PieceState.Dissolving) target = 1f - Mathf.Clamp01(_dissolve * 2f);
            else
            {
                int grains = Mathf.Max(1, _cloud.Count - 2 * _cap);
                target = 1f - Mathf.Clamp01(_looseCount / (float)grains * 2.5f);
            }
            _seamPresence = dt > 0f ? Mathf.MoveTowards(_seamPresence, target, dt * 4f) : target;
            bool show = _seamPresence > 0.01f;
            if (_seamRenderer.enabled != show) _seamRenderer.enabled = show;
            if (!show) return;
            _seamBlock.SetColor(SeamColorId, new Color(0.96f, 0.78f, 0.42f, 1f));
            _seamBlock.SetColor(SeamDeepId, new Color(0.42f, 0.28f, 0.10f, 1f));
            _seamBlock.SetColor(SeamLightId, new Color(1f, 0.95f, 0.82f, 1f));
            _seamBlock.SetFloat(SeamGlowId, _config.seamGlow * (0.55f + 1.1f * flowing));
            _seamBlock.SetFloat(SeamSheenId, 0.6f);
            _seamBlock.SetFloat(SeamRadiusId, _config.seamRadius);
            _seamBlock.SetFloat(SeamPressId, press);
            _seamBlock.SetFloat(SeamStaggerId, 1.5f);
            _seamBlock.SetFloat(SeamStampId, 0f);
            _seamBlock.SetFloat(SeamAppearId, _seamPresence);
            _seamBlock.SetVector(SeamOriginId, Vector4.zero);
            _seamBlock.SetFloat(SeamBobId, 0f);
            _seamBlock.SetFloat(SeamGrowId, 0f);
            _seamRenderer.SetPropertyBlock(_seamBlock);
        }

        void DisposeSeam()
        {
            if (_seamRenderer != null) RitualUtil.Kill(_seamRenderer.gameObject);
            _seamRenderer = null;
            if (_seamMesh != null) RitualUtil.Kill(_seamMesh);
            _seamMesh = null;
        }

        static void Collect(TimePieceView piece, float3[] world, float3[] velocity, float[] glow, int offset, int count)
        {
            RitualSplatCloud cloud = piece._cloud;
            float3x3 rot = new float3x3((quaternion)cloud.transform.rotation);
            float3 pos = cloud.transform.position;
            int start = 2 * piece._cap;
            for (int q = 0; q < count; q++)
            {
                int s = start + q;
                if (s >= cloud.Count)
                {
                    world[offset + q] = pos;
                    continue;
                }
                world[offset + q] = pos + math.mul(rot, cloud.home[s]) + cloud.disp[s];
                velocity[offset + q] = cloud.vel[s];
                glow[offset + q] = cloud.glow[s];
            }
        }

        public void BeginPour(Vector3 spoutWS)
        {
            state = PieceState.Pouring;
            ClearLoose();
            _pourProgress = 0f;
            drift = Vector3.zero;
            int n = _cloud.Count;
            if (shape == PieceShape.Rod)
            {
                Segment(out Vector3 a, out Vector3 b);
                _pourFromTip = (b - spoutWS).sqrMagnitude < (a - spoutWS).sqrMagnitude;
                float len = Mathf.Max(1e-5f, Length);
                for (int i = 0; i < n; i++)
                {
                    float z = Mathf.Clamp01(_cloud.home[i].z / len);
                    _order[i] = _pourFromTip ? 1f - z : z;
                }
            }
            else
            {
                float3 down = math.mul(math.inverse((quaternion)_cloud.transform.rotation), new float3(0f, -1f, 0f));
                float inv = 1f / Mathf.Max(1e-5f, _blobRadius);
                for (int i = 0; i < n; i++) _order[i] = math.saturate(0.5f - 0.5f * math.dot(_cloud.home[i] * inv, down));
            }
            _nodeTarget = 0f;
            _awake = true;
        }

        public void PourStep(float progress, Vector3 spoutWS, float dt)
        {
            _pourProgress = Mathf.Clamp01(progress);
            Transform t = _cloud.transform;
            float follow = 1f - Mathf.Exp(-dt * 9f);
            if (shape == PieceShape.Rod)
            {
                Vector3 want = _pourFromTip ? Vector3.down : Vector3.up;
                Quaternion rot = Quaternion.Slerp(t.rotation, Quaternion.FromToRotation(t.forward, want) * t.rotation, follow);
                float front = _pourFromTip ? Length * (1f - _pourProgress) : Length * _pourProgress;
                Vector3 pos = spoutWS - rot * new Vector3(0f, 0f, front);
                SetPose(Vector3.Lerp(t.position, pos, follow), rot, _config.carry);
            }
            else
            {
                Vector3 pos = spoutWS + Vector3.up * (_blobRadius * (1f - 2f * _pourProgress));
                SetPose(Vector3.Lerp(t.position, pos, follow), t.rotation, _config.carry);
            }
            _awake = true;
            if (_pourProgress >= 1f) Kill();
        }

        public float MeltRadius
        {
            get
            {
                if (shape == PieceShape.Rod) return _radius;
                float h = 1f - 2f * _pourProgress;
                return _blobRadius * Mathf.Sqrt(Mathf.Max(0.05f, 1f - h * h));
            }
        }

        public void Kill()
        {
            state = PieceState.Dead;
            _shadow = null;
            if (_label != null) RitualUtil.Kill(_label.gameObject);
            _label = null;
            DisposeSeam();
            _seams.Clear();
            _cloud?.Dispose();
            _cloud = null;
        }

        public void Tick(float now, float dt)
        {
            if (_cloud == null || state == PieceState.Dead) return;
            dt = Mathf.Min(dt, 1f / 45f);
            _age += dt;
            if (!_bridgeHeld && _bridgeAmount > 0f)
            {
                _bridgeAmount = 0f;
                _awake = true;
            }

            if (state == PieceState.Floating)
            {
                if (drift.sqrMagnitude > 1e-8f)
                {
                    _rest += drift * dt;
                    drift *= Mathf.Exp(-_config.pieceDrag * dt);
                    if (drift.sqrMagnitude < 1e-7f) drift = Vector3.zero;
                }
                if (_hasFloat)
                {
                    if (_floatWait > 0f) _floatWait -= dt;
                    else
                    {
                        _floatAge += dt;
                        Vector3 to = _floatTarget - _rest;
                        float dist = to.magnitude;
                        float ease = Mathf.Clamp01(_floatAge / 0.8f);
                        float speed = Mathf.Min(_config.floatSpeed * ease * ease, dist * 1.2f);
                        if (dist < 0.002f) _hasFloat = false;
                        else _rest += to / dist * Mathf.Min(dist, speed * dt);
                    }
                }
                float bob = Mathf.Sin(now * _config.hoverBobSpeed * 6.2831853f + _bobPhase) * _config.hoverBob;
                _cloud.transform.position = _rest + Vector3.up * bob;
            }
            else if (state == PieceState.Dissolving)
            {
                _dissolve += dt / 0.28f;
                if (_dissolve >= 1f)
                {
                    Kill();
                    return;
                }
            }

            if (state != PieceState.Pulling && _bend.sqrMagnitude > 0f)
            {
                _bend *= Mathf.Exp(-dt * Mathf.Max(0.1f, _config.threadRelax));
                if (_bend.sqrMagnitude < 1e-10f) _bend = Vector3.zero;
                _awake = true;
            }
            float nodeGoal = Mathf.Max(_nodeTarget, nodePulse > 0f ? Mathf.Sin(Mathf.Clamp01(nodePulse) * Mathf.PI) : 0f);
            if (state == PieceState.Pulling) nodeGoal = 1f;
            float nodeRate = nodeGoal > _nodeAmount ? 1f / Mathf.Max(0.01f, _config.nodeRiseSeconds) : 1f / Mathf.Max(0.01f, _config.nodeMeltSeconds);
            float nodeBefore = _nodeAmount;
            _nodeAmount = Mathf.MoveTowards(_nodeAmount, nodeGoal, dt * nodeRate);
            if (nodePulse > 0f) nodePulse = Mathf.Max(0f, nodePulse - dt / Mathf.Max(0.05f, _config.mergeSeconds + 0.4f));
            if (!Mathf.Approximately(nodeBefore, _nodeAmount) || nodePulse > 0f) _awake = true;
            if (state == PieceState.Pulling) _visibleUnits = Mathf.MoveTowards(_visibleUnits, _targetUnits, dt * _config.unitGrowRate);
            bool twinkling = _nodeAmount > 0.01f && category != null && category.nodeTwinkle > 0.001f && shape == PieceShape.Rod;
            if (twinkling) _awake = true;

            if (_awake)
            {
                Simulate(now, dt);
                bool busy = state == PieceState.Pulling || state == PieceState.Held || state == PieceState.Pouring || state == PieceState.Dissolving;
                if (!busy && !twinkling && _energy < 2e-6f && nodePulse <= 0f && Mathf.Approximately(_nodeAmount, _nodeTarget) && _age > Mathf.Max(_config.mergeDelaySpread, 0.42f) + 0.1f)
                    _awake = false;
            }
            _flash = Mathf.Max(0f, _flash - dt * 3.5f);
            _cloud.Flush();
            _surface.Apply(_cloud.renderer, _cloud.SimTexture, now, 1f + _flash * 0.25f);
            UpdateLabel();
            ApplySeam(dt);
            _bridgeHeld = false;
        }

        void Simulate(float now, float dt)
        {
            Transform tr = _cloud.transform;
            float3x3 rot = new float3x3((quaternion)tr.rotation);
            float3 pos = tr.position;
            int cap2 = 2 * _cap;
            bool rod = shape == PieceShape.Rod;
            bool pulling = state == PieceState.Pulling;
            bool pouring = state == PieceState.Pouring;
            bool dissolving = state == PieceState.Dissolving;
            bool merging = (_reformed || data.mergedCount > 0) && _age < _mergeWindow;
            float k = merging ? _config.mergeStiffness : (feel == PieceFeel.Water ? 180f : 260f);
            float c = merging ? _config.mergeDamping : (feel == PieceFeel.Water ? 13.5f : 16.0f);
            float maxAccel = feel == PieceFeel.Water ? 25f : 32f;
            if (!merging && !pulling && !pouring && !dissolving && shape == PieceShape.Blob)
            {
                k *= 1.15f;
                c *= 1.10f;
            }
            float nodeGrain = category != null ? category.nodeGrain : 0.75f;
            float nodeGlow = category != null ? category.nodeGlow : 0.55f;
            float twinkle = category != null ? category.nodeTwinkle : 0f;
            float twinkleSpeed = category != null ? category.nodeTwinkleSpeed : 6f;
            float len = Mathf.Max(_unitLength, data.units * _unitLength);
            float stretch = pulling ? Mathf.Clamp(_tension, -0.35f, 0.65f) * _unitLength * _config.stretchShare : 0f;
            float invLen = 1f / len;
            float decay = _config.glowDecay * dt;
            float grow = dt * 14f;
            float3 tipWS = _tipWS;
            float3 tipPull = _tipPull;
            float3 dissolveTo = _dissolveTo;
            float3 bend = _bend;
            bool bent = rod && math.lengthsq(bend) > 1e-10f;
            float squash = rod ? _squeeze : 0f;
            float knead = rod ? 0f : _squeeze * _config.blobSquash;
            float3 kneadAxis = _squeezeAxis;
            float squeezeMid = len * 0.5f;
            float shorten = squash * _config.squeezeShorten;
            float3[] disp = _cloud.disp;
            float3[] vel = _cloud.vel;
            float3[] home = _cloud.home;
            float[] scale = _cloud.scale;
            float[] glow = _cloud.glow;
            int n = _cloud.Count;
            if (pulling)
            {
                int keep = Mathf.Max(1, _targetUnits);
                for (int i = 0; i < n; i++)
                {
                    disp[i] = float3.zero;
                    vel[i] = float3.zero;
                    scale[i] = i >= cap2 && (i - cap2) / _per < keep ? 1f : 0f;
                    float fg = i >= cap2 ? _config.unitFlash * 0.15f : 0f;
                    glow[i] = math.max(fg, glow[i] - decay);
                }
                _energy = 0f;
                _cloud.MarkSim();
                return;
            }
            bool bridging = _bridgeAmount > 0.001f && !pouring && !dissolving;
            float3 bridgePoint = _bridgePoint;
            float3 bridgeDir = math.normalizesafe(bridgePoint - pos, new float3(0f, 0f, 1f));
            float bridgeAmount = _bridgeAmount;
            float bridgeShare = _config.bridgeShare;
            float bridgeReach = _config.bridgeReach;
            float[] loose = _loose;
            float[] soft = _soft;
            float flight = Mathf.Max(0f, _config.scatterFlight);
            float flightDrag = Mathf.Max(0f, _config.scatterDrag);
            float returnSeconds = Mathf.Max(0.2f, _config.returnSeconds);
            float returnSwirl = _config.returnSwirl;
            float softRate = dt / Mathf.Max(0.05f, _config.partRecover);
            int looseCount = 0;
            float energy = 0f;
            float glowMax = 0f;
            for (int i = 0; i < n; i++)
            {
                if ((!merging || i < cap2) && _age < _delay[i])
                {
                    energy = 1f;
                    continue;
                }
                float3 target = float3.zero;
                float want = 1f;
                float floor = 0f;
                float3 h = home[i];
                if (i < cap2)
                {
                    if (!rod || !pulling || i < _cap) want = 0f;
                    else
                    {
                        want = 0.35f;
                        float reach = 0.35f + 0.65f * (1f - (i - _cap + 0.5f) / _cap);
                        target = tipPull * reach;
                    }
                }
                else if (rod)
                {
                    int idx = i - cap2;
                    int ku = idx / _per;
                    int j = (idx - ku * _per) / _ring;
                    target = math.mul(rot, new float3(0f, 0f, h.z * invLen * stretch));
                    if (pulling)
                    {
                        want = math.saturate(_visibleUnits - ku);
                        if (ku >= _targetUnits) target = tipWS - (pos + math.mul(rot, h));
                    }
                    if (j == _rings - 1 && _nodeAmount > 0.001f)
                    {
                        want *= math.lerp(1f, nodeGrain, _nodeAmount);
                        float flick = twinkle > 0f ? 1f - twinkle + twinkle * (0.5f + 0.5f * math.sin(now * twinkleSpeed + i * 2.399f)) : 1f;
                        floor = nodeGlow * _nodeAmount * _strength[ku + 1] * flick;
                    }
                }
                if (bent)
                {
                    float u = math.saturate(h.z * invLen);
                    target += bend * (4f * u * (1f - u));
                }
                if (squash > 0f)
                {
                    target += math.mul(rot, new float3(h.x * squash * 0.18f, h.y * squash * 0.18f, (squeezeMid - h.z) * shorten));
                }
                if (knead > 0f && i >= cap2)
                {
                    float3 hw = math.mul(rot, h);
                    float along = math.dot(hw, kneadAxis);
                    target += kneadAxis * (-along * knead) + (hw - kneadAxis * along) * (knead * 0.48f);
                }
                float stick = 0f;
                if (bridging && i >= cap2)
                {
                    float3 hw = math.mul(rot, h);
                    float hl = math.length(hw);
                    if (hl > 1e-6f)
                    {
                        float facing = math.dot(hw / hl, bridgeDir);
                        if (facing > 0.3f)
                        {
                            float strand = RitualUtil.Hash(i * 7 + 5) < bridgeShare ? 1f : 0.22f;
                            float ft = math.saturate((facing - 0.3f) / 0.65f);
                            stick = ft * ft * (3f - 2f * ft) * strand * bridgeAmount;
                            target += (bridgePoint - (pos + hw)) * (stick * bridgeReach);
                        }
                    }
                }
                if (pouring && _order[i] < _pourProgress) want = 0f;
                if (dissolving)
                {
                    want = 0f;
                    target = dissolveTo - (pos + math.mul(rot, h));
                }
                float3 d = disp[i];
                float3 v = vel[i];
                if (merging && !pouring && !dissolving && i >= cap2)
                {
                    float hash = RitualUtil.Hash(i * 19 + 3);
                    if (_age < _delay[i])
                    {
                        float3 swirl = SplatSimKernel.HashDirection(hash) * 0.08f;
                        v += swirl * dt;
                        v.y -= 0.35f * dt;
                        v *= math.exp(-3.85f * dt);
                        d += v * dt;
                        disp[i] = ClampVec(d, 0.35f);
                        vel[i] = ClampVec(v, 1.8f);
                        scale[i] = math.min(1.08f, scale[i] + grow);
                        float bg = math.max(floor, math.max(glow[i] - decay, 0.78f));
                        glow[i] = bg;
                        if (bg > floor + 1e-3f) glowMax = math.max(glowMax, bg);
                        energy = 1f;
                        continue;
                    }
                    float gatherAge = _age - _delay[i];
                    float ramp = math.saturate(gatherAge / 0.24f);
                    float3 to = target - d;
                    float dist = math.length(to);
                    float3 dir = dist > 1e-5f ? to / dist : float3.zero;
                    float swirlFade = 1f - math.saturate(gatherAge / 0.36f);
                    float speedWant = math.min(0.85f, dist * 6.5f);
                    float3 side = math.cross(dir, SplatSimKernel.HashDirection(hash));
                    float3 swirlVel = dir * speedWant + side * (0.65f * speedWant * math.saturate(dist * 12f));
                    float3 swirlAccel = (swirlVel - v) * (20f * swirlFade);
                    float3 springAccel = SplatSimKernel.ClampLength(to * (180f * _stiff[i] * ramp), 28f * (0.5f + 0.7f * ramp));
                    v += (swirlAccel + springAccel) * dt;
                    v *= math.exp(-math.lerp(6.0f, 14.0f, ramp) * dt);
                    d += v * dt;
                    want = math.lerp(1.08f, want, ramp);
                    if (gatherAge < 0.36f)
                        floor = math.max(floor, math.saturate(dist * 16f) * 0.85f);
                }
                else if (loose[i] >= 0f && !pouring && !dissolving)
                {
                    float age = loose[i] + dt;
                    float3 to = target - d;
                    float dist = math.length(to);
                    if (age < flight)
                    {
                        v *= math.exp(-flightDrag * dt);
                        d += v * dt;
                    }
                    else
                    {
                        float back = age - flight;
                        float u = math.saturate(back / returnSeconds);
                        float ease = math.saturate(back / (returnSeconds * 0.25f));
                        ease = ease * ease * (3f - 2f * ease);
                        float3 dir = dist > 1e-5f ? to / dist : float3.zero;
                        float speedWant = math.min(0.45f, dist * (3f / returnSeconds)) * ease;
                        float3 side = math.cross(dir, SplatSimKernel.HashDirection(RitualUtil.Hash(i * 19 + 3))) * (returnSwirl * (1f - u) * speedWant);
                        float3 wantV = dir * speedWant + side;
                        v += (wantV - v) * (1f - math.exp(-7f * dt));
                        d += v * dt;
                        if (u >= 1f || (dist < 0.003f && back > returnSeconds * 0.3f))
                        {
                            age = -1f;
                            soft[i] = 1f;
                        }
                    }
                    loose[i] = age;
                    if (age >= 0f) looseCount++;
                    energy = 1f;
                }
                else
                {
                    float sf = soft[i];
                    float kk = k * _stiff[i] * (1f - 0.85f * sf) * (1f - 0.6f * stick);
                    float3 spring = SplatSimKernel.ClampLength((target - d) * kk, maxAccel);
                    v += spring * dt;
                    v *= math.exp(-c * (1f - 0.5f * sf) * dt);
                    d += v * dt;
                    if (sf > 0f) soft[i] = math.max(0f, sf - softRate);
                }
                float maxDisp = merging || loose[i] >= 0f ? 0.35f : (stick > 0f ? 0.14f : (feel == PieceFeel.Water ? 0.08f : 0.06f));
                if (!math.all(math.isfinite(v)) || !math.all(math.isfinite(d)))
                {
                    d = float3.zero;
                    v = float3.zero;
                }
                else
                {
                    v = ClampVec(v, 1.8f);
                    d = ClampVec(d, maxDisp);
                }
                disp[i] = d;
                vel[i] = v;
                float s = scale[i];
                s = s < want ? math.min(want, s + grow) : math.max(want, s - grow);
                scale[i] = s;
                float g = math.max(floor, glow[i] - decay);
                glow[i] = g;
                if (g > floor + 1e-3f) glowMax = math.max(glowMax, g);
                energy = math.max(energy, math.lengthsq(target - d) * 2e4f + math.lengthsq(v) + math.abs(s - want) + soft[i] * 1e-3f);
            }
            _looseCount = looseCount;
            _energy = energy + glowMax * 1e-3f;
            _cloud.MarkSim();
        }

        void UpdateLabel()
        {
            if (_label == null) return;
            bool numbers = _config.timeLabels;
            bool inHand = state == PieceState.Held || state == PieceState.Pulling;
            bool show = _config.showMinutesLabel && data.units > 0 && state != PieceState.Pouring && state != PieceState.Dissolving && (numbers || inHand);
            if (_label.gameObject.activeSelf != show) _label.gameObject.SetActive(show);
            if (!show) return;
            bool active = state == PieceState.Pulling || state == PieceState.Held || _age < 2.2f || _flash > 0.05f || nodePulse > 0.05f;
            bool compact = !active && state == PieceState.Floating;
            if (_labelUnits != data.units || _labelShape != shape || _labelFeel != feel || _labelMerged != data.mergedCount || _labelCompact != compact || _labelNumbers != numbers)
            {
                _labelNumbers = numbers;
                _labelUnits = data.units;
                _labelShape = shape;
                _labelFeel = feel;
                _labelMerged = data.mergedCount;
                _labelCompact = compact;
                string sym = category != null ? category.EffectiveSymbol : "*";
                string title = category != null ? category.EffectiveTitleEn : "Time";
                string mem = category != null ? category.EffectiveMemoryEn : "";
                string dur = TimeUnits.LabelEn(data.units);
                string line1 = $"{sym} {title} · {dur}";
                string line2 = compact ? "" : mem;
                string formatted = !numbers ? title : string.IsNullOrEmpty(line2) ? line1 : $"{line1}\n{line2}";
                _label.text = formatted;
                if (_shadow != null) _shadow.text = formatted;
            }
            float targetAlpha = active ? 0.98f : 0.72f;
            _labelAlpha = Mathf.MoveTowards(_labelAlpha, targetAlpha, 0.04f);
            _label.color = new Color(0.98f, 0.96f, 0.92f, _labelAlpha);
            if (_shadow != null) _shadow.color = new Color(0.02f, 0.02f, 0.03f, _labelAlpha * 0.85f);
            Transform t = _cloud.transform;
            Vector3 tip = shape == PieceShape.Blob ? t.position + Vector3.up * (_blobRadius + 0.006f) : t.TransformPoint(new Vector3(0f, 0f, Length));
            _label.transform.position = tip + Vector3.up * 0.022f;
            Vector3 eyePos = (_head != null && _head.position.sqrMagnitude > 1e-4f) ? _head.position : new Vector3(0f, 1.17f, 0.02f);
            Vector3 view = _label.transform.position - eyePos;
            float horiz = Mathf.Sqrt(view.x * view.x + view.z * view.z);
            view.y = Mathf.Clamp(view.y, -horiz * 0.18f, horiz * 0.18f);
            if (view.sqrMagnitude > 1e-6f) _label.transform.rotation = Quaternion.LookRotation(view.normalized, Vector3.up);
        }
    }
}
