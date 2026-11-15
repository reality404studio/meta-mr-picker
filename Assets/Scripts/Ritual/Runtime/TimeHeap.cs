using MoodPicker.Splatter;
using UnityEngine;

namespace MoodPicker.Ritual
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(SplatLetterRenderer))]
    public sealed class TimeHeap : MonoBehaviour
    {
        [SerializeField] TimeCategoryAsset category;
        [SerializeField] TimeRitualConfig config;
        [SerializeField] SplatTuning tuning;
        [SerializeField] Material splatMaterial;
        [SerializeField] Mesh heapMesh;
        [SerializeField] int seed = 777;

        SplatLetterRenderer _body;
        SplatSurfaceMaterial _look;
        MeshRenderer _renderer;
        float _pulse;
        float _hover;
        float _hoverTarget;
        float _presence = 1f;
        float _presenceTarget = 1f;
        Vector3 _baseScale = Vector3.one;
        Vector3 _spinAxis = Vector3.right;
        float _spinRate;
        bool _spun;
        bool _ready;
        RitualSpringSwarm _swarm;
        float _invite;
        float _inviteTarget;
        Vector3 _inviteToward;

        public TimeCategoryAsset Category => category;
        public SplatSurfaceMaterial Look => _look;
        public float Radius => (config != null ? config.heapDiameter * 0.5f : 0.03f) * _presence;
        public bool Present => _presence > 0.5f && _presenceTarget > 0.5f;
        public float SpinRate => _spinRate;
        public bool Swarming => _swarm != null;
        public RitualSpringSwarm Swarm => _swarm;
        public float Invitation => _invite;

        public void Init(SplatSurfaceMaterial look, TimeRitualConfig liveConfig)
        {
            if (_ready) return;
            _ready = true;
            if (liveConfig != null) config = liveConfig;
            _body = GetComponent<SplatLetterRenderer>();
            _renderer = GetComponent<MeshRenderer>();
            RitualUtil.Quiet(_renderer);
            _look = look ?? SplatSurfaceMaterial.Clay("heap");
            _baseScale = transform.localScale;
            int splats = config != null ? config.heapSplats : 1800;
            float size = config != null ? config.heapTextureSize : 0.18f;
            if (config != null && config.springSwarm && splatMaterial != null)
            {
                _swarm = new RitualSpringSwarm(transform, config, tuning, splatMaterial, _look, category);
                _renderer.enabled = false;
            }
            else if (heapMesh != null && splatMaterial != null) _body.RebuildFromMesh(heapMesh, splatMaterial, splats, size, false, seed);
            _body.Mode = (SplatShaderMode)_look.ResolveMode(-1);
        }

        public void Pulse()
        {
            _pulse = config != null ? config.heapPulse : 0.35f;
        }

        public void Hover(float amount)
        {
            _hoverTarget = Mathf.Max(_hoverTarget, Mathf.Clamp01(amount));
        }

        public void Invite(Vector3 towardWS)
        {
            _inviteTarget = 1f;
            _inviteToward = towardWS;
        }

        public void SetPresent(bool present)
        {
            _presenceTarget = present ? 1f : 0f;
        }

        public void Spin(float pulledLength, Vector3 pullDirection, float dt)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, pullDirection);
            if (axis.sqrMagnitude < 1e-6f) axis = transform.right;
            axis.Normalize();
            float gain = config != null ? config.heapSpin : 1f;
            float angle = pulledLength / Mathf.Max(0.005f, Radius) * gain;
            if (Mathf.Abs(angle) > 1e-6f) transform.Rotate(axis, angle * Mathf.Rad2Deg, Space.World);
            _spinAxis = axis;
            _spinRate = Mathf.Lerp(_spinRate, angle / Mathf.Max(dt, 1e-4f), 0.5f);
            _spun = true;
        }

        void Coast(float dt)
        {
            if (_spun)
            {
                _spun = false;
                return;
            }
            if (Mathf.Abs(_spinRate) < 0.01f)
            {
                _spinRate = 0f;
                return;
            }
            transform.Rotate(_spinAxis, _spinRate * dt * Mathf.Rad2Deg, Space.World);
            _spinRate *= Mathf.Exp(-dt * (config != null ? config.heapSpinDrag : 2.5f));
        }

        void OnDestroy()
        {
            _swarm?.Dispose();
            _swarm = null;
        }

        public void Tick(float now, float dt)
        {
            Coast(dt);
            if (!_ready || _body == null) return;
            _pulse = Mathf.MoveTowards(_pulse, 0f, dt * 1.5f);
            _hover = Mathf.Lerp(_hover, _hoverTarget, 1f - Mathf.Exp(-dt * 10f));
            _hoverTarget = 0f;
            _invite = Mathf.MoveTowards(_invite, _inviteTarget, dt * 1.5f);
            _inviteTarget = 0f;
            float fade = config != null ? config.heapFadeSeconds : 1.6f;
            float before = _presence;
            _presence = Mathf.MoveTowards(_presence, _presenceTarget, dt / Mathf.Max(0.05f, fade));
            if (!Mathf.Approximately(before, _presence))
            {
                float s = RitualUtil.Smooth(_presence);
                transform.localScale = _baseScale * Mathf.Max(1e-4f, s);
            }
            bool visible = _presence > 0.002f;
            if (_swarm != null)
            {
                Vector3 to = _inviteToward - transform.position;
                Vector3 lean = to.sqrMagnitude > 1e-8f ? to.normalized * (config != null ? config.hintLean : 0.02f) : Vector3.zero;
                _swarm.Tick(now, dt, _presence, _pulse, _hover, _spinRate, lean, _invite);
                return;
            }
            if (_renderer.enabled != visible) _renderer.enabled = visible;
            if (!visible) return;
            SplatLook look = tuning != null ? tuning.ToLook() : SplatLook.Default;
            _body.SetLook(look);
            float hover = config != null ? config.heapHover : 0.18f;
            var state = new SplatSurfaceState
            {
                current = _look,
                currentFrame = _look.FrameAt(now),
                apply = SplatSurfaceState.NoApply,
                applyShape = new Vector4(0.03f, 0.004f, 0.15f, 0f),
                time = now,
                triplanarScale = tuning != null ? tuning.triplanarScale : 1.15f,
                intensity = (tuning != null ? tuning.intensity : 1.15f) * (1f + _pulse + _hover * hover + _invite * 0.5f * (0.5f + 0.5f * Mathf.Sin(now * 2.2f))),
                outline = false
            };
            _body.ApplyState(state);
        }
    }
}
