using UnityEngine;

namespace MoodPicker.Ritual
{
    public sealed class RitualEnding
    {
        public enum Phase { Hidden, Waiting, Rising, Ready, Snuffing, Fading, Done }

        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int RevealId = Shader.PropertyToID("_Reveal");
        static readonly int BreathId = Shader.PropertyToID("_Breath");
        static readonly int SwirlId = Shader.PropertyToID("_Swirl");
        static readonly int FlatId = Shader.PropertyToID("_Flat");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EdgeId = Shader.PropertyToID("_Edge");
        static readonly int FadeId = Shader.PropertyToID("_Fade");

        public const float SnuffSeconds = 1.2f;

        readonly TimeRitualConfig _config;
        readonly GameObject _root;
        readonly MaterialPropertyBlock _orbBlock = new MaterialPropertyBlock();
        readonly MaterialPropertyBlock _veilBlock = new MaterialPropertyBlock();
        MeshRenderer _orb;
        Mesh _orbMesh;
        TextMesh _label;
        MeshRenderer _veil;
        Mesh _veilMesh;
        Material _veilMaterial;
        Vector3 _home;
        Vector3 _eye;
        float _age;
        float _wait;

        public Phase State { get; private set; } = Phase.Hidden;
        public Vector3 Position { get; private set; }
        public float OrbLight { get; private set; }
        public float Mood { get; private set; } = 1f;
        public float Fade { get; private set; }
        public bool QuitRequested { get; private set; }
        public string Trigger { get; private set; } = "";
        public bool Visible => State == Phase.Rising || State == Phase.Ready || State == Phase.Snuffing;

        public RitualEnding(TimeRitualConfig config, Material glow, Material frost, Font font, Transform head)
        {
            _config = config;
            _root = new GameObject("RitualEnding");
            _root.hideFlags = HideFlags.DontSave;
            if (glow != null) BuildOrb(glow);
            if (font != null)
            {
                var go = new GameObject("EndLabel");
                go.transform.SetParent(_root.transform, false);
                _label = go.AddComponent<TextMesh>();
                _label.font = font;
                _label.fontSize = 64;
                _label.characterSize = 0.0014f;
                _label.anchor = TextAnchor.UpperCenter;
                _label.alignment = TextAlignment.Center;
                _label.text = "End Ritual";
                _label.color = new Color(0.86f, 0.9f, 1f, 0f);
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = font.material;
                RitualUtil.Quiet(r);
                go.SetActive(false);
            }
            if (frost != null && head != null) BuildVeil(frost, head);
        }

        void BuildOrb(Material glow)
        {
            var b = RitualStaticSplats.Builder.Create(90);
            Color moon = new Color(0.78f, 0.86f, 1f, 1f);
            Add(ref b, Vector3.zero, 0.012f, Color.white * 0.9f, 0f, 0f, 1.1f);
            Add(ref b, Vector3.zero, 0.026f, moon * 0.4f, 1f, 0f, 0.9f);
            Add(ref b, Vector3.zero, 0.05f, moon * 0.12f, 2f, 0f, 0.7f);
            for (int i = 0; i < 60; i++)
            {
                float h1 = RitualUtil.Hash(i * 13 + 1), h2 = RitualUtil.Hash(i * 17 + 2), h3 = RitualUtil.Hash(i * 19 + 3);
                float a = h1 * 6.2831853f;
                float y = (h2 - 0.5f) * 2f;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                Vector3 p = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r) * Mathf.Lerp(0.006f, 0.016f, h3);
                Add(ref b, p, 0.0022f, moon * 0.8f, h1 * 6.28f, h3 > 0.8f ? 2f : 0f, 0.6f + h2);
            }
            _orbMesh = b.ToMesh("EndOrb", 0.1f);
            _orb = RitualStaticSplats.Spawn("EndOrb", _root.transform, _orbMesh, glow);
            _orb.enabled = false;
        }

        static void Add(ref RitualStaticSplats.Builder b, Vector3 pos, float size, Color color, float phase, float kind, float speed)
        {
            b.Add(pos, Vector3.up, new Vector4(1f, 0f, size, size), new Vector4(color.r, color.g, color.b, phase), new Vector4(0f, kind, speed, 1f));
        }

        void BuildVeil(Material frost, Transform head)
        {
            _veilMesh = new Mesh { name = "EndVeil", hideFlags = HideFlags.DontSave };
            _veilMesh.SetVertices(new[] { new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f) });
            _veilMesh.SetUVs(0, new[] { new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f) });
            _veilMesh.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
            _veilMesh.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            _veilMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4f);
            _veilMaterial = new Material(frost) { name = "EndVeil", hideFlags = HideFlags.DontSave, renderQueue = 4000 };
            _veilMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            var go = new GameObject("EndVeil");
            go.transform.SetParent(head, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.2f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = _veilMesh;
            _veil = go.AddComponent<MeshRenderer>();
            _veil.sharedMaterial = _veilMaterial;
            RitualUtil.Quiet(_veil);
            _veil.enabled = false;
        }

        public void Reset()
        {
            State = Phase.Hidden;
            _age = 0f;
            OrbLight = 0f;
            Mood = 1f;
            Fade = 0f;
            QuitRequested = false;
            Trigger = "";
            if (_orb != null) _orb.enabled = false;
            if (_label != null) _label.gameObject.SetActive(false);
            if (_veil != null) _veil.enabled = false;
        }

        public void Arm(Vector3 home, Vector3 eye, float delay)
        {
            if (State != Phase.Hidden) return;
            _home = home;
            _eye = eye;
            _wait = Mathf.Max(0f, delay);
            _age = 0f;
            State = Phase.Waiting;
        }

        public void Tick(float dt, in RitualHand left, in RitualHand right)
        {
            switch (State)
            {
                case Phase.Hidden:
                case Phase.Done:
                    return;
                case Phase.Waiting:
                    _wait -= dt;
                    if (_wait <= 0f)
                    {
                        State = Phase.Rising;
                        _age = 0f;
                        Position = _home + Vector3.down * 0.05f;
                    }
                    return;
            }
            _age += dt;
            float rise = 0f;
            if (State == Phase.Rising)
            {
                rise = RitualUtil.Smooth(Mathf.Clamp01(_age / 1.1f));
                OrbLight = rise;
                if (_age >= 1.1f) State = Phase.Ready;
            }
            if (State == Phase.Rising || State == Phase.Ready)
            {
                if (State == Phase.Ready) rise = 1f;
                float bob = 0.004f * Mathf.Sin(_age * 1.3f);
                Position = _home + Vector3.down * (0.05f * (1f - rise)) + Vector3.up * bob;
                if (State == Phase.Ready)
                {
                    OrbLight = 0.85f + 0.15f * Mathf.Sin(_age * 1.6f);
                    if (Touched(in left) || Touched(in right))
                    {
                        State = Phase.Snuffing;
                        _age = 0f;
                        Debug.Log($"[MP] ritual end orb {Trigger}");
                    }
                }
            }
            else if (State == Phase.Snuffing)
            {
                float u = Mathf.Clamp01(_age / SnuffSeconds);
                OrbLight = (1f - u) * (1f - u) * (1f + 0.6f * Mathf.Sin(u * Mathf.PI));
                Position += Vector3.down * (0.03f * dt);
                Mood = Mathf.Lerp(1f, 0.35f, RitualUtil.Smooth(u));
                if (_age >= SnuffSeconds)
                {
                    State = Phase.Fading;
                    _age = 0f;
                }
            }
            else if (State == Phase.Fading)
            {
                OrbLight = 0f;
                Mood = 0.35f;
                Fade = RitualUtil.Smooth(Mathf.Clamp01(_age / Mathf.Max(0.2f, _config.endFadeSeconds)));
                if (_age >= _config.endFadeSeconds + 0.5f)
                {
                    Fade = 1f;
                    State = Phase.Done;
                    QuitRequested = true;
                    Debug.Log("[MP] ritual ended");
                }
            }
            Render();
        }

        bool Touched(in RitualHand hand)
        {
            if (!hand.tracked) return false;
            float r = _config.endOrbRadius;
            float palm = Vector3.Distance(hand.palm, Position);
            float tip = Vector3.Distance(hand.index, Position);
            float pinch = Vector3.Distance(hand.pinch, Position);
            if (palm < r * 1.2f && hand.openness < 0.055f)
            {
                Trigger = "cupped";
                return true;
            }
            if (hand.pinching && pinch < r * 0.8f)
            {
                Trigger = "pinched";
                return true;
            }
            if ((tip < r || palm < r * 1.2f) && hand.palmVelocity.y < -0.3f)
            {
                Trigger = "swept";
                return true;
            }
            return false;
        }

        void Render()
        {
            if (_orb != null)
            {
                bool show = OrbLight > 0.003f;
                if (_orb.enabled != show) _orb.enabled = show;
                if (show)
                {
                    float scale = State == Phase.Snuffing ? Mathf.Lerp(1f, 0.15f, Mathf.Clamp01(_age / SnuffSeconds)) : 1f;
                    _orb.transform.SetPositionAndRotation(Position, Quaternion.identity);
                    _orb.transform.localScale = Vector3.one * scale;
                    _orbBlock.SetFloat(IntensityId, OrbLight);
                    _orbBlock.SetFloat(RevealId, 10f);
                    _orbBlock.SetFloat(BreathId, 0.12f);
                    _orbBlock.SetFloat(SwirlId, 0.6f);
                    _orbBlock.SetFloat(FlatId, 0f);
                    _orb.SetPropertyBlock(_orbBlock);
                }
            }
            if (_label != null)
            {
                float a = State == Phase.Ready ? Mathf.Clamp01((_age - 1.1f) / 0.8f) * 0.6f : 0f;
                bool show = a > 0.01f;
                if (_label.gameObject.activeSelf != show) _label.gameObject.SetActive(show);
                if (show)
                {
                    Vector3 p = Position + Vector3.down * 0.03f;
                    Vector3 look = p - _eye;
                    _label.transform.SetPositionAndRotation(p, look.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(look, Vector3.up) : Quaternion.identity);
                    Color c = _label.color;
                    c.a = a;
                    _label.color = c;
                }
            }
            if (_veil != null)
            {
                bool show = Fade > 0.002f;
                if (_veil.enabled != show) _veil.enabled = show;
                if (show)
                {
                    _veilBlock.SetColor(ColorId, new Color(0f, 0f, 0f, Fade));
                    _veilBlock.SetFloat(EdgeId, 0f);
                    _veilBlock.SetFloat(FadeId, 1f);
                    _veil.SetPropertyBlock(_veilBlock);
                }
            }
        }

        public void Dispose()
        {
            RitualUtil.Kill(_orbMesh);
            RitualUtil.Kill(_veilMesh);
            RitualUtil.Kill(_veilMaterial);
            if (_veil != null) RitualUtil.Kill(_veil.gameObject);
            if (_root != null) RitualUtil.Kill(_root);
        }
    }
}
