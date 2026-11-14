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

        const int GripNone = 0, GripPull = 1, GripHold = 2, GripJar = 3, GripSqueeze = 4, GripWax = 5;
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
        [SerializeField] Transform trackingSpace;
        [SerializeField] Transform head;
        [SerializeField] TimePullController pull;
        [SerializeField] RitualAudio sound;
        [SerializeField] TimeCategoryAsset[] categories;
        [SerializeField] Material splatMaterial;
        [SerializeField] Material glassMaterial;
        [SerializeField] Material waxMaterial;
        [SerializeField] Material glowMaterial;
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
        RitualJar _jar;
        RitualWax _wax;
        RitualDeskGlow _glow;
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

        public TimeRitualConfig Config => _config;
        public IReadOnlyList<TimePieceView> Pieces => _pieces;
        public RitualJar Jar => _jar;
        public RitualWax Wax => _wax;
        public RitualDeskGlow Glow => _glow;
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
        public float HoldProgress => _press.progress;
        public bool Ready => _ready;

        public void Init()
        {
            if (_ready) return;
            _ready = true;
            _config = config != null ? Instantiate(config) : ScriptableObject.CreateInstance<TimeRitualConfig>();
            _config.hideFlags = HideFlags.DontSave;
            _resetSeen = _config.resetCounter;
            _input = handInputSource as IHandInput;
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
            if (sound != null) sound.Init(_config.volume);
            Font.textureRebuilt += OnFontTextureRebuilt;
            if (labelFont != null)
            {
                MakePanel(labelFont);
                _roomNote = MakeRoomNote(labelFont);
                _heapNote = MakeHeapNote(labelFont);
            }
            _dim = _config.baseDim;
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
            return new TimePieceView(piecesRoot, heap.Category, IndexOf(heap.Category), LookOf(heap.Category), splatMaterial, _config, tuning, labelFont, head, glowMaterial);
        }

        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            _placed = true;
            PlaceJar();
        }

        void PlaceJar()
        {
            if (_jar == null) return;
            Vector3 p = transform.TransformPoint(_config.jarOffset);
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            Quaternion yaw = fwd.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(fwd.normalized, Vector3.up) : Quaternion.identity;
            _jar.root.SetPositionAndRotation(p, yaw);
            _deskY = p.y;
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
            Tick(Time.time, Time.deltaTime, frame);
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
            for (int h = 0; h < 2; h++) StepHand(h, now, dt);
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
            StepRoomNote(dt);
            StepHeapNote(dt);
            _jar.Tick(now, dt);
            _wax.Tick(dt);
            _glow.Tick(dt);
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
                    if (g.piece != null && g.piece.Alive) g.piece.Drop();
                    g = default;
                }
                else HoldPiece(h, ref g, hand, now, dt);
                return;
            }
            if (g.mode == GripJar)
            {
                if (!hand.tracked || !hand.pinching)
                {
                    g = default;
                    _deskY = _jar.root.position.y;
                    if (_sealed) _glow.root.position = _jar.root.position + Vector3.up * 0.0015f;
                }
                else _jar.root.position = hand.pinch + g.jarOffset;
                return;
            }
            if (g.mode == GripSqueeze)
            {
                StepSqueeze(h, ref g, hand, now, dt);
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
            if (!_sealed && pull != null)
            {
                TimeHeap near = pull.HeapAt(hand.pinch, _config.heapHoverRange * 1.35f, out float d);
                if (near != null)
                {
                    if (d < _config.heapHoverRange) near.Hover(1f - Mathf.Clamp01(d / Mathf.Max(1e-4f, _config.heapHoverRange)));
                    if (near.Present) _hoveredHeap = near;
                }
            }
            if (!hand.pinchStarted) return;
            if (!_sealed)
            {
                float heapDistance = float.MaxValue;
                TimeHeap heap = pull != null ? pull.HeapAt(hand.pinch, _config.grabRadius, out heapDistance) : null;
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
                    _pieces.Add(pull.Begin(h, heap, hand.pinch, CreatePiece));
                    return;
                }
                if (piece != null)
                {
                    Grab(h, ref g, piece, hand);
                    return;
                }
            }
            if (_jar.InBody(hand.pinch))
            {
                g.mode = GripJar;
                g.jarOffset = _jar.root.position - hand.pinch;
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
            piece.Hold();
            Anchor(ref g, hand);
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
            ShowRoomNote(room > 0 ? "Room Left · " + TimeUnits.LabelEn(room) : "Vessel is Full");
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
            bool show = !_sealed && _hoveredHeap != null && _hoveredHeap.Category != null && !pulling;
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
            Quaternion rotation = Quaternion.Slerp(t.rotation, wanted, 1f - Mathf.Exp(-dt * 14f * _config.holdRotate));
            Vector3 target = hand.pinch - rotation * g.local;
            Vector3 position = Vector3.Lerp(t.position, target, 1f - Mathf.Exp(-dt * _config.holdFollow));

            bool magnetSpout = !_sealed && !_press.pressing && !_jar.Pouring && _jar.CanAccept(piece.Units);
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

            for (int i = 0; i < _pieces.Count; i++)
            {
                TimePieceView other = _pieces[i];
                if (other == piece || (other.state != PieceState.Floating && other.state != PieceState.Held)) continue;
                float d = piece.DistanceTo(other);
                if (other.categoryIndex == piece.categoryIndex)
                {
                    if (other.state == PieceState.Held && d < _config.mergeDistance)
                    {
                        Merge(piece, other);
                        Anchor(ref g, hand);
                        break;
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

            if (!_sealed && !_press.pressing && _jar.InPourZone(piece))
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

        void StepTouch(float now, float dt)
        {
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
                float wave = Mathf.Clamp01(_sealTime / 1.35f);
                _jar.SetPulse(wave < 1f ? wave : 0f);
                _glow.SetLift(_jar.root.position.y - _deskY);
                _dim = Mathf.MoveTowards(_dim, _config.baseDim + _config.sealDim, dt * 0.12f);
                return;
            }
            float gathered = Mathf.Clamp01(_pieces.Count * 0.12f + _jar.TotalUnits / 36f * 0.55f);
            float targetDim = _config.baseDim + _config.sealDim * (0.55f * gathered);
            _dim = Mathf.MoveTowards(_dim, targetDim, dt * 0.18f);
            if (_jar.TotalUnits > 0 && !_jar.Pouring)
            {
                _waxTimer += dt;
                if (_waxTimer >= _config.waxAppearDelay) _wax.ShowStick(DominantWax());
            }
            else _waxTimer = 0f;

            float depth = 0f;
            for (int h = 0; h < 2; h++)
                if (_grips[h].mode == GripNone) depth = Mathf.Max(depth, _wax.Depth(_hands[h]));
            if (!_wax.Ready) depth = 0f;
            _press.Step(depth, dt, _config.pressDepth, _config.holdSeconds, _config.recoverSeconds);
            _wax.SetHold(_press.progress);
            if (sound != null) sound.Press(_wax.DropCenter, _press.pressing ? _press.progress : 0f);
            if (_press.sealedShut) Seal();
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
            if (sound != null) sound.Seal(_jar.MouthCenter);
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
            _glow.Bloom(_jar.root.position, _jar.root.rotation, _glowColors, _glowUnits, dominant);
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
            _jar.Dispose();
            RitualUtil.Kill(_labelCardMesh);
            RitualUtil.Kill(_labelBorderMesh);
            RitualUtil.Kill(_labelTextMesh);
            _labelCardMesh = null;
            _labelBorderMesh = null;
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
            _labelAttach = 0f;
            _labelGlow = 0f;
            _panelTimer = 0f;
            _sealTime = 0f;
            BuildJar();
            if (labelFont != null) MakePanel(labelFont);
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
            Debug.Log($"{Tag} ritual pour done {layer.category?.id} total={_jar.TotalUnits}u grains={_jar.TotalGrains} surface={_jar.SurfaceMean:0.000}");
        }

        void OnGrainsLanded(RitualJar.Layer layer, int count)
        {
            if (sound != null) sound.Landed(_jar.root.position + Vector3.up * _jar.SurfaceMean, count);
            _labelGlow = Mathf.Max(_labelGlow, 0.65f);
            _panelTimer = 0f;
            if (_jar.Pouring && sound != null && layer.landedCount >= layer.cloud.Count) sound.PourStop();
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
            _labelRoot.SetPositionAndRotation(Vector3.Lerp(floatPos, attachPos, s) + arcLift, Quaternion.Slerp(floatRot, attachRot, s));
            _labelRoot.localScale = Vector3.one * scale;

            if (_labelCard != null)
            {
                Color cardCol = new Color(0.16f, 0.13f, 0.11f, 1f);
                Color cardDeep = new Color(0.07f, 0.05f, 0.04f, 1f);
                Color cardLight = Color.Lerp(new Color(0.85f, 0.74f, 0.55f, 1f), _labelAccent, 0.35f);
                PushLabelMaterial(_labelCard, _labelCardBlock, cardCol, cardDeep, cardLight, 0.08f + _labelGlow * 0.25f, 0.14f, 0.001f, s, _labelAppear, 0f);
            }
            if (_labelBorder != null)
            {
                Color borderCol = Color.Lerp(new Color(0.94f, 0.78f, 0.46f, 1f), _labelAccent, 0.45f);
                Color borderDeep = new Color(0.42f, 0.30f, 0.14f, 1f);
                Color borderLight = new Color(1f, 0.96f, 0.86f, 1f);
                PushLabelMaterial(_labelBorder, _labelBorderBlock, borderCol, borderDeep, borderLight, 0.38f + _labelGlow * 0.85f, 0.55f, 0.001f, s, _labelAppear, 0f);
            }

            _panelTimer -= dt;
            if (_panelTimer <= 0f || _labelTextDirty)
            {
                _panelTimer = 0.25f;
                Tally();
                _text.Length = 0;
                _text.Append(_jar.TotalUnits);
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

        void RebuildLabelTextGlyphs()
        {
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
            return $"{_config.glowStyle}|{_config.glowSeepSplats}|{_config.glowSeepGain}|{_config.glowSeepMotes}|{_config.glowPetals}|{_config.glowMotes}|{_config.glowRadius}";
        }

        public void RebloomGlow()
        {
            if (!_sealed || _glowColors.Count == 0) return;
            _glow.Bloom(_jar.root.position, _jar.root.rotation, _glowColors, _glowUnits, _glowDominant);
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
            _jar?.Dispose();
        }
    }
}
