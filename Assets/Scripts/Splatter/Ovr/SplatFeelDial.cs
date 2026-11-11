using System;
using System.Collections.Generic;
using System.Text;
using MoodPicker.Hands;
using UnityEngine;

namespace MoodPicker.Splatter
{
    public sealed class SplatDialRow
    {
        public string name;
        public Func<float> get;
        public Action<float> set;
        public Action act;
        public float min;
        public float max = 1f;
        public int steps = 16;
        public bool log;

        public bool IsAction => act != null;

        public float Normalized(float value)
        {
            if (log) return Mathf.Log(Mathf.Max(value, min) / min) / Mathf.Log(max / min);
            return Mathf.InverseLerp(min, max, value);
        }

        public float FromNormalized(float t)
        {
            t = Mathf.Clamp01(t);
            return log ? min * Mathf.Pow(max / min, t) : Mathf.Lerp(min, max, t);
        }

        public float Stepped(float value, int direction)
        {
            int k = Mathf.RoundToInt(Normalized(value) * steps) + direction;
            return FromNormalized(Mathf.Clamp(k, 0, steps) / (float)steps);
        }
    }

    public sealed class SplatFeelDial : MonoBehaviour
    {
        const string Tag = "[MP]";

        [SerializeField] SplatTuning tuning;
        [SerializeField] MonoBehaviour microgestureSource;
        [SerializeField] TextMesh label;
        [SerializeField] Transform anchor;
        [SerializeField] Transform head;
        [SerializeField] Handedness hand = Handedness.Right;
        [SerializeField] Vector3 offset = new Vector3(-0.13f, 0.02f, 0f);
        [SerializeField] float repeatGuardSeconds = 0.22f;
        [SerializeField] bool startVisible = true;

        readonly List<SplatDialRow> _rows = new List<SplatDialRow>();
        readonly StringBuilder _text = new StringBuilder(1024);
        IMicrogestureInput _input;
        Microgesture _last;
        float _lastTime = -10f;
        float _saveAt = -1f;
        int _selected;
        bool _visible;
        bool _dirty = true;

        public IReadOnlyList<SplatDialRow> Rows => _rows;
        public int Selected => _selected;
        public bool Visible => _visible;

        void Awake()
        {
            _input = microgestureSource as IMicrogestureInput;
            _visible = startVisible;
            BuildRows();
        }

        void Row(string name, Func<float> get, Action<float> set, float min, float max, bool log = false, int steps = 16)
        {
            _rows.Add(new SplatDialRow { name = name, get = get, set = set, min = min, max = max, log = log, steps = steps });
        }

        void Toggle(string name, Func<bool> get, Action<bool> set)
        {
            _rows.Add(new SplatDialRow { name = name, get = () => get() ? 1f : 0f, set = v => set(v > 0.5f), min = 0f, max = 1f, steps = 1 });
        }

        void Command(string name, Action act)
        {
            _rows.Add(new SplatDialRow { name = name, act = act });
        }

        public void BuildRows()
        {
            _rows.Clear();
            if (tuning == null) return;
            SplatTuning t = tuning;
            Row("Weight", () => t.feelWeight, v => { t.feelWeight = v; t.DeriveFromFeel(); }, 0f, 1f, false, 10);
            Row("Cohesion*", () => t.feelCohesion, v => { t.feelCohesion = v; t.DeriveFromFeel(); }, 0f, 1f, false, 10);
            Row("Viscosity*", () => t.feelViscosity, v => { t.feelViscosity = v; t.DeriveFromFeel(); }, 0f, 1f, false, 10);
            Row("Bounce", () => t.feelBounce, v => { t.feelBounce = v; t.DeriveFromFeel(); }, 0f, 1f, false, 10);
            Row("Grain*", () => t.feelGrain, v => { t.feelGrain = v; t.DeriveFromFeel(); }, 0f, 1f, false, 10);
            Row("Fluff", () => t.feelFluff, v => { t.feelFluff = v; t.DeriveFromFeel(); }, 0f, 1f, false, 10);
            Row("Fluff length", () => t.fluffLength, v => t.fluffLength = v, 0.5f, 8f, false, 15);
            Row("Spring", () => t.homeStiffness, v => t.homeStiffness = v, 20f, 600f, true);
            Row("Damping", () => t.homeDamping, v => t.homeDamping = v, 0.5f, 30f, true);
            Row("Return limit", () => t.maxSpringAccel, v => t.maxSpringAccel = v, 2f, 80f, true);
            Row("Cohesion", () => t.cohesion, v => t.cohesion = v, 0f, 1f);
            Row("Blob spring", () => t.clusterStiffness, v => t.clusterStiffness = v, 10f, 400f, true);
            Row("Blob wobble damp", () => t.clusterDamping, v => t.clusterDamping = v, 0.3f, 20f, true);
            Row("Viscosity", () => t.clusterViscosity, v => t.clusterViscosity = v, 0f, 80f);
            Row("Gather", () => t.clusterGather, v => t.clusterGather = v, 0.1f, 1f);
            Row("Push", () => t.handPush, v => t.handPush = v, 5f, 200f, true);
            Row("Drag", () => t.handDrag, v => t.handDrag = v, 0f, 60f);
            Row("Attract", () => t.handAttract, v => t.handAttract = v, 0f, 10f);
            Row("Stick", () => t.stickFraction, v => t.stickFraction = v, 0f, 0.6f);
            Row("Slosh", () => 1f - t.carry, v => t.carry = 1f - v, 0f, 1f);
            Row("Weight", () => t.weight, v => t.weight = v, 0f, 9.8f);
            Row("Grain", () => t.grain, v => t.grain = v, 0.3f, 2f);
            Row("Stretch", () => t.stretchMax, v => t.stretchMax = v, 0f, 6f);
            Row("Overlay", () => t.overlay, v => t.overlay = v, 0f, 1f, false, 4);
            Row("Spread speed", () => t.applySpeed, v => t.applySpeed = v, 0.05f, 1.5f, true);
            Row("Room dim", () => -t.passthroughBrightness, v => t.passthroughBrightness = -v, 0f, 1f);
            Row("Look mode", () => t.shaderMode, v => t.shaderMode = Mathf.RoundToInt(v), -1f, 5f, false, 6);
            Toggle("Live camera", () => t.liveCamera, v => t.liveCamera = v);
            Command("Catch", t.Catch);
            Command("Release catch", t.ReleaseCatch);
            Command("Next shape", t.NextShape);
            Command("Freeze", t.Freeze);
            Command("Save still", t.SaveStill);
            Command("Summon / dissolve", t.ToggleSummon);
            Command("Recenter", t.Recenter);
            _selected = Mathf.Clamp(_selected, 0, _rows.Count - 1);
            _dirty = true;
        }

        public void Apply(Microgesture gesture)
        {
            if (gesture == Microgesture.None || _rows.Count == 0) return;
            if (gesture == Microgesture.ThumbTap)
            {
                _visible = !_visible;
                _dirty = true;
                return;
            }
            if (!_visible) return;

            SplatDialRow row = _rows[_selected];
            switch (gesture)
            {
                case Microgesture.SwipeForward:
                    _selected = (_selected + _rows.Count - 1) % _rows.Count;
                    break;
                case Microgesture.SwipeBackward:
                    _selected = (_selected + 1) % _rows.Count;
                    break;
                case Microgesture.SwipeRight:
                    if (row.IsAction) row.act();
                    else Change(row, 1);
                    break;
                case Microgesture.SwipeLeft:
                    if (!row.IsAction) Change(row, -1);
                    break;
            }
            _dirty = true;
        }

        void Change(SplatDialRow row, int direction)
        {
            float before = row.get();
            float after = row.Stepped(before, direction);
            row.set(after);
            _saveAt = Time.unscaledTime + 1.5f;
            Debug.Log($"{Tag} dial {row.name} {before:0.###} -> {row.get():0.###}");
        }

        void Update()
        {
            if (_input != null)
            {
                Microgesture g = _input.Current(hand);
                bool fresh = g != Microgesture.None && (g != _last || Time.unscaledTime - _lastTime >= repeatGuardSeconds);
                if (fresh)
                {
                    _lastTime = Time.unscaledTime;
                    Apply(g);
                }
                _last = g;
            }
            if (_saveAt > 0f && Time.unscaledTime >= _saveAt && tuning != null)
            {
                _saveAt = -1f;
                tuning.SaveTuning();
            }
        }

        void LateUpdate()
        {
            if (label == null) return;
            if (label.gameObject.activeSelf != _visible) label.gameObject.SetActive(_visible);
            if (!_visible) return;
            if (anchor != null && head != null)
            {
                Vector3 view = Vector3.ProjectOnPlane(anchor.position - head.position, Vector3.up);
                if (view.sqrMagnitude > 1e-6f)
                {
                    view.Normalize();
                    Vector3 right = Vector3.Cross(Vector3.up, view);
                    Vector3 pos = anchor.position + right * offset.x + Vector3.up * offset.y + view * offset.z;
                    label.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(Vector3.ProjectOnPlane(pos - head.position, Vector3.up).normalized, Vector3.up));
                }
            }
            if (_dirty) Redraw();
        }

        public string Compose()
        {
            _text.Clear();
            for (int i = 0; i < _rows.Count; i++)
            {
                SplatDialRow row = _rows[i];
                bool selected = i == _selected;
                if (selected) _text.Append("<color=#FFD166>> ");
                else _text.Append("   ");
                _text.Append(row.name);
                if (!row.IsAction)
                {
                    _text.Append("  ");
                    _text.Append(row.steps == 1 ? (row.get() > 0.5f ? "on" : "off") : row.get().ToString("0.###"));
                }
                else if (selected) _text.Append("  (swipe right)");
                if (selected) _text.Append("</color>");
                _text.Append('\n');
            }
            SplatDialRow current = _rows.Count > 0 ? _rows[_selected] : null;
            if (current != null && !current.IsAction)
            {
                int filled = Mathf.Clamp(Mathf.RoundToInt(current.Normalized(current.get()) * 16f), 0, 16);
                _text.Append("<color=#FFD166>");
                _text.Append('|', filled);
                _text.Append("</color>");
                _text.Append('.', 16 - filled);
            }
            return _text.ToString();
        }

        void Redraw()
        {
            _dirty = false;
            label.text = Compose();
        }
    }
}
