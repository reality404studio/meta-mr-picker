using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MoodPicker.Ritual
{
    public sealed class RitualShelf
    {
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int RevealId = Shader.PropertyToID("_Reveal");
        static readonly int BreathId = Shader.PropertyToID("_Breath");
        static readonly int SwirlId = Shader.PropertyToID("_Swirl");
        static readonly int FlatId = Shader.PropertyToID("_Flat");
        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int InnerId = Shader.PropertyToID("_Inner");
        static readonly int RimId = Shader.PropertyToID("_Rim");
        static readonly int SpecId = Shader.PropertyToID("_Spec");
        static readonly int BodyId = Shader.PropertyToID("_Body");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int PulseId = Shader.PropertyToID("_Pulse");

        const string Glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ,.:\u00b7";
        const float RingRadius = 0.056f;
        const float PedestalRadius = 0.072f;

        public sealed class ShelfJar
        {
            public int slot;
            public DayRecord record;
            public Transform root;
            public MeshRenderer glass;
            public readonly MaterialPropertyBlock glassBlock = new MaterialPropertyBlock();
            public readonly MaterialPropertyBlock strataBlock = new MaterialPropertyBlock();
            public readonly List<MeshRenderer> strata = new List<MeshRenderer>();
            public readonly List<Mesh> meshes = new List<Mesh>();
            public readonly RitualSwirl swirl = new RitualSwirl();
            public Color dominantGlow = Color.white;
            public RitualJar vessel;
            public RitualWax wax;
            public float level;
            public string info = "";
            public float light = 1f;
            public float poke;
            public float cooldown;
        }

        sealed class Glow
        {
            public MeshRenderer renderer;
            public Mesh mesh;
            public bool flat;
            public float breath = 0.08f;
            public readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        }

        readonly TimeRitualConfig _config;
        readonly Material _glow;
        readonly Material _frost;
        readonly Material _glass;
        readonly Font _font;
        readonly Func<string, Color> _colorOf;
        readonly Func<string, string> _nameOf;
        readonly GameObject _root;
        readonly List<ShelfJar> _jars = new List<ShelfJar>();
        readonly List<TextMesh> _dates = new List<TextMesh>();
        readonly MaterialPropertyBlock _surfaceBlock = new MaterialPropertyBlock();
        readonly Glow _edge = new Glow();
        readonly Glow _rings = new Glow { flat = true, breath = 0.35f };
        readonly Glow _todayRing = new Glow { flat = true, breath = 0f };
        readonly Glow _pools = new Glow { flat = true, breath = 0.1f };
        readonly Glow _todayPool = new Glow { flat = true, breath = 0.1f };
        readonly Glow[] _ripples = { new Glow { flat = true, breath = 0f }, new Glow { flat = true, breath = 0f }, new Glow { flat = true, breath = 0f } };
        readonly int[] _inside = { -1, -1 };

        MeshRenderer _surface;
        Mesh _surfaceMesh;
        Mesh _rippleMesh;
        TextMesh _note;
        string _noteText = "";
        Vector3[] _slots = new Vector3[0];
        Vector3 _eye;
        Vector3 _forward = Vector3.forward;
        float _surfaceY;
        DateTime _today = DateTime.Today;
        float _brightness;
        float _clock;
        float _rippleAge = -1f;
        float _poolAge = -1f;
        float _noteAge = -1f;
        float _noteHold;
        Vector3 _notePos;
        bool _filled;
        float _stage = 1f;
        Vector3 _corner;
        bool _hasCorner;
        float _flashAge = -1f;
        bool _farQuality;

        public Mesh GlassMesh { get; set; }
        public Func<Transform, DayRecord, (RitualJar, RitualWax)> MakeJar { get; set; }
        public float Stage => _stage;
        public float Flash { get; private set; }
        public float Scale => _root != null ? _root.transform.localScale.x : 1f;
        public int SlotCount => _slots.Length;
        public int TodaySlot => Mathf.Clamp(_config.shelfTodaySlot, 0, Mathf.Max(0, _slots.Length - 1));
        public Vector3 TodayPosition => _slots.Length > 0 ? _root.transform.TransformPoint(_slots[TodaySlot]) : Vector3.zero;
        public Vector3 TodaySeat => TodayPosition;
        public Vector3 Slot(int i) => _root.transform.TransformPoint(_slots[i]);
        public Vector3 Eye => _eye;

        public Quaternion TodayFacing
        {
            get
            {
                if (_slots == null || _slots.Length == 0) return Quaternion.LookRotation(_forward, Vector3.up);
                SideAt(_slots[TodaySlot], out Vector3 toEye);
                return Quaternion.LookRotation(-toEye, Vector3.up);
            }
        }
        public float SurfaceY => _surfaceY;
        public bool TodayFilled => _filled;
        public float Brightness => _brightness;
        public int MemoryJars => _jars.Count;
        public int MemorySplats { get; private set; }
        public IReadOnlyList<ShelfJar> Jars => _jars;
        public float TodayPulse { get; private set; }
        public float TodayNear { get; private set; }
        public int Surfaces { get; private set; }
        public bool Rippling => _rippleAge >= 0f;
        public string NoteText => _noteAge >= 0f ? _noteText : "";
        public float NoteAlpha { get; private set; }
        public int Pokes { get; private set; }

        public RitualShelf(TimeRitualConfig config, Material glow, Material frost, Material glass, Font font, Func<string, Color> colorOf, Func<string, string> nameOf)
        {
            _config = config;
            _glow = glow;
            _frost = frost;
            _glass = glass;
            _font = font;
            _colorOf = colorOf;
            _nameOf = nameOf;
            _root = new GameObject("RitualShelf");
            _root.hideFlags = HideFlags.DontSave;
        }

        public static string DateLabel(DateTime day) => day.ToString("MMM d", CultureInfo.InvariantCulture);
        public static string LoggedLabel(DateTime day) => day.ToString("MMMM d", CultureInfo.InvariantCulture) + ", Logged.";

        public DateTime DateOf(int slot) => _today.AddDays(slot - TodaySlot);
        public string DateText(int slot) => DateLabel(DateOf(slot));

        public void Place(Vector3 eye, Vector3 forward, float surfaceY, DateTime today)
        {
            _eye = eye;
            _surfaceY = surfaceY;
            _today = today.Date;
            Vector3 f = Vector3.ProjectOnPlane(forward, Vector3.up);
            _forward = f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
            int n = Mathf.Max(1, _config.shelfSlots);
            _slots = new Vector3[n];
            for (int i = 0; i < n; i++) _slots[i] = OnArc(AngleOf(i), _config.shelfDistance);
            Rest();
            BuildSurface();
            BuildEdge();
            BuildRings();
            BuildDates();
            ClearToday();
            ApplyStage();
        }

        void Rest()
        {
            _root.transform.localPosition = Vector3.zero;
            _root.transform.localRotation = Quaternion.identity;
            _root.transform.localScale = Vector3.one;
        }

        public void SetStage(float near)
        {
            near = Mathf.Clamp01(near);
            if (Mathf.Approximately(near, _stage) && _root.transform.localScale.x > 0f) return;
            _stage = near;
            ApplyStage();
        }

        public void SetCorner(Vector3 world)
        {
            _corner = world;
            _hasCorner = true;
            ApplyStage();
        }

        public Vector3 Corner => _corner;

        void ApplyStage()
        {
            if (_slots.Length == 0) return;
            Vector3 pivot = _slots[TodaySlot];
            Vector3 corner = _hasCorner ? _corner : pivot;
            float scale = Mathf.Lerp(Mathf.Clamp(_config.shelfMiniScale, 0.05f, 1f), 1f, _stage);
            Vector3 at = Vector3.Lerp(corner, pivot, _stage) + Vector3.up * (0.06f * Mathf.Sin(_stage * Mathf.PI));
            _root.transform.localScale = Vector3.one * scale;
            _root.transform.localPosition = at - pivot * scale;
            bool far = _stage < 0.5f;
            if (far == _farQuality) return;
            _farQuality = far;
            float q = Mathf.Clamp(_config.shelfPastQuality * (far ? 0.6f : 1f), 0.25f, 1f);
            bool thin = far && _config.shelfMiniDetail < 0.999f;
            foreach (ShelfJar j in _jars)
            {
                if (j.vessel == null) continue;
                j.vessel.SetQuality(q);
                j.vessel.SetThin(thin);
                j.wax?.SetThin(thin);
            }
        }

        float AngleOf(float slot)
        {
            int n = Mathf.Max(1, _config.shelfSlots);
            return (slot - (n - 1) * 0.5f) * _config.shelfSpacingDeg;
        }

        Vector3 OnArc(float angleDeg, float radius)
        {
            Vector3 dir = Quaternion.AngleAxis(angleDeg, Vector3.up) * _forward;
            return new Vector3(_eye.x, _surfaceY, _eye.z) + dir * radius;
        }

        Vector3 SideAt(Vector3 p, out Vector3 toEye)
        {
            toEye = Vector3.ProjectOnPlane(_eye - p, Vector3.up);
            toEye = toEye.sqrMagnitude > 1e-6f ? toEye.normalized : -_forward;
            return Vector3.Cross(Vector3.up, toEye).normalized;
        }

        public float CaptureDistance(Vector3 jarBase)
        {
            if (_slots.Length == 0) return float.MaxValue;
            Vector3 seat = TodaySeat;
            Vector3 d = jarBase - seat;
            Vector3 radial = Vector3.ProjectOnPlane(seat - _eye, Vector3.up);
            if (radial.sqrMagnitude < 1e-6f) return d.magnitude;
            radial.Normalize();
            float along = Vector3.Dot(d, radial);
            d -= radial * (along * (1f - Mathf.Clamp(_config.shelfDepthTolerance, 0.05f, 1f)));
            return d.magnitude;
        }

        static void Add(ref RitualStaticSplats.Builder b, Vector3 pos, float size, Color color, float phase, float kind, float speed)
        {
            b.Add(pos, Vector3.up, new Vector4(1f, 0f, size, size), new Vector4(color.r, color.g, color.b, phase), new Vector4(0f, kind, speed, 0f));
        }

        static void AddFlat(ref RitualStaticSplats.Builder b, Vector3 pos, Vector3 along, float length, float width, Color color, float phase, float speed)
        {
            Vector2 axis = new Vector2(along.x, along.z);
            axis = axis.sqrMagnitude > 1e-8f ? axis.normalized : new Vector2(1f, 0f);
            b.Add(pos, Vector3.up, new Vector4(axis.x, axis.y, length, width), new Vector4(color.r, color.g, color.b, phase), new Vector4(0f, 0f, speed, 0f));
        }

        void Rebuild(Glow g, string name, RitualStaticSplats.Builder b)
        {
            if (g.renderer != null) RitualUtil.Kill(g.renderer.gameObject);
            RitualUtil.Kill(g.mesh);
            g.renderer = null;
            g.mesh = null;
            if (b.Count == 0 || _glow == null) return;
            g.mesh = b.ToMesh(name, _config.shelfDistance + 1f);
            g.renderer = RitualStaticSplats.Spawn(name, _root.transform, g.mesh, _glow);
        }

        void BuildSurface()
        {
            if (_surface != null) RitualUtil.Kill(_surface.gameObject);
            RitualUtil.Kill(_surfaceMesh);
            _surface = null;
            _surfaceMesh = null;
            Surfaces = 0;
            int n = _slots.Length;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var cols = new List<Color>();
            var tris = new List<int>();
            Vector3 lift = Vector3.down * 0.0015f;
            if (_config.shelfStyle == ShelfStyle.Ledge)
            {
                float from = AngleOf(-0.75f), to = AngleOf(n - 0.25f);
                float half = _config.shelfDepth * 0.5f;
                const int steps = 72;
                for (int i = 0; i <= steps; i++)
                {
                    float u = i / (float)steps;
                    float a = Mathf.Lerp(from, to, u);
                    float mask = Mathf.Clamp01(Mathf.Min(u, 1f - u) / 0.16f);
                    mask = mask * mask * (3f - 2f * mask);
                    verts.Add(OnArc(a, _config.shelfDistance - half) + lift);
                    uvs.Add(new Vector2(u, 1f));
                    cols.Add(new Color(1f, 1f, 1f, mask));
                    verts.Add(OnArc(a, _config.shelfDistance + half) + lift);
                    uvs.Add(new Vector2(u, 0f));
                    cols.Add(new Color(1f, 1f, 1f, mask));
                    if (i > 0)
                    {
                        int k = verts.Count - 4;
                        tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
                        tris.Add(k + 1); tris.Add(k + 3); tris.Add(k + 2);
                    }
                }
                Surfaces = 1;
            }
            else
            {
                const int seg = 40;
                for (int s = 0; s < n; s++)
                {
                    int c0 = verts.Count;
                    verts.Add(_slots[s] + lift);
                    uvs.Add(new Vector2(0.5f, 0.5f));
                    cols.Add(Color.white);
                    for (int i = 0; i <= seg; i++)
                    {
                        float a = i / (float)seg * 6.2831853f;
                        verts.Add(_slots[s] + lift + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * PedestalRadius);
                        uvs.Add(new Vector2(i / (float)seg, 1f));
                        cols.Add(Color.white);
                        if (i > 0)
                        {
                            tris.Add(c0); tris.Add(c0 + i); tris.Add(c0 + i + 1);
                        }
                    }
                    Surfaces++;
                }
            }
            if (_frost == null || verts.Count == 0) return;
            _surfaceMesh = new Mesh { name = "ShelfSurface", hideFlags = HideFlags.DontSave };
            _surfaceMesh.SetVertices(verts);
            _surfaceMesh.SetUVs(0, uvs);
            _surfaceMesh.SetColors(cols);
            _surfaceMesh.SetTriangles(tris, 0);
            _surfaceMesh.RecalculateBounds();
            var go = new GameObject("Surface");
            go.transform.SetParent(_root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _surfaceMesh;
            _surface = go.AddComponent<MeshRenderer>();
            _surface.sharedMaterial = _frost;
            RitualUtil.Quiet(_surface);
        }

        void BuildEdge()
        {
            var b = RitualStaticSplats.Builder.Create(260);
            Color warm = new Color(1f, 0.9f, 0.76f, 1f);
            if (_config.shelfStyle == ShelfStyle.Ledge)
            {
                int n = _slots.Length;
                float from = AngleOf(-0.75f), to = AngleOf(n - 0.25f);
                float r = _config.shelfDistance - _config.shelfDepth * 0.5f;
                const int count = 200;
                for (int i = 0; i < count; i++)
                {
                    float u = (i + 0.5f) / count;
                    float edge = Mathf.Clamp01(Mathf.Min(u, 1f - u) / 0.14f);
                    Add(ref b, OnArc(Mathf.Lerp(from, to, u), r), 0.0022f, warm * (0.7f * edge), u * 31f, 0f, 0.5f);
                }
            }
            Rebuild(_edge, "ShelfEdge", b);
        }

        void BuildRings()
        {
            var b = RitualStaticSplats.Builder.Create(400);
            var t = RitualStaticSplats.Builder.Create(80);
            Color warm = new Color(1f, 0.92f, 0.8f, 1f);
            for (int s = 0; s < _slots.Length; s++)
            {
                bool today = s == TodaySlot;
                const int dashes = 44;
                for (int i = 0; i < dashes; i++)
                {
                    float a = (i + 0.5f) / dashes * 6.2831853f;
                    Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
                    Vector3 p = _slots[s] + radial * RingRadius + Vector3.up * 0.0006f;
                    if (today) AddFlat(ref t, p, tangent, 0.0095f, 0.0018f, warm * 0.9f, 0f, 0f);
                    else AddFlat(ref b, p, tangent, 0.0095f, 0.0016f, warm * 0.55f, s * 1.7f, 0.9f);
                }
                if (_config.shelfStyle == ShelfStyle.Pedestals)
                {
                    const int rim = 64;
                    for (int i = 0; i < rim; i++)
                    {
                        float a = (i + 0.5f) / rim * 6.2831853f;
                        float r = PedestalRadius * (1f + 0.045f * Mathf.Sin(a * 7f + s));
                        Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
                        AddFlat(ref b, _slots[s] + radial * r + Vector3.up * 0.0004f, tangent, 0.009f, 0.0022f, warm * 0.6f, a * 3f + s, 0.7f);
                    }
                }
            }
            Rebuild(_rings, "ShelfRings", b);
            Rebuild(_todayRing, "ShelfTodayRing", t);
            if (_rippleMesh == null)
            {
                var r = RitualStaticSplats.Builder.Create(64);
                for (int i = 0; i < 64; i++)
                {
                    float a = (i + 0.5f) / 64f * 6.2831853f;
                    Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    AddFlat(ref r, radial, new Vector3(-radial.z, 0f, radial.x), 0.1f, 0.016f, Color.white, 0f, 0f);
                }
                _rippleMesh = r.ToMesh("ShelfRipple", 1.5f);
            }
            for (int k = 0; k < _ripples.Length; k++)
            {
                Glow g = _ripples[k];
                if (g.renderer == null && _glow != null)
                {
                    g.renderer = RitualStaticSplats.Spawn("Ripple" + k, _root.transform, _rippleMesh, _glow);
                    g.renderer.enabled = false;
                }
            }
        }

        void BuildDates()
        {
            foreach (TextMesh d in _dates)
                if (d != null) RitualUtil.Kill(d.gameObject);
            _dates.Clear();
            if (_font == null) return;
            _font.RequestCharactersInTexture(Glyphs, 64);
            for (int s = 0; s < _slots.Length; s++)
            {
                TextMesh tm = MakeText("Date" + s, 64, 0.0019f, TextAnchor.MiddleCenter);
                tm.text = DateText(s);
                SideAt(_slots[s], out Vector3 toEye);
                Transform tr = tm.transform;
                tr.position = _slots[s] + toEye * (RingRadius + 0.024f) + Vector3.up * 0.0012f;
                tr.rotation = Quaternion.LookRotation(Vector3.down, -toEye);
                _dates.Add(tm);
            }
            if (_note == null)
            {
                _note = MakeText("ShelfNote", 64, 0.0017f, TextAnchor.LowerCenter);
                _note.gameObject.SetActive(false);
            }
        }

        TextMesh MakeText(string name, int size, float charSize, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.fontSize = size;
            tm.characterSize = charSize;
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.95f, 0.93f, 0.88f, 0f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = _font.material;
            RitualUtil.Quiet(r);
            return tm;
        }

        public void Show(IReadOnlyList<DayRecord> past)
        {
            foreach (ShelfJar j in _jars) KillJar(j);
            _jars.Clear();
            MemorySplats = 0;
            Rest();
            _farQuality = false;
            var pools = RitualStaticSplats.Builder.Create(256);
            if (past != null && _slots.Length > 0)
            {
                for (int s = 0; s < TodaySlot; s++)
                {
                    string key = RitualDayStore.Key(DateOf(s));
                    DayRecord rec = null;
                    for (int i = 0; i < past.Count; i++)
                        if (past[i] != null && past[i].date == key) rec = past[i];
                    if (rec == null || rec.TotalUnits <= 0) continue;
                    float age = TodaySlot <= 1 ? 1f : s / (float)(TodaySlot - 1);
                    ShelfJar jar = BuildJar(s, rec, Mathf.Lerp(0.6f, 1f, age));
                    _jars.Add(jar);
                    AddPool(ref pools, _slots[s], rec, jar.light);
                }
            }
            Rebuild(_pools, "ShelfPools", pools);
            for (int s = 0; s < _dates.Count; s++) _dates[s].text = DateText(s);
            _farQuality = !(_stage < 0.5f);
            ApplyStage();
        }

        ShelfJar BuildJar(int slot, DayRecord rec, float light)
        {
            var jar = new ShelfJar { slot = slot, record = rec, light = light * _config.shelfMemoryLight };
            var go = new GameObject("Day " + rec.date);
            go.transform.SetParent(_root.transform, false);
            SideAt(_slots[slot], out Vector3 toEye);
            go.transform.SetPositionAndRotation(_slots[slot], Quaternion.LookRotation(-toEye, Vector3.up));
            jar.root = go.transform;
            jar.info = InfoFor(rec);
            if (!string.IsNullOrEmpty(rec.dominant) && _colorOf != null) jar.dominantGlow = _colorOf(rec.dominant);
            if (MakeJar != null)
            {
                (RitualJar vessel, RitualWax wax) = MakeJar(jar.root, rec);
                jar.vessel = vessel;
                jar.wax = wax;
                if (vessel != null)
                {
                    vessel.SetQuality(Mathf.Clamp(_config.shelfPastQuality, 0.25f, 1f));
                    foreach (RitualJar.Layer l in vessel.Layers)
                        if (l.cloud != null) jar.swirl.Add(l.cloud.transform);
                    MemorySplats += vessel.TotalGrains;
                    return jar;
                }
            }
            if (GlassMesh != null && _glass != null)
            {
                var g = new GameObject("Glass");
                g.transform.SetParent(jar.root, false);
                g.AddComponent<MeshFilter>().sharedMesh = GlassMesh;
                jar.glass = g.AddComponent<MeshRenderer>();
                jar.glass.sharedMaterial = _glass;
                RitualUtil.Quiet(jar.glass);
            }
            JarShape shape = _config.jar;
            int total = Mathf.Max(1, rec.TotalUnits);
            float full = Mathf.Max(1f, _config.hoursWhenFull * RitualFill.UnitsPerHour);
            float top = Mathf.Min(shape.FillCeiling, shape.HeightForVolume(total * RitualFill.VolumePerUnit(shape, _config.hoursWhenFull)));
            float span = Mathf.Max(0.005f, top - shape.floor);
            float filled = 0f;
            int seed = slot * 977 + rec.date.GetHashCode();
            int li = 0;
            foreach (DayLayer layer in rec.layers)
            {
                float share = layer.units / (float)total;
                float y0 = shape.floor + filled * span;
                float y1 = shape.floor + (filled + share) * span;
                filled += share;
                Color col = _colorOf != null ? _colorOf(layer.category) : Color.white;
                int count = Mathf.Clamp(Mathf.RoundToInt(260f * share * Mathf.Min(1f, total / full + 0.4f)), 8, 260);
                var b = RitualStaticSplats.Builder.Create(count);
                for (int i = 0; i < count; i++)
                {
                    float h1 = RitualUtil.Hash(seed + li * 1009 + i * 31 + 5);
                    float h2 = RitualUtil.Hash(seed + li * 1013 + i * 37 + 9);
                    float h3 = RitualUtil.Hash(seed + li * 1019 + i * 41 + 13);
                    float y = Mathf.Lerp(y0, y1, h1);
                    float rr = shape.InnerRadius(y) * Mathf.Sqrt(h2) * 0.9f;
                    float a = h3 * 6.2831853f;
                    Vector3 p = new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr);
                    Add(ref b, p, Mathf.Lerp(0.0032f, 0.005f, h2), col * Mathf.Lerp(0.7f, 1.1f, h1), h3 * 6.28f, h1 > 0.86f ? 2f : 0f, 0.5f);
                }
                MemorySplats += count;
                li++;
                if (_glow == null) continue;
                Mesh m = b.ToMesh("Strata " + layer.category, shape.height);
                MeshRenderer r = RitualStaticSplats.Spawn("Layer " + layer.category, jar.root, m, _glow);
                jar.meshes.Add(m);
                jar.strata.Add(r);
                jar.swirl.Add(r.transform);
            }
            if (!string.IsNullOrEmpty(rec.dominant))
            {
                Color wax = (_colorOf != null ? _colorOf(rec.dominant) : Color.white) * 0.9f;
                var b = RitualStaticSplats.Builder.Create(24);
                for (int i = 0; i < 18; i++)
                {
                    float a = i / 18f * 6.2831853f;
                    float rr = shape.mouthRadius * (i % 2 == 0 ? 0.85f : 0.45f);
                    Add(ref b, new Vector3(Mathf.Cos(a) * rr, shape.height + 0.003f, Mathf.Sin(a) * rr), 0.0055f, wax, i, 0f, 0.4f);
                }
                Add(ref b, new Vector3(0f, shape.height + 0.004f, 0f), 0.009f, wax, 0f, 0f, 0.4f);
                MemorySplats += b.Count;
                if (_glow != null)
                {
                    Mesh m = b.ToMesh("Wax", shape.height + 0.02f);
                    jar.meshes.Add(m);
                    jar.strata.Add(RitualStaticSplats.Spawn("Wax", jar.root, m, _glow));
                }
            }
            return jar;
        }

        void AddPool(ref RitualStaticSplats.Builder b, Vector3 at, DayRecord rec, float light)
        {
            int total = Mathf.Max(1, rec.TotalUnits);
            int seed = rec.date.GetHashCode();
            int i = 0;
            foreach (DayLayer layer in rec.layers)
            {
                float share = layer.units / (float)total;
                Color col = _colorOf != null ? _colorOf(layer.category) : Color.white;
                int count = Mathf.Max(2, Mathf.RoundToInt(16f * share));
                for (int k = 0; k < count; k++)
                {
                    float h1 = RitualUtil.Hash(seed + i * 71 + k * 13 + 1);
                    float h2 = RitualUtil.Hash(seed + i * 73 + k * 17 + 2);
                    float a = h1 * 6.2831853f;
                    float r = Mathf.Lerp(0.012f, 0.06f, Mathf.Sqrt(h2));
                    Vector3 p = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r + Vector3.up * 0.0008f;
                    float size = Mathf.Lerp(0.05f, 0.03f, h2);
                    AddFlat(ref b, p, new Vector3(1f, 0f, 0f), size, size, col * (0.16f * light * (1.2f - h2)), h1 * 6.28f, 0.6f);
                }
                i++;
            }
        }

        public static string FormatMinutes(int minutes)
        {
            int h = minutes / 60, m = minutes % 60;
            if (h > 0 && m > 0) return h + "h " + m + "m";
            if (h > 0) return h + "h";
            return m + "m";
        }

        public string InfoFor(DayRecord rec)
        {
            if (rec == null) return "";
            var sorted = new List<DayLayer>();
            foreach (DayLayer l in rec.layers)
            {
                int at = sorted.FindIndex(x => x.category == l.category);
                if (at >= 0) sorted[at] = new DayLayer { category = l.category, units = sorted[at].units + l.units };
                else sorted.Add(new DayLayer { category = l.category, units = l.units });
            }
            sorted.Sort((a, b) => b.units.CompareTo(a.units));
            var parts = new List<string>();
            for (int i = 0; i < sorted.Count && i < 3; i++)
            {
                string name = _nameOf != null ? _nameOf(sorted[i].category) : sorted[i].category;
                parts.Add(name + " " + FormatMinutes(TimeUnits.Minutes(sorted[i].units)));
            }
            return string.Join(" · ", parts);
        }

        void KillJar(ShelfJar j)
        {
            if (j == null) return;
            j.swirl.Clear();
            j.wax?.Dispose();
            j.vessel?.Dispose();
            foreach (Mesh m in j.meshes) RitualUtil.Kill(m);
            if (j.root != null) RitualUtil.Kill(j.root.gameObject);
        }

        public void Fill(bool filled)
        {
            _filled = filled;
            if (!filled) ClearToday();
        }

        void ClearToday()
        {
            _rippleAge = -1f;
            _poolAge = -1f;
            _noteAge = -1f;
            _flashAge = -1f;
            Flash = 0f;
            if (_note != null) _note.gameObject.SetActive(false);
            Rebuild(_todayPool, "ShelfTodayPool", RitualStaticSplats.Builder.Create(1));
            foreach (Glow g in _ripples)
                if (g.renderer != null) g.renderer.enabled = false;
        }

        public void Landed(DayRecord today, Vector3 jarTop)
        {
            _filled = true;
            _rippleAge = 0f;
            _poolAge = 0f;
            _flashAge = 0f;
            var pool = RitualStaticSplats.Builder.Create(96);
            if (today != null && _slots.Length > 0) AddPool(ref pool, _slots[TodaySlot], today, 1.4f);
            Rebuild(_todayPool, "ShelfTodayPool", pool);
            ShowNote(LoggedLabel(_today), jarTop + Vector3.up * 0.04f, 3f);
        }

        public void ShowNote(string text, Vector3 at, float hold)
        {
            _noteText = text;
            _noteAge = 0f;
            _noteHold = Mathf.Max(0.5f, hold);
            _notePos = at;
            if (_note == null) return;
            _note.text = text;
            _note.gameObject.SetActive(true);
        }

        public int Touch(int hand, Vector3 tip, bool tracked)
        {
            int was = _inside[hand];
            int now = -1;
            if (tracked)
            {
                JarShape shape = _config.jar;
                for (int i = 0; i < _jars.Count; i++)
                {
                    Transform t = _jars[i].root;
                    if (t == null) continue;
                    Vector3 p = t.InverseTransformPoint(tip);
                    if (p.y < 0f || p.y > shape.height + 0.012f) continue;
                    float r = Mathf.Sqrt(p.x * p.x + p.z * p.z);
                    if (r < shape.OuterRadius(Mathf.Min(p.y, shape.height)) + _config.shelfPokeRadius)
                    {
                        now = i;
                        break;
                    }
                }
            }
            _inside[hand] = now;
            if (now < 0 || now == was || _jars[now].cooldown > 0f) return -1;
            return now;
        }

        public void Poke(int index)
        {
            if (index < 0 || index >= _jars.Count) return;
            ShelfJar j = _jars[index];
            j.swirl.Kick(_config.shelfSwirl);
            j.poke = 1f;
            j.cooldown = 0.5f;
            Pokes++;
            ShowNote(j.info, j.root.position + Vector3.up * (_config.jar.height + 0.035f), _config.shelfInfoSeconds);
        }

        public void Tick(float dt, float emphasis, float near = 0f)
        {
            _clock += dt;
            _brightness = Mathf.MoveTowards(_brightness, Mathf.Clamp01(emphasis), dt * 0.8f);
            TodayNear = Mathf.MoveTowards(TodayNear, _filled ? 0f : Mathf.Clamp01(near), dt * 4f);
            float pulse = _filled ? 0f : 0.5f + 0.5f * Mathf.Sin(_clock * (emphasis > 0.9f ? 4.2f : 2.4f));
            TodayPulse = _filled ? 0f : _brightness * (0.35f + 0.65f * pulse);
            float b = _brightness * _config.shelfLight;
            if (_surface != null)
            {
                bool show = b > 0.003f;
                if (_surface.enabled != show) _surface.enabled = show;
                if (show)
                {
                    _surfaceBlock.SetFloat(FadeId, b);
                    _surfaceBlock.SetColor(ColorId, new Color(0.86f, 0.92f, 1f, _config.shelfFrost));
                    _surface.SetPropertyBlock(_surfaceBlock);
                }
            }
            Push(_edge, b);
            Push(_rings, b * 0.8f);
            Push(_todayRing, _filled ? b * 0.25f : TodayPulse + TodayNear * 0.9f);
            if (_flashAge >= 0f)
            {
                _flashAge += dt;
                Flash = _flashAge < 0.35f ? RitualUtil.Smooth(_flashAge / 0.35f) : Mathf.Lerp(1f, 0.3f, RitualUtil.Smooth((_flashAge - 0.35f) / 2.2f));
            }
            Push(_pools, _brightness * (1f + Flash));
            float poolIn = _poolAge < 0f ? 0f : RitualUtil.Smooth(Mathf.Clamp01(_poolAge / 1.2f));
            if (_poolAge >= 0f) _poolAge += dt;
            Push(_todayPool, _brightness * poolIn);
            StepRipples(dt);
            StepJars(dt);
            StepDates();
            StepNote(dt);
        }

        void StepRipples(float dt)
        {
            if (_rippleAge < 0f) return;
            _rippleAge += dt;
            bool any = false;
            for (int k = 0; k < _ripples.Length; k++)
            {
                Glow g = _ripples[k];
                float t = (_rippleAge - k * 0.22f) / 1.5f;
                if (t < 1f) any = true;
                if (g.renderer == null) continue;
                if (t <= 0f || t >= 1f)
                {
                    if (g.renderer.enabled) g.renderer.enabled = false;
                    continue;
                }
                float e = 1f - (1f - t) * (1f - t) * (1f - t);
                float radius = Mathf.Lerp(0.03f, _config.shelfStyle == ShelfStyle.Ledge ? 0.15f : 0.11f, e);
                Transform tr = g.renderer.transform;
                tr.SetPositionAndRotation(TodaySeat + Vector3.up * 0.001f, Quaternion.identity);
                tr.localScale = Vector3.one * radius;
                Push(g, _brightness * 0.9f * Mathf.Pow(1f - t, 1.5f) * (1f - 0.25f * k));
            }
            if (!any) _rippleAge = -1f;
        }

        void StepJars(float dt)
        {
            for (int i = 0; i < _jars.Count; i++)
            {
                ShelfJar j = _jars[i];
                j.swirl.Tick(dt);
                j.cooldown = Mathf.Max(0f, j.cooldown - dt);
                j.poke = Mathf.MoveTowards(j.poke, 0f, dt * 0.8f);
                if (j.vessel != null)
                {
                    j.level = _config.sealedIntensity * j.light * Mathf.Lerp(0.45f, 1f, _brightness) * (1f + 0.7f * Flash + 0.3f * j.poke);
                    j.vessel.SetLevel(j.level);
                    j.vessel.SetInnerGlow(j.dominantGlow * (0.05f + 0.1f * _brightness + 0.45f * Flash + 0.35f * j.poke));
                    j.vessel.Tick(_clock, dt);
                    j.wax?.Tick(dt);
                    continue;
                }
                float b = _brightness * j.light * (1f + Flash);
                j.level = b;
                if (j.glass != null)
                {
                    bool show = b > 0.003f;
                    if (j.glass.enabled != show) j.glass.enabled = show;
                    j.glassBlock.SetColor(TintId, new Color(0.86f, 0.94f, 1f, 1f));
                    j.glassBlock.SetColor(InnerId, j.dominantGlow * (0.18f * b + 0.35f * j.poke));
                    j.glassBlock.SetFloat(RimId, _config.glassRim * b);
                    j.glassBlock.SetFloat(SpecId, _config.glassSpec * b);
                    j.glassBlock.SetFloat(BodyId, _config.glassBody);
                    j.glassBlock.SetFloat(RadiusId, _config.glassSplatRadius);
                    j.glassBlock.SetFloat(PulseId, 0f);
                    j.glass.SetPropertyBlock(j.glassBlock);
                }
                float strata = b * (1f + 0.6f * j.poke);
                bool on = strata > 0.003f;
                j.strataBlock.SetFloat(IntensityId, strata);
                j.strataBlock.SetFloat(RevealId, 10f);
                j.strataBlock.SetFloat(BreathId, 0.08f);
                j.strataBlock.SetFloat(SwirlId, 0f);
                j.strataBlock.SetFloat(FlatId, 0f);
                foreach (MeshRenderer r in j.strata)
                {
                    if (r == null) continue;
                    if (r.enabled != on) r.enabled = on;
                    if (on) r.SetPropertyBlock(j.strataBlock);
                }
            }
        }

        void StepDates()
        {
            for (int s = 0; s < _dates.Count; s++)
            {
                TextMesh d = _dates[s];
                if (d == null) continue;
                bool today = s == TodaySlot;
                bool filled = s < TodaySlot ? HasJar(s) : (today && _filled);
                float a = _brightness * (today ? (_filled ? 0.75f : 0.55f + 0.35f * TodayPulse) : (filled ? 0.5f : 0.38f));
                Color c = d.color;
                c.a = a;
                d.color = c;
                bool show = a > 0.01f;
                if (d.gameObject.activeSelf != show) d.gameObject.SetActive(show);
            }
        }

        bool HasJar(int slot)
        {
            foreach (ShelfJar j in _jars)
                if (j.slot == slot) return true;
            return false;
        }

        void StepNote(float dt)
        {
            if (_noteAge < 0f)
            {
                NoteAlpha = 0f;
                return;
            }
            _noteAge += dt;
            float a = Mathf.Clamp01(_noteAge / 0.5f) * Mathf.Clamp01((_noteHold + 1f - _noteAge) / 1f);
            NoteAlpha = a;
            if (_noteAge > _noteHold + 1f)
            {
                _noteAge = -1f;
                NoteAlpha = 0f;
                if (_note != null) _note.gameObject.SetActive(false);
                return;
            }
            if (_note == null) return;
            Vector3 p = _notePos + Vector3.up * (0.012f * RitualUtil.Smooth(Mathf.Clamp01(_noteAge / 1.6f)));
            Vector3 look = p - _eye;
            _note.transform.SetPositionAndRotation(p, look.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(look, Vector3.up) : Quaternion.identity);
            Color c = _note.color;
            c.a = a * Mathf.Max(0.35f, _brightness);
            _note.color = c;
        }

        void Push(Glow g, float intensity)
        {
            MeshRenderer r = g.renderer;
            if (r == null) return;
            bool show = intensity > 0.003f;
            if (r.enabled != show) r.enabled = show;
            if (!show) return;
            g.block.SetFloat(IntensityId, intensity);
            g.block.SetFloat(RevealId, 10f);
            g.block.SetFloat(BreathId, g.breath);
            g.block.SetFloat(SwirlId, 0f);
            g.block.SetFloat(FlatId, g.flat ? 1f : 0f);
            r.SetPropertyBlock(g.block);
        }

        public void Dispose()
        {
            foreach (ShelfJar j in _jars) KillJar(j);
            _jars.Clear();
            RitualUtil.Kill(_surfaceMesh);
            RitualUtil.Kill(_edge.mesh);
            RitualUtil.Kill(_rings.mesh);
            RitualUtil.Kill(_todayRing.mesh);
            RitualUtil.Kill(_pools.mesh);
            RitualUtil.Kill(_todayPool.mesh);
            RitualUtil.Kill(_rippleMesh);
            if (_root != null) RitualUtil.Kill(_root);
        }
    }
}
