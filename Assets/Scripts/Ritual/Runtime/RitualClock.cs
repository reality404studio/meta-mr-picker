using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoodPicker.Ritual
{
    public enum ClockPhase { Hidden, Showing, Frozen, Collapsing, Done }

    public sealed class RitualClock
    {
        const string Digits = "0123456789";
        const int FontSize = 48;
        const int Springs = 5;
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        public event Action<Vector3, bool> Ticked;

        readonly TimeRitualConfig _config;
        readonly Font _font;
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        const int Slots = 13;
        readonly char[] _shown = { '0', '0', ':', '0', '0', ':', '0', '0', '0', '0', '.', '0', '0' };
        readonly bool[] _big = { true, true, true, true, true, false, false, false, false, false, false, false, false };
        readonly bool[] _date = { false, false, false, false, false, false, false, false, true, true, true, true, true };
        readonly float[] _slotX = new float[Slots];
        readonly int[] _slotStart = new int[Slots];
        readonly int[] _slotCount = new int[Slots];
        readonly Vector3[] _springs = new Vector3[Springs];
        readonly Color[] _springColors = new Color[Springs];
        readonly int[] _springTotal = new int[Springs];
        readonly int[] _springArrived = new int[Springs];
        readonly List<Vector3> _mp = new List<Vector3>(4096);
        readonly List<Vector2> _muv = new List<Vector2>(4096);
        readonly List<Color32> _mc = new List<Color32>(4096);
        readonly List<int> _mi = new List<int>(6144);
        readonly CharacterInfo[] _glyphInfo = new CharacterInfo[11];
        GameObject _go;
        Mesh _mesh;
        MeshRenderer _renderer;
        Vector3 _center;
        Quaternion _rot = Quaternion.identity;
        float _width;
        int _n;
        Vector3[] _pos;
        Vector3[] _start;
        Vector2[] _local;
        int[] _slot;
        int[] _glyph;
        float[] _hash;
        int[] _spring;
        Vector3[] _outDir;
        float[] _outR;
        float[] _sign;
        bool[] _arrived;
        Vector2[] _tmp;
        ClockPhase _phase = ClockPhase.Hidden;
        float _phaseAge;
        int _lastSecond = -1;
        bool _tock;
        bool _springsSet;

        public ClockPhase Phase => _phase;
        public string Text => new string(_shown, 0, 8);
        public string DateText => new string(_shown, 8, 5);
        public int Count => _n;
        public int TickCount { get; private set; }
        public float Spread { get; private set; }
        public float Farthest { get; private set; }
        public Vector3 Center => _center;
        public float Width => _width;
        public bool Visible => _renderer != null && _renderer.enabled;
        public Vector3 ParticlePosition(int i) => _pos[i];
        public float ArrivalShare(int spring) => _springTotal[spring] > 0 ? _springArrived[spring] / (float)_springTotal[spring] : 0f;

        public RitualClock(TimeRitualConfig config, Font font, Material material)
        {
            _config = config;
            _font = font;
            Layout();
            if (material == null) return;
            _go = new GameObject("RitualClock");
            _go.hideFlags = HideFlags.DontSave;
            _mesh = new Mesh { name = "RitualClock", hideFlags = HideFlags.DontSave };
            _mesh.MarkDynamic();
            _go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = _go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            RitualUtil.Quiet(_renderer);
            _renderer.enabled = false;
        }

        void Layout()
        {
            float h = Mathf.Max(0.02f, _config.clockDigitHeight);
            int perDigit = Mathf.Max(20, _config.clockGlyphsPerDigit);
            float small = 0.5f;
            float x = 0f;
            float dateX = 0f;
            int total = 0;
            for (int s = 0; s < Slots; s++)
            {
                float scale = _big[s] ? 1f : small;
                char c = _shown[s];
                if (_date[s])
                {
                    _slotX[s] = dateX;
                    dateX += RitualClockFont.Advance(c) * h * scale;
                }
                else
                {
                    _slotX[s] = x;
                    x += RitualClockFont.Advance(c) * h * scale;
                }
                bool mark = c == ':' || c == '.';
                int count = mark ? Mathf.Max(6, perDigit / (_big[s] ? 4 : 7)) : (_big[s] ? perDigit : Mathf.Max(12, perDigit * 5 / 9));
                _slotStart[s] = total;
                _slotCount[s] = count;
                total += count;
            }
            _width = x;
            for (int s = 0; s < Slots; s++) _slotX[s] -= _width * 0.5f;
            _n = total;
            _pos = new Vector3[_n];
            _start = new Vector3[_n];
            _local = new Vector2[_n];
            _slot = new int[_n];
            _glyph = new int[_n];
            _hash = new float[_n];
            _spring = new int[_n];
            _outDir = new Vector3[_n];
            _outR = new float[_n];
            _sign = new float[_n];
            _arrived = new bool[_n];
            _tmp = new Vector2[Mathf.Max(perDigit, 16) + 8];
            for (int s = 0; s < Slots; s++)
                for (int k = 0; k < _slotCount[s]; k++) _slot[_slotStart[s] + k] = s;
            for (int i = 0; i < _n; i++)
            {
                float h0 = RitualUtil.Hash(i * 29 + 3);
                float h1 = RitualUtil.Hash(i * 31 + 5);
                float h2 = RitualUtil.Hash(i * 37 + 7);
                _hash[i] = h0;
                _glyph[i] = Mathf.Min(9, (int)(h1 * 10f));
                float z = 1f - 2f * h2;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                float a = h1 * 6.2831853f + h0 * 1.7f;
                _outDir[i] = new Vector3(r * Mathf.Cos(a), z * 0.45f + 0.1f, r * Mathf.Sin(a)).normalized;
                _outR[i] = Mathf.Lerp(0.35f, 1f, Mathf.Sqrt(h0));
                _sign[i] = h2 > 0.2f ? 1f : -1f;
            }
        }

        public void Place(Vector3 center, Quaternion rotation)
        {
            _center = center;
            _rot = rotation;
        }

        public void SetSprings(IReadOnlyList<Vector3> positions, IReadOnlyList<Color> colors)
        {
            for (int i = 0; i < Springs; i++)
            {
                _springs[i] = i < positions.Count ? positions[i] : _center + Vector3.down * 0.15f;
                _springColors[i] = colors != null && i < colors.Count ? colors[i] : Color.white;
            }
            _springsSet = true;
        }

        Vector3 World(Vector2 local) => _center + _rot * new Vector3(local.x, local.y, 0f);

        void Target(int slot, char c, bool snap)
        {
            _shown[slot] = c;
            float h = _config.clockDigitHeight;
            float scale = _big[slot] ? 1f : 0.5f;
            int count = _slotCount[slot];
            int start = _slotStart[slot];
            RitualClockFont.Sample(c, count, _tmp, 0, 0.035f, slot * 977 + c * 31);
            float baseY = _date[slot] ? 0.5f * h + 0.16f * h : -0.5f * h;
            for (int k = 0; k < count; k++)
            {
                Vector2 p = _tmp[k];
                int i = start + k;
                _local[i] = new Vector2(_slotX[slot] + p.x * h * scale, baseY + p.y * h * scale);
                if (snap) _pos[i] = World(_local[i]);
            }
        }

        public bool Appeared => _phase != ClockPhase.Showing || _phaseAge > 0f;

        public void Show(DateTime time, float delay = 0f)
        {
            string text = Stamp(time);
            for (int s = 0; s < Slots; s++) Target(s, text[s], true);
            _lastSecond = time.Second;
            _phase = ClockPhase.Showing;
            _phaseAge = -Mathf.Max(0f, delay);
            TickCount = 0;
            Spread = 0f;
            Farthest = 0f;
            for (int i = 0; i < Springs; i++)
            {
                _springTotal[i] = 0;
                _springArrived[i] = 0;
            }
            for (int i = 0; i < _n; i++) _arrived[i] = false;
        }

        static string Stamp(DateTime time)
        {
            return time.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + time.ToString("MM.dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        public void Hide()
        {
            _phase = ClockPhase.Hidden;
            if (_renderer != null) _renderer.enabled = false;
        }

        bool Touched(RitualHands hands)
        {
            if (hands == null) return false;
            float halfW = _width * 0.5f + 0.02f;
            float halfH = _config.clockDigitHeight * 0.75f + 0.03f;
            Quaternion inv = Quaternion.Inverse(_rot);
            for (int h = 0; h < 2; h++)
            {
                ref RitualHand hand = ref hands[h];
                if (!hand.tracked) continue;
                if (Inside(inv * (hand.index - _center), halfW, halfH)) return true;
                if (Inside(inv * (hand.thumb - _center), halfW, halfH)) return true;
                if (Inside(inv * (hand.middle - _center), halfW, halfH)) return true;
                if (Inside(inv * (hand.palm - _center), halfW, halfH)) return true;
            }
            return false;
        }

        bool Inside(Vector3 q, float halfW, float halfH)
        {
            return Mathf.Abs(q.z) < _config.clockTouchDepth && Mathf.Abs(q.x) < halfW && Mathf.Abs(q.y) < halfH;
        }

        public void Touch()
        {
            if (_phase != ClockPhase.Showing) return;
            _phase = ClockPhase.Frozen;
            _phaseAge = 0f;
        }

        void BeginCollapse()
        {
            _phase = ClockPhase.Collapsing;
            _phaseAge = 0f;
            for (int i = 0; i < Springs; i++)
            {
                _springTotal[i] = 0;
                _springArrived[i] = 0;
            }
            for (int i = 0; i < _n; i++)
            {
                _start[i] = _pos[i];
                float across = Mathf.Clamp01(_local[i].x / Mathf.Max(1e-3f, _width) + 0.5f);
                int byX = Mathf.Clamp(Mathf.FloorToInt(across * Springs), 0, Springs - 1);
                int s = RitualUtil.Hash(i * 43 + 11) < 0.6f ? byX : Mathf.Min(Springs - 1, (int)(RitualUtil.Hash(i * 47 + 13) * Springs));
                _spring[i] = s;
                _springTotal[s]++;
                _arrived[i] = false;
            }
        }

        public void Tick(float dt, DateTime time, RitualHands hands, Vector3 eye)
        {
            if (_phase == ClockPhase.Hidden || _phase == ClockPhase.Done) return;
            _phaseAge += dt;
            if (_phase == ClockPhase.Showing)
            {
                if (time.Second != _lastSecond)
                {
                    _lastSecond = time.Second;
                    string text = Stamp(time);
                    for (int s = 0; s < Slots; s++)
                        if (text[s] != _shown[s]) Target(s, text[s], false);
                    if (_phaseAge > 0f)
                    {
                        TickCount++;
                        Ticked?.Invoke(_center, _tock);
                        _tock = !_tock;
                    }
                }
                float follow = 1f - Mathf.Exp(-dt * 9f);
                for (int i = 0; i < _n; i++) _pos[i] = Vector3.Lerp(_pos[i], World(_local[i]), follow);
                if (_phaseAge > 0.3f && Touched(hands)) Touch();
            }
            else if (_phase == ClockPhase.Frozen)
            {
                float tremble = 0.0012f * Mathf.Clamp01(_phaseAge / Mathf.Max(0.05f, _config.clockSilence));
                for (int i = 0; i < _n; i++)
                    _pos[i] = World(_local[i]) + _rot * new Vector3(Mathf.Sin(_phaseAge * 31f + _hash[i] * 40f), Mathf.Cos(_phaseAge * 27f + _hash[i] * 33f), 0f) * tremble;
                if (_phaseAge >= _config.clockSilence) BeginCollapse();
            }
            else if (_phase == ClockPhase.Collapsing)
            {
                Collapse(eye);
            }
            Render(eye);
        }

        void Collapse(Vector3 eye)
        {
            float dur = Mathf.Max(0.5f, _config.clockCollapseSeconds);
            float u = Mathf.Clamp01(_phaseAge / dur);
            float reach = _config.clockSpread;
            bool crumble = _config.clockCollapse == ClockCollapse.Crumble;
            Vector3 low = _center + Vector3.down * 0.18f;
            double spread = 0;
            float far = 0f;
            for (int i = 0; i < _n; i++)
            {
                float h = _hash[i];
                Vector3 target = _springsSet ? _springs[_spring[i]] : _center + Vector3.down * 0.15f;
                Vector3 origin = crumble ? low : _center;
                Vector3 mid;
                if (crumble)
                {
                    float height = Mathf.Clamp01(_local[i].y / Mathf.Max(1e-3f, _config.clockDigitHeight) + 0.5f);
                    float fall = RitualVortex.Smooth01(u / 0.3f - 0.35f * (1f - height));
                    Vector3 fallen = _start[i] + Vector3.down * (0.32f * fall * fall) + new Vector3(_outDir[i].x, 0f, _outDir[i].z) * (0.05f * fall);
                    float s = RitualVortex.Smooth01((u - 0.22f) / 0.3f);
                    mid = s > 0f ? RitualVortex.Spread(fallen, origin, _outDir[i], _outR[i] * reach, s, _sign[i], h) : fallen;
                }
                else
                {
                    float s = RitualVortex.Smooth01(u / 0.26f);
                    mid = RitualVortex.Spread(_start[i], origin, _outDir[i], _outR[i] * reach, s, _sign[i], h);
                }
                float startIn = 0.5f + 0.2f * h;
                float q = RitualVortex.Smooth01((u - startIn) / (1f - startIn));
                Vector3 p;
                if (q > 0f)
                {
                    Vector3 outPos = RitualVortex.Drift(RitualVortex.OutPoint(origin, _outDir[i], _outR[i] * reach, _sign[i]), _phaseAge, h, 0.04f * (1f - q));
                    p = RitualVortex.Spiral(outPos, target, q, _sign[i], 1.0f, h);
                }
                else p = RitualVortex.Drift(mid, _phaseAge, h, 0.04f);
                _pos[i] = p;
                if (!_arrived[i] && q >= 0.985f)
                {
                    _arrived[i] = true;
                    _springArrived[_spring[i]]++;
                }
                float d = Vector3.Distance(p, _center);
                spread += d;
                if (d > far) far = d;
            }
            Spread = (float)(spread / Mathf.Max(1, _n));
            Farthest = far;
            if (u >= 1f)
            {
                for (int i = 0; i < _n; i++)
                    if (!_arrived[i])
                    {
                        _arrived[i] = true;
                        _springArrived[_spring[i]]++;
                    }
                _phase = ClockPhase.Done;
                if (_renderer != null) _renderer.enabled = false;
            }
        }

        float Progress(int i)
        {
            if (_phase != ClockPhase.Collapsing) return 0f;
            float dur = Mathf.Max(0.5f, _config.clockCollapseSeconds);
            float u = Mathf.Clamp01(_phaseAge / dur);
            float startIn = 0.5f + 0.2f * _hash[i];
            return RitualVortex.Smooth01((u - startIn) / (1f - startIn));
        }

        void Render(Vector3 eye)
        {
            if (_renderer == null || _font == null || _phase == ClockPhase.Done) return;
            _font.RequestCharactersInTexture(Digits + ".", FontSize, FontStyle.Normal);
            for (int g = 0; g < 10; g++) _font.GetCharacterInfo(Digits[g], out _glyphInfo[g], FontSize, FontStyle.Normal);
            _font.GetCharacterInfo('.', out _glyphInfo[10], FontSize, FontStyle.Normal);
            _mp.Clear();
            _muv.Clear();
            _mc.Clear();
            _mi.Clear();
            float appear = _phase == ClockPhase.Showing ? RitualVortex.Smooth01(_phaseAge / 1.6f) : 1f;
            if (_phase != ClockPhase.Showing) appear = 1f;
            float size = _config.clockGlyphSize;
            Color baseColor = new Color(1f, 0.9f, 0.74f, 1f);
            float t = _phaseAge;
            for (int i = 0; i < _n; i++)
            {
                if (_arrived[i]) continue;
                Vector3 p = _pos[i];
                Vector3 toEye = eye - p;
                float dist = toEye.magnitude;
                if (dist < 0.12f) continue;
                Vector3 fwd = toEye / Mathf.Max(1e-4f, dist);
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
                right.Normalize();
                Vector3 up = Vector3.Cross(fwd, right);
                float q = Progress(i);
                bool big = _big[_slot[i]];
                float twinkle = 0.82f + 0.18f * Mathf.Sin(t * (2.1f + 3f * _hash[i]) + _hash[i] * 20f);
                float spreadGlow = _phase == ClockPhase.Collapsing ? 1.15f : 1f;
                Color col = _phase == ClockPhase.Collapsing ? Color.Lerp(baseColor, Color.Lerp(_springColors[_spring[i]], Color.white, 0.25f), q) : baseColor;
                float light = appear * twinkle * spreadGlow * (big ? 1f : 0.85f) * (1f - 0.6f * q * q);
                float s = size * (big ? 1f : 0.8f) * (1f - 0.35f * q);
                float dot = RitualVortex.Smooth01((q - 0.5f) / 0.35f);
                if (dot < 0.999f) Quad(p, right, up, s * 0.62f, s, _glyphInfo[_glyph[i]], col * (light * (1f - dot)));
                if (dot > 0.001f) Quad(p, right, up, s * 0.55f, s * 0.55f, _glyphInfo[10], col * (light * dot * 1.4f));
            }
            bool show = _mp.Count > 0;
            if (_renderer.enabled != show) _renderer.enabled = show;
            if (!show) return;
            _mesh.Clear(false);
            _mesh.SetVertices(_mp);
            _mesh.SetUVs(0, _muv);
            _mesh.SetColors(_mc);
            _mesh.SetTriangles(_mi, 0, false);
            _mesh.bounds = new Bounds(_center, Vector3.one * (_config.clockSpread * 2f + 2f));
            if (_font.material != null && _font.material.mainTexture != null) _block.SetTexture(MainTexId, _font.material.mainTexture);
            _block.SetFloat(IntensityId, 1f);
            _renderer.SetPropertyBlock(_block);
        }

        void Quad(Vector3 c, Vector3 right, Vector3 up, float w, float h, CharacterInfo ci, Color color)
        {
            int v = _mp.Count;
            Vector3 rx = right * (w * 0.5f), uy = up * (h * 0.5f);
            _mp.Add(c - rx - uy);
            _mp.Add(c + rx - uy);
            _mp.Add(c + rx + uy);
            _mp.Add(c - rx + uy);
            _muv.Add(ci.uvBottomLeft);
            _muv.Add(ci.uvBottomRight);
            _muv.Add(ci.uvTopRight);
            _muv.Add(ci.uvTopLeft);
            Color32 c32 = new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), 1f);
            _mc.Add(c32);
            _mc.Add(c32);
            _mc.Add(c32);
            _mc.Add(c32);
            _mi.Add(v);
            _mi.Add(v + 1);
            _mi.Add(v + 2);
            _mi.Add(v);
            _mi.Add(v + 2);
            _mi.Add(v + 3);
        }

        public void Dispose()
        {
            if (_go != null) RitualUtil.Kill(_go);
            RitualUtil.Kill(_mesh);
            _go = null;
            _mesh = null;
            _renderer = null;
        }
    }
}
