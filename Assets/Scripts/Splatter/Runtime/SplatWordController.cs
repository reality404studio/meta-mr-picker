using System.Diagnostics;
using MoodPicker.Splatter.Sim;
using Pigment;
using UnityEngine;

namespace MoodPicker.Splatter
{
    public struct SplatHandPoints
    {
        public const int Count = 6;

        public bool tracked;
        public bool pinching;
        public Vector3 thumb;
        public Vector3 index;
        public Vector3 middle;
        public Vector3 ring;
        public Vector3 little;
        public Vector3 palm;

        public Vector3 Pinch => 0.5f * (thumb + index);

        public Vector3 this[int i]
        {
            get
            {
                switch (i)
                {
                    case 0: return thumb;
                    case 1: return index;
                    case 2: return middle;
                    case 3: return ring;
                    case 4: return little;
                    default: return palm;
                }
            }
        }
    }

    public sealed class SplatWordController : MonoBehaviour
    {
        const float MaxHandSpeed = 3f;

        [SerializeField] SplatLetterRenderer[] letters;
        [SerializeField] SplatPigmentSlotAsset[] presetSlots;
        [SerializeField] Transform head;
        [SerializeField] SplatTuning tuning;
        [SerializeField] float placeDistance = 0.38f;
        [SerializeField] float placeOffsetY = -0.06f;
        [SerializeField] float letterSize = 0.12f;
        [SerializeField] float triplanarScale = 1.15f;
        [SerializeField] float intensity = 1.15f;
        [SerializeField] bool outlineEnabled = false;
        [SerializeField] bool autoFillSampleSlot0 = true;
        [SerializeField] bool summonOnPlace = true;

        readonly BuiltPigmentSlot[] _slots = new BuiltPigmentSlot[SplatWordState.MaxSlots];
        readonly Texture2D[] _slotTextures = new Texture2D[SplatWordState.MaxSlots];
        readonly bool[] _ownsTexture = new bool[SplatWordState.MaxSlots];
        readonly float[] _slotFrames = new float[SplatWordState.MaxSlots];
        readonly SplatCollider[] _colliders = new SplatCollider[SplatHandPoints.Count * 2];
        readonly Vector3[] _prevPoint = new Vector3[SplatHandPoints.Count * 2];
        readonly Vector3[] _pointVelocity = new Vector3[SplatHandPoints.Count * 2];
        readonly bool[] _hadHand = new bool[2];
        readonly Stopwatch _watch = new Stopwatch();

        SplatWordState _state;
        int _colliderCount;
        int _placeFrames;
        bool _placed;
        int _strideFrames;
        float _strideDt;
        float _awakeSmoothed;
        float _nextSimLog;
        Texture _overrideTexture;
        Vector4 _overrideCrop = new Vector4(1f, 1f, 0f, 0f);
        float _overrideGamma = 1f;

        float _wordScale = 1f;
        bool _twoHandGrabbing;
        Vector3 _grabStartMidWS;
        float _grabStartSpan = 0.2f;
        Vector3 _grabStartWordPos;
        float _grabStartWordScale = 1f;
        float _grabStartYawDeg;
        Quaternion _grabStartWordRot = Quaternion.identity;

        Vector3 _emitterWS;
        Vector3 _emitterVelocityWS;
        bool _hasEmitter;
        bool _wasLeftPinching;
        float _lastToggleTime = -10f;

        public SplatWordState State => _state;
        public float WordScale => _wordScale;
        public bool IsTwoHandGrabbing => _twoHandGrabbing;
        public int ColliderCount => _colliderCount;

        public SplatSimMode SimMode
        {
            get
            {
                SplatSimRunner sim = FirstSim();
                return sim != null ? sim.Mode : SplatSimMode.Formed;
            }
        }

        void Awake()
        {
            _wordScale = Mathf.Max(0.1f, transform.localScale.x);
            InitializeIfNeeded();
        }

        void OnEnable()
        {
            if (tuning == null) return;
            tuning.RecenterRequested += Recenter;
            tuning.ToggleRequested += ToggleSummon;
        }

        void OnDisable()
        {
            if (tuning == null) return;
            tuning.RecenterRequested -= Recenter;
            tuning.ToggleRequested -= ToggleSummon;
        }

        SplatSimRunner FirstSim()
        {
            if (letters == null) return null;
            for (int i = 0; i < letters.Length; i++)
                if (letters[i] != null && letters[i].Sim != null) return letters[i].Sim;
            return null;
        }

        public void InitializeIfNeeded()
        {
            if (letters == null || letters.Length == 0)
                letters = GetComponentsInChildren<SplatLetterRenderer>();

            int count = letters != null ? letters.Length : 0;
            if (count > 0 && _state == null)
            {
                _state = new SplatWordState(count, 0, SplatShaderMode.Raw);
                _state.SetSharedParameters(triplanarScale, intensity, outlineEnabled);
                for (int i = 0; i < count; i++)
                    if (letters[i] != null) _state.SetLetterDirect(i, letters[i].AssignedSlot, letters[i].Mode);
            }

            if (presetSlots != null)
            {
                for (int s = 0; s < presetSlots.Length && s < SplatWordState.MaxSlots; s++)
                    if (presetSlots[s] != null && presetSlots[s].atlasTexture != null && _slots[s] == null)
                        SetSlotAsset(s, presetSlots[s]);
            }

            if (autoFillSampleSlot0 && _slots[0] == null)
                SetSlot(0, PigmentAtlasBuilder.BuildSampleWaterWaveSlot(64, 24, 15f));
        }

        void ReleaseOwnedTexture(int slotIndex)
        {
            if (!_ownsTexture[slotIndex] || _slotTextures[slotIndex] == null) return;
            if (Application.isPlaying) Destroy(_slotTextures[slotIndex]);
            else DestroyImmediate(_slotTextures[slotIndex]);
        }

        public void SetSlotAsset(int slotIndex, SplatPigmentSlotAsset asset)
        {
            if (slotIndex < 0 || slotIndex >= SplatWordState.MaxSlots || asset == null || asset.atlasTexture == null) return;
            ReleaseOwnedTexture(slotIndex);
            _slots[slotIndex] = new BuiltPigmentSlot
            {
                Layout = asset.ToLayout(),
                Palette = asset.ToPalette(),
                SeamErrorBefore = asset.seamErrorBefore,
                SeamErrorAfter = asset.seamErrorAfter
            };
            _slotTextures[slotIndex] = asset.atlasTexture;
            _ownsTexture[slotIndex] = false;
        }

        public void SetSlot(int slotIndex, BuiltPigmentSlot built)
        {
            if (slotIndex < 0 || slotIndex >= SplatWordState.MaxSlots || built == null) return;
            ReleaseOwnedTexture(slotIndex);
            _slots[slotIndex] = built;
            var tex = new Texture2D(built.Layout.Width, built.Layout.Height, TextureFormat.RGBA32, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            tex.SetPixelData(built.AtlasRgba, 0);
            tex.Apply(false, false);
            _slotTextures[slotIndex] = tex;
            _ownsTexture[slotIndex] = true;
        }

        public void SetOverrideTexture(Texture texture, Vector4 crop, float gamma)
        {
            _overrideTexture = texture;
            _overrideCrop = crop;
            _overrideGamma = gamma;
        }

        public void ClearOverrideTexture()
        {
            _overrideTexture = null;
        }

        int ClusterSize => tuning != null ? Mathf.Max(8, tuning.particlesPerCluster) : 300;

        SplatSimParams CurrentParams()
        {
            float floorBelow = tuning != null ? tuning.floorBelow : 0.45f;
            float floorY = transform.position.y - floorBelow;
            if (tuning != null) return tuning.ToSimParams(floorY);
            SplatSimParams p = SplatSimParams.WaterDroplet;
            p.floorY = floorY;
            return p;
        }

        Vector3 FallbackEmitter()
        {
            if (head != null) return head.position + head.forward * 0.22f + Vector3.down * 0.14f;
            return transform.position + Vector3.down * 0.2f;
        }

        public void BeginSummon(Vector3 emitterWS)
        {
            if (letters == null) return;
            float spread = tuning != null ? tuning.summonSpread : 1.4f;
            _emitterWS = emitterWS;
            for (int i = 0; i < letters.Length; i++)
            {
                if (letters[i] == null) continue;
                SplatSimRunner sim = letters[i].EnsureSim(ClusterSize);
                sim?.BeginSummon(letters[i].transform.localToWorldMatrix, emitterWS, spread, 0.9f);
            }
        }

        public void BeginDissolve()
        {
            if (letters == null) return;
            float sag = tuning != null ? tuning.dissolveSag : 0.2f;
            float spread = tuning != null ? tuning.dissolveSpread : 0.4f;
            for (int i = 0; i < letters.Length; i++)
            {
                if (letters[i] == null) continue;
                SplatSimRunner sim = letters[i].EnsureSim(ClusterSize);
                sim?.BeginDissolve(sag, spread, 2.2f);
            }
        }

        public void ToggleSummon()
        {
            SplatSimMode mode = SimMode;
            if (mode == SplatSimMode.Formed || mode == SplatSimMode.Summoning) BeginDissolve();
            else BeginSummon(_hasEmitter ? _emitterWS : FallbackEmitter());
            UnityEngine.Debug.Log($"[MP] toggle from={mode} to={SimMode}");
        }

        public void Recenter()
        {
            if (head == null) return;
            Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            transform.position = head.position + fwd * placeDistance + Vector3.up * placeOffsetY;
            transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        void TrackHand(int handIndex, in SplatHandPoints hand, float dt)
        {
            int baseIndex = handIndex * SplatHandPoints.Count;
            if (!hand.tracked)
            {
                _hadHand[handIndex] = false;
                for (int k = 0; k < SplatHandPoints.Count; k++) _pointVelocity[baseIndex + k] = Vector3.zero;
                return;
            }
            float blend = 1f - Mathf.Exp(-20f * dt);
            for (int k = 0; k < SplatHandPoints.Count; k++)
            {
                Vector3 p = hand[k];
                Vector3 raw = _hadHand[handIndex] ? (p - _prevPoint[baseIndex + k]) / dt : Vector3.zero;
                _pointVelocity[baseIndex + k] = Vector3.Lerp(_pointVelocity[baseIndex + k], Vector3.ClampMagnitude(raw, MaxHandSpeed), blend);
                _prevPoint[baseIndex + k] = p;
            }
            _hadHand[handIndex] = true;
        }

        void AddColliders(int handIndex, in SplatHandPoints hand)
        {
            if (!hand.tracked) return;
            float tip = tuning != null ? tuning.tipRadius : 0.022f;
            float palm = tuning != null ? tuning.palmRadius : 0.045f;
            int baseIndex = handIndex * SplatHandPoints.Count;
            for (int k = 0; k < SplatHandPoints.Count; k++)
            {
                _colliders[_colliderCount++] = new SplatCollider
                {
                    position = hand[k],
                    velocity = _pointVelocity[baseIndex + k],
                    radius = k == SplatHandPoints.Count - 1 ? palm : tip,
                    strength = 1f
                };
            }
        }

        public void UpdateHands(in SplatHandPoints left, in SplatHandPoints right, float timeSeconds, float dt)
        {
            float safeDt = Mathf.Max(dt, 0.001f);
            TrackHand(0, left, safeDt);
            TrackHand(1, right, safeDt);
            _colliderCount = 0;

            if (left.tracked)
            {
                _emitterWS = left.Pinch;
                _emitterVelocityWS = 0.5f * (_pointVelocity[0] + _pointVelocity[1]);
                _hasEmitter = true;
            }

            bool formed = SimMode == SplatSimMode.Formed;
            bool bothPinching = left.tracked && right.tracked && left.pinching && right.pinching;
            if (bothPinching && formed)
            {
                Vector3 l = left.Pinch;
                Vector3 r = right.Pinch;
                Vector3 midWS = 0.5f * (l + r);
                float span = Mathf.Max(0.04f, Vector3.Distance(l, r));
                Vector3 handLine = r - l;
                handLine.y = 0f;
                float currentYaw = handLine.sqrMagnitude > 1e-4f ? Mathf.Atan2(handLine.z, handLine.x) * Mathf.Rad2Deg : 0f;

                if (!_twoHandGrabbing)
                {
                    float maxGrabDist = Mathf.Max(0.55f, 0.65f * _wordScale);
                    if (Vector3.Distance(midWS, transform.position) <= maxGrabDist)
                    {
                        _twoHandGrabbing = true;
                        _grabStartMidWS = midWS;
                        _grabStartSpan = span;
                        _grabStartWordPos = transform.position;
                        _grabStartWordScale = Mathf.Max(0.1f, _wordScale);
                        _grabStartYawDeg = currentYaw;
                        _grabStartWordRot = transform.rotation;
                    }
                }
                else
                {
                    float scaleRatio = Mathf.Clamp(span / _grabStartSpan, 0.2f, 4.5f);
                    _wordScale = Mathf.Clamp(_grabStartWordScale * scaleRatio, 0.25f, 3.80f);
                    transform.localScale = Vector3.one * _wordScale;

                    float follow = 1f - Mathf.Exp(-28f * safeDt);
                    transform.position = Vector3.Lerp(transform.position, _grabStartWordPos + (midWS - _grabStartMidWS), follow);
                    if (handLine.sqrMagnitude > 1e-4f)
                    {
                        float yawDelta = Mathf.DeltaAngle(_grabStartYawDeg, currentYaw);
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, -yawDelta, 0f) * _grabStartWordRot, follow);
                    }
                }
                _wasLeftPinching = false;
                return;
            }

            _twoHandGrabbing = false;

            bool leftOnlyPinch = left.tracked && left.pinching && !(right.tracked && right.pinching);
            bool leftTriggered = leftOnlyPinch && !_wasLeftPinching;
            _wasLeftPinching = leftOnlyPinch;
            if (leftTriggered && timeSeconds - _lastToggleTime >= 0.45f)
            {
                _lastToggleTime = timeSeconds;
                ToggleSummon();
            }

            AddColliders(0, left);
            AddColliders(1, right);
        }

        public void SetColliders(SplatCollider[] source, int count)
        {
            _colliderCount = source != null ? Mathf.Min(count, _colliders.Length) : 0;
            for (int i = 0; i < _colliderCount; i++) _colliders[i] = source[i];
        }

        public void StepSimulation(float dt)
        {
            if (letters == null) return;
            int stride = tuning != null ? Mathf.Clamp(tuning.stepEveryFrames, 1, 3) : 1;
            _strideFrames++;
            _strideDt += dt;
            if (_strideFrames < stride) return;
            float stepDt = _strideDt;
            _strideFrames = 0;
            _strideDt = 0f;

            SplatSimParams p = CurrentParams();
            bool frozen = tuning != null && tuning.frozen;
            bool anyAwake = false;
            float activity = 0f;
            int active = 0;
            _watch.Restart();
            for (int i = 0; i < letters.Length; i++)
            {
                if (letters[i] == null) continue;
                SplatSimRunner sim = letters[i].EnsureSim(ClusterSize);
                if (sim == null) continue;
                sim.Frozen = frozen;
                sim.Step(letters[i].transform.localToWorldMatrix, stepDt, p, _colliders, _colliderCount, _emitterWS, _emitterVelocityWS);
                anyAwake |= sim.DrivesShader && sim.Mode != SplatSimMode.Hidden && !sim.Asleep;
                activity = Mathf.Max(activity, sim.Activity);
                active = Mathf.Max(active, sim.LastColliderCount);
            }
            _watch.Stop();

            if (tuning == null) return;
            float ms = (float)_watch.Elapsed.TotalMilliseconds;
            tuning.simMilliseconds = Mathf.Lerp(tuning.simMilliseconds, ms, 0.1f);
            _awakeSmoothed = Mathf.Lerp(_awakeSmoothed, anyAwake ? 1f : 0f, 0.02f);
            tuning.awakeFraction = _awakeSmoothed;
            tuning.simAsleep = !anyAwake;
            tuning.activity = activity;
            tuning.activeColliders = active;
            if (Time.unscaledTime >= _nextSimLog)
            {
                _nextSimLog = Time.unscaledTime + 5f;
                SplatSimRunner first = FirstSim();
                UnityEngine.Debug.Log($"[MP] sim ms={tuning.simMilliseconds:0.00} awake={_awakeSmoothed:0.00} colliders={active} mode={SimMode} activity={activity:E2} moved={first != null && first.LastMoved} touching={first != null && first.LastTouching} wakes={(first != null ? first.WakeCount : 0)} damping={tuning.homeDamping:0.00} cohesion={tuning.cohesion:0.00} stride={tuning.stepEveryFrames}");
            }
        }

        public void StepAnimationAndMaterials(float timeSeconds)
        {
            InitializeIfNeeded();
            if (letters == null || _state == null) return;

            float scale = tuning != null ? tuning.triplanarScale : triplanarScale;
            float gain = tuning != null ? tuning.intensity : intensity;
            _state.SetSharedParameters(scale, gain, outlineEnabled);

            SplatLook look = SplatLook.Default;
            if (tuning != null)
            {
                look.flowAmplitude = tuning.flowAmplitude;
                look.flowSpeed = tuning.flowSpeed;
                look.contrast = tuning.contrast;
                look.detachDistance = tuning.detachDistance;
                look.stretchSeconds = tuning.stretchSeconds;
                look.stretchMax = tuning.stretchMax;
                look.grain = tuning.grain;
                look.glowGain = tuning.glowGain;
                look.sparkle = tuning.sparkle;
                look.glintAmount = tuning.glintAmount;
                look.glintSpeed = tuning.glintSpeed;
                look.glintScale = tuning.glintScale;
                look.glintRough = tuning.glintRough;
                look.glintSharpness = tuning.glintSharpness;
                look.glintSize = tuning.glintSize;
                look.glintMaterial = tuning.glintMaterial;
            }

            for (int s = 0; s < SplatWordState.MaxSlots; s++)
            {
                BuiltPigmentSlot slot = _slots[s];
                _slotFrames[s] = slot != null
                    ? LetterPoseMath.SlotFrameContinuous(timeSeconds, slot.Layout.Fps, slot.Layout.Frames)
                    : 0f;
            }

            for (int i = 0; i < letters.Length; i++)
            {
                SplatLetterRenderer letter = letters[i];
                if (letter == null) continue;

                int slotIdx = _state.GetSlot(i);
                letter.AssignedSlot = slotIdx;
                letter.Mode = tuning != null ? (SplatShaderMode)Mathf.Clamp(tuning.shaderMode, 0, 4) : _state.GetMode(i);
                letter.SetLook(look);

                bool hasSlot = slotIdx >= 0 && slotIdx < SplatWordState.MaxSlots && _slots[slotIdx] != null && _slotTextures[slotIdx] != null;
                letter.SetCrop(_overrideTexture != null, _overrideCrop, _overrideGamma);
                if (_overrideTexture != null)
                {
                    letter.ApplyPigmentState(_overrideTexture, new FlipbookLayout { Frames = 1, Cols = 1, Rows = 1, Tile = 256, Fps = 1f }, 0f, true,
                        hasSlot ? _slots[slotIdx].Palette : default, _state.TriplanarScale, _state.Intensity, _state.OutlineEnabled);
                }
                else if (hasSlot)
                {
                    BuiltPigmentSlot slot = _slots[slotIdx];
                    letter.ApplyPigmentState(_slotTextures[slotIdx], slot.Layout, _slotFrames[slotIdx], true, slot.Palette,
                        _state.TriplanarScale, _state.Intensity, _state.OutlineEnabled);
                }
                else
                {
                    letter.ApplyPigmentState(null, default, 0f, false, default,
                        _state.TriplanarScale, _state.Intensity, _state.OutlineEnabled);
                }
            }
        }

        void PlaceWhenHeadIsReady()
        {
            if (_placed || head == null) return;
            if (_placeFrames == 0 && summonOnPlace && letters != null)
            {
                for (int i = 0; i < letters.Length; i++)
                    if (letters[i] != null) letters[i].EnsureSim(ClusterSize)?.Hide();
            }
            _placeFrames++;
            Recenter();
            if (head.position.sqrMagnitude <= 1e-4f && _placeFrames < 20) return;
            _placed = true;
            if (summonOnPlace) BeginSummon(FallbackEmitter());
        }

        void Update()
        {
            PlaceWhenHeadIsReady();
            StepSimulation(Time.deltaTime);
            StepAnimationAndMaterials(Time.time);
        }

        void OnDestroy()
        {
            for (int i = 0; i < _slotTextures.Length; i++) ReleaseOwnedTexture(i);
        }
    }
}
