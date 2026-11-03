using System.Collections.Generic;
using MoodPicker.Hands;
using UnityEngine;

namespace MoodPicker.DeskKeyboard
{
    public class DeskKeyboardExperiment : MonoBehaviour
    {
        const string Tag = "[MP]";

        [SerializeField] MonoBehaviour handInputSource;
        [SerializeField] Transform trackingSpace;
        [SerializeField] Material keyMaterial;
        [SerializeField] Material markerMaterial;
        [SerializeField] AudioSource clickSource;

        [Header("Keyboard")]
        [SerializeField] int rows = 3;
        [SerializeField] int cols = 10;
        [SerializeField] float pitch = 0.022f;
        [SerializeField] float keySize = 0.016f;
        [SerializeField] float keyHeight = 0.006f;
        [SerializeField] float pressDepth = 0.006f;
        [SerializeField] float pressSeconds = 0.09f;

        [Header("Timing")]
        [SerializeField] float predictLead = 0.04f;
        [SerializeField] bool pressOnPredicted = true;

        IHandInput _input;
        RectCalibrator _calibrator;
        TouchDetector _touch;
        DeskPlane _plane;
        Transform _keyboard;
        readonly List<Transform> _keys = new List<Transform>();
        readonly Dictionary<int, float> _pressStart = new Dictionary<int, float>();
        readonly Dictionary<Finger, (float t, int key)> _pending = new Dictionary<Finger, (float, int)>();
        Vector2 _keyboardCenter;
        AudioClip[] _sounds;
        int _preset;
        static readonly string[] PresetNames = { "A_thock", "B_deep_thock", "C_click" };
        static readonly Color Sky = new Color(0x7F / 255f, 0xA6 / 255f, 0xD9 / 255f, 1f);
        int SwitchKey => cols - 1;
        MaterialPropertyBlock _mpb;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        int _taps, _late;
        LineRenderer _outline;
        RectReject _lastState = RectReject.NotTracked;
        float _nextStateLog;
        KeyboardGrab _grab;
        Transform _handle;
        int SpaceKey => rows * cols;
        float HalfWidth => cols * 0.5f * pitch + 0.01f;
        float SpaceZ => KeyZ(rows);
        float SpaceWidth => pitch * 5f - (pitch - keySize);

        void Awake()
        {
            _input = handInputSource as IHandInput;
            int rate = AudioSettings.outputSampleRate;
            _sounds = new[] { MakeThock(rate, 260f, 150f, 0.018f, 900f, 0.10f), MakeThock(rate, 190f, 110f, 0.026f, 600f, 0f), MakeClick(rate) };
            _mpb = new MaterialPropertyBlock();
            _calibrator = new RectCalibrator();
            _calibrator.Completed += OnCalibrated;
            var cfg = AudioSettings.GetConfiguration();
            Debug.Log($"{Tag} kb start dsp={cfg.dspBufferSize} rate={cfg.sampleRate} lead={predictLead * 1000f:0}ms pressOnPredicted={pressOnPredicted}");
        }

        void Update()
        {
            if (_input == null || !_input.TryGetFrame(out HandFrame f)) return;
            if (!_calibrator.Done)
            {
                _calibrator.Update(f);
                UpdateOutline();
                return;
            }
            _grab.Update(f);
            if (_grab.Grabbing)
            {
                MoveKeyboard(_grab.Origin);
                AnimateKeys();
                return;
            }
            _touch.Update(f);
            AnimateKeys();
        }

        void MoveKeyboard(Vector3 origin)
        {
            _plane.origin = origin;
            _touch.plane = _plane;
            _keyboard.localPosition = origin;
        }

        void UpdateOutline()
        {
            if (_outline == null)
            {
                var go = new GameObject("CalibrationOutline");
                go.transform.SetParent(trackingSpace, false);
                _outline = go.AddComponent<LineRenderer>();
                _outline.useWorldSpace = false;
                _outline.loop = true;
                _outline.positionCount = 4;
                _outline.sharedMaterial = markerMaterial;
                _outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var s = _calibrator.State;
            if (s != _lastState && Time.time >= _nextStateLog) { Debug.Log($"{Tag} kb rect state={s} progress={_calibrator.Progress:0.00}"); _lastState = s; _nextStateLog = Time.time + 0.5f; }
            bool show = s == RectReject.None || s == RectReject.Moving || _calibrator.Done;
            _outline.enabled = show;
            if (!show) return;
            _outline.SetPositions(_calibrator.Corners);
            _outline.widthMultiplier = 0.0015f + 0.0035f * _calibrator.Progress;
        }

        void OnCalibrated(DeskPlane plane, Vector3[] corners)
        {
            _plane = plane;
            UpdateOutline();
            Destroy(_outline.gameObject, 0.6f);
            _touch = new TouchDetector(plane) { predictLead = predictLead };
            _touch.Predicted += OnPredicted;
            _touch.Contact += OnContact;
            _keyboardCenter = Vector2.zero;
            BuildKeyboard();
            _grab = new KeyboardGrab(plane.origin, plane.right * -(HalfWidth + 0.035f) + plane.up * 0.01f);
            _grab.Grabbed += o => { _handle.localScale = Vector3.one * 0.02f; Debug.Log($"{Tag} kb grab start height={o.y:0.000}"); };
            _grab.Released += o => { _handle.localScale = Vector3.one * 0.014f; Debug.Log($"{Tag} kb grab end height={o.y:0.000} pos={o.ToString("F3")}"); };
            float width = Vector3.Distance((corners[0] + corners[1]) * 0.5f, (corners[2] + corners[3]) * 0.5f);
            Debug.Log($"{Tag} kb calib done height={plane.origin.y:0.000} right={plane.right.ToString("F2")} width={width:0.000}m");
        }

        void BuildKeyboard()
        {
            _keyboard = new GameObject("Keyboard").transform;
            _keyboard.SetParent(trackingSpace, false);
            _keyboard.localPosition = _plane.ToWorld(new Vector3(_keyboardCenter.x, 0f, _keyboardCenter.y));
            _keyboard.localRotation = _plane.Rotation;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    var k = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(k.GetComponent<Collider>());
                    k.name = $"Key_{r}_{c}";
                    k.transform.SetParent(_keyboard, false);
                    k.transform.localScale = new Vector3(keySize, keyHeight, keySize);
                    k.transform.localPosition = new Vector3(KeyX(c, r), keyHeight * 0.5f, KeyZ(r));
                    k.GetComponent<Renderer>().sharedMaterial = keyMaterial;
                    if (r * cols + c == SwitchKey) { _mpb.SetColor(BaseColor, Sky); k.GetComponent<Renderer>().SetPropertyBlock(_mpb); }
                    _keys.Add(k.transform);
                }

            var space = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(space.GetComponent<Collider>());
            space.name = "Key_Space";
            space.transform.SetParent(_keyboard, false);
            space.transform.localScale = new Vector3(SpaceWidth, keyHeight, keySize);
            space.transform.localPosition = new Vector3(0f, keyHeight * 0.5f, SpaceZ);
            space.GetComponent<Renderer>().sharedMaterial = keyMaterial;
            _keys.Add(space.transform);

            var h = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(h.GetComponent<Collider>());
            h.name = "Handle";
            h.transform.SetParent(_keyboard, false);
            h.transform.localPosition = new Vector3(-(HalfWidth + 0.035f), 0.01f, 0f);
            h.transform.localScale = Vector3.one * 0.014f;
            h.GetComponent<Renderer>().sharedMaterial = markerMaterial;
            _handle = h.transform;
        }

        float KeyX(int c, int r) => (c - (cols - 1) * 0.5f) * pitch + r * pitch * 0.25f;
        float KeyZ(int r) => ((rows - 1) * 0.5f - r) * pitch;

        int NearestKey(Vector3 local)
        {
            float x = local.x - _keyboardCenter.x, z = local.z - _keyboardCenter.y;
            if (Mathf.Abs(x) <= SpaceWidth * 0.5f + 0.003f && Mathf.Abs(z - SpaceZ) <= pitch * 0.5f) return SpaceKey;
            int best = -1; float bestD = pitch * 0.75f;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    float d = Vector2.Distance(new Vector2(x, z), new Vector2(KeyX(c, r), KeyZ(r)));
                    if (d < bestD) { bestD = d; best = r * cols + c; }
                }
            return best;
        }

        void OnPredicted(TouchEvent e)
        {
            int key = NearestKey(e.local);
            if (key < 0) return;
            clickSource.PlayOneShot(_sounds[_preset]);
            if (pressOnPredicted || e.late) _pressStart[key] = Time.time;
            _pending[e.finger] = (e.t, key);
        }

        void OnContact(TouchEvent e)
        {
            int key = NearestKey(e.local);
            if (key < 0) return;
            _taps++;
            if (e.late) _late++;
            if (key == SwitchKey)
            {
                _preset = (_preset + 1) % _sounds.Length;
                Debug.Log($"{Tag} kb sound preset={PresetNames[_preset]}");
            }
            if (!pressOnPredicted) _pressStart[key] = Time.time;
            float lead = _pending.TryGetValue(e.finger, out var p) ? (e.t - p.t) * 1000f : -1f;
            _pending.Remove(e.finger);
            Debug.Log($"{Tag} kb tap n={_taps} finger={e.finger} key={key} late={e.late} leadMs={lead:0} speed={e.speed:0.00} lateTotal={_late}");
        }

        void AnimateKeys()
        {
            if (_pressStart.Count == 0) return;
            var done = new List<int>();
            foreach (var kv in _pressStart)
            {
                float u = (Time.time - kv.Value) / pressSeconds;
                Transform k = _keys[kv.Key];
                float depth = u < 0.35f ? Mathf.SmoothStep(0f, 1f, u / 0.35f) : Mathf.SmoothStep(1f, 0f, (u - 0.35f) / 0.65f);
                if (u >= 1f) { depth = 0f; done.Add(kv.Key); }
                k.localPosition = new Vector3(k.localPosition.x, keyHeight * 0.5f - pressDepth * depth, k.localPosition.z);
                _mpb.SetColor(BaseColor, Color.Lerp(BaseOf(kv.Key), kv.Key == SwitchKey ? Color.white : Sky, depth));
                k.GetComponent<Renderer>().SetPropertyBlock(_mpb);
            }
            foreach (int k in done) _pressStart.Remove(k);
        }

        Color BaseOf(int key) => key == SwitchKey ? Sky : keyMaterial.GetColor(BaseColor);

        static AudioClip MakeThock(int rate, float f0, float f1, float decay, float noiseCutoff, float tick)
        {
            int n = Mathf.CeilToInt(rate * 0.09f);
            var data = new float[n];
            var rng = new System.Random(5);
            float phase = 0f, lp = 0f;
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * noiseCutoff / rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float f = f1 + (f0 - f1) * Mathf.Exp(-t / 0.012f);
                phase += 2f * Mathf.PI * f / rate;
                float attack = Mathf.Clamp01(t / 0.0015f);
                float body = Mathf.Sin(phase) * attack * Mathf.Exp(-t / decay);
                lp += a * (((float)rng.NextDouble() * 2f - 1f) - lp);
                float squish = lp * 2.2f * Mathf.Exp(-t / 0.008f);
                float tk = tick * Mathf.Sin(2f * Mathf.PI * 1200f * t) * Mathf.Exp(-t / 0.003f);
                data[i] = Mathf.Clamp((body * 0.8f + squish * 0.45f + tk), -1f, 1f) * 0.9f;
            }
            var clip = AudioClip.Create("kb_thock", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeClick(int rate)
        {
            int n = Mathf.CeilToInt(rate * 0.03f);
            var data = new float[n];
            var rng = new System.Random(3);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Exp(-t * 260f);
                float tone = Mathf.Sin(2f * Mathf.PI * 3100f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 1700f * t) * 0.3f;
                float noise = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 900f);
                data[i] = (tone * env + noise * 0.8f) * 0.7f;
            }
            var clip = AudioClip.Create("kb_click", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
