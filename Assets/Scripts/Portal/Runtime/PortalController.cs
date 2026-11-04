using System;
using System.Collections.Generic;
using System.IO;
using MoodPicker.Catch;
using MoodPicker.Hands;
using UnityEngine;
using UnityEngine.Rendering;

namespace MoodPicker.Portal
{
    public class PortalController : MonoBehaviour, IPinchGate
    {
        const string Tag = "[MP]";
        const int MaxChips = 12;

        [SerializeField] Transform trackingSpace;
        [SerializeField] MonoBehaviour handInputSource;
        [SerializeField] PortalSettings settings;
        [SerializeField] Material lineMaterial;
        [SerializeField] Material chipMaterial;

        [Header("Look (baseline)")]
        [SerializeField] float lineWidth = 0.0015f;
        [SerializeField] float chipSize = 0.036f;
        [SerializeField] float tipSize = 0.014f;

        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        public PaintRecord CurrentPaint => _paint?.Paint;
        public Texture2D CurrentTile => _paint?.Tile;
        public event Action<PaintRecord, Texture2D> PaintPicked;
        public event Action Washed;

        IHandInput _input;
        PalettePortal _portal;
        PaintStore _store;
        LineRenderer _ring, _front, _well;
        TipPaint<PaintRecord, Texture2D> _paint;
        readonly List<Renderer> _chips = new List<Renderer>();
        readonly List<PaintRecord> _shown = new List<PaintRecord>();
        readonly Dictionary<string, Texture2D> _tiles = new Dictionary<string, Texture2D>();
        Renderer _tip;
        Transform _cam;
        float _openedAt;

        public bool SuppressesPinch(float t) => _portal != null && _portal.SuppressesPinch(t);

        void Awake()
        {
            _input = handInputSource as IHandInput;
            _cam = Camera.main != null ? Camera.main.transform : null;
            if (settings == null) settings = PortalSettings.Defaults();
            _portal = new PalettePortal(settings);
            _store = new PaintStore(PaintStore.DefaultRoot);
            _portal.Opened += OnOpened;
            _portal.Closed += t => { SetVisible(false); Debug.Log($"{Tag} portal closed t={t:0.000} open={t - _openedAt:0.00}s"); };
            _paint = new TipPaint<PaintRecord, Texture2D>(_portal, i => i >= 0 && i < _shown.Count ? (_shown[i], Tile(_shown[i])) : (null, null));
            _paint.Picked += OnPicked;
            _paint.Washed += OnWashed;
            _portal.Rotated += (i, t) => Debug.Log($"{Tag} portal wheel front={i} chip={_portal.FrontChip} t={t:0.000}");

            _ring = Line("PortalRing");
            _front = Line("PortalFrontSlot");
            _well = Line("PortalWell");
            for (int i = 0; i < MaxChips; i++) _chips.Add(Quad("PortalChip" + i));
            _tip = Quad("FingertipPaint");
            _tip.gameObject.SetActive(false);
            SetVisible(false);
        }

        LineRenderer Line(string name)
        {
            var lr = new GameObject(name).AddComponent<LineRenderer>();
            lr.transform.SetParent(transform, false);
            lr.sharedMaterial = lineMaterial;
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.widthMultiplier = lineWidth;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        Renderer Quad(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = chipMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        void SetVisible(bool on)
        {
            _ring.enabled = on;
            _front.enabled = on;
            _well.enabled = on;
            for (int i = 0; i < _chips.Count; i++) _chips[i].gameObject.SetActive(on && i < _shown.Count);
        }

        void OnOpened(float t)
        {
            _openedAt = t;
            _shown.Clear();
            List<PaintRecord> all = _store.LoadAll();
            for (int i = all.Count - 1; i >= 0 && _shown.Count < MaxChips; i--) _shown.Add(all[i]);
            _portal.ChipCount = _shown.Count;
            for (int i = 0; i < _shown.Count; i++) _chips[i].material.SetTexture(BaseMap, Tile(_shown[i]));
            SetVisible(true);
            Debug.Log($"{Tag} portal open t={t:0.000} chips={_shown.Count}");
        }

        Texture2D Tile(PaintRecord r)
        {
            if (_tiles.TryGetValue(r.id, out Texture2D tex) && tex != null) return tex;
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            string path = _store.PathOf(r, PaintStore.TileFile);
            if (!File.Exists(path) || !tex.LoadImage(File.ReadAllBytes(path))) Debug.Log($"{Tag} portal tile missing {r.id}");
            _tiles[r.id] = tex;
            return tex;
        }

        void OnPicked(PaintRecord paint, Texture2D tile, float t)
        {
            _tip.material.SetTexture(BaseMap, tile);
            _tip.gameObject.SetActive(true);
            PaintPicked?.Invoke(paint, tile);
            Debug.Log($"{Tag} portal dip No.{paint.number:000} id={paint.id} t={t:0.000}");
        }

        void OnWashed(float t)
        {
            _tip.gameObject.SetActive(false);
            Washed?.Invoke();
            Debug.Log($"{Tag} portal washed t={t:0.000}");
        }

        void Update()
        {
            if (_input == null || !_input.TryGetFrame(out HandFrame f)) return;
            _portal.Update(f);
            if (_portal.IsOpen) Layout();
            if (_tip.gameObject.activeSelf && f.right.tracked)
            {
                Vector3 tip = trackingSpace.TransformPoint(f.right.indexTip);
                _tip.transform.position = tip;
                if (_cam != null) _tip.transform.rotation = Quaternion.LookRotation(tip - _cam.position);
                _tip.transform.localScale = Vector3.one * tipSize;
            }
        }

        void Layout()
        {
            PortalPose p = _portal.Pose;
            Vector3 c = trackingSpace.TransformPoint(p.center);
            Vector3 n = trackingSpace.TransformDirection(p.normal), r = trackingSpace.TransformDirection(p.right), fw = trackingSpace.TransformDirection(p.forward);
            Circle(_ring, c, r, fw, settings.radius, 40);
            Circle(_well, c + n * 0.001f, r, fw, settings.wellRadius, 24);
            float size = _portal.ChipSize(chipSize);
            Vector2 fs = _portal.FrontSlot;
            Vector3 fc = trackingSpace.TransformPoint(p.ToWorld(fs)) + n * 0.001f;
            float h = size * 0.5f + 0.003f;
            _front.positionCount = 4;
            _front.SetPositions(new[] { fc - r * h - fw * h, fc + r * h - fw * h, fc + r * h + fw * h, fc - r * h + fw * h });
            Quaternion face = Quaternion.LookRotation(-n, fw);
            for (int i = 0; i < _shown.Count; i++)
            {
                Transform t = _chips[i].transform;
                t.position = trackingSpace.TransformPoint(p.ToWorld(_portal.ChipSlot(i)));
                t.rotation = face;
                t.localScale = Vector3.one * size;
            }
        }

        static void Circle(LineRenderer lr, Vector3 c, Vector3 r, Vector3 fw, float radius, int seg)
        {
            lr.positionCount = seg;
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                lr.SetPosition(i, c + (r * Mathf.Cos(a) + fw * Mathf.Sin(a)) * radius);
            }
        }

        void OnDestroy()
        {
            foreach (Texture2D t in _tiles.Values) if (t != null) Destroy(t);
        }
    }
}
