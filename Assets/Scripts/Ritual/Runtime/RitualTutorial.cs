using System.Text;
using UnityEngine;

namespace MoodPicker.Ritual
{
    public enum TutorialStep { Start = 0, Clock = 1, Pull = 2, PullMore = 3, Pour = 4, PourMore = 5, Lid = 6, Wax = 7, Shelf = 8, Look = 9, End = 10, Finished = 11 }

    public enum TutorialCard { None = 0, Clock = 1, Pull = 2, Pulling = 3, PullMore = 4, Merge = 5, Pour = 6, PourMore = 7, Lid = 8, Wax = 9, Shelf = 10, Look = 11, End = 12 }

    public struct TutorialState
    {
        public bool clockShowing;
        public bool springsReady;
        public bool pulling;
        public int pieces;
        public bool pairOfKind;
        public bool merged;
        public int poured;
        public bool lidClosed;
        public bool waxReady;
        public bool vesselSealed;
        public float sealAge;
        public bool shelved;
        public bool endOrbReady;
        public bool ended;
    }

    public sealed class RitualTutorial
    {
        public const string HideFull = "Right hand: tap your thumb on your index finger to hide this guide.";
        public const string HideShort = "Right thumb tap · hide guide";
        public const string HoursLine = "Left thumb tap · see today in hours";
        public const int MainFontSize = 64;
        public const int FootFontSize = 48;
        const float Pad = 0.011f;
        const float Gap = 0.007f;
        const float Breath = 1.6f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EdgeId = Shader.PropertyToID("_Edge");
        static readonly int FadeId = Shader.PropertyToID("_Fade");

        public static readonly TutorialCard[] Cards =
        {
            TutorialCard.Clock, TutorialCard.Pull, TutorialCard.Pulling, TutorialCard.PullMore, TutorialCard.Merge, TutorialCard.Pour,
            TutorialCard.PourMore, TutorialCard.Lid, TutorialCard.Wax, TutorialCard.Shelf, TutorialCard.Look, TutorialCard.End,
        };

        readonly TimeRitualConfig _cfg;
        readonly Font _font;
        readonly Material _textMat;
        readonly GameObject _rootGo;
        readonly Transform _root;
        readonly Transform _card;
        readonly MeshRenderer _cardR;
        readonly MaterialPropertyBlock _cardBlock = new MaterialPropertyBlock();
        readonly Mesh _frame;
        readonly MeshRenderer _frameR;
        readonly Material _frameMat;
        readonly MaterialPropertyBlock _frameBlock = new MaterialPropertyBlock();
        readonly Vector3[] _frameVerts = new Vector3[16];
        readonly TextMesh _main;
        readonly TextMesh _foot;
        readonly GameObject _glowGo;
        readonly Transform _glow;
        readonly MeshRenderer _glowR;
        readonly MaterialPropertyBlock _glowBlock = new MaterialPropertyBlock();
        readonly Mesh _quad;
        readonly Mesh _disc;
        readonly StringBuilder _sb = new StringBuilder(128);

        TutorialCard _shown;
        float _open;
        float _textAlpha;
        float _mergeShown;
        float _offAge = -1f;
        float _glowAge = -1f;
        bool _offHintDone;
        int _cardsShown;
        float _pulse;

        public TutorialStep Step { get; private set; }
        public TutorialCard Card => _shown;
        public bool Enabled { get; private set; }
        public float Openness => _open;
        public float Height { get; private set; } = 0.06f;
        public float Width => _cfg.tutorialCardWidth;
        public int MainLines { get; private set; }
        public float WidestLine { get; private set; }
        public string MainText => _main != null ? _main.text : "";
        public string FootText => _foot != null ? _foot.text : "";
        public bool OffHintShowing => _glowAge >= 0f;
        public bool OffHintShown => _offHintDone;
        public Transform Root => _root;
        public Renderer FrameRenderer => _frameR;
        public Bounds FrameBounds => _frame.bounds;
        public static readonly Color FrameInk = new Color(0.27f, 0.25f, 0.23f, 1f);
        public const float TextPad = Pad;

        public static string Line(TutorialCard card)
        {
            switch (card)
            {
                case TutorialCard.Clock: return "Reach out and touch the clock.";
                case TutorialCard.Pull: return "Pinch a glowing cloud and pull it out slowly.";
                case TutorialCard.Pulling: return "Let go whenever it feels like enough.";
                case TutorialCard.PullMore: return "Pull again from any cloud, as many times as you like.";
                case TutorialCard.Merge: return "Hold two of the same kind together until they merge.";
                case TutorialCard.Pour: return "Rest a piece on your open palm, then tilt it over the vessel.";
                case TutorialCard.PourMore: return "Pour in the rest the same way.";
                case TutorialCard.Lid: return "All in? Pinch the lid and set it on the vessel.";
                case TutorialCard.Wax: return "Press the glowing wax with your thumb, hold, then lift.";
                case TutorialCard.Shelf: return "Pinch the vessel and set it in today's place on the shelf.";
                case TutorialCard.Look: return "Touch any vessel to look back on its day.";
                case TutorialCard.End: return "Cup the moon in your hand to end the ritual.";
                default: return "";
            }
        }

        public static string Foot(TutorialStep step, bool first)
        {
            string hide = first ? HideFull : HideShort;
            return step >= TutorialStep.Pour && step <= TutorialStep.Wax ? hide + "\n" + HoursLine : hide;
        }

        public static TutorialStep Advance(TutorialStep step, in TutorialState s, float idle, TimeRitualConfig cfg)
        {
            if (s.ended) return TutorialStep.Finished;
            TutorialStep c = step;
            if (c < TutorialStep.Clock && s.clockShowing) c = TutorialStep.Clock;
            if (c < TutorialStep.Pull && s.springsReady) c = TutorialStep.Pull;
            if (c >= TutorialStep.Pull && c < TutorialStep.PullMore && s.pieces > 0) c = TutorialStep.PullMore;
            if (c == TutorialStep.PullMore && !s.pulling && (s.poured > 0 || (s.pieces >= 2 && idle >= cfg.tutorialPourIdle) || (s.pieces >= 1 && idle >= cfg.tutorialPourIdle * 2f))) c = TutorialStep.Pour;
            if (c >= TutorialStep.PullMore && c < TutorialStep.PourMore && s.poured > 0) c = TutorialStep.PourMore;
            if (c == TutorialStep.PourMore && ((s.pieces == 0 && idle >= 1f) || idle >= cfg.tutorialLidIdle)) c = TutorialStep.Lid;
            if (c < TutorialStep.Wax && s.lidClosed) c = TutorialStep.Wax;
            if (c < TutorialStep.Shelf && s.vesselSealed) c = TutorialStep.Shelf;
            if (c < TutorialStep.Look && s.shelved) c = TutorialStep.Look;
            if (c < TutorialStep.End && s.endOrbReady) c = TutorialStep.End;
            return c;
        }

        public static TutorialCard Show(TutorialStep step, in TutorialState s, bool mergeAllowed, TimeRitualConfig cfg)
        {
            if (step == TutorialStep.Start || step == TutorialStep.Finished) return TutorialCard.None;
            if (s.pulling && step <= TutorialStep.PullMore) return TutorialCard.Pulling;
            if (mergeAllowed && s.pairOfKind && !s.merged && step >= TutorialStep.PullMore && step <= TutorialStep.PourMore) return TutorialCard.Merge;
            switch (step)
            {
                case TutorialStep.Clock: return TutorialCard.Clock;
                case TutorialStep.Pull: return TutorialCard.Pull;
                case TutorialStep.PullMore: return TutorialCard.PullMore;
                case TutorialStep.Pour: return TutorialCard.Pour;
                case TutorialStep.PourMore: return TutorialCard.PourMore;
                case TutorialStep.Lid: return TutorialCard.Lid;
                case TutorialStep.Wax: return s.waxReady && !s.vesselSealed ? TutorialCard.Wax : TutorialCard.None;
                case TutorialStep.Shelf: return s.sealAge >= cfg.tutorialShelfDelay && !s.shelved ? TutorialCard.Shelf : TutorialCard.None;
                case TutorialStep.Look: return TutorialCard.Look;
                case TutorialStep.End: return TutorialCard.End;
                default: return TutorialCard.None;
            }
        }

        public RitualTutorial(Transform parent, TimeRitualConfig config, Font font, Material cardMaterial)
        {
            _cfg = config;
            _font = font;
            _rootGo = new GameObject("TutorialCard");
            _root = _rootGo.transform;
            _root.SetParent(parent, false);
            _quad = Quad();
            var cardGo = new GameObject("Card");
            _card = cardGo.transform;
            _card.SetParent(_root, false);
            cardGo.AddComponent<MeshFilter>().sharedMesh = _quad;
            _cardR = cardGo.AddComponent<MeshRenderer>();
            _cardR.sharedMaterial = cardMaterial;
            RitualUtil.Quiet(_cardR);
            _frame = FrameMesh();
            var frameGo = new GameObject("Frame");
            frameGo.transform.SetParent(_root, false);
            frameGo.transform.localPosition = new Vector3(0f, 0f, -0.0008f);
            frameGo.AddComponent<MeshFilter>().sharedMesh = _frame;
            _frameR = frameGo.AddComponent<MeshRenderer>();
            _frameMat = new Material(cardMaterial) { name = "TutorialFrame", renderQueue = 3007 };
            _frameR.sharedMaterial = _frameMat;
            RitualUtil.Quiet(_frameR);
            _textMat = new Material(font.material) { name = "TutorialText", renderQueue = 3010 };
            _main = Text("Line", MainFontSize, config.tutorialTextSize, TextAnchor.UpperCenter);
            _foot = Text("Foot", FootFontSize, config.tutorialFootSize, TextAnchor.LowerCenter);
            _disc = Disc(20);
            _glowGo = new GameObject("TutorialThumbGlow");
            _glow = _glowGo.transform;
            _glow.SetParent(parent, false);
            _glowGo.AddComponent<MeshFilter>().sharedMesh = _disc;
            _glowR = _glowGo.AddComponent<MeshRenderer>();
            _glowR.sharedMaterial = cardMaterial;
            RitualUtil.Quiet(_glowR);
            _glowGo.SetActive(false);
            _rootGo.SetActive(false);
            Enabled = config.tutorial;
        }

        public void Restart()
        {
            Step = TutorialStep.Start;
            _shown = TutorialCard.None;
            _open = 0f;
            _textAlpha = 0f;
            _mergeShown = 0f;
            _cardsShown = 0;
            _pulse = 0f;
            _rootGo.SetActive(false);
        }

        public void SetEnabled(bool on)
        {
            if (on == Enabled) return;
            Enabled = on;
            if (!on && !_offHintDone) _offAge = 0f;
            if (on)
            {
                _offAge = -1f;
                _glowAge = -1f;
            }
        }

        public void Dispose()
        {
            RitualUtil.Kill(_rootGo);
            RitualUtil.Kill(_glowGo);
            RitualUtil.Kill(_quad);
            RitualUtil.Kill(_disc);
            RitualUtil.Kill(_textMat);
            RitualUtil.Kill(_frameMat);
            RitualUtil.Kill(_frame);
        }

        public void Tick(float dt, in TutorialState s, float idle, Vector3 anchor, Vector3 foldFrom, Vector3 eye, in RitualHand right)
        {
            Step = Advance(Step, s, idle, _cfg);
            TutorialCard want = Enabled ? Show(Step, s, _mergeShown < _cfg.tutorialMergeSeconds, _cfg) : TutorialCard.None;
            if (want == TutorialCard.Merge && _shown == TutorialCard.Merge) _mergeShown += dt;
            if (want != _shown)
            {
                if (_shown == TutorialCard.None || _open <= 0.001f)
                {
                    if (want != TutorialCard.None) Apply(want);
                    _shown = want;
                    _textAlpha = want != TutorialCard.None ? 1f : 0f;
                }
                else
                {
                    _textAlpha = Mathf.MoveTowards(_textAlpha, 0f, dt / 0.2f);
                    if (_textAlpha <= 0f && want != TutorialCard.None)
                    {
                        Apply(want);
                        _shown = want;
                    }
                }
            }
            else if (_shown != TutorialCard.None) _textAlpha = Mathf.MoveTowards(_textAlpha, 1f, dt / 0.25f);
            float target = _shown != TutorialCard.None && want != TutorialCard.None ? 1f : 0f;
            _open = Mathf.MoveTowards(_open, target, dt / Mathf.Max(0.05f, _cfg.tutorialFoldSeconds));
            bool visible = _open > 0.001f;
            if (_rootGo.activeSelf != visible) _rootGo.SetActive(visible);
            if (visible)
            {
                Place(anchor, foldFrom, eye);
                _pulse = idle >= _cfg.tutorialPulseIdle && _open >= 0.999f ? _pulse + dt : 0f;
                Paint();
            }
            StepGlow(dt, idle, right, eye);
        }

        void Apply(TutorialCard card)
        {
            float inner = _cfg.tutorialCardWidth - 2f * Pad;
            string main = Wrap(Line(card), MainFontSize, _cfg.tutorialTextSize, inner, out int lines, out float widest);
            string foot = Wrap(Foot(Step, _cardsShown == 0), FootFontSize, _cfg.tutorialFootSize, inner, out int footLines, out float footWidest);
            _cardsShown++;
            MainLines = lines;
            WidestLine = Mathf.Max(widest, footWidest);
            _main.text = main;
            _foot.text = foot;
            Height = Pad + lines * LineHeight(MainFontSize, _cfg.tutorialTextSize) + Gap + footLines * LineHeight(FootFontSize, _cfg.tutorialFootSize) + Pad;
            _card.localScale = new Vector3(_cfg.tutorialCardWidth, Height, 1f);
            ShapeFrame(_cfg.tutorialCardWidth, Height);
            _main.transform.localPosition = new Vector3(0f, Height * 0.5f - Pad, -0.0015f);
            _foot.transform.localPosition = new Vector3(0f, -Height * 0.5f + Pad, -0.0015f);
        }

        void Place(Vector3 anchor, Vector3 foldFrom, Vector3 eye)
        {
            float u = RitualUtil.Smooth(_open);
            Vector3 p = Vector3.Lerp(foldFrom, anchor, u);
            _root.position = p;
            Vector3 view = p - eye;
            float horiz = Mathf.Sqrt(view.x * view.x + view.z * view.z);
            view.y = Mathf.Clamp(view.y, -horiz * 1.5f, horiz * 1.5f);
            if (view.sqrMagnitude > 1e-6f) _root.rotation = Quaternion.LookRotation(view.normalized, Vector3.up);
            _root.localScale = Vector3.one * Mathf.Lerp(0.06f, 1f, u);
        }

        void Paint()
        {
            float b = _cfg.tutorialCardBrightness;
            float breathe = _pulse > 0f ? 0.5f - 0.5f * Mathf.Cos(_pulse * (2f * Mathf.PI / Breath)) : 0f;
            _cardBlock.SetColor(ColorId, new Color(b, b * 0.975f, b * 0.93f, 0.96f));
            _cardBlock.SetFloat(EdgeId, 0.35f * breathe);
            _cardBlock.SetFloat(FadeId, 1f);
            _cardR.SetPropertyBlock(_cardBlock);
            _frameBlock.SetColor(ColorId, new Color(1f, 1f, 1f, _cfg.tutorialFrameAlpha));
            _frameBlock.SetFloat(EdgeId, 0f);
            _frameBlock.SetFloat(FadeId, 1f);
            _frameR.SetPropertyBlock(_frameBlock);
            _main.color = new Color(0.09f, 0.08f, 0.07f, _textAlpha);
            _foot.color = new Color(0.27f, 0.25f, 0.23f, _textAlpha);
            if (_textMat.mainTexture != _font.material.mainTexture) _textMat.mainTexture = _font.material.mainTexture;
        }

        void StepGlow(float dt, float idle, in RitualHand right, Vector3 eye)
        {
            if (_offAge >= 0f && !Enabled)
            {
                _offAge += dt;
                if (_offAge > _cfg.tutorialOffHintWindow) _offAge = -1f;
                else if (!_offHintDone && _glowAge < 0f && idle >= _cfg.tutorialOffHintIdle && right.tracked)
                {
                    _glowAge = 0f;
                    _offHintDone = true;
                }
            }
            if (_glowAge < 0f)
            {
                if (_glowGo.activeSelf) _glowGo.SetActive(false);
                return;
            }
            _glowAge += dt;
            if (_glowAge >= Breath * 2f || Enabled || !right.tracked)
            {
                _glowAge = -1f;
                _glowGo.SetActive(false);
                return;
            }
            float a = Mathf.Sin(Mathf.PI * (_glowAge % Breath) / Breath);
            Vector3 p = (right.thumb + right.index) * 0.5f;
            _glow.position = p;
            Vector3 view = p - eye;
            if (view.sqrMagnitude > 1e-6f) _glow.rotation = Quaternion.LookRotation(view.normalized, Vector3.up);
            _glow.localScale = Vector3.one * 0.045f;
            _glowBlock.SetColor(ColorId, new Color(1f, 0.86f, 0.62f, 0.85f * a));
            _glowBlock.SetFloat(EdgeId, 0f);
            _glowBlock.SetFloat(FadeId, 1f);
            _glowR.SetPropertyBlock(_glowBlock);
            if (!_glowGo.activeSelf) _glowGo.SetActive(true);
        }

        float LineHeight(int size, float charSize)
        {
            float ratio = _font.fontSize > 0 && _font.lineHeight > 0 ? _font.lineHeight / (float)_font.fontSize : 1.25f;
            return size * ratio * charSize * 0.1f;
        }

        float Measure(string word, int size)
        {
            float w = 0f;
            for (int i = 0; i < word.Length; i++)
                if (_font.GetCharacterInfo(word[i], out CharacterInfo ci, size, FontStyle.Normal)) w += ci.advance;
            return w;
        }

        string Wrap(string text, int size, float charSize, float maxWidth, out int lines, out float widest)
        {
            lines = 0;
            widest = 0f;
            if (string.IsNullOrEmpty(text)) return "";
            _font.RequestCharactersInTexture(text + " ", size, FontStyle.Normal);
            float unit = charSize * 0.1f;
            float space = Measure(" ", size) * unit;
            _sb.Clear();
            string[] paragraphs = text.Split('\n');
            for (int p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0) _sb.Append('\n');
                float lineW = 0f;
                bool started = false;
                string[] words = paragraphs[p].Split(' ');
                for (int i = 0; i < words.Length; i++)
                {
                    if (words[i].Length == 0) continue;
                    float w = Measure(words[i], size) * unit;
                    if (started && lineW + space + w > maxWidth)
                    {
                        widest = Mathf.Max(widest, lineW);
                        _sb.Append('\n');
                        lines++;
                        lineW = 0f;
                        started = false;
                    }
                    if (started)
                    {
                        _sb.Append(' ');
                        lineW += space;
                    }
                    _sb.Append(words[i]);
                    lineW += w;
                    started = true;
                }
                widest = Mathf.Max(widest, lineW);
                lines++;
            }
            return _sb.ToString();
        }

        TextMesh Text(string name, int size, float charSize, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.fontSize = size;
            tm.characterSize = charSize;
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Center;
            tm.richText = false;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = _textMat;
            RitualUtil.Quiet(r);
            return tm;
        }

        void ShapeFrame(float width, float height)
        {
            float w = Mathf.Max(0.0002f, _cfg.tutorialFrameWidth);
            float ox = width * 0.5f - _cfg.tutorialFrameInset;
            float oy = height * 0.5f - _cfg.tutorialFrameInset;
            Bar(0, -ox, oy - w, ox, oy);
            Bar(4, -ox, -oy, ox, -oy + w);
            Bar(8, -ox, -oy + w, -ox + w, oy - w);
            Bar(12, ox - w, -oy + w, ox, oy - w);
            _frame.SetVertices(_frameVerts);
            _frame.RecalculateBounds();
        }

        void Bar(int at, float x0, float y0, float x1, float y1)
        {
            _frameVerts[at] = new Vector3(x0, y0, 0f);
            _frameVerts[at + 1] = new Vector3(x1, y0, 0f);
            _frameVerts[at + 2] = new Vector3(x0, y1, 0f);
            _frameVerts[at + 3] = new Vector3(x1, y1, 0f);
        }

        static Mesh FrameMesh()
        {
            var m = new Mesh { name = "TutorialCardFrame", hideFlags = HideFlags.DontSave };
            var verts = new Vector3[16];
            var uvs = new Vector2[16];
            var cols = new Color[16];
            var tris = new int[24];
            for (int b = 0; b < 4; b++)
            {
                int v = b * 4;
                tris[b * 6] = v;
                tris[b * 6 + 1] = v + 2;
                tris[b * 6 + 2] = v + 1;
                tris[b * 6 + 3] = v + 1;
                tris[b * 6 + 4] = v + 2;
                tris[b * 6 + 5] = v + 3;
            }
            for (int i = 0; i < 16; i++)
            {
                uvs[i] = new Vector2(0.5f, 0.5f);
                cols[i] = FrameInk;
            }
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetColors(cols);
            m.SetTriangles(tris, 0);
            m.MarkDynamic();
            return m;
        }

        static Mesh Quad()
        {
            var m = new Mesh { name = "TutorialCardQuad", hideFlags = HideFlags.DontSave };
            m.SetVertices(new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) });
            m.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            m.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
            m.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            m.RecalculateBounds();
            return m;
        }

        static Mesh Disc(int segments)
        {
            var verts = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var cols = new Color[segments + 1];
            var tris = new int[segments * 3];
            verts[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            cols[0] = Color.white;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f);
                uvs[i + 1] = new Vector2(0.5f, 0.5f);
                cols[i + 1] = new Color(1f, 1f, 1f, 0f);
                tris[i * 3] = 0;
                tris[i * 3 + 1] = 1 + (i + 1) % segments;
                tris[i * 3 + 2] = 1 + i;
            }
            var m = new Mesh { name = "TutorialGlowDisc", hideFlags = HideFlags.DontSave };
            m.vertices = verts;
            m.uv = uvs;
            m.colors = cols;
            m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }
    }
}
