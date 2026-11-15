using System.Collections.Generic;
using System.IO;
using System.Text;
using MoodPicker.Hands;
using MoodPicker.Splatter;
using UnityEngine;

namespace MoodPicker.Ritual
{
    public sealed class TimeRitual : MonoBehaviour
    {
        const string Tag = "[MP]";
        public const string LiveFile = "ritual.json";

        const int GripNone = 0, GripPull = 1, GripHold = 2, GripJar = 3, GripSqueeze = 4, GripWax = 5, GripLid = 6;
        const float LabelAttachScale = 0.56f;
        const float LabelDividerY = 0.024f;
        const float LabelMaxWidth = 0.084f;
        const float LabelMaxTitleHeight = 0.013f;
        const float LabelMaxHeaderHeight = 0.017f;
        const float LabelMaxBodyHeight = 0.074f;
        const int LabelFontPixelSize = 68;

        static readonly int LabelColorId = Shader.PropertyToID("_Color");
        static readonly int LabelDeepId = Shader.PropertyToID("_Deep");
        static readonly int LabelLightId = Shader.PropertyToID("_Light");
        static readonly int LabelGlowId = Shader.PropertyToID("_Glow");
        static readonly int LabelSheenId = Shader.PropertyToID("_Sheen");
        static readonly int LabelRadiusId = Shader.PropertyToID("_Radius");
        static readonly int LabelPressId = Shader.PropertyToID("_Press");
        static readonly int LabelStampId = Shader.PropertyToID("_Stamp");
        static readonly int LabelAppearId = Shader.PropertyToID("_Appear");
        static readonly int LabelOriginId = Shader.PropertyToID("_Origin");
        static readonly int LabelBobId = Shader.PropertyToID("_Bob");
        static readonly int LabelGrowId = Shader.PropertyToID("_GrowWithPress");

        struct Grip
        {
            public int mode;
            public TimePieceView piece;
            public Vector3 local;
            public Quaternion relative;
            public Vector3 jarOffset;
            public float span;
            public Vector3 vel;
        }

        struct Bond
        {
            public TimePieceView other;
            public float t;
        }

        readonly Bond[] _bonds = new Bond[2];
        readonly Vector3[] _probes = new Vector3[6];
        readonly float[] _probeRadii = new float[6];
        readonly float[] _pinchEndedAt = { -10f, -10f };
        public int Snaps { get; private set; }
        public int ScatterEvents { get; private set; }
        readonly bool[] _scattering = new bool[2];
        public float BondProgress(int hand) => _bonds[Mathf.Clamp(hand, 0, 1)].t;

        public static float WeightOmega(TimeRitualConfig cfg, int units)
        {
            float heavy = 1f + Mathf.Max(0f, cfg.weightLag) * Mathf.Sqrt(Mathf.Max(0, units) / Mathf.Max(1f, cfg.weightUnits));
            return Mathf.Max(0.5f, cfg.weightFollow) / heavy;
        }

        struct LabelGlyphVert
        {
            public float x;
            public float y;
            public float z;
            public Vector2 uv;
            public Color32 color;
        }

        [SerializeField] TimeRitualConfig config;
        [SerializeField] SplatTuning tuning;
        [SerializeField] MonoBehaviour handInputSource;
        [SerializeField] MonoBehaviour microgestureSource;
        [SerializeField] Transform trackingSpace;
        [SerializeField] Transform head;
        [SerializeField] TimePullController pull;
        [SerializeField] RitualAudio sound;
        [SerializeField] TimeCategoryAsset[] categories;
        [SerializeField] Material splatMaterial;
        [SerializeField] Material glassMaterial;
        [SerializeField] Material waxMaterial;
        [SerializeField] Material glowMaterial;
        [SerializeField] Material frostMaterial;
        [SerializeField] Material glyphMaterial;
        [SerializeField] Font labelFont;
        [SerializeField] Transform piecesRoot;
        [SerializeField] bool autoPlace = true;
        [SerializeField] int settleFrames = 30;

        readonly RitualHands _hands = new RitualHands();
        readonly List<TimePieceView> _pieces = new List<TimePieceView>();
        readonly Dictionary<TimeCategoryAsset, SplatSurfaceMaterial> _looks = new Dictionary<TimeCategoryAsset, SplatSurfaceMaterial>();
        readonly Grip[] _grips = new Grip[2];
        readonly float[] _nextTouchSoundTime = new float[2];
        readonly List<Color> _glowColors = new List<Color>();
        readonly List<int> _glowUnits = new List<int>();
        Color _glowDominant;
        readonly StringBuilder _text = new StringBuilder(128);
        readonly List<LabelGlyphVert> _labelGlyphVerts = new List<LabelGlyphVert>(1024);
        readonly List<Vector3> _labelTextPos = new List<Vector3>(1024);
        readonly List<Vector2> _labelTextUv = new List<Vector2>(1024);
        readonly List<Color32> _labelTextCol = new List<Color32>(1024);
        readonly List<int> _labelTextShadowIdx = new List<int>(768);
        readonly List<int> _labelTextFgIdx = new List<int>(768);
        readonly List<int> _labelTextIdx = new List<int>(1536);
        readonly string[] _labelBodyLines = new string[8];

        TimeRitualConfig _config;
        IHandInput _input;
        IMicrogestureInput _micro;
        Microgesture _lastRightMicro;
        Microgesture _lastLeftMicro;
        float _lastGuideToggleTime = -10f;
        float _lastLabelToggleTime = -10f;
        RitualLightField _lightField;
        float _light = 1f;
        Color _jarGlow = Color.clear;
        float _jarFill;
        float _labelTag = 1f;
        string _labelShownText = "";
        MeshRenderer _labelBorderTag;
        int _floatSlots;
        RitualSealWave _sealWave;
        RitualClock _clock;
        RitualShelf _shelf;
        RitualDayStore _store;
        DayRecord _today;
        bool _shelved;
        float _shelveAge = -1f;
        Vector3 _shelveFrom;
        bool _jarWasHeld;
        float _shelfNear;
        RitualEnding _ending;
        RitualEnding.Phase _endSeen;
        bool _quitIssued;
        readonly RitualSwirl _todaySwirl = new RitualSwirl();
        readonly bool[] _todayInside = new bool[2];
        float _todayPoke;
        float _todayPokeCool;
        float _shelfStage;
        bool _clockDone = true;
        bool _clockSand;
        readonly Vector3[] _springPos = new Vector3[5];
        readonly System.Diagnostics.Stopwatch _watch = new System.Diagnostics.Stopwatch();
        float _lastTickMs;
        int _hitches;
        readonly Color[] _springCol = new Color[5];
        bool _lidClosed;
        float _lidCloseAge;
        Vector3 _lidLocal;
        Vector3 _lidFrom;
        Vector3 _lidGrabOffset;
        float _meltAge = -1f;
        bool _materialized;
        bool _everPulled;
        float _idle;
        Mesh _labelBorderTagMesh;
        RitualJar _jar;
        RitualWax _wax;
        RitualDeskGlow _glow;
        RitualDeskPool _pool;
        float _tableY;
        RitualGuideTrails _guideTrails;
        Color _pourPreviewGlow;
        Mesh _labelTextMesh;
        MeshRenderer _labelTextRenderer;
        string _labelCachedState = "";
        bool _labelTextDirty = true;
        bool _labelTextBuffersDirty = true;
        float _labelDeformedPress = -1f;
        float _labelDeformedAppear = -1f;
        Transform _labelRoot;
        MeshRenderer _labelCard;
        MeshRenderer _labelBorder;
        Mesh _labelCardMesh;
        Mesh _labelBorderMesh;
        MaterialPropertyBlock _labelCardBlock;
        MaterialPropertyBlock _labelBorderBlock;
        float _labelAppear;
        float _labelAttach;
        float _labelGlow;
        Color _labelAccent = new Color(0.95f, 0.78f, 0.45f, 1f);
        TextMesh _roomNote;
        TextMesh _heapNote;
        TimeHeap _hoveredHeap;
        float _heapNoteAlpha;
        float _roomTimer;
        int[] _pieceUnits;
        int[] _jarUnits;
        SealPress _press;
        bool _ready;
        bool _placed;
        bool _sealed;
        int _frames;
        float _waxTimer;
        float _sealTime;
        float _deskY;
        float _quality = 1f;
        float _frameMs = 13.9f;
        float _gpuMs = float.NaN;
        float _cpuMs = float.NaN;
        float _governAge;
        float _over;
        float _calm;
        float _liveTimer;
        long _liveStamp;
        int _resetSeen;
        float _panelTimer;
        float _dim;
        float _logTimer;
        bool _twoHandJarActive;
        Quaternion _jarTwoHandLocalRot = Quaternion.identity;
        Vector3 _jarTwoHandLocalPos;
        Quaternion _lastJarRot = Quaternion.identity;

        public TimeRitualConfig Config => _config;
        public IReadOnlyList<TimePieceView> Pieces => _pieces;
        public RitualJar Jar => _jar;
        public RitualWax Wax => _wax;
        public RitualDeskGlow Glow => _glow;
        public RitualDeskPool DeskPool => _pool;
        public float TableY => _tableY;
        public RitualGuideTrails GuideTrails => _guideTrails;
        public bool GuideTrailsEnabled => _config != null && _config.guideTrails;
        public bool Sealed => _sealed;
        public float Quality => _quality;
        public float FrameMs => _frameMs;
        public float GpuMs => _gpuMs;
        public float CpuMs => _cpuMs;
        public float RoomNoteSeconds => _roomTimer;
        public string RoomNoteText => _roomNote != null ? _roomNote.text : "";
        public int Refusals { get; private set; }
        public int Crumples { get; private set; }
        public int Compactions { get; private set; }
        public float PassthroughDim => _dim;
        public float PassthroughLight => _light;
        public RitualLightField LightField => _lightField;
        public bool TimeLabels => _config != null && _config.timeLabels;
        public string LabelShownText => _labelShownText;
        public Transform LabelRoot => _labelRoot;
        public bool EverPulled => _everPulled;
        public bool LidShown => _wax != null && _wax.LidShown;
        public bool LidClosed => _lidClosed;
        public Vector3 LidPosition => _wax != null ? _wax.lidRoot.position : Vector3.zero;
        public RitualSealWave SealWave => _sealWave;
        public RitualClock Clock => _clock;
        public RitualShelf Shelf => _shelf;
        public RitualEnding Ending => _ending;
        public RitualSwirl TodaySwirl => _todaySwirl;
        public bool QuitIssued => _quitIssued;
        public RitualDayStore Store => _store;
        public DayRecord Today => _today;
        public bool Shelved => _shelved;
        public bool ClockDone => _clockDone;
        public bool PressFull => _press.full;
        public bool Melting => _meltAge >= 0f && !_materialized;
        public float IdleSeconds => _idle;
        public float HoldProgress => _press.progress;
        public bool Ready => _ready;

        public void SetMicrogestureInput(IMicrogestureInput input)
        {
            _micro = input;
        }

        public void SetGuideTrailsEnabled(bool enabled)
        {
            if (_config != null) _config.guideTrails = enabled;
        }

        public void Init()
        {
            if (_ready) return;
            _ready = true;
            _config = config != null ? Instantiate(config) : ScriptableObject.CreateInstance<TimeRitualConfig>();
            _config.hideFlags = HideFlags.DontSave;
            _resetSeen = _config.resetCounter;
            _input = handInputSource as IHandInput;
            if (_micro == null) _micro = microgestureSource as IMicrogestureInput;
            if (piecesRoot == null) piecesRoot = transform;
            int count = categories != null ? categories.Length : 0;
            _pieceUnits = new int[count];
            _jarUnits = new int[count];
            if (pull != null)
            {
                TimeHeap[] heaps = pull.Heaps;
                for (int i = 0; heaps != null && i < heaps.Length; i++)
                    if (heaps[i] != null) heaps[i].Init(LookOf(heaps[i].Category), _config);
                pull.UnitChanged += OnUnitChanged;
                pull.Released += OnReleased;
                pull.Cancelled += OnCancelled;
            }
            BuildJar();
            _guideTrails = new RitualGuideTrails(transform, _config, glowMaterial);
            if (sound != null) sound.Init(_config.volume);
            Font.textureRebuilt += OnFontTextureRebuilt;
            if (labelFont != null)
            {
                MakePanel(labelFont);
                _roomNote = MakeRoomNote(labelFont);
                _heapNote = MakeHeapNote(labelFont);
            }
            if (_config.shelf)
            {
                _store = new RitualDayStore(Path.Combine(Application.persistentDataPath, _config.shelfFile));
                _shelf = new RitualShelf(_config, glowMaterial, frostMaterial, glassMaterial, labelFont, ColorOfCategory, NameOfCategory);
                _shelf.MakeJar = MakeShelfJar;
                if (_config.ending) _ending = new RitualEnding(_config, glowMaterial, frostMaterial, labelFont, head);
            }
            if (_config.clockIntro && labelFont != null)
            {
                _clock = new RitualClock(_config, labelFont, glyphMaterial);
                _clock.Ticked += OnClockTicked;
                _clockDone = false;
                HideSprings();
                if (sound != null) sound.HoldBgm(true);
            }
            _dim = _config.roomDarkFromStart ? 0f : _config.baseDim;
            _light = 1f;
            _labelTag = _config.timeLabels ? 0f : 1f;
            _lightField = new RitualLightField(_config, glowMaterial);
            _sealWave = new RitualSealWave(_config, glowMaterial);
            Debug.Log($"{Tag} ritual ready categories={count} heaps={(pull != null && pull.Heaps != null ? pull.Heaps.Length : 0)} capacity={_jar.Capacity}u");
        }

        void BuildJar()
        {
            _jar = new RitualJar(piecesRoot, _config, tuning, splatMaterial, glassMaterial, LookOf);
            _jar.PourStarted += OnPourStarted;
            _jar.PourFinished += OnPourFinished;
            _jar.GrainsLanded += OnGrainsLanded;
            _wax = new RitualWax(_jar.root, _config, waxMaterial, glowMaterial);
            _wax.Landed += OnWaxLanded;
            _glow = new RitualDeskGlow(piecesRoot, _config, glowMaterial);
            _pool = new RitualDeskPool(piecesRoot, _config, glowMaterial);
            PlaceJar();
        }

        void OnFontTextureRebuilt(Font rebuiltFont)
        {
            if (rebuiltFont != null && rebuiltFont == labelFont) _labelTextDirty = true;
        }

        void MakePanel(Font font)
        {
            const float halfW = 0.056f;
            const float halfH = 0.068f;
            const float chamfer = 0.013f;

            var cardGo = new GameObject("BottleLabel");
            _labelRoot = cardGo.transform;
            _labelRoot.SetParent(_jar != null ? _jar.root : transform, false);

            _labelCardBlock ??= new MaterialPropertyBlock();
            _labelBorderBlock ??= new MaterialPropertyBlock();
            if (waxMaterial != null) BuildLabelSplats(waxMaterial, halfW, halfH, chamfer);

            _labelTextMesh = new Mesh { name = "BottleLabelText", hideFlags = HideFlags.DontSave };
            var textGo = new GameObject("LabelText");
            textGo.transform.SetParent(_labelRoot, false);
            textGo.AddComponent<MeshFilter>().sharedMesh = _labelTextMesh;
            _labelTextRenderer = textGo.AddComponent<MeshRenderer>();
            _labelTextRenderer.sharedMaterial = font.material;
            RitualUtil.Quiet(_labelTextRenderer);
            _labelCachedState = "";
            _labelTextDirty = true;
            _labelTextBuffersDirty = true;
            _labelDeformedPress = -1f;
            _labelDeformedAppear = -1f;

            _labelRoot.gameObject.SetActive(false);
        }

        void BuildLabelSplats(Material material, float halfW, float halfH, float chamfer)
        {
            float cylR = (_config.jar.bodyRadius + 0.003f) / LabelAttachScale;

            const int wingCols = 6;
            const int midCols = 20;
            const int totalCols = wingCols * 2 + midCols;
            const int rows = 12;
            int cardVertCount = (totalCols + 1) * (rows + 1);

            var cardPos = new List<Vector3>(cardVertCount);
            var cardNorm = new List<Vector3>(cardVertCount);
            var cardUv0 = new List<Vector2>(cardVertCount);
            var cardUv1 = new List<Vector4>(cardVertCount);
            var cardUv2 = new List<Vector4>(cardVertCount);
            var cardUv3 = new List<Vector4>(cardVertCount);
            var cardIdx = new List<int>(totalCols * rows * 6);

            for (int cx = 0; cx <= totalCols; cx++)
            {
                float x;
                if (cx <= wingCols) x = Mathf.Lerp(-halfW, -halfW + chamfer, cx / (float)wingCols);
                else if (cx <= wingCols + midCols) x = Mathf.Lerp(-halfW + chamfer, halfW - chamfer, (cx - wingCols) / (float)midCols);
                else x = Mathf.Lerp(halfW - chamfer, halfW, (cx - wingCols - midCols) / (float)wingCols);

                float yTop = Mathf.Min(halfH, halfH + halfW - chamfer - Mathf.Abs(x));
                for (int ry = 0; ry <= rows; ry++)
                {
                    float y = Mathf.Lerp(-yTop, yTop, ry / (float)rows);
                    AddLabelSurfaceVertex(cardPos, cardNorm, cardUv0, cardUv1, cardUv2, cardUv3, x, y, 0.0008f, cylR, halfH);
                }
            }

            int stride = rows + 1;
            for (int cx = 0; cx < totalCols; cx++)
            {
                for (int ry = 0; ry < rows; ry++)
                {
                    int v00 = cx * stride + ry;
                    int v01 = v00 + 1;
                    int v10 = (cx + 1) * stride + ry;
                    int v11 = v10 + 1;
                    cardIdx.Add(v00);
                    cardIdx.Add(v01);
                    cardIdx.Add(v11);
                    cardIdx.Add(v00);
                    cardIdx.Add(v11);
                    cardIdx.Add(v10);
                }
            }

            _labelCardMesh = BuildSurfaceMesh("BottleLabelCard", halfH * 1.6f, cardPos, cardNorm, cardUv0, cardUv1, cardUv2, cardUv3, cardIdx);
            _labelCard = RitualStaticSplats.Spawn("LabelCard", _labelRoot, _labelCardMesh, material);

            var borderPos = new List<Vector3>(600);
            var borderNorm = new List<Vector3>(600);
            var borderUv0 = new List<Vector2>(600);
            var borderUv1 = new List<Vector4>(600);
            var borderUv2 = new List<Vector4>(600);
            var borderUv3 = new List<Vector4>(600);
            var borderIdx = new List<int>(1500);

            Vector2[] outerRing = OctagonRing(halfW, halfH, chamfer);
            AddClosedLoopRibbon(borderPos, borderNorm, borderUv0, borderUv1, borderUv2, borderUv3, borderIdx,
                outerRing, 0.0018f, 10, -0.0006f, cylR, halfH);

            Vector2[] innerRing = OctagonRing(halfW - 0.0038f, halfH - 0.0038f, chamfer * 0.85f);
            AddClosedLoopRibbon(borderPos, borderNorm, borderUv0, borderUv1, borderUv2, borderUv3, borderIdx,
                innerRing, 0.0010f, 10, -0.0006f, cylR, halfH);

            _labelBorderTagMesh = BuildSurfaceMesh("BottleTagBorder", halfH * 1.6f, borderPos, borderNorm, borderUv0, borderUv1, borderUv2, borderUv3, borderIdx);
            _labelBorderTag = RitualStaticSplats.Spawn("TagBorder", _labelRoot, _labelBorderTagMesh, material);

            float divX = halfW - 0.0085f;
            AddRibbonSegment(borderPos, borderNorm, borderUv0, borderUv1, borderUv2, borderUv3, borderIdx,
                new Vector2(-divX, LabelDividerY), new Vector2(divX, LabelDividerY), 0.0013f, 20, -0.0007f, cylR, halfH);

            float emblemY = halfH - 0.0085f;
            Vector2[] diamond =
            {
                new Vector2(0f, emblemY + 0.0028f),
                new Vector2(0.0048f, emblemY),
                new Vector2(0f, emblemY - 0.0028f),
                new Vector2(-0.0048f, emblemY),
            };
            AddClosedLoopRibbon(borderPos, borderNorm, borderUv0, borderUv1, borderUv2, borderUv3, borderIdx,
                diamond, 0.0011f, 3, -0.0007f, cylR, halfH);

            _labelBorderMesh = BuildSurfaceMesh("BottleLabelBorder", halfH * 1.6f, borderPos, borderNorm, borderUv0, borderUv1, borderUv2, borderUv3, borderIdx);
            _labelBorder = RitualStaticSplats.Spawn("LabelBorder", _labelRoot, _labelBorderMesh, material);
        }

        static Vector2[] OctagonRing(float hw, float hh, float ch)
        {
            return new[]
            {
                new Vector2(-hw + ch, hh),
                new Vector2(hw - ch, hh),
                new Vector2(hw, hh - ch),
                new Vector2(hw, -hh + ch),
                new Vector2(hw - ch, -hh),
                new Vector2(-hw + ch, -hh),
                new Vector2(-hw, -hh + ch),
                new Vector2(-hw, hh - ch),
            };
        }

        static Mesh BuildSurfaceMesh(string name, float boundsRadius, List<Vector3> pos, List<Vector3> norm,
            List<Vector2> uv0, List<Vector4> uv1, List<Vector4> uv2, List<Vector4> uv3, List<int> idx)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(pos);
            mesh.SetNormals(norm);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetUVs(2, uv2);
            mesh.SetUVs(3, uv3);
            mesh.SetTriangles(idx, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (boundsRadius * 2f));
            mesh.UploadMeshData(true);
            return mesh;
        }

        static void AddClosedLoopRibbon(List<Vector3> pos, List<Vector3> norm, List<Vector2> uv0, List<Vector4> uv1,
            List<Vector4> uv2, List<Vector4> uv3, List<int> idx, Vector2[] ring, float width, int stepsPerEdge, float z, float cylR, float cardHalfH)
        {
            int n = ring.Length;
            float halfWidth = width * 0.5f;
            var innerCorners = new Vector2[n];
            var outerCorners = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 a = ring[(i + n - 1) % n];
                Vector2 b = ring[i];
                Vector2 c = ring[(i + 1) % n];
                Vector2 d0 = (b - a).normalized;
                Vector2 d1 = (c - b).normalized;
                Vector2 n0 = new Vector2(-d0.y, d0.x);
                Vector2 n1 = new Vector2(-d1.y, d1.x);
                Vector2 miter = (n0 + n1).normalized;
                float miterScale = 1f / Mathf.Max(0.45f, Vector2.Dot(miter, n1));
                Vector2 offset = miter * (halfWidth * miterScale);
                innerCorners[i] = b - offset;
                outerCorners[i] = b + offset;
            }
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                AddQuadStrip(pos, norm, uv0, uv1, uv2, uv3, idx,
                    innerCorners[i], outerCorners[i], innerCorners[next], outerCorners[next], stepsPerEdge, z, cylR, cardHalfH);
            }
        }

        static void AddRibbonSegment(List<Vector3> pos, List<Vector3> norm, List<Vector2> uv0, List<Vector4> uv1,
            List<Vector4> uv2, List<Vector4> uv3, List<int> idx, Vector2 from, Vector2 to, float width, int steps, float z, float cylR, float cardHalfH)
        {
            Vector2 dir = (to - from).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x) * (width * 0.5f);
            AddQuadStrip(pos, norm, uv0, uv1, uv2, uv3, idx,
                from - perp, from + perp, to - perp, to + perp, steps, z, cylR, cardHalfH);
        }

        static void AddQuadStrip(List<Vector3> pos, List<Vector3> norm, List<Vector2> uv0, List<Vector4> uv1,
            List<Vector4> uv2, List<Vector4> uv3, List<int> idx, Vector2 aIn, Vector2 aOut, Vector2 bIn, Vector2 bOut,
            int steps, float z, float cylR, float cardHalfH)
        {
            int count = Mathf.Max(1, steps);
            int baseVert = pos.Count;
            for (int k = 0; k <= count; k++)
            {
                float t = (float)k / count;
                Vector2 pIn = Vector2.Lerp(aIn, bIn, t);
                Vector2 pOut = Vector2.Lerp(aOut, bOut, t);
                AddLabelSurfaceVertex(pos, norm, uv0, uv1, uv2, uv3, pIn.x, pIn.y, z, cylR, cardHalfH);
                AddLabelSurfaceVertex(pos, norm, uv0, uv1, uv2, uv3, pOut.x, pOut.y, z, cylR, cardHalfH);
            }
            for (int k = 0; k < count; k++)
            {
                int i0 = baseVert + k * 2;
                int o0 = i0 + 1;
                int i1 = i0 + 2;
                int o1 = i0 + 3;
                idx.Add(i0);
                idx.Add(o0);
                idx.Add(o1);
                idx.Add(i0);
                idx.Add(o1);
                idx.Add(i1);
            }
        }

        static void AddLabelSurfaceVertex(List<Vector3> pos, List<Vector3> norm, List<Vector2> uv0, List<Vector4> uv1,
            List<Vector4> uv2, List<Vector4> uv3, float x, float y, float z, float cylR, float cardHalfH)
        {
            float theta = x / Mathf.Max(0.02f, cylR);
            Vector3 flat = new Vector3(x, y, z);
            Vector3 flatNormal = Vector3.back;
            Vector3 curved = new Vector3(cylR * Mathf.Sin(theta), y, z + cylR * (1f - Mathf.Cos(theta)));
            Vector3 curvedNormal = new Vector3(Mathf.Sin(theta), 0f, -Mathf.Cos(theta));
            float delay = Mathf.Clamp01((cardHalfH - y) / Mathf.Max(0.01f, 2f * cardHalfH));
            pos.Add(flat);
            norm.Add(flatNormal);
            uv0.Add(Vector2.zero);
            uv1.Add(new Vector4(curved.x, curved.y, curved.z, delay));
            uv2.Add(new Vector4(curvedNormal.x, curvedNormal.y, curvedNormal.z, 0f));
            uv3.Add(new Vector4(0f, 0f, -1f, 0f));
        }

        TextMesh MakeRoomNote(Font font)
        {
            var go = new GameObject("RoomNote");
            go.transform.SetParent(transform, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 62;
            tm.characterSize = 0.0013f;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.94f, 0.93f, 0.90f, 0f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = font.material;
            RitualUtil.Quiet(r);
            go.SetActive(false);
            return tm;
        }

        TextMesh MakeHeapNote(Font font)
        {
            var go = new GameObject("HeapNote");
            go.transform.SetParent(transform, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = 0.00135f;
            tm.lineSpacing = 1.05f;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.96f, 0.95f, 0.92f, 0f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = font.material;
            RitualUtil.Quiet(r);
            go.SetActive(false);
            return tm;
        }

        SplatSurfaceMaterial LookOf(TimeCategoryAsset category)
        {
            if (category == null) return SplatSurfaceMaterial.Clay("none");
            if (_looks.TryGetValue(category, out SplatSurfaceMaterial look)) return look;
            look = (category.look != null ? SplatSurfaceMaterial.FromAsset(category.look) : null) ?? SplatSurfaceMaterial.Clay(category.id);
            _looks[category] = look;
            return look;
        }

        int IndexOf(TimeCategoryAsset category)
        {
            for (int i = 0; categories != null && i < categories.Length; i++)
                if (categories[i] == category) return i;
            return -1;
        }

        TimePieceView CreatePiece(TimeHeap heap)
        {
            return new TimePieceView(piecesRoot, heap.Category, IndexOf(heap.Category), LookOf(heap.Category), splatMaterial, _config, tuning, labelFont, head, glowMaterial, waxMaterial);
        }

        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            _placed = true;
            PlaceJar();
            if (_clock != null && !_clockDone) PlaceClock(true);
            PlaceShelf();
        }

        void PlaceJar()
        {
            if (_jar == null) return;
            Vector3 p = transform.TransformPoint(_config.jarOffset);
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            Quaternion yaw = fwd.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(fwd.normalized, Vector3.up) : Quaternion.identity;
            _jar.root.SetPositionAndRotation(p, yaw);
            _jar.root.localScale = Vector3.one;
            _lastJarRot = yaw;
            _twoHandJarActive = false;
            _deskY = p.y;
            _tableY = p.y;
        }

        void Update()
        {
            if (!_ready) Init();
            if (!_placed && autoPlace && head != null)
            {
                if (head.position.sqrMagnitude > 1e-6f && ++_frames >= settleFrames)
                {
                    Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                    if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
                    fwd.Normalize();
                    PlaceAt(head.position + fwd * _config.placeDistance + Vector3.down * _config.placeBelowEye, Quaternion.LookRotation(fwd, Vector3.up));
                    Debug.Log($"{Tag} ritual placed at {transform.position} jar={_jar.root.position}");
                }
            }
            HandFrame frame = default;
            if (_input != null) _input.TryGetFrame(out frame);
            float gapMs = Time.unscaledDeltaTime * 1000f;
            if (gapMs > 60f && _ready && Time.frameCount > 120)
            {
                _hitches++;
                Debug.Log($"{Tag} ritual hitch gap={gapMs:0}ms lastTick={_lastTickMs:0.0}ms sealed={_sealed} sealT={_sealTime:0.00} clock={(_clock != null ? _clock.Phase.ToString() : "-")} pouring={(_jar != null && _jar.Pouring)} pieces={_pieces.Count} n={_hitches}");
            }
            _watch.Restart();
            Tick(Time.time, Time.deltaTime, frame);
            _watch.Stop();
            _lastTickMs = (float)_watch.Elapsed.TotalMilliseconds;
            Govern(Time.unscaledDeltaTime);
            PollLive(Time.unscaledDeltaTime);
        }

        public void Tick(float now, float dt, in HandFrame frame)
        {
            if (!_ready) Init();
            dt = Mathf.Clamp(dt, 0f, 0.05f);
            _pourPreviewGlow = Color.clear;
            _hoveredHeap = null;
            _hands.Update(frame, trackingSpace, _config, dt);
            StepClock(dt);
            for (int h = 0; h < 2; h++) StepHand(h, now, dt);
            StepHint(dt);
            StepJarPose(dt);
            StepShelf(dt);
            if (!_sealed) _jar.SetInnerGlow(_pourPreviewGlow);
            if (_config.sweepEnabled && !_sealed) StepSweep(now, dt);
            if (!_sealed) StepTouch(now, dt);
            for (int i = _pieces.Count - 1; i >= 0; i--)
            {
                TimePieceView piece = _pieces[i];
                piece.Tick(now, dt);
                if (!piece.Alive) _pieces.RemoveAt(i);
            }
            StepSeal(now, dt);
            StepMicrogesture(now);
            StepGuideTrails(dt);
            StepRoomNote(dt);
            StepHeapNote(dt);
            _jar.Tick(now, dt);
            _wax.Tick(dt);
            _glow.Tick(dt);
            _pool.Tick(dt, _jar.root.position, _tableY);
            StepLight();
            _sealWave?.Tick(dt, EyePosition());
            if (pull != null && pull.Heaps != null)
                for (int i = 0; i < pull.Heaps.Length; i++)
                    if (pull.Heaps[i] != null) pull.Heaps[i].Tick(now, dt);
            if (sound != null) sound.Tick(dt, _config.volume);
            StepPanel(dt);
        }

        void StepHand(int h, float now, float dt)
        {
            ref RitualHand hand = ref _hands[h];
            ref Grip g = ref _grips[h];
            if (g.mode == GripPull)
            {
                pull.Tick(h, hand, _config, dt);
                if (!pull.IsPulling(h)) g = default;
                return;
            }
            if (g.mode == GripHold)
            {
                if (!hand.tracked || !hand.pinching || g.piece == null || !g.piece.Alive || g.piece.state != PieceState.Held)
                {
                    if (g.piece != null && g.piece.Alive) Release(h, ref g);
                    _bonds[h] = default;
                    g = default;
                }
                else HoldPiece(h, ref g, hand, now, dt);
                return;
            }
            if (g.mode == GripJar)
            {
                if (!hand.tracked || !hand.pinching) g = default;
                return;
            }
            if (g.mode == GripSqueeze)
            {
                StepSqueeze(h, ref g, hand, now, dt);
                return;
            }
            if (g.mode == GripLid)
            {
                if (!hand.tracked || !hand.pinching)
                {
                    ReleaseLid();
                    g = default;
                }
                else MoveLid(hand.pinch);
                return;
            }
            if (g.mode == GripWax)
            {
                if (!hand.tracked || !hand.pinching)
                {
                    bool dripping = _wax.EndDraw(!_jar.Pouring && !_sealed);
                    Debug.Log($"{Tag} ritual wax {(dripping ? "drip" : "return")} size={_wax.DropSize:0.00}");
                    if (!dripping && sound != null) sound.Cancel(_wax.DropPosition);
                    g = default;
                }
                else _wax.DrawTo(hand.pinch, dt);
                return;
            }
            if (!hand.tracked) return;
            if (!_sealed && !_lidClosed && pull != null)
            {
                TimeHeap near = pull.HeapAt(hand.pinch, _config.heapHoverRange * 1.35f, out float d);
                if (near != null)
                {
                    if (d < _config.heapHoverRange) near.Hover(1f - Mathf.Clamp01(d / Mathf.Max(1e-4f, _config.heapHoverRange)));
                    if (near.Present) _hoveredHeap = near;
                }
            }
            if (!hand.pinchStarted) return;
            if (!_sealed && !_lidClosed && !_jar.Pouring && _wax.LidShown && !_config.waxStick
                && Vector3.Distance(hand.pinch, _wax.lidRoot.position) < _config.lidGrabRadius
                && _grips[1 - h].mode != GripLid)
            {
                g.mode = GripLid;
                _lidGrabOffset = _wax.lidRoot.position - hand.pinch;
                Debug.Log($"{Tag} ritual lid grab hand={h}");
                return;
            }
            if (!_sealed)
            {
                float heapDistance = float.MaxValue;
                TimeHeap heap = pull != null && !_lidClosed ? pull.HeapAt(hand.pinch, _config.grabRadius, out heapDistance) : null;
                TimePieceView piece = NearestPiece(hand.pinch, _config.holdReach, out float pieceDistance);
                float stickDistance = _wax.StickDistance(hand.pinch);
                if (stickDistance < _config.grabRadius && (heap == null || stickDistance < heapDistance) && (piece == null || stickDistance < pieceDistance))
                {
                    if (_wax.BeginDraw(hand.pinch))
                    {
                        g.mode = GripWax;
                        Debug.Log($"{Tag} ritual wax draw start hand={h}");
                        return;
                    }
                }
                if (heap != null && (piece == null || (pieceDistance >= _config.holdReach * 0.65f && heapDistance < pieceDistance - 0.015f)))
                {
                    g.mode = GripPull;
                    _everPulled = true;
                    _pieces.Add(pull.Begin(h, heap, hand.pinch, CreatePiece));
                    return;
                }
                if (piece != null)
                {
                    Grab(h, ref g, piece, hand);
                    return;
                }
            }
            bool otherHoldingJar = _grips[1 - h].mode == GripJar;
            if (!_shelved && _jar.InGrabZone(hand.pinch, _sealed, otherHoldingJar))
            {
                g.mode = GripJar;
                g.jarOffset = _jar.root.position - hand.pinch;
                if (_sealed) Debug.Log($"{Tag} ritual jar grab hand={h} shelfD={(_shelf != null ? _shelf.CaptureDistance(_jar.root.position) : -1f):0.000}");
            }
        }

        void Grab(int h, ref Grip g, TimePieceView piece, in RitualHand hand)
        {
            int o = 1 - h;
            ref Grip other = ref _grips[o];
            if (_config.crumpleEnabled && other.mode == GripHold && other.piece == piece)
            {
                float span = Vector3.Distance(hand.pinch, _hands[o].pinch);
                bool rod = piece.shape == PieceShape.Rod;
                if (rod ? span >= _config.crumpleMinSpan && span > _config.crumpleDistance + 0.01f : span >= _config.compactMinSpan)
                {
                    g.mode = GripSqueeze;
                    g.piece = piece;
                    g.span = span;
                    Debug.Log($"{Tag} ritual squeeze start {piece.data.category} units={piece.Units} span={span:0.000}");
                    return;
                }
            }
            for (int k = 0; k < 2; k++)
                if ((_grips[k].mode == GripHold || _grips[k].mode == GripSqueeze) && _grips[k].piece == piece) _grips[k] = default;
            g.mode = GripHold;
            g.piece = piece;
            g.vel = Vector3.zero;
            _bonds[h] = default;
            piece.Hold();
            Anchor(ref g, hand);
        }

        void Release(int h, ref Grip g)
        {
            TimePieceView piece = g.piece;
            piece.Drop();
            if (piece.state != PieceState.Floating) return;
            if (_config.releaseGlide > 0f)
            {
                Vector3 glide = Vector3.ClampMagnitude(g.vel * _config.releaseGlide, Mathf.Max(0f, _config.releaseGlideMax));
                if (glide.sqrMagnitude > 1e-8f) piece.Nudge(glide);
            }
            Bond b = _bonds[h];
            _bonds[h] = default;
            if (!_config.stickyMerge || b.t <= 0f || b.other == null || !b.other.Alive) return;
            if (b.other.state != PieceState.Floating && b.other.state != PieceState.Held) return;
            if (piece.DistanceTo(b.other) >= _config.bondBreak) return;
            int o = 1 - h;
            if (_grips[o].mode == GripHold && _grips[o].piece == b.other)
            {
                Merge(b.other, piece);
                Anchor(ref _grips[o], _hands[o]);
            }
            else Merge(piece, b.other);
        }

        void StepSqueeze(int h, ref Grip g, in RitualHand hand, float now, float dt)
        {
            int o = 1 - h;
            ref Grip holder = ref _grips[o];
            ref RitualHand holderHand = ref _hands[o];
            TimePieceView piece = g.piece;
            bool held = holder.mode == GripHold && holder.piece == piece && holderHand.tracked && holderHand.pinching;
            if (!hand.tracked || !hand.pinching || piece == null || !piece.Alive || !held || piece.state != PieceState.Held)
            {
                if (piece != null && piece.Alive)
                {
                    piece.SetSqueeze(0f);
                    if (held) Anchor(ref holder, holderHand);
                }
                g = default;
                return;
            }
            Vector3 a = holderHand.pinch, b = hand.pinch;
            float d = Vector3.Distance(a, b);
            Transform t = piece.Transform;
            float follow = 1f - Mathf.Exp(-dt * _config.holdFollow);
            Vector3 axis = d > 1e-4f ? (b - a) / d : t.forward;
            if (piece.shape == PieceShape.Blob)
            {
                g.span = Mathf.Max(g.span, d);
                float travel = Mathf.Max(_config.compactTravel, g.span * _config.compactShare);
                float amount = (g.span - d) / travel;
                piece.SetSqueeze(Mathf.Clamp01(amount * 0.65f), axis);
                Vector3 midBlob = (a + b) * 0.5f;
                piece.SetPose(Vector3.Lerp(t.position, midBlob, follow), t.rotation, _config.carry);
                Vector3 blobRelVel = (hand.palmVelocity - holderHand.palmVelocity) * 0.5f;
                float blobContact = piece.Touch(midBlob, blobRelVel, 0.045f, dt);
                if (blobContact > 0.15f && blobRelVel.sqrMagnitude > 0.002f && now >= _nextTouchSoundTime[h])
                {
                    if (sound != null) sound.Knead(midBlob, piece.feel, IsWindChime(piece));
                    _nextTouchSoundTime[h] = now + 0.26f;
                }
                return;
            }
            piece.SetSqueeze((g.span - d) / Mathf.Max(1e-3f, g.span - _config.crumpleDistance), axis);
            if (Vector3.Dot(axis, t.forward) < 0f) axis = -axis;
            Quaternion rot = Quaternion.LookRotation(axis, Mathf.Abs(axis.y) < 0.95f ? Vector3.up : Vector3.right);
            Vector3 pos = (a + b) * 0.5f - axis * (piece.Length * 0.5f);
            piece.SetPose(Vector3.Lerp(t.position, pos, follow), Quaternion.Slerp(t.rotation, rot, follow), _config.carry);
            Vector3 mid = (a + b) * 0.5f;
            Vector3 relVel = (hand.palmVelocity - holderHand.palmVelocity) * 0.5f;
            float contact = piece.Touch(mid, relVel, 0.045f, dt);
            if (contact > 0.15f && relVel.sqrMagnitude > 0.002f && now >= _nextTouchSoundTime[h])
            {
                if (sound != null) sound.Knead(mid, piece.feel, IsWindChime(piece));
                _nextTouchSoundTime[h] = now + 0.26f;
            }
        }

        void Refuse(TimePieceView piece)
        {
            piece.Drop();
            Vector3 mouth = _jar.MouthCenter;
            Vector3 away = piece.Center - mouth;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-6f && head != null) away = Vector3.ProjectOnPlane(head.position - mouth, Vector3.up);
            if (away.sqrMagnitude < 1e-6f) away = -_jar.root.forward;
            piece.Nudge(away.normalized * _config.rejectBounce + Vector3.up * (_config.rejectBounce * 0.5f));
            piece.AddGlow(0.6f);
            if (sound != null) sound.Cancel(mouth);
            Refusals++;
            int room = _jar.Room;
            if (_config.timeLabels) ShowRoomNote(room > 0 ? "Room Left · " + TimeUnits.LabelEn(room) : "Vessel is Full");
            Debug.Log($"{Tag} ritual pour refused {piece.data.category} units={piece.Units} room={room}u total={_jar.TotalUnits}u");
        }

        void ShowRoomNote(string text)
        {
            _roomTimer = _config.roomNoteSeconds;
            if (_roomNote == null) return;
            if (_roomNote.text != text) _roomNote.text = text;
            _roomNote.gameObject.SetActive(true);
        }

        void StepRoomNote(float dt)
        {
            if (_roomTimer <= 0f) return;
            _roomTimer = Mathf.Max(0f, _roomTimer - dt);
            if (_roomNote == null) return;
            Transform t = _roomNote.transform;
            t.position = _jar.MouthCenter + new Vector3(0f, -0.035f, -0.062f);
            Vector3 eyePos = (head != null && head.position.sqrMagnitude > 1e-4f) ? head.position : new Vector3(0f, 1.17f, 0.02f);
            Vector3 view = t.position - eyePos;
            float horiz = Mathf.Sqrt(view.x * view.x + view.z * view.z);
            view.y = Mathf.Clamp(view.y, -horiz * 0.18f, horiz * 0.18f);
            if (view.sqrMagnitude > 1e-6f) t.rotation = Quaternion.LookRotation(view.normalized, Vector3.up);
            Color c = _roomNote.color;
            c.a = 0.9f * Mathf.Clamp01(_roomTimer / 0.5f) * Mathf.Clamp01((_config.roomNoteSeconds - _roomTimer) / 0.15f + 0.2f);
            _roomNote.color = c;
            if (_roomTimer <= 0f) _roomNote.gameObject.SetActive(false);
        }

        void StepHeapNote(float dt)
        {
            if (_heapNote == null) return;
            bool pulling = pull != null && (pull.IsPulling(0) || pull.IsPulling(1));
            bool show = _config.timeLabels && !_sealed && _hoveredHeap != null && _hoveredHeap.Category != null && !pulling;
            if (show)
            {
                _heapNoteAlpha = Mathf.MoveTowards(_heapNoteAlpha, 0.92f, dt * 6f);
                TimeCategoryAsset cat = _hoveredHeap.Category;
                string s = $"{cat.EffectiveSymbol} {cat.EffectiveTitleEn}\n{cat.EffectiveMemoryEn}";
                if (_heapNote.text != s) _heapNote.text = s;
                _heapNote.transform.position = _hoveredHeap.transform.position + Vector3.up * (_hoveredHeap.Radius + 0.032f);
                Vector3 eyePos = (head != null && head.position.sqrMagnitude > 1e-4f) ? head.position : new Vector3(0f, 1.17f, 0.02f);
                Vector3 view = _heapNote.transform.position - eyePos;
                float horiz = Mathf.Sqrt(view.x * view.x + view.z * view.z);
                view.y = Mathf.Clamp(view.y, -horiz * 0.18f, horiz * 0.18f);
                if (view.sqrMagnitude > 1e-6f) _heapNote.transform.rotation = Quaternion.LookRotation(view.normalized, Vector3.up);
            }
            else
            {
                _heapNoteAlpha = Mathf.MoveTowards(_heapNoteAlpha, 0f, dt * 4f);
            }
            bool active = _heapNoteAlpha > 0.01f;
            if (_heapNote.gameObject.activeSelf != active) _heapNote.gameObject.SetActive(active);
            if (active)
            {
                Color c = _heapNote.color;
                c.a = _heapNoteAlpha;
                _heapNote.color = c;
            }
        }

        void Anchor(ref Grip g, in RitualHand hand)
        {
            Transform t = g.piece.Transform;
            g.local = Quaternion.Inverse(t.rotation) * (hand.pinch - t.position);
            g.relative = Quaternion.Inverse(hand.rotation) * t.rotation;
        }

        TimePieceView NearestPiece(Vector3 point, float reach, out float bestDistance)
        {
            TimePieceView best = null;
            bestDistance = reach;
            for (int i = 0; i < _pieces.Count; i++)
            {
                TimePieceView piece = _pieces[i];
                if (piece.state != PieceState.Floating && piece.state != PieceState.Held) continue;
                float d = piece.SurfaceDistance(point);
                if (d < bestDistance)
                {
                    best = piece;
                    bestDistance = d;
                }
            }
            return best;
        }

        void HoldPiece(int h, ref Grip g, in RitualHand hand, float now, float dt)
        {
            TimePieceView piece = g.piece;
            if (_grips[1 - h].mode == GripSqueeze && _grips[1 - h].piece == piece) return;
            Transform t = piece.Transform;
            Quaternion wanted = hand.rotation * g.relative;
            bool weighted = _config.weightLag > 0f;
            float heavy = weighted ? Mathf.Max(0.5f, _config.weightFollow) / WeightOmega(_config, piece.Units) : 1f;
            Quaternion rotation = Quaternion.Slerp(t.rotation, wanted, 1f - Mathf.Exp(-dt * 14f * _config.holdRotate / Mathf.Sqrt(heavy)));
            Vector3 target = hand.pinch - rotation * g.local;
            Vector3 position;
            if (weighted && dt > 0f)
            {
                float omega = WeightOmega(_config, piece.Units);
                Vector3 x = t.position;
                Vector3 err = target - x;
                float maxLag = Mathf.Max(0.005f, _config.weightMaxLag);
                if (err.magnitude > maxLag)
                {
                    x = target - err.normalized * maxLag;
                    err = target - x;
                }
                float zeta = _config.weightDamping;
                g.vel = (g.vel + err * (dt * omega * omega)) / (1f + 2f * zeta * omega * dt + omega * omega * dt * dt);
                position = x + g.vel * dt;
            }
            else
            {
                position = Vector3.Lerp(t.position, target, 1f - Mathf.Exp(-dt * _config.holdFollow));
                if (dt > 0f) g.vel = (position - t.position) / dt;
            }

            bool magnetSpout = !_sealed && !_lidClosed && !_press.pressing && !_jar.Pouring && _jar.CanAccept(piece.Units);
            if (magnetSpout)
            {
                float dSpout = Vector3.Distance(piece.Center, _jar.Spout);
                if (dSpout < 0.052f)
                {
                    float pullK = 1f - dSpout / 0.052f;
                    position = Vector3.Lerp(position, position + (_jar.Spout - piece.Center), 0.22f * pullK);
                    if (piece.category != null) _pourPreviewGlow = piece.category.glowColor * (0.14f * pullK);
                }
            }
            piece.SetPose(position, rotation, _config.carry);

            float roll = Mathf.Clamp((hand.rollSpeed - 0.9f) * _config.rollGlow, 0f, _config.rollGlowMax);
            if (roll > 0.03f) piece.AddGlow(roll);

            if (_config.stickyMerge && StepBond(h, piece, dt))
            {
                Anchor(ref g, hand);
                g.vel = Vector3.zero;
            }

            for (int i = 0; i < _pieces.Count; i++)
            {
                TimePieceView other = _pieces[i];
                if (other == piece || (other.state != PieceState.Floating && other.state != PieceState.Held)) continue;
                float d = piece.DistanceTo(other);
                if (other.categoryIndex == piece.categoryIndex)
                {
                    if (_config.stickyMerge) continue;
                    if (other.state == PieceState.Held && d < _config.mergeDistance)
                    {
                        Merge(piece, other);
                        Anchor(ref g, hand);
                        break;
                    }
                    if (other.state == PieceState.Floating && d < _config.mergeMagnetRange)
                    {
                        if (d < _config.mergeTouchDistance)
                        {
                            Merge(piece, other);
                            Anchor(ref g, hand);
                            break;
                        }
                        Attract(other, piece, d, dt);
                    }
                    continue;
                }
                if (other.state == PieceState.Floating && d < _config.repelDistance)
                {
                    Vector3 away = other.Center - piece.Center;
                    if (away.sqrMagnitude < 1e-8f) away = Vector3.up;
                    float push = Mathf.Clamp(1f - d / Mathf.Max(1e-4f, _config.repelDistance), 0f, 1.5f);
                    other.Nudge(away.normalized * (_config.repelPush * push * dt * 8f));
                }
            }

            if (!_sealed && !_lidClosed && !_press.pressing && _jar.InPourZone(piece))
            {
                if (_jar.CanAccept(piece.Units))
                {
                    _wax.Hide();
                    _press = default;
                    _jar.BeginPour(piece);
                    g = default;
                }
                else if (!_jar.Pouring && !_jar.Fits(piece.Units))
                {
                    Refuse(piece);
                    g = default;
                }
            }
        }

        void Attract(TimePieceView loose, TimePieceView toward, float gap, float dt)
        {
            Attract(loose, toward, gap, dt, _config.mergeMagnetSpeed);
        }

        void Attract(TimePieceView loose, TimePieceView toward, float gap, float dt, float speed)
        {
            loose.Segment(out Vector3 a, out Vector3 b);
            toward.Segment(out Vector3 c, out Vector3 e);
            RitualUtil.SegmentDistance(a, b, c, e, out Unity.Mathematics.float3 from, out Unity.Mathematics.float3 to);
            Vector3 dir = (Vector3)to - (Vector3)from;
            if (dir.sqrMagnitude < 1e-8f) dir = toward.Center - loose.Center;
            if (dir.sqrMagnitude < 1e-8f) return;
            float k = 1f - Mathf.Clamp01(gap / Mathf.Max(1e-4f, _config.mergeMagnetRange));
            Vector3 want = dir.normalized * (speed * (0.35f + 0.65f * k));
            loose.Nudge((want - loose.drift) * (1f - Mathf.Exp(-dt * 10f)));
            if (_config.mergeMagnetGlow > 0f) loose.AddGlow(_config.mergeMagnetGlow * k);
        }

        bool StepBond(int h, TimePieceView piece, float dt)
        {
            ref Bond b = ref _bonds[h];
            float range = Mathf.Max(_config.mergeMagnetRange, _config.bondBreak);
            TimePieceView best = null;
            float bestGap = range;
            for (int i = 0; i < _pieces.Count; i++)
            {
                TimePieceView other = _pieces[i];
                if (other == piece || other.categoryIndex != piece.categoryIndex) continue;
                if (other.state != PieceState.Floating && other.state != PieceState.Held) continue;
                float gap = piece.DistanceTo(other);
                if (gap < bestGap)
                {
                    best = other;
                    bestGap = gap;
                }
            }
            int o = 1 - h;
            if (best != null && h > o && _grips[o].mode == GripHold && _grips[o].piece == best)
            {
                b = default;
                return false;
            }
            if (best != b.other)
            {
                if (b.other != null && b.other.Alive && b.t > 0f) Snap(piece, b.other);
                b.other = best;
                b.t = 0f;
            }
            if (best == null) return false;

            if (best.state == PieceState.Floating)
            {
                float rest = _config.bondStart * 0.5f;
                if (bestGap < 0f)
                {
                    Vector3 away = best.Center - piece.Center;
                    if (away.sqrMagnitude < 1e-8f) away = Vector3.up;
                    best.Nudge(away.normalized * Mathf.Min(0.4f, -bestGap * 14f) - best.drift * (1f - Mathf.Exp(-dt * 12f)));
                }
                else if (bestGap <= rest) best.Nudge(-best.drift * (1f - Mathf.Exp(-dt * 10f)));
                else if (b.t > 0f) Attract(best, piece, bestGap, dt, _config.bondFollow);
                else if (bestGap < _config.mergeMagnetRange) Attract(best, piece, bestGap, dt);
            }
            Vector3 contact = Contact(piece, best);
            if (b.t > 0f && bestGap >= _config.bondBreak)
            {
                Snap(piece, best);
                b.t = 0f;
            }
            else if (b.t > 0f || bestGap < _config.bondStart)
            {
                if (b.t <= 0f) Debug.Log($"{Tag} ritual bond start {piece.data.category} {piece.Units}+{best.Units} gap={bestGap:0.000}");
                b.t += dt / Mathf.Max(0.05f, _config.bondSeconds);
            }

            float amount;
            if (b.t > 0f)
            {
                float stretch = Mathf.Clamp01((bestGap - _config.bondStart) / Mathf.Max(1e-4f, _config.bondBreak - _config.bondStart));
                amount = Mathf.Lerp(_config.bondLean, 1f, RitualUtil.Smooth(b.t * 1.6f)) * (1f - 0.55f * stretch);
            }
            else
            {
                float lean = 1f - Mathf.Clamp01(bestGap / Mathf.Max(1e-4f, _config.mergeMagnetRange));
                amount = _config.bondLean * lean;
            }
            if (amount > 0.001f)
            {
                piece.SetBridge(contact, amount);
                best.SetBridge(contact, amount);
            }

            if (b.t < 1f) return false;
            b = default;
            Merge(piece, best);
            return true;
        }

        static Vector3 Contact(TimePieceView a, TimePieceView b)
        {
            a.Segment(out Vector3 a0, out Vector3 a1);
            b.Segment(out Vector3 b0, out Vector3 b1);
            RitualUtil.SegmentDistance(a0, a1, b0, b1, out Unity.Mathematics.float3 ca, out Unity.Mathematics.float3 cb);
            Vector3 dir = (Vector3)cb - (Vector3)ca;
            if (dir.sqrMagnitude < 1e-8f) dir = b.Center - a.Center;
            if (dir.sqrMagnitude < 1e-8f) return a.Center;
            dir.Normalize();
            Vector3 sa = (Vector3)ca + dir * a.Radius;
            Vector3 sb = (Vector3)cb - dir * b.Radius;
            return (sa + sb) * 0.5f;
        }

        void Snap(TimePieceView a, TimePieceView b)
        {
            a.Snap();
            b.Snap();
            Snaps++;
            Vector3 at = Contact(a, b);
            if (sound != null) sound.Snap(at);
            Debug.Log($"{Tag} ritual bond snap {a.data.category} gap={a.DistanceTo(b):0.000} n={Snaps}");
        }

        void Merge(TimePieceView keeper, TimePieceView absorbed)
        {
            for (int o = 0; o < 2; o++)
                if ((_grips[o].mode == GripHold || _grips[o].mode == GripSqueeze) && _grips[o].piece == absorbed) _grips[o] = default;
            int before = keeper.Units;
            int added = absorbed.Units;
            keeper.Absorb(absorbed, _config.mergedShape);
            if (sound != null) sound.Chimes(keeper.Center, before, Mathf.Min(added, 24), _config.chimeInterval, IsWindChime(keeper));
            Debug.Log($"{Tag} ritual merge {keeper.data.category} {before}+{added}={keeper.Units} merged={keeper.data.mergedCount} shape={keeper.shape}");
        }

        void StepSweep(float now, float dt)
        {
            for (int h = 0; h < 2; h++)
            {
                ref RitualHand hand = ref _hands[h];
                if (!hand.tracked || hand.pinching || _grips[h].mode != GripNone) continue;
                if (hand.openness < _config.sweepOpenFinger) continue;
                float speed = hand.palmVelocity.magnitude;
                if (speed < _config.sweepSpeed) continue;
                for (int i = 0; i < _pieces.Count; i++)
                {
                    TimePieceView piece = _pieces[i];
                    if (piece.state != PieceState.Floating) continue;
                    Vector3 to = hand.palm - piece.Center;
                    float d = to.magnitude;
                    if (d > _config.sweepRadius || d < 1e-4f) continue;
                    piece.Nudge(to / d * (_config.sweepPull * dt * (0.4f + speed)));
                    piece.sweptAt = now;
                }
            }
            for (int i = 0; i < _pieces.Count; i++)
            {
                TimePieceView a = _pieces[i];
                if (a.state != PieceState.Floating) continue;
                for (int j = i + 1; j < _pieces.Count; j++)
                {
                    TimePieceView b = _pieces[j];
                    if (b.state != PieceState.Floating || b.categoryIndex != a.categoryIndex) continue;
                    if (now - a.sweptAt > 0.6f && now - b.sweptAt > 0.6f) continue;
                    if (a.DistanceTo(b) >= _config.mergeDistance) continue;
                    if (a.Units >= b.Units) Merge(a, b);
                    else Merge(b, a);
                    return;
                }
            }
        }

        static float ClosestProbe(TimePieceView piece, in RitualHand hand, out Vector3 probe)
        {
            probe = hand.index;
            float best = piece.SurfaceDistance(probe);
            float d = piece.SurfaceDistance(hand.middle);
            if (d < best) { best = d; probe = hand.middle; }
            d = piece.SurfaceDistance(hand.thumb);
            if (d < best) { best = d; probe = hand.thumb; }
            d = piece.SurfaceDistance(hand.palm);
            if (d < best) { best = d; probe = hand.palm; }
            return best;
        }

        public static bool WouldScatter(TimeRitualConfig cfg, in RitualHand hand, float sinceRelease)
        {
            if (!hand.tracked || hand.pinching) return false;
            if (sinceRelease < cfg.scatterGrace) return false;
            if (Vector3.Distance(hand.thumb, hand.index) < cfg.pinchFormDistance) return false;
            return hand.palmVelocity.magnitude >= cfg.scatterSpeed;
        }

        void StepField(float now, float dt)
        {
            for (int h = 0; h < 2; h++)
            {
                ref RitualHand hand = ref _hands[h];
                if (hand.pinchEnded) _pinchEndedAt[h] = now;
                int mode = _grips[h].mode;
                if (!hand.tracked || mode == GripPull || mode == GripJar || mode == GripWax) continue;
                bool scatter = WouldScatter(_config, hand, now - _pinchEndedAt[h]);
                bool scatteredAny = false;
                _probes[0] = hand.thumb;
                _probes[1] = hand.index;
                _probes[2] = hand.middle;
                _probes[3] = hand.ring;
                _probes[4] = hand.little;
                _probes[5] = hand.palm;
                for (int k = 0; k < 5; k++) _probeRadii[k] = _config.fieldFinger;
                _probeRadii[5] = _config.fieldPalm;
                for (int i = 0; i < _pieces.Count; i++)
                {
                    TimePieceView piece = _pieces[i];
                    if (piece.state != PieceState.Floating && piece.state != PieceState.Held) continue;
                    if ((mode == GripHold || mode == GripSqueeze) && _grips[h].piece == piece) continue;
                    float extent = piece.shape == PieceShape.Blob ? piece.Radius : piece.Length * 0.5f + piece.Radius;
                    if (Vector3.Distance(hand.palm, piece.Center) > extent + _config.fieldReach) continue;
                    float contact = piece.Field(_probes, _probeRadii, 6, hand.palmVelocity, scatter, dt);
                    if (piece.LastScattered > 0 && !scatteredAny)
                    {
                        scatteredAny = true;
                        if (!_scattering[h])
                        {
                            ScatterEvents++;
                            Debug.Log($"{Tag} ritual scatter {piece.data.category} grains={piece.LastScattered} speed={hand.palmVelocity.magnitude:0.00} pinchGap={Vector3.Distance(hand.thumb, hand.index):0.000} n={ScatterEvents}");
                        }
                    }
                    if (contact > 0.15f && hand.palmVelocity.sqrMagnitude > 0.002f && now >= _nextTouchSoundTime[h])
                    {
                        if (sound != null) sound.Knead(hand.palm, piece.feel, IsWindChime(piece));
                        _nextTouchSoundTime[h] = now + (scatter ? 0.12f : 0.26f);
                    }
                }
                _scattering[h] = scatteredAny;
            }
        }

        void StepTouch(float now, float dt)
        {
            if (_config.handField)
            {
                StepField(now, dt);
                return;
            }
            for (int h = 0; h < 2; h++)
            {
                ref RitualHand hand = ref _hands[h];
                int mode = _grips[h].mode;
                if (!hand.tracked || mode == GripPull || mode == GripJar || mode == GripWax) continue;
                for (int i = 0; i < _pieces.Count; i++)
                {
                    TimePieceView piece = _pieces[i];
                    if (piece.state != PieceState.Floating && piece.state != PieceState.Held) continue;
                    if ((mode == GripHold || mode == GripSqueeze) && _grips[h].piece == piece) continue;
                    float dist = ClosestProbe(piece, hand, out Vector3 probe);
                    if (dist >= 0.028f) continue;
                    float contact = piece.Touch(probe, hand.palmVelocity, 0.036f, dt);
                    if (contact > 0.15f && hand.palmVelocity.sqrMagnitude > 0.002f && now >= _nextTouchSoundTime[h])
                    {
                        if (sound != null) sound.Knead(probe, piece.feel, IsWindChime(piece));
                        _nextTouchSoundTime[h] = now + 0.26f;
                    }
                }
            }
        }

        void StepSeal(float now, float dt)
        {
            if (_sealed)
            {
                _sealTime += dt;
                float t = _sealTime / Mathf.Max(0.1f, _config.breathSeconds);
                float level;
                if (t < 0.25f) level = Mathf.Lerp(1f, 1f + _config.breathRise, RitualUtil.Smooth(t / 0.25f));
                else if (t < 1f) level = Mathf.Lerp(1f + _config.breathRise, _config.sealedIntensity, RitualUtil.Smooth((t - 0.25f) / 0.75f));
                else level = _config.sealedIntensity * (1f + _config.idleBreath * Mathf.Sin((_sealTime - _config.breathSeconds) * 6.2831853f / Mathf.Max(0.5f, _config.idleBreathSeconds)));
                _jar.SetLevel(level);
                float surge = _sealWave != null ? _sealWave.JarGlow : 0f;
                _jar.SetInnerGlow(_glowDominant * (0.1f + _config.bloomJarGlow * surge + 0.4f * _todayPoke));
                _glow.SetBoost(surge);
                float wave = Mathf.Clamp01(_sealTime / 1.35f);
                _jar.SetPulse(wave < 1f ? wave : 0f);
                if (_ending != null && _ending.State >= RitualEnding.Phase.Snuffing)
                {
                    _dim = Mathf.MoveTowards(_dim, 0f, dt * 0.5f);
                    _light = Mathf.MoveTowards(_light, _ending.State == RitualEnding.Phase.Snuffing ? 0.4f : 0f, dt * 0.5f);
                }
                else if (_shelved)
                {
                    _dim = Mathf.MoveTowards(_dim, 0f, dt * 0.5f);
                    _light = Mathf.MoveTowards(_light, 1f, dt / Mathf.Max(0.1f, _config.shelfLightBack));
                }
                else if (_config.roomDarkFromStart)
                {
                    _dim = Mathf.MoveTowards(_dim, 0f, dt * 0.5f);
                    _light = Mathf.MoveTowards(_light, _config.roomLightSealed, dt * _config.roomLightRate);
                }
                else
                {
                    _dim = Mathf.MoveTowards(_dim, _config.baseDim + _config.sealDim, dt * 0.12f);
                    _light = Mathf.MoveTowards(_light, 1f, dt * 0.5f);
                }
                return;
            }
            if (_config.roomDarkFromStart)
            {
                float hold = _press.pressing || _press.full ? _press.progress : 0f;
                float target = _config.roomLight * Mathf.Lerp(1f, _config.pressGather, hold);
                _dim = Mathf.MoveTowards(_dim, 0f, dt * 0.5f);
                _light = Mathf.MoveTowards(_light, target, dt * (hold > 0f ? 0.6f : _config.roomLightRate));
            }
            else
            {
                float gathered = Mathf.Clamp01(_pieces.Count * 0.12f + _jar.TotalUnits / 36f * 0.55f);
                float targetDim = _config.baseDim + _config.sealDim * (0.55f * gathered);
                _dim = Mathf.MoveTowards(_dim, targetDim, dt * 0.18f);
                _light = Mathf.MoveTowards(_light, 1f, dt * 0.5f);
            }
            if (_jar.TotalUnits > 0 && !_jar.Pouring)
            {
                _waxTimer += dt;
                if (_waxTimer >= _config.waxAppearDelay)
                {
                    if (_config.waxStick) _wax.ShowStick(DominantWax());
                    else if (!_lidClosed) _wax.ShowLid();
                }
            }
            else _waxTimer = 0f;
            StepLid(now, dt);
            StepMelt(dt);

            float depth = 0f;
            for (int h = 0; h < 2; h++)
                if (_grips[h].mode == GripNone) depth = Mathf.Max(depth, _wax.Depth(_hands[h]));
            if (!_wax.Ready) depth = 0f;
            _press.Step(depth, dt, _config.pressDepth, _config.holdSeconds, _config.recoverSeconds, _config.sealOnRelease);
            _wax.SetHold(_press.progress);
            float pressing = _press.pressing || _press.full ? _press.progress : 0f;
            if (pressing > 0f)
            {
                Color gather = DominantGlow();
                _jar.SetInnerGlow(_pourPreviewGlow + gather * (0.28f * pressing));
            }
            if (sound != null) sound.Press(_wax.DropCenter, _press.pressing ? _press.progress : 0f);
            if (_press.sealedShut) Seal();
        }

        Color DominantGlow()
        {
            int d = DominantIndex();
            return d >= 0 && categories[d] != null ? categories[d].glowColor : new Color(1f, 0.85f, 0.6f, 1f);
        }

        void StepLid(float now, float dt)
        {
            if (_config.waxStick || _wax == null) return;
            bool held = _grips[0].mode == GripLid || _grips[1].mode == GripLid;
            if (held) return;
            Vector3 home = _wax.LidHomeLocal;
            if (_lidClosed)
            {
                _lidCloseAge += dt;
                float u = RitualUtil.Smooth(Mathf.Clamp01(_lidCloseAge / Mathf.Max(0.05f, _config.lidCloseSeconds)));
                float dip = (_press.pressing || _press.full ? _press.progress : 0f) * 0.0025f;
                _lidLocal = Vector3.Lerp(_lidFrom, home, u) + Vector3.down * dip;
                _wax.PlaceLid(_lidLocal, Quaternion.identity);
                return;
            }
            Vector3 float_ = _config.lidOffset + Vector3.up * (0.004f * Mathf.Sin(now * 1.3f));
            if (_lidLocal == Vector3.zero) _lidLocal = float_;
            _lidLocal = Vector3.Lerp(_lidLocal, float_, 1f - Mathf.Exp(-dt * 3f));
            _wax.PlaceLid(_lidLocal, Quaternion.Euler(7f * Mathf.Sin(now * 0.8f), now * 12f, 4f * Mathf.Cos(now * 0.7f)));
        }

        void MoveLid(Vector3 pinch)
        {
            Vector3 world = pinch + _lidGrabOffset;
            _lidLocal = _jar.root.InverseTransformPoint(world);
            _wax.PlaceLid(_lidLocal, Quaternion.Slerp(_wax.lidRoot.localRotation, Quaternion.identity, 0.2f));
            if (Vector3.Distance(_lidLocal, _wax.LidHomeLocal) < _config.lidSnapDistance * 0.4f) CloseLid();
        }

        void ReleaseLid()
        {
            if (_lidClosed) return;
            if (Vector3.Distance(_lidLocal, _wax.LidHomeLocal) < _config.lidSnapDistance) CloseLid();
            else Debug.Log($"{Tag} ritual lid released away from the mouth");
        }

        void CloseLid()
        {
            if (_lidClosed) return;
            _lidClosed = true;
            _lidCloseAge = 0f;
            _lidFrom = _lidLocal;
            for (int h = 0; h < 2; h++)
                if (_grips[h].mode == GripLid) _grips[h] = default;
            _meltAge = 0f;
            _materialized = false;
            Vector3 drop = _wax.DropCenter + Vector3.up * _config.waxLift;
            if (pull != null)
            {
                pull.Abort();
                for (int i = 0; pull.Heaps != null && i < pull.Heaps.Length; i++)
                    if (pull.Heaps[i] != null) pull.Heaps[i].MeltInto(drop, _config.meltSeconds * (0.85f + 0.06f * i));
            }
            if (sound != null) sound.Landed(_jar.MouthCenter, 40);
            Debug.Log($"{Tag} ritual lid closed units={_jar.TotalUnits}");
        }

        void StepMelt(float dt)
        {
            if (_meltAge < 0f || _materialized) return;
            _meltAge += dt;
            if (_meltAge < _config.meltSeconds * 0.78f) return;
            _materialized = true;
            _wax.Materialize(DominantWax());
            Debug.Log($"{Tag} ritual springs melted into wax color={_wax.Color}");
        }

        int DominantIndex()
        {
            Tally();
            return RitualTally.Dominant(_jarUnits);
        }

        Color DominantWax()
        {
            int d = DominantIndex();
            return d >= 0 && categories[d] != null ? categories[d].waxColor : new Color(0.55f, 0.16f, 0.12f, 1f);
        }

        void Seal()
        {
            _sealed = true;
            _sealTime = 0f;
            _wax.Seal();
            if (sound != null)
            {
                if (_config.sealOnRelease)
                {
                    sound.Hush(_config.hushSeconds);
                    sound.Bowl(_jar.MouthCenter);
                }
                else sound.Seal(_jar.MouthCenter);
            }
            if (pull != null)
            {
                pull.Abort();
                for (int i = 0; pull.Heaps != null && i < pull.Heaps.Length; i++)
                    if (pull.Heaps[i] != null) pull.Heaps[i].SetPresent(false);
            }
            for (int i = 0; i < _pieces.Count; i++)
            {
                TimePieceView piece = _pieces[i];
                if (piece.Alive && piece.state != PieceState.Pouring) piece.Dissolve(piece.Center + Vector3.up * 0.03f);
            }
            for (int h = 0; h < 2; h++)
                if (_grips[h].mode != GripJar) _grips[h] = default;

            _glowColors.Clear();
            _glowUnits.Clear();
            IReadOnlyList<RitualJar.Layer> layers = _jar.Layers;
            for (int i = 0; i < layers.Count; i++)
            {
                _glowColors.Add(layers[i].category != null ? layers[i].category.glowColor : Color.white);
                _glowUnits.Add(layers[i].units);
            }
            int d = DominantIndex();
            Color dominant = d >= 0 && categories[d] != null ? categories[d].glowColor : new Color(1f, 0.85f, 0.6f, 1f);
            _deskY = _jar.root.position.y;
            _glowDominant = dominant;
            _today = RecordToday();
            if (_store != null) _store.Save(_today);
            _glow.Bloom(_jar.root.position, _jar.root.rotation, _glowColors, _glowUnits, dominant);
            if (_config.sealOnRelease && _sealWave != null) _sealWave.Begin(_jar.BodyCenter, dominant, _glowColors);
            _pool.Bloom(_jar.root.position, _jar.root.rotation, _tableY, _glowColors, _glowUnits, dominant, _config.breathSeconds * 0.25f, 1f, 0.4f);
            _jar.SetInnerGlow(dominant * 0.1f);
            Debug.Log($"{Tag} ritual sealed units={_jar.TotalUnits} layers={layers.Count} grains={_jar.TotalGrains} dominant={(d >= 0 ? categories[d].id : "-")}");
        }

        public void ResetDay()
        {
            for (int i = 0; i < _pieces.Count; i++) _pieces[i].Kill();
            _pieces.Clear();
            if (pull != null)
            {
                pull.Abort();
                for (int i = 0; pull.Heaps != null && i < pull.Heaps.Length; i++)
                    if (pull.Heaps[i] != null) pull.Heaps[i].SetPresent(true);
            }
            for (int h = 0; h < 2; h++)
            {
                _grips[h] = default;
                _nextTouchSoundTime[h] = 0f;
            }
            _wax.Dispose();
            _glow.Dispose();
            _pool.Dispose();
            _jar.Dispose();
            RitualUtil.Kill(_labelCardMesh);
            RitualUtil.Kill(_labelBorderMesh);
            RitualUtil.Kill(_labelBorderTagMesh);
            RitualUtil.Kill(_labelTextMesh);
            _labelCardMesh = null;
            _labelBorderMesh = null;
            _labelBorderTagMesh = null;
            _labelBorderTag = null;
            _jarGlow = Color.clear;
            _jarFill = 0f;
            _labelTextMesh = null;
            _labelRoot = null;
            _labelCard = null;
            _labelBorder = null;
            _labelTextRenderer = null;
            _labelCachedState = "";
            _labelTextDirty = true;
            _labelTextBuffersDirty = true;
            _labelDeformedPress = -1f;
            _labelDeformedAppear = -1f;
            _press = default;
            _sealed = false;
            _waxTimer = 0f;
            _roomTimer = 0f;
            if (_roomNote != null) _roomNote.gameObject.SetActive(false);
            _hoveredHeap = null;
            _heapNoteAlpha = 0f;
            if (_heapNote != null) _heapNote.gameObject.SetActive(false);
            _labelAppear = 0f;
            _floatSlots = 0;
            _shelved = false;
            _shelveAge = -1f;
            _today = null;
            _todaySwirl.Clear();
            _todayInside[0] = _todayInside[1] = false;
            _todayPoke = 0f;
            _todayPokeCool = 0f;
            _shelfStage = 0f;
            _ending?.Reset();
            _endSeen = RitualEnding.Phase.Hidden;
            _quitIssued = false;
            if (sound != null) sound.Restore();
            _jarWasHeld = false;
            _lidClosed = false;
            _lidCloseAge = 0f;
            _lidLocal = Vector3.zero;
            _meltAge = -1f;
            _materialized = false;
            _everPulled = false;
            _idle = 0f;
            _labelAttach = 0f;
            _labelGlow = 0f;
            _panelTimer = 0f;
            _sealTime = 0f;
            BuildJar();
            if (labelFont != null) MakePanel(labelFont);
            PlaceShelf();
            if (_clock != null && _config.clockIntro)
            {
                _clockDone = false;
                _clockSand = false;
                _clock.Ticked -= OnClockTicked;
                _clock.Dispose();
                _clock = new RitualClock(_config, labelFont, glyphMaterial);
                _clock.Ticked += OnClockTicked;
                HideSprings();
                PlaceClock(true);
                if (sound != null) sound.HoldBgm(true);
            }
            Debug.Log($"{Tag} ritual reset");
        }

        void OnWaxLanded(Vector3 position)
        {
            if (sound != null) sound.Unit(position, 1, false);
            Debug.Log($"{Tag} ritual wax landed color={_wax.Color}");
        }

        static bool IsWindChime(TimePieceView piece) => piece != null && piece.category != null && piece.category.UsesWindChime;

        void OnUnitChanged(TimePieceView piece, int before, int after)
        {
            if (sound != null) sound.Unit(piece.Center, after, after > before, IsWindChime(piece));
        }

        void OnReleased(TimePieceView piece)
        {
            if (piece.Crumple() && sound != null && IsWindChime(piece)) sound.Knead(piece.Center, piece.feel, true);
            Transform t = piece.Transform;
            if (_config.releaseDrift > 1e-4f && t != null) piece.Nudge(t.forward * _config.releaseDrift);
            if (_config.floatAway)
            {
                float wait = _config.formStyle == FormStyle.Flow ? _config.formFlowSeconds + 0.35f : 0.6f;
                piece.FloatTo(NextFloatSlot(), wait);
            }
        }

        Vector3 EyePosition()
        {
            return (head != null && head.position.sqrMagnitude > 1e-4f) ? head.position : transform.position + Vector3.up * _config.placeBelowEye - transform.forward * _config.placeDistance;
        }

        public Vector3 NextFloatSlot()
        {
            int n = _floatSlots++;
            Vector3 eye = EyePosition();
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            fwd.Normalize();
            float u = Mathf.Repeat(n * 0.6180340f + 0.31f, 1f);
            float v = Mathf.Repeat(n * 0.4142136f + 0.17f, 1f);
            float w = Mathf.Repeat(n * 0.7548777f + 0.53f, 1f);
            float azimuth = Mathf.Lerp(-_config.floatSpread, _config.floatSpread, u);
            Vector3 dir = Quaternion.AngleAxis(azimuth, Vector3.up) * fwd;
            float dist = _config.floatDistance * Mathf.Lerp(0.88f, 1.12f, w);
            Vector3 slot = eye + dir * dist + Vector3.up * Mathf.Lerp(_config.floatLow, _config.floatHigh, v);
            if (_jar != null)
            {
                Vector3 jarTop = _jar.root.position + Vector3.up * (_config.jar.height + 0.06f);
                Vector3 flat = slot - jarTop;
                flat.y = 0f;
                if (flat.magnitude < 0.12f && slot.y < jarTop.y) slot.y = jarTop.y;
            }
            return slot;
        }

        Color ColorOfCategory(string id)
        {
            for (int i = 0; categories != null && i < categories.Length; i++)
                if (categories[i] != null && categories[i].id == id) return categories[i].glowColor;
            return new Color(1f, 0.9f, 0.75f, 1f);
        }

        List<DayRecord> PastDays()
        {
            System.DateTime now = System.DateTime.Now;
            if (_config.shelfDemo) return RitualDayStore.Demo(now, Mathf.Max(0, _config.shelfDemoDays));
            int n = _shelf != null ? Mathf.Max(0, _shelf.TodaySlot) : Mathf.Max(0, _config.shelfTodaySlot);
            return _store != null ? _store.Before(now, n) : new List<DayRecord>();
        }

        string NameOfCategory(string id)
        {
            for (int i = 0; categories != null && i < categories.Length; i++)
                if (categories[i] != null && categories[i].id == id) return categories[i].displayEn;
            return id;
        }

        int IndexOfCategory(string id)
        {
            for (int i = 0; categories != null && i < categories.Length; i++)
                if (categories[i] != null && categories[i].id == id) return i;
            return 0;
        }

        void PlaceShelf()
        {
            if (_shelf == null) return;
            _shelf.GlassMesh = _jar != null ? _jar.GlassMesh : null;
            _shelf.SetCorner(transform.TransformPoint(_config.shelfCorner));
            _shelf.Place(EyePosition(), transform.forward, _tableY + _config.shelfLift, System.DateTime.Now);
            _shelf.Show(PastDays());
            _shelf.Fill(_shelved);
            _shelf.SetStage(RitualUtil.Smooth(_shelfStage));
        }

        (RitualJar, RitualWax) MakeShelfJar(Transform parent, DayRecord rec)
        {
            var vessel = new RitualJar(parent, _config, tuning, splatMaterial, glassMaterial, LookOf);
            foreach (DayLayer layer in rec.layers)
            {
                TimeCategoryAsset cat = null;
                for (int i = 0; categories != null && i < categories.Length; i++)
                    if (categories[i] != null && categories[i].id == layer.category) cat = categories[i];
                if (cat == null || layer.units <= 0 || vessel.Room <= 0) continue;
                vessel.FillInstant(cat, IndexOf(cat), Mathf.Min(layer.units, vessel.Room));
            }
            RitualWax wax = null;
            if (waxMaterial != null)
            {
                wax = new RitualWax(vessel.root, _config, waxMaterial, glowMaterial);
                wax.PlaceLid(wax.LidHomeLocal, Quaternion.identity);
                int d = IndexOfCategory(rec.dominant);
                wax.Materialize(categories != null && d < categories.Length && categories[d] != null ? categories[d].waxColor : new Color(0.55f, 0.16f, 0.12f, 1f));
                wax.SetHold(1f);
                wax.Seal();
                for (int i = 0; i < 40; i++) wax.Tick(0.25f);
            }
            return (vessel, wax);
        }

        DayRecord RecordToday()
        {
            var rec = new DayRecord
            {
                date = RitualDayStore.Key(System.DateTime.Now),
                sealedAtUtc = System.DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture)
            };
            IReadOnlyList<RitualJar.Layer> layers = _jar.Layers;
            for (int i = 0; i < layers.Count; i++)
                rec.layers.Add(new DayLayer { category = layers[i].category != null ? layers[i].category.id : "", units = layers[i].units });
            int d = DominantIndex();
            rec.dominant = d >= 0 && categories[d] != null ? categories[d].id : "";
            return rec;
        }

        void Shelve()
        {
            if (_shelved || _shelf == null) return;
            _shelved = true;
            _shelveAge = 0f;
            _shelveFrom = _jar.root.position;
            for (int h = 0; h < 2; h++)
                if (_grips[h].mode == GripJar) _grips[h] = default;
            _shelf.Fill(true);
            _todaySwirl.Clear();
            for (int i = 0; i < _jar.Layers.Count; i++)
                if (_jar.Layers[i].cloud != null) _todaySwirl.Add(_jar.Layers[i].cloud.transform);
            Debug.Log($"{Tag} ritual shelved date={(_today != null ? _today.date : "-")}");
        }

        void StepShelf(float dt)
        {
            if (_shelf == null) return;
            float emphasis = !_clockDone ? 0.35f : (_shelved ? 0.85f : (_sealed ? 1f : 0.6f));
            if (_ending != null) emphasis *= _ending.Mood * (1f - _ending.Fade);
            bool approach = _sealed && _sealTime >= _config.shelfApproachDelay;
            float stageBefore = _shelfStage;
            _shelfStage = Mathf.MoveTowards(_shelfStage, approach ? 1f : 0f, dt / Mathf.Max(0.1f, _config.shelfApproachSeconds));
            if (_shelfStage != stageBefore || !Mathf.Approximately(_shelf.Stage, RitualUtil.Smooth(_shelfStage))) _shelf.SetStage(RitualUtil.Smooth(_shelfStage));
            if (stageBefore <= 0f && _shelfStage > 0f) Debug.Log($"{Tag} ritual shelf approaches");
            _shelf.Tick(dt, emphasis, _shelfNear);
            _todaySwirl.Tick(dt);
            _todayPoke = Mathf.MoveTowards(_todayPoke, 0f, dt * 0.8f);
            _todayPokeCool = Mathf.Max(0f, _todayPokeCool - dt);
            if (_clockDone && (_ending == null || _ending.State < RitualEnding.Phase.Snuffing)) StepPokes();
            StepEnding(dt);
            if (_shelveAge < 0f) return;
            _shelveAge += dt;
            float u = RitualUtil.Smooth(Mathf.Clamp01(_shelveAge / 0.55f));
            Vector3 target = _shelf.TodaySeat;
            Vector3 p = Vector3.Lerp(_shelveFrom, target, u) + Vector3.up * (0.03f * Mathf.Sin(u * Mathf.PI));
            Quaternion upright = UprightYaw(_jar.root, transform.forward);
            _jar.root.SetPositionAndRotation(p, Quaternion.Slerp(_jar.root.rotation, upright, u));
            if (_shelveAge >= 0.55f)
            {
                _jar.root.SetPositionAndRotation(target, upright);
                _shelveAge = -1f;
                if (sound != null) sound.Landed(target, 60);
                _shelf.Landed(_today, _jar.MouthCenter);
                Vector3 toEye = Vector3.ProjectOnPlane(_shelf.Eye - target, Vector3.up);
                toEye = toEye.sqrMagnitude > 1e-6f ? toEye.normalized : -transform.forward;
                _ending?.Arm(target + toEye * 0.12f + Vector3.down * 0.07f, _shelf.Eye, _config.endOrbDelay);
                Debug.Log($"{Tag} ritual landed on shelf note=\"{_shelf.NoteText}\"");
            }
        }

        void StepPokes()
        {
            for (int h = 0; h < 2; h++)
            {
                RitualHand hand = _hands[h];
                bool free = hand.tracked && _grips[h].mode == GripNone && !hand.pinching;
                int hit = _shelf.Touch(h, hand.index, free);
                if (hit >= 0)
                {
                    RitualShelf.ShelfJar j = _shelf.Jars[hit];
                    _shelf.Poke(hit);
                    if (sound != null) sound.DropChord(j.root.position, IndexOfCategory(j.record.dominant));
                    Debug.Log($"{Tag} ritual poke past {j.record.date} \"{j.info}\"");
                }
                bool inside = free && _shelved && _shelveAge < 0f && _jar.InBody(hand.index);
                if (inside && !_todayInside[h] && _todayPokeCool <= 0f)
                {
                    _todaySwirl.Kick(_config.shelfSwirl);
                    _todayPoke = 1f;
                    _todayPokeCool = 0.5f;
                    if (sound != null) sound.DropChord(_jar.BodyCenter, Mathf.Max(0, DominantIndex()));
                    string info = _shelf.InfoFor(_today);
                    _shelf.ShowNote(info, _jar.MouthCenter + Vector3.up * 0.035f, _config.shelfInfoSeconds);
                    Debug.Log($"{Tag} ritual poke today \"{info}\"");
                }
                _todayInside[h] = inside;
            }
        }

        void StepEnding(float dt)
        {
            if (_ending == null) return;
            _ending.Tick(dt, in _hands[0], in _hands[1]);
            RitualEnding.Phase now = _ending.State;
            if (now != _endSeen)
            {
                if (now == RitualEnding.Phase.Rising) Debug.Log($"{Tag} ritual end orb rises at {_ending.Position}");
                if (now == RitualEnding.Phase.Snuffing && sound != null) sound.Snuff(_ending.Position);
                if (now == RitualEnding.Phase.Fading && sound != null) sound.FadeOut(_config.endFadeSeconds);
                _endSeen = now;
            }
            if (_ending.QuitRequested && !_quitIssued)
            {
                _quitIssued = true;
                if (_config.endQuits && !Application.isEditor) Application.Quit();
            }
        }

        void HideSprings()
        {
            if (pull == null || pull.Heaps == null) return;
            for (int i = 0; i < pull.Heaps.Length; i++)
                if (pull.Heaps[i] != null) pull.Heaps[i].HideNow();
        }

        void SetClockSprings()
        {
            if (pull == null || pull.Heaps == null) return;
            for (int i = 0; i < _springPos.Length; i++)
            {
                TimeHeap heap = i < pull.Heaps.Length ? pull.Heaps[i] : null;
                _springPos[i] = heap != null ? heap.transform.position : transform.position;
                _springCol[i] = heap != null && heap.Category != null ? heap.Category.glowColor : Color.white;
            }
            _clock.SetSprings(_springPos, _springCol);
        }

        void PlaceClock(bool show)
        {
            Vector3 eye = EyePosition();
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            fwd.Normalize();
            _clock.Place(eye + fwd * _config.clockDistance + Vector3.up * _config.clockHeight, Quaternion.LookRotation(fwd, Vector3.up));
            SetClockSprings();
            if (show) _clock.Show(System.DateTime.Now, _config.clockDelay);
        }

        void OnClockTicked(Vector3 position, bool tock)
        {
            if (sound != null) sound.ClockTick(position, tock);
        }

        void StepClock(float dt)
        {
            if (_clockDone || _clock == null) return;
            ClockPhase before = _clock.Phase;
            if (before == ClockPhase.Showing || before == ClockPhase.Frozen) SetClockSprings();
            _clock.Tick(dt, System.DateTime.Now, _hands, EyePosition());
            ClockPhase after = _clock.Phase;
            if (after != before) Debug.Log($"{Tag} ritual clock {after}");
            if (after == ClockPhase.Collapsing && pull != null && pull.Heaps != null)
            {
                if (!_clockSand)
                {
                    _clockSand = true;
                    if (sound != null) sound.PourStart(_clock.Center);
                }
                for (int i = 0; i < pull.Heaps.Length; i++)
                    if (pull.Heaps[i] != null) pull.Heaps[i].GrowTo(_clock.ArrivalShare(i));
            }
            if (after == ClockPhase.Done)
            {
                _clockDone = true;
                if (pull != null && pull.Heaps != null)
                    for (int i = 0; i < pull.Heaps.Length; i++)
                        if (pull.Heaps[i] != null) pull.Heaps[i].GrowTo(1f);
                if (sound != null)
                {
                    if (_clockSand) sound.PourStop();
                    sound.HoldBgm(false);
                }
                _clockSand = false;
                _idle = 0f;
            }
        }

        void StepHint(float dt)
        {
            if (!_config.pullHint || !_clockDone || _everPulled || _sealed || pull == null || pull.Heaps == null) return;
            bool busy = _grips[0].mode != GripNone || _grips[1].mode != GripNone || _hands[0].pinching || _hands[1].pinching;
            _idle = busy ? 0f : _idle + dt;
            if (_idle < _config.hintIdleSeconds) return;
            Vector3 toward = EyePosition();
            float bestHand = float.MaxValue;
            for (int h = 0; h < 2; h++)
            {
                if (!_hands[h].tracked) continue;
                float d = Vector3.Distance(_hands[h].palm, transform.position);
                if (d < bestHand)
                {
                    bestHand = d;
                    toward = _hands[h].palm;
                }
            }
            TimeHeap target = null;
            float best = float.MaxValue;
            for (int i = 0; i < pull.Heaps.Length; i++)
            {
                TimeHeap heap = pull.Heaps[i];
                if (heap == null || !heap.Present) continue;
                float d = Vector3.Distance(heap.transform.position, toward);
                if (d < best)
                {
                    best = d;
                    target = heap;
                }
            }
            if (target != null) target.Invite(toward);
        }

        void OnCancelled(TimePieceView piece)
        {
            if (sound != null) sound.Cancel(piece.Center, IsWindChime(piece));
        }

        void OnPourStarted(RitualJar.Layer layer)
        {
            if (sound != null) sound.PourStart(_jar.MouthCenter);
            if (layer.category != null) _labelAccent = layer.category.glowColor;
            _labelGlow = 1f;
            _panelTimer = 0f;
            Debug.Log($"{Tag} ritual pour start {layer.category?.id} units={layer.units} grains={layer.cloud.Count}");
        }

        void OnPourFinished(RitualJar.Layer layer)
        {
            if (sound != null) sound.PourStop();
            _panelTimer = 0f;
            FeedJarLight();
            Debug.Log($"{Tag} ritual pour done {layer.category?.id} total={_jar.TotalUnits}u grains={_jar.TotalGrains} surface={_jar.SurfaceMean:0.000}");
        }

        void OnGrainsLanded(RitualJar.Layer layer, int count)
        {
            if (sound != null) sound.Landed(_jar.root.position + Vector3.up * _jar.SurfaceMean, count);
            _labelGlow = Mathf.Max(_labelGlow, 0.65f);
            _panelTimer = 0f;
            if (_jar.Pouring && sound != null && layer.landedCount >= layer.cloud.Count) sound.PourStop();
        }

        void FeedJarLight()
        {
            _glowColors.Clear();
            _glowUnits.Clear();
            IReadOnlyList<RitualJar.Layer> layers = _jar.Layers;
            Color sum = Color.clear;
            int total = 0;
            for (int i = 0; i < layers.Count; i++)
            {
                Color c = layers[i].category != null ? layers[i].category.glowColor : Color.white;
                _glowColors.Add(c);
                _glowUnits.Add(layers[i].units);
                sum += c * Mathf.Max(1, layers[i].units);
                total += Mathf.Max(1, layers[i].units);
            }
            _jarGlow = total > 0 ? sum / total : Color.clear;
            _jarGlow.a = 1f;
            _jarFill = Mathf.Clamp01(_jar.TotalUnits / Mathf.Max(1f, _config.hoursWhenFull * RitualFill.UnitsPerHour));
            if (_sealed || layers.Count == 0) return;
            int d = DominantIndex();
            Color dominant = d >= 0 && categories[d] != null ? categories[d].glowColor : _jarGlow;
            float gain = Mathf.Lerp(_config.deskPoolOpenMin, _config.deskPoolOpenMax, _jarFill);
            _pool.Bloom(_jar.root.position, _jar.root.rotation, _tableY, _glowColors, _glowUnits, dominant, 0f, gain);
        }

        void StepLight()
        {
            if (_lightField == null) return;
            _lightField.BeginFrame();
            if (!_sealed && pull != null && pull.Heaps != null)
            {
                for (int i = 0; i < pull.Heaps.Length; i++)
                {
                    TimeHeap heap = pull.Heaps[i];
                    if (heap == null || !heap.Present || heap.Category == null) continue;
                    _lightField.AddSource(heap.transform.position, Vector3.up, 0f, heap.Radius, heap.Category.glowColor, 1f - heap.Melt);
                }
            }
            for (int i = 0; i < _pieces.Count; i++)
            {
                TimePieceView piece = _pieces[i];
                if (piece.category == null || piece.Transform == null) continue;
                if (piece.state != PieceState.Floating && piece.state != PieceState.Held && piece.state != PieceState.Pulling) continue;
                if (piece.shape == PieceShape.Blob)
                {
                    _lightField.AddSource(piece.Center, Vector3.up, 0f, piece.BlobRadiusNow, piece.category.glowColor, 1f);
                    continue;
                }
                piece.Segment(out Vector3 a, out Vector3 b);
                Vector3 ab = b - a;
                float len = ab.magnitude;
                _lightField.AddSource((a + b) * 0.5f, len > 1e-5f ? ab / len : Vector3.up, len, piece.Radius, piece.category.glowColor, 1f);
            }
            if (_jar.TotalUnits > 0 && _jarGlow.a > 0f)
            {
                float surface = Mathf.Max(_config.jar.floor + 0.01f, _jar.SurfaceMean);
                Vector3 center = _jar.root.TransformPoint(new Vector3(0f, (_config.jar.floor + surface) * 0.5f, 0f));
                float weight = _config.haloJar * Mathf.Lerp(0.5f, 1f, _jarFill);
                _lightField.AddSource(center, _jar.root.up, Mathf.Max(0f, surface - _config.jar.floor), _config.jar.bodyRadius, _jarGlow, weight, !_sealed);
            }
            Vector3 eye = (head != null && head.position.sqrMagnitude > 1e-4f) ? head.position : transform.position + new Vector3(0f, 0.14f, -0.42f);
            _lightField.EndFrame(eye, _hands);
        }

        void Tally()
        {
            for (int i = 0; i < _pieceUnits.Length; i++)
            {
                _pieceUnits[i] = 0;
                _jarUnits[i] = 0;
            }
            for (int i = 0; i < _pieces.Count; i++)
            {
                TimePieceView piece = _pieces[i];
                if (piece.categoryIndex >= 0 && (piece.state == PieceState.Floating || piece.state == PieceState.Held || piece.state == PieceState.Pulling))
                    _pieceUnits[piece.categoryIndex] += piece.Units;
            }
            IReadOnlyList<RitualJar.Layer> layers = _jar.Layers;
            for (int i = 0; i < layers.Count; i++)
                if (layers[i].categoryIndex >= 0) _jarUnits[layers[i].categoryIndex] += layers[i].units;
        }

        public int PieceUnits(int category) => _pieceUnits[category];
        public int JarUnits(int category) => _jarUnits[category];

        void StepPanel(float dt)
        {
            if (_labelTextMesh == null || _labelTextRenderer == null || _labelRoot == null || labelFont == null) return;
            bool show = _config.showTotals && _jar != null;
            bool wasActive = _labelRoot.gameObject.activeSelf;
            if (wasActive != show)
            {
                _labelRoot.gameObject.SetActive(show);
                if (show) _panelTimer = 0f;
            }
            if (!show) return;

            _labelAppear = Mathf.MoveTowards(_labelAppear, 1f, dt * 1.8f);
            _labelAttach = Mathf.MoveTowards(_labelAttach, _sealed ? 1f : 0f, dt / 1.1f);
            _labelGlow = Mathf.MoveTowards(_labelGlow, 0f, dt * 1.4f);

            Vector3 eyePos = (head != null && head.position.sqrMagnitude > 1e-4f) ? head.position : new Vector3(0f, 1.17f, 0.02f);
            Vector3 toJar = _jar.root.position - eyePos;
            toJar.y = 0f;
            Vector3 right = toJar.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, toJar.normalized) : _jar.root.right;
            Vector3 floatPos = _jar.root.position + right * 0.138f + Vector3.up * 0.115f;
            Vector3 floatView = floatPos - eyePos;
            float horiz = Mathf.Sqrt(floatView.x * floatView.x + floatView.z * floatView.z);
            floatView.y = Mathf.Clamp(floatView.y, -horiz * 0.32f, horiz * 0.32f);
            Quaternion floatRot = floatView.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(floatView.normalized, Vector3.up) : _jar.root.rotation;

            Vector3 attachPos = _jar.root.TransformPoint(new Vector3(0f, 0.086f, -(_config.jar.bodyRadius + 0.003f)));
            Quaternion attachRot = Quaternion.LookRotation(_jar.root.forward, _jar.root.up);

            float s = RitualUtil.Smooth(_labelAttach);
            float hoverBob = (1f - s) * 0.002f * Mathf.Sin(Time.time * 1.3f + 1.7f);
            Vector3 arcLift = Vector3.up * (hoverBob + 0.035f * Mathf.Sin(s * Mathf.PI)) - _jar.root.forward * (0.04f * Mathf.Sin(s * Mathf.PI));
            float scale = Mathf.Lerp(1f, LabelAttachScale, s);
            Vector3 numbersPos = Vector3.Lerp(floatPos, attachPos, s) + arcLift;
            Quaternion numbersRot = Quaternion.Slerp(floatRot, attachRot, s);

            _labelTag = Mathf.MoveTowards(_labelTag, _config.timeLabels ? 0f : 1f, dt / Mathf.Max(0.05f, _config.labelSwitchSeconds));
            float tag = RitualUtil.Smooth(_labelTag);
            Vector3 tagPos = _jar.root.TransformPoint(new Vector3(_config.labelTagSide, _config.labelTagHeight, -(_config.jar.bodyRadius + 0.006f)));
            Quaternion tagRot = attachRot * Quaternion.AngleAxis(_config.labelTagTilt, Vector3.forward);
            _labelRoot.SetPositionAndRotation(Vector3.Lerp(numbersPos, tagPos, tag), Quaternion.Slerp(numbersRot, tagRot, tag));
            _labelRoot.localScale = Vector3.one * Mathf.Lerp(scale, _config.labelTagScale, tag);
            s *= 1f - tag;
            if (_labelBorder != null && _labelBorder.enabled != tag < 0.5f) _labelBorder.enabled = tag < 0.5f;
            if (_labelBorderTag != null && _labelBorderTag.enabled != tag >= 0.5f) _labelBorderTag.enabled = tag >= 0.5f;

            if (_labelCard != null)
            {
                Color cardCol = new Color(0.16f, 0.13f, 0.11f, 1f);
                Color cardDeep = new Color(0.07f, 0.05f, 0.04f, 1f);
                Color cardLight = Color.Lerp(new Color(0.85f, 0.74f, 0.55f, 1f), _labelAccent, 0.35f);
                PushLabelMaterial(_labelCard, _labelCardBlock, cardCol, cardDeep, cardLight, 0.08f + _labelGlow * 0.25f, 0.14f, 0.001f, s, _labelAppear, 0f);
            }
            {
                Color borderCol = Color.Lerp(new Color(0.94f, 0.78f, 0.46f, 1f), _labelAccent, 0.45f);
                Color borderDeep = new Color(0.42f, 0.30f, 0.14f, 1f);
                Color borderLight = new Color(1f, 0.96f, 0.86f, 1f);
                if (_labelBorder != null) PushLabelMaterial(_labelBorder, _labelBorderBlock, borderCol, borderDeep, borderLight, 0.38f + _labelGlow * 0.85f, 0.55f, 0.001f, s, _labelAppear, 0f);
                if (_labelBorderTag != null) PushLabelMaterial(_labelBorderTag, _labelBorderBlock, borderCol, borderDeep, borderLight, 0.38f + _labelGlow * 0.85f, 0.55f, 0.001f, s, _labelAppear, 0f);
            }

            _panelTimer -= dt;
            if (_panelTimer <= 0f || _labelTextDirty)
            {
                _panelTimer = 0.25f;
                Tally();
                _text.Length = 0;
                _text.Append(_config.timeLabels ? 'n' : 't').Append(_sealed ? 's' : 'o').Append(_jar.TotalUnits);
                for (int i = 0; i < _jarUnits.Length; i++) _text.Append('|').Append(_jarUnits[i]);
                string stateKey = _text.ToString();
                if (_labelTextDirty || _labelCachedState != stateKey)
                {
                    _labelCachedState = stateKey;
                    RebuildLabelTextGlyphs();
                    _labelTextDirty = false;
                    _labelTextBuffersDirty = true;
                    _labelDeformedPress = -1f;
                }
            }

            if (_labelTextBuffersDirty || Mathf.Abs(s - _labelDeformedPress) > 1e-4f || Mathf.Abs(_labelAppear - _labelDeformedAppear) > 1e-4f)
            {
                DeformLabelTextVertices(s, _labelAppear);
                _labelDeformedPress = s;
                _labelDeformedAppear = _labelAppear;
                _labelTextBuffersDirty = false;
            }
        }

        static string TagDate()
        {
            return System.DateTime.Now.ToString("MMMM d\nyyyy", System.Globalization.CultureInfo.InvariantCulture);
        }

        void RebuildTagGlyphs()
        {
            int count;
            if (_sealed)
            {
                string[] parts = TagDate().Split('\n');
                count = Mathf.Min(parts.Length, _labelBodyLines.Length);
                for (int i = 0; i < count; i++) _labelBodyLines[i] = parts[i];
            }
            else
            {
                _labelBodyLines[0] = "Pour your day";
                _labelBodyLines[1] = "into the vessel";
                count = 2;
            }
            for (int i = 0; i < count; i++)
                labelFont.RequestCharactersInTexture(_labelBodyLines[i], LabelFontPixelSize, FontStyle.Normal);
            _labelGlyphVerts.Clear();
            _labelTextUv.Clear();
            _labelTextCol.Clear();
            _labelTextShadowIdx.Clear();
            _labelTextFgIdx.Clear();
            _labelTextIdx.Clear();
            AppendTextBlockGlyphs(_labelBodyLines, count, 0.00205f, 0.09f, 0.36f, 1.22f,
                0f, false, true, LabelMaxWidth, LabelMaxBodyHeight,
                new Color32(252, 245, 224, 255), new Color32(10, 8, 5, 195));
            _labelShownText = string.Join(" ", _labelBodyLines, 0, count);
            for (int i = 0; i < _labelGlyphVerts.Count; i++)
            {
                LabelGlyphVert v = _labelGlyphVerts[i];
                _labelTextUv.Add(v.uv);
                _labelTextCol.Add(v.color);
            }
            for (int i = 0; i < _labelTextShadowIdx.Count; i++) _labelTextIdx.Add(_labelTextShadowIdx[i]);
            for (int i = 0; i < _labelTextFgIdx.Count; i++) _labelTextIdx.Add(_labelTextFgIdx[i]);
        }

        void RebuildLabelTextGlyphs()
        {
            if (!_config.timeLabels)
            {
                RebuildTagGlyphs();
                return;
            }
            const string titleLine = "TODAY'S TIME";
            string totalLine = TimeUnits.LabelEn(_jar.TotalUnits);
            int bodyCount = 0;
            bool empty = _jar.TotalUnits <= 0;
            if (empty)
            {
                _labelBodyLines[0] = "Pour your day";
                _labelBodyLines[1] = "into the vessel";
                bodyCount = 2;
            }
            else
            {
                for (int i = 0; i < _jarUnits.Length && bodyCount < _labelBodyLines.Length; i++)
                {
                    if (_jarUnits[i] <= 0) continue;
                    if (categories == null || i >= categories.Length || categories[i] == null) continue;
                    _labelBodyLines[bodyCount++] = categories[i].EffectiveTitleEn + "  " + TimeUnits.ShortEn(_jarUnits[i]);
                }
            }

            labelFont.RequestCharactersInTexture(titleLine, LabelFontPixelSize, FontStyle.Normal);
            labelFont.RequestCharactersInTexture(totalLine, LabelFontPixelSize, FontStyle.Normal);
            for (int i = 0; i < bodyCount; i++)
                labelFont.RequestCharactersInTexture(_labelBodyLines[i], LabelFontPixelSize, FontStyle.Normal);

            _labelGlyphVerts.Clear();
            _labelTextUv.Clear();
            _labelTextCol.Clear();
            _labelTextShadowIdx.Clear();
            _labelTextFgIdx.Clear();
            _labelTextIdx.Clear();

            var titleLines = _labelBodyLines;
            string saved0 = titleLines[0];
            titleLines[0] = titleLine;
            AppendTextBlockGlyphs(titleLines, 1, 0.00072f, 0.16f, 0.48f, 1.0f,
                LabelDividerY + 0.0220f, true, false, LabelMaxWidth, LabelMaxTitleHeight,
                new Color32(242, 227, 194, 255), new Color32(10, 8, 5, 195));

            titleLines[0] = totalLine;
            AppendTextBlockGlyphs(titleLines, 1, 0.00148f, 0.11f, 0.36f, 1.0f,
                LabelDividerY + 0.0042f, true, false, LabelMaxWidth, LabelMaxHeaderHeight,
                new Color32(252, 245, 224, 255), new Color32(10, 8, 5, 195));
            titleLines[0] = saved0;

            if (empty)
            {
                AppendTextBlockGlyphs(_labelBodyLines, bodyCount, 0.00182f, 0.09f, 0.36f, 1.22f,
                    -0.019f, false, true, LabelMaxWidth, LabelMaxBodyHeight,
                    new Color32(252, 245, 224, 255), new Color32(10, 8, 5, 195));
            }
            else if (bodyCount > 0)
            {
                float bodyCharSize = bodyCount == 1 ? 0.00205f : bodyCount == 2 ? 0.00190f : bodyCount == 3 ? 0.00175f : bodyCount == 4 ? 0.00162f : 0.00150f;
                AppendTextBlockGlyphs(_labelBodyLines, bodyCount, bodyCharSize, 0.085f, 0.32f, 1.15f,
                    LabelDividerY - 0.0065f, false, false, LabelMaxWidth, LabelMaxBodyHeight,
                    new Color32(252, 245, 224, 255), new Color32(10, 8, 5, 195));
            }

            for (int i = 0; i < _labelGlyphVerts.Count; i++)
            {
                LabelGlyphVert v = _labelGlyphVerts[i];
                _labelTextUv.Add(v.uv);
                _labelTextCol.Add(v.color);
            }
            for (int i = 0; i < _labelTextShadowIdx.Count; i++) _labelTextIdx.Add(_labelTextShadowIdx[i]);
            for (int i = 0; i < _labelTextFgIdx.Count; i++) _labelTextIdx.Add(_labelTextFgIdx[i]);
            _labelShownText = titleLine + " " + totalLine;
            for (int i = 0; i < bodyCount; i++) _labelShownText += " " + _labelBodyLines[i];
        }

        void AppendTextBlockGlyphs(string[] lines, int lineCount, float charSize, float trackingEm, float wordSpaceEm, float lineSpacing,
            float anchorY, bool lowerAnchor, bool middleAnchor, float maxWidth, float maxHeight, Color32 fgColor, Color32 shadowColor)
        {
            if (lineCount <= 0) return;
            float trackingPx = trackingEm * LabelFontPixelSize;
            float spacePx = wordSpaceEm * LabelFontPixelSize;
            float maxLinePx = 0f;
            float[] widths = new float[lineCount];
            for (int i = 0; i < lineCount; i++)
            {
                widths[i] = MeasureTrackedLinePx(labelFont, lines[i], trackingPx, spacePx);
                if (widths[i] > maxLinePx) maxLinePx = widths[i];
            }
            float capHeightPx = LabelFontPixelSize * 0.72f;
            float lineStepPx = LabelFontPixelSize * lineSpacing;
            float totalHeightPx = (lineCount - 1) * lineStepPx + capHeightPx;

            float scale = charSize * 0.1f;
            if (maxLinePx > 1e-3f) scale = Mathf.Min(scale, maxWidth / maxLinePx);
            if (totalHeightPx > 1e-3f) scale = Mathf.Min(scale, maxHeight / totalHeightPx);

            float topBaselineY;
            if (lowerAnchor) topBaselineY = anchorY + (lineCount - 1) * lineStepPx * scale;
            else if (middleAnchor) topBaselineY = anchorY + (totalHeightPx * 0.5f - capHeightPx) * scale;
            else topBaselineY = anchorY - capHeightPx * scale;

            for (int i = 0; i < lineCount; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;
                float penX = -widths[i] * 0.5f * scale;
                float baseY = topBaselineY - i * lineStepPx * scale;
                bool prevGlyph = false;
                for (int c = 0; c < line.Length; c++)
                {
                    char ch = line[c];
                    if (ch == ' ')
                    {
                        penX += spacePx * scale;
                        prevGlyph = false;
                        continue;
                    }
                    if (prevGlyph) penX += trackingPx * scale;
                    if (labelFont.GetCharacterInfo(ch, out CharacterInfo ci, LabelFontPixelSize, FontStyle.Normal))
                    {
                        if (ci.maxX > ci.minX && ci.maxY > ci.minY)
                        {
                            float x0 = penX + ci.minX * scale;
                            float x1 = penX + ci.maxX * scale;
                            float xm = (x0 + x1) * 0.5f;
                            float y0 = baseY + ci.minY * scale;
                            float y1 = baseY + ci.maxY * scale;
                            Vector2 uvBL = ci.uvBottomLeft;
                            Vector2 uvBR = ci.uvBottomRight;
                            Vector2 uvTL = ci.uvTopLeft;
                            Vector2 uvTR = ci.uvTopRight;
                            Vector2 uvBM = Vector2.Lerp(uvBL, uvBR, 0.5f);
                            Vector2 uvTM = Vector2.Lerp(uvTL, uvTR, 0.5f);
                            AddGlyphQuadSubdivided(x0 + 0.00045f, xm + 0.00045f, x1 + 0.00045f, y0 - 0.00045f, y1 - 0.00045f, -0.0014f,
                                uvBL, uvBM, uvBR, uvTL, uvTM, uvTR, shadowColor, _labelTextShadowIdx);
                            AddGlyphQuadSubdivided(x0, xm, x1, y0, y1, -0.0018f,
                                uvBL, uvBM, uvBR, uvTL, uvTM, uvTR, fgColor, _labelTextFgIdx);
                        }
                        penX += ci.advance * scale;
                    }
                    prevGlyph = true;
                }
            }
        }

        static float MeasureTrackedLinePx(Font font, string line, float trackingPx, float spacePx)
        {
            if (string.IsNullOrEmpty(line)) return 0f;
            float w = 0f;
            bool prevGlyph = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == ' ')
                {
                    w += spacePx;
                    prevGlyph = false;
                    continue;
                }
                if (prevGlyph) w += trackingPx;
                if (font.GetCharacterInfo(ch, out CharacterInfo ci, LabelFontPixelSize, FontStyle.Normal))
                    w += ci.advance;
                prevGlyph = true;
            }
            return w;
        }

        void AddGlyphQuadSubdivided(float x0, float xm, float x1, float y0, float y1, float z,
            Vector2 uvBL, Vector2 uvBM, Vector2 uvBR, Vector2 uvTL, Vector2 uvTM, Vector2 uvTR,
            Color32 color, List<int> idxList)
        {
            int v = _labelGlyphVerts.Count;
            _labelGlyphVerts.Add(new LabelGlyphVert { x = x0, y = y0, z = z, uv = uvBL, color = color });
            _labelGlyphVerts.Add(new LabelGlyphVert { x = x0, y = y1, z = z, uv = uvTL, color = color });
            _labelGlyphVerts.Add(new LabelGlyphVert { x = xm, y = y0, z = z, uv = uvBM, color = color });
            _labelGlyphVerts.Add(new LabelGlyphVert { x = xm, y = y1, z = z, uv = uvTM, color = color });
            _labelGlyphVerts.Add(new LabelGlyphVert { x = x1, y = y0, z = z, uv = uvBR, color = color });
            _labelGlyphVerts.Add(new LabelGlyphVert { x = x1, y = y1, z = z, uv = uvTR, color = color });
            idxList.Add(v);
            idxList.Add(v + 1);
            idxList.Add(v + 3);
            idxList.Add(v);
            idxList.Add(v + 3);
            idxList.Add(v + 2);
            idxList.Add(v + 2);
            idxList.Add(v + 3);
            idxList.Add(v + 5);
            idxList.Add(v + 2);
            idxList.Add(v + 5);
            idxList.Add(v + 4);
        }

        void DeformLabelTextVertices(float press, float appear)
        {
            const float cardHalfH = 0.068f;
            float cylR = (_config.jar.bodyRadius + 0.003f) / LabelAttachScale;
            float safeR = Mathf.Max(0.02f, cylR);
            float a = Mathf.Clamp01(appear);
            float grow = a * a * (3f - 2f * a);

            _labelTextPos.Clear();
            for (int i = 0; i < _labelGlyphVerts.Count; i++)
            {
                LabelGlyphVert v = _labelGlyphVerts[i];
                float theta = v.x / safeR;
                Vector3 flat = new Vector3(v.x, v.y, v.z);
                Vector3 curved = new Vector3(cylR * Mathf.Sin(theta), v.y, v.z + cylR * (1f - Mathf.Cos(theta)));
                float delay = Mathf.Clamp01((cardHalfH - v.y) / (2f * cardHalfH));
                float p = Mathf.Clamp01(press * 1.6f - delay * 0.6f);
                p = p * p * (3f - 2f * p);
                Vector3 c = Vector3.Lerp(flat, curved, p);
                c = Vector3.Lerp(c * 0.15f, c, grow);
                _labelTextPos.Add(c);
            }

            if (_labelTextBuffersDirty)
            {
                _labelTextMesh.Clear(false);
                _labelTextMesh.SetVertices(_labelTextPos);
                _labelTextMesh.SetUVs(0, _labelTextUv);
                _labelTextMesh.SetColors(_labelTextCol);
                _labelTextMesh.SetTriangles(_labelTextIdx, 0, false);
                _labelTextMesh.bounds = new Bounds(Vector3.zero, Vector3.one * (cardHalfH * 3.2f));
            }
            else
            {
                _labelTextMesh.SetVertices(_labelTextPos);
            }
        }

        static void PushLabelMaterial(MeshRenderer r, MaterialPropertyBlock block, Color col, Color deep, Color light, float glow, float sheen, float radius, float press, float appear, float bob)
        {
            block.SetColor(LabelColorId, col);
            block.SetColor(LabelDeepId, deep);
            block.SetColor(LabelLightId, light);
            block.SetFloat(LabelGlowId, glow);
            block.SetFloat(LabelSheenId, sheen);
            block.SetFloat(LabelRadiusId, radius);
            block.SetFloat(LabelPressId, press);
            block.SetFloat(LabelStampId, 0f);
            block.SetFloat(LabelAppearId, appear);
            block.SetVector(LabelOriginId, Vector4.zero);
            block.SetFloat(LabelBobId, bob);
            block.SetFloat(LabelGrowId, 0f);
            r.SetPropertyBlock(block);
        }

        public void ReportWork(float gpuMs, float cpuMs)
        {
            _gpuMs = SmoothMs(_gpuMs, gpuMs);
            _cpuMs = SmoothMs(_cpuMs, cpuMs);
        }

        static float SmoothMs(float current, float sample)
        {
            if (float.IsNaN(sample)) return float.NaN;
            return float.IsNaN(current) ? sample : Mathf.Lerp(current, sample, 0.05f);
        }

        static string Ms(float v) => float.IsNaN(v) ? "-" : v.ToString("0.0");

        void Govern(float dt)
        {
            _frameMs = Mathf.Lerp(_frameMs, dt * 1000f, 0.05f);
            _governAge += dt;
            _logTimer += dt;
            if (_logTimer >= 10f)
            {
                _logTimer = 0f;
                Debug.Log($"{Tag} ritual perf frame={_frameMs:0.0}ms gpu={Ms(_gpuMs)}ms cpu={Ms(_cpuMs)}ms quality={_quality:0.00} pieces={_pieces.Count} grains={_jar.TotalGrains} sealed={_sealed}");
            }
            if (!_config.governor)
            {
                if (_quality < 1f)
                {
                    _quality = 1f;
                    _jar.SetQuality(1f);
                }
                return;
            }
            if (_governAge < _config.governorWarmup) return;
            GovernorVerdict verdict = RitualGovernor.Judge(_frameMs, RitualGovernor.Work(_gpuMs, _cpuMs), _config.frameBudgetMs);
            if (verdict == GovernorVerdict.Over)
            {
                _over += dt;
                _calm = 0f;
                if (_over > 1.5f && _quality > _config.minQuality)
                {
                    _quality = Mathf.Max(_config.minQuality, _quality - 0.1f);
                    _over = 0f;
                    _jar.SetQuality(_quality);
                    Debug.Log($"{Tag} ritual quality down {_quality:0.00} frame={_frameMs:0.0}ms gpu={Ms(_gpuMs)}ms cpu={Ms(_cpuMs)}ms");
                }
                return;
            }
            _over = Mathf.Max(0f, _over - dt);
            if (verdict == GovernorVerdict.Hold)
            {
                _calm = 0f;
                return;
            }
            _calm += dt;
            if (_calm > 20f && _quality < 1f)
            {
                _quality = Mathf.Min(1f, _quality + 0.05f);
                _calm = 0f;
                _jar.SetQuality(_quality);
                Debug.Log($"{Tag} ritual quality up {_quality:0.00} frame={_frameMs:0.0}ms gpu={Ms(_gpuMs)}ms");
            }
        }

        void PollLive(float dt)
        {
            _liveTimer -= dt;
            if (_liveTimer > 0f) return;
            _liveTimer = 0.5f;
            string path = Path.Combine(Application.persistentDataPath, LiveFile);
            if (!File.Exists(path)) return;
            long stamp = File.GetLastWriteTimeUtc(path).Ticks;
            if (stamp == _liveStamp) return;
            _liveStamp = stamp;
            string glowBefore = GlowSignature();
            try
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(path), _config);
                Debug.Log($"{Tag} ritual live config applied unit={_config.unitLength:0.000} shape={_config.mergedShape} nodesAlways={_config.nodesAlwaysVisible} glow={_config.glowStyle}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{Tag} ritual live config rejected: {e.Message}");
                return;
            }
            if (_sealed && _glowColors.Count > 0 && GlowSignature() != glowBefore) RebloomGlow();
            if (_config.resetCounter != _resetSeen)
            {
                _resetSeen = _config.resetCounter;
                ResetDay();
            }
        }

        string GlowSignature()
        {
            return $"{_config.glowStyle}|{_config.glowSeepSplats}|{_config.glowSeepGain}|{_config.glowSeepMotes}|{_config.glowPetals}|{_config.glowMotes}|{_config.glowRadius}|{_config.deskPool}|{_config.deskPoolGain}";
        }

        public void RebloomGlow()
        {
            if (!_sealed || _glowColors.Count == 0) return;
            _glow.Bloom(_jar.root.position, _jar.root.rotation, _glowColors, _glowUnits, _glowDominant);
            _pool.Bloom(_jar.root.position, _jar.root.rotation, _tableY, _glowColors, _glowUnits, _glowDominant, 0f);
            Debug.Log($"{Tag} ritual glow rebloom style={_config.glowStyle}");
        }

        void OnDestroy()
        {
            Font.textureRebuilt -= OnFontTextureRebuilt;
            for (int i = 0; i < _pieces.Count; i++) _pieces[i].Kill();
            _pieces.Clear();
            RitualUtil.Kill(_labelCardMesh);
            RitualUtil.Kill(_labelBorderMesh);
            RitualUtil.Kill(_labelTextMesh);
            _wax?.Dispose();
            _glow?.Dispose();
            _pool?.Dispose();
            _lightField?.Dispose();
            _sealWave?.Dispose();
            _clock?.Dispose();
            _shelf?.Dispose();
            _ending?.Dispose();
            RitualUtil.Kill(_labelBorderTagMesh);
            _guideTrails?.Dispose();
            _jar?.Dispose();
        }

        void StepMicrogesture(float now)
        {
            if (_micro == null) _micro = microgestureSource as IMicrogestureInput;
            StepLabelGesture(now);
            Microgesture g = _micro != null ? _micro.Current(Handedness.Right) : Microgesture.None;
            bool fresh = g != Microgesture.None && g != _lastRightMicro && now - _lastGuideToggleTime >= 0.35f;
            _lastRightMicro = g;
            if (!fresh || _config == null) return;
            _lastGuideToggleTime = now;
            _config.guideTrails = !_config.guideTrails;
            if (sound != null)
            {
                Vector3 anchor = _jar != null ? _jar.Spout : transform.position;
                sound.Unit(anchor, _config.guideTrails ? 6 : 2, _config.guideTrails, true);
            }
            if (_config.timeLabels) ShowRoomNote(_config.guideTrails ? "Light Guide · On" : "Light Guide · Off");
            Debug.Log($"{Tag} ritual guide trails enabled={_config.guideTrails} gesture={g}");
        }

        void StepLabelGesture(float now)
        {
            Microgesture g = _micro != null ? _micro.Current(Handedness.Left) : Microgesture.None;
            bool fresh = g != Microgesture.None && g != _lastLeftMicro && now - _lastLabelToggleTime >= 0.35f;
            _lastLeftMicro = g;
            if (!fresh || _config == null) return;
            _lastLabelToggleTime = now;
            _config.timeLabels = !_config.timeLabels;
            _labelTextDirty = true;
            if (!_config.timeLabels)
            {
                _roomTimer = 0f;
                if (_roomNote != null) _roomNote.gameObject.SetActive(false);
            }
            if (sound != null)
            {
                Vector3 anchor = _labelRoot != null ? _labelRoot.position : (_jar != null ? _jar.Spout : transform.position);
                sound.Unit(anchor, _config.timeLabels ? 6 : 3, _config.timeLabels, false);
            }
            Debug.Log($"{Tag} ritual time labels {(_config.timeLabels ? "on" : "off")} gesture={g}");
        }

        void StepGuideTrails(float dt)
        {
            if (_guideTrails == null) return;
            RitualGuideTrails.ArcSpec primary = default;
            RitualGuideTrails.ArcSpec secondary = default;
            if (_sealed || _config == null || !_config.guideTrails)
            {
                _guideTrails.SetStreams(false, in primary, in secondary, dt);
                return;
            }

            Vector3 spout = _jar != null ? _jar.Spout : transform.position + Vector3.up * 0.18f;
            Color warmGold = new Color(1f, 0.88f, 0.60f, 1f);

            TimePieceView held = null;
            for (int h = 1; h >= 0; h--)
            {
                if ((_grips[h].mode == GripHold || _grips[h].mode == GripSqueeze) && _grips[h].piece != null && _grips[h].piece.Alive)
                {
                    held = _grips[h].piece;
                    break;
                }
            }

            if (held != null)
            {
                Color hCol = CategoryGlow(held.category, warmGold);
                if (_jar != null && !_jar.Pouring && _jar.CanAccept(held.Units))
                {
                    float lift = 0.055f + 0.16f * Vector3.Distance(held.Center, spout);
                    primary = MakeArc(held.Center, spout, lift, hCol, 0.72f, 0.0085f, 0.42f);
                }
                TimePieceView mergePartner = NearestSameCategory(held, 0.36f, out float mergeDist);
                if (mergePartner != null)
                {
                    float nearK = Mathf.Clamp01(1.12f - mergeDist / 0.36f);
                    secondary = MakeArc(mergePartner.Center, held.Center, 0.025f, Color.Lerp(hCol, Color.white, 0.2f), 0.66f * nearK, 0.006f, 0.48f);
                }
                _guideTrails.SetStreams(true, in primary, in secondary, dt);
                return;
            }

            TimePieceView pullingPiece = null;
            for (int i = _pieces.Count - 1; i >= 0; i--)
            {
                if (_pieces[i].Alive && _pieces[i].state == PieceState.Pulling)
                {
                    pullingPiece = _pieces[i];
                    break;
                }
            }
            if (pullingPiece != null)
            {
                Color pCol = CategoryGlow(pullingPiece.category, warmGold);
                Vector3 tip = pullingPiece.Units > 0 && pullingPiece.Transform != null
                    ? pullingPiece.Transform.TransformPoint(new Vector3(0f, 0f, pullingPiece.Length))
                    : pullingPiece.Center;
                float lift = 0.055f + 0.16f * Vector3.Distance(tip, spout);
                primary = MakeArc(tip, spout, lift, pCol, pullingPiece.Units > 0 ? 0.62f : 0.38f, 0.008f, 0.38f);
                _guideTrails.SetStreams(true, in primary, in secondary, dt);
                return;
            }

            if (_wax != null && (_wax.DrawState == WaxDraw.Drawing || _wax.DrawState == WaxDraw.Falling))
            {
                Color wCol = Color.Lerp(_wax.Color, warmGold, 0.45f);
                primary = MakeArc(_wax.DropPosition, spout, 0.045f, wCol, 0.72f, 0.007f, 0.46f);
                _guideTrails.SetStreams(true, in primary, in secondary, dt);
                return;
            }

            if (_wax != null && _wax.Ready)
            {
                Color wCol = Color.Lerp(_wax.Color, warmGold, 0.48f);
                Vector3 pressTop = _wax.DropCenter + Vector3.up * 0.115f;
                Vector3 pressBot = _wax.DropCenter + Vector3.up * 0.008f;
                primary = MakeArc(pressTop, pressBot, 0f, wCol, 0.72f, 0.011f, 0.40f);
                _guideTrails.SetStreams(true, in primary, in secondary, dt);
                return;
            }

            TimePieceView floating = null;
            for (int i = _pieces.Count - 1; i >= 0; i--)
            {
                if (_pieces[i].Alive && _pieces[i].state == PieceState.Floating)
                {
                    floating = _pieces[i];
                    break;
                }
            }
            if (floating != null && _jar != null && !_jar.Pouring)
            {
                Color fCol = CategoryGlow(floating.category, warmGold);
                if (_jar.CanAccept(floating.Units))
                {
                    float lift = 0.055f + 0.15f * Vector3.Distance(floating.Center, spout);
                    primary = MakeArc(floating.Center, spout, lift, fCol, 0.64f, 0.008f, 0.38f);
                }
                TimePieceView mergePartner = NearestSameCategory(floating, 0.34f, out float _);
                if (mergePartner != null)
                {
                    secondary = MakeArc(mergePartner.Center, floating.Center, 0.025f, Color.Lerp(fCol, Color.white, 0.2f), 0.58f, 0.006f, 0.45f);
                }
                else if (_wax != null && _wax.StickShown && !_wax.Dripped && _jar.TotalUnits > 0)
                {
                    Color wCol = Color.Lerp(_wax.Color, warmGold, 0.48f);
                    secondary = MakeArc(_wax.stickRoot.position, spout, 0.055f, wCol, 0.46f, 0.007f, 0.34f);
                }
                _guideTrails.SetStreams(true, in primary, in secondary, dt);
                return;
            }

            if (_wax != null && _wax.StickShown && !_wax.Dripped && _jar != null && _jar.TotalUnits > 0)
            {
                Color wCol = Color.Lerp(_wax.Color, warmGold, 0.48f);
                primary = MakeArc(_wax.stickRoot.position, spout, 0.06f, wCol, 0.66f, 0.008f, 0.38f);
                _guideTrails.SetStreams(true, in primary, in secondary, dt);
                return;
            }

            if (_hoveredHeap != null)
            {
                Vector3 heapTop = _hoveredHeap.transform.position + Vector3.up * _hoveredHeap.Radius;
                primary = MakeArc(heapTop, heapTop + Vector3.up * 0.125f, 0f, CategoryGlow(_hoveredHeap.Category, warmGold), 0.62f, 0.011f, 0.36f);
                _guideTrails.SetStreams(true, in primary, in secondary, dt);
                return;
            }

            if (_jar != null && _jar.TotalUnits == 0 && _pieces.Count == 0 && pull != null && pull.Heaps != null && pull.Heaps.Length > 0)
            {
                int idx0 = Mathf.Clamp(2, 0, pull.Heaps.Length - 1);
                TimeHeap h0 = pull.Heaps[idx0];
                if (h0 != null && h0.Present)
                {
                    Vector3 b0 = h0.transform.position + Vector3.up * h0.Radius;
                    primary = MakeArc(b0, b0 + Vector3.up * 0.105f, 0f, CategoryGlow(h0.Category, warmGold), 0.42f, 0.010f, 0.28f);
                }
                if (pull.Heaps.Length > 3)
                {
                    TimeHeap h1 = pull.Heaps[3];
                    if (h1 != null && h1 != h0 && h1.Present)
                    {
                        Vector3 b1 = h1.transform.position + Vector3.up * h1.Radius;
                        secondary = MakeArc(b1, b1 + Vector3.up * 0.105f, 0f, CategoryGlow(h1.Category, warmGold), 0.42f, 0.010f, 0.28f);
                    }
                }
            }

            _guideTrails.SetStreams(true, in primary, in secondary, dt);
        }

        TimePieceView NearestSameCategory(TimePieceView reference, float maxDist, out float bestDist)
        {
            TimePieceView best = null;
            bestDist = maxDist;
            for (int i = 0; i < _pieces.Count; i++)
            {
                TimePieceView other = _pieces[i];
                if (other == reference || !other.Alive) continue;
                if (other.state != PieceState.Floating && other.state != PieceState.Held) continue;
                if (other.categoryIndex != reference.categoryIndex) continue;
                float d = reference.DistanceTo(other);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = other;
                }
            }
            return best;
        }

        static Color CategoryGlow(TimeCategoryAsset category, Color fallback)
        {
            return category != null ? Color.Lerp(category.glowColor, Color.white, 0.24f) : fallback;
        }

        void StepJarPose(float dt)
        {
            if (_jar == null) return;
            bool hold0 = _grips[0].mode == GripJar && _hands[0].tracked && _hands[0].pinching;
            bool hold1 = _grips[1].mode == GripJar && _hands[1].tracked && _hands[1].pinching;
            bool heldNow = hold0 || hold1;
            _shelfNear = 0f;
            if (_sealed && !_shelved && _shelveAge < 0f && _shelf != null)
            {
                float d = _shelf.CaptureDistance(_jar.root.position);
                if (heldNow)
                {
                    _shelfNear = Mathf.Clamp01(1f - (d - _config.shelfSnapDistance) / Mathf.Max(0.01f, _config.shelfReleaseDistance * 1.5f));
                    if (d < _config.shelfSnapDistance)
                    {
                        Debug.Log($"{Tag} ritual jar drawn to shelf d={d:0.000}");
                        Shelve();
                    }
                }
                else if (_jarWasHeld)
                {
                    Debug.Log($"{Tag} ritual jar release shelfD={d:0.000} at={_jar.root.position}");
                    if (d < _config.shelfReleaseDistance) Shelve();
                }
            }
            _jarWasHeld = heldNow && !_shelved;
            bool pinned = _shelveAge >= 0f || _shelved;
            if (pinned)
            {
                _twoHandJarActive = false;
            }
            else if (hold0 && hold1 && _sealed)
            {
                Vector3 mid = (_hands[0].pinch + _hands[1].pinch) * 0.5f;
                if (TryTwoHandFrame(in _hands[0], in _hands[1], _jar.root.up, out Quaternion frameRot))
                {
                    if (!_twoHandJarActive)
                    {
                        _twoHandJarActive = true;
                        _jarTwoHandLocalRot = Quaternion.Inverse(frameRot) * _jar.root.rotation;
                        _jarTwoHandLocalPos = Quaternion.Inverse(_jar.root.rotation) * (_jar.root.position - mid);
                    }
                    else
                    {
                        Quaternion wantedRot = frameRot * _jarTwoHandLocalRot;
                        Vector3 wantedPos = mid + wantedRot * _jarTwoHandLocalPos;
                        float follow = 1f - Mathf.Exp(-dt * 24f);
                        _jar.root.SetPositionAndRotation(
                            Vector3.Lerp(_jar.root.position, wantedPos, follow),
                            Quaternion.Slerp(_jar.root.rotation, wantedRot, follow));
                    }
                }
                _jar.root.localScale = Vector3.one;
            }
            else if (hold0 || hold1)
            {
                int h = hold0 ? 0 : 1;
                if (_twoHandJarActive)
                {
                    _twoHandJarActive = false;
                    _grips[h].jarOffset = _jar.root.position - _hands[h].pinch;
                }
                Vector3 targetPos = _hands[h].pinch + _grips[h].jarOffset;
                Quaternion nextRot = _jar.root.rotation;
                if (Vector3.Dot(_jar.root.up, Vector3.up) < 0.9999f)
                    nextRot = Quaternion.Slerp(_jar.root.rotation, UprightYaw(_jar.root, transform.forward), 1f - Mathf.Exp(-dt * 5f));
                _jar.root.SetPositionAndRotation(targetPos, nextRot);
                _jar.root.localScale = Vector3.one;
            }
            else
            {
                _twoHandJarActive = false;
                if (Vector3.Dot(_jar.root.up, Vector3.up) < 0.9999f)
                {
                    Vector3 center = _jar.BodyCenter;
                    Quaternion upright = UprightYaw(_jar.root, transform.forward);
                    Quaternion nextRot = Quaternion.Slerp(_jar.root.rotation, upright, 1f - Mathf.Exp(-dt * 4.5f));
                    Vector3 nextPos = center - nextRot * new Vector3(0f, _config.jar.height * 0.45f, 0f);
                    _jar.root.SetPositionAndRotation(nextPos, nextRot);
                }
                _jar.root.localScale = Vector3.one;
                _deskY = _jar.root.position.y;
            }

            float angSpeed = Quaternion.Angle(_lastJarRot, _jar.root.rotation) * Mathf.Deg2Rad / Mathf.Max(dt, 1e-4f);
            _lastJarRot = _jar.root.rotation;
            if (_sealed && _glow != null) _glow.TrackVessel(_jar.root.position, _jar.root.rotation, angSpeed, dt);
        }

        static bool TryTwoHandFrame(in RitualHand left, in RitualHand right, Vector3 fallbackUp, out Quaternion rot)
        {
            Vector3 span = right.pinch - left.pinch;
            if (span.sqrMagnitude < 1e-6f)
            {
                rot = Quaternion.identity;
                return false;
            }
            Vector3 xAxis = span.normalized;
            Vector3 handUp = left.rotation * Vector3.up + right.rotation * Vector3.up;
            if (handUp.sqrMagnitude < 1e-6f) handUp = fallbackUp;
            Vector3 orthoUp = handUp - xAxis * Vector3.Dot(handUp, xAxis);
            if (orthoUp.sqrMagnitude < 1e-6f)
            {
                orthoUp = fallbackUp - xAxis * Vector3.Dot(fallbackUp, xAxis);
                if (orthoUp.sqrMagnitude < 1e-6f)
                    orthoUp = Vector3.Cross(xAxis, Mathf.Abs(xAxis.y) < 0.9f ? Vector3.up : Vector3.right);
            }
            orthoUp.Normalize();
            Vector3 zAxis = Vector3.Cross(xAxis, orthoUp).normalized;
            rot = Quaternion.LookRotation(zAxis, orthoUp);
            return true;
        }

        static Quaternion UprightYaw(Transform jarRoot, Vector3 fallbackForward)
        {
            Vector3 up = jarRoot.up;
            Quaternion q = jarRoot.rotation;
            Quaternion swing = Vector3.Dot(up, Vector3.up) < -0.999f
                ? Quaternion.AngleAxis(180f, jarRoot.right)
                : Quaternion.FromToRotation(up, Vector3.up);
            Quaternion rectified = swing * q;
            Vector3 fwd = Vector3.ProjectOnPlane(rectified * Vector3.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(fallbackForward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            return Quaternion.LookRotation(fwd.normalized, Vector3.up);
        }

        static RitualGuideTrails.ArcSpec MakeArc(Vector3 start, Vector3 end, float arcHeight, Color color, float alpha, float radius, float speed)
        {
            Vector3 mid = (start + end) * 0.5f + Vector3.up * arcHeight;
            return new RitualGuideTrails.ArcSpec
            {
                active = true,
                start = start,
                mid = mid,
                end = end,
                color = color,
                alpha = alpha,
                radius = radius,
                speed = speed
            };
        }
    }
}
