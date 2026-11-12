using System.Diagnostics;
using System.Threading.Tasks;
using MoodPicker.Splatter.Sim;
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
        const string Tag = "[MP]";
        const float MaxHandSpeed = 3f;
        public const int ColliderSlots = SplatHandPoints.Count * 2;

        [SerializeField] SplatLetterRenderer[] letters;
        [SerializeField] SplatPigmentSlotAsset[] presetSlots;
        [SerializeField] Transform head;
        [SerializeField] SplatTuning tuning;
        [SerializeField] SplatShelf shelf;
        [SerializeField] SplatStillCapture stillCapture;
        [SerializeField] SplatFeelAsset startFeel;
        [SerializeField] float pokeRadius = 0.07f;
        [SerializeField] float pokeSpeed = 0.35f;
        [SerializeField] Mesh[] shapes;
        [SerializeField] string[] shapeNames;
        [SerializeField] int startShape;
        [SerializeField] float shapeTextureSize = 0.18f;
        [SerializeField] int minShapeSplats = 8000;
        [SerializeField] int maxShapeSplats = 32000;
        [SerializeField] float splatsPerSquareMeter = 1000000f;
        [SerializeField] float placeDistance = 0.38f;
        [SerializeField] float placeOffsetY = -0.06f;
        [SerializeField] float letterSize = 0.12f;
        [SerializeField] float triplanarScale = 1.15f;
        [SerializeField] float intensity = 1.15f;
        [SerializeField] bool outlineEnabled = false;
        [SerializeField] bool autoFillSampleSlot0 = true;
        [SerializeField] bool summonOnPlace = true;

        readonly SplatCollider[] _colliders = new SplatCollider[ColliderSlots];
        readonly Vector3[] _prevPoint = new Vector3[ColliderSlots];
        readonly Vector3[] _pointVelocity = new Vector3[ColliderSlots];
        readonly bool[] _hadHand = new bool[2];
        readonly Stopwatch _watch = new Stopwatch();

        int _colliderCount;
        int _placeFrames;
        bool _placed;
        bool _initialized;
        int _strideFrames;
        float _strideDt;
        float _awakeSmoothed;
        float _nextSimLog;

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

        SplatSurfaceMaterial _current;
        SplatSurfaceMaterial _previous;
        SplatSurfaceMaterial _preview;
        Vector3 _applyOriginOS;
        float _applyRadius = 1000f;
        float _applyMax;

        bool _frozen;
        float _clock;

        int _shapeIndex;
        int _pendingShape = -1;
        bool _morph;
        SplatTransitionShape _morphShape;
        Task<SplatBodyBuild> _pendingBuild;

        public float WordScale => _wordScale;
        public bool IsTwoHandGrabbing => _twoHandGrabbing;
        public bool IsFrozen => _frozen;
        public int ColliderCount => _colliderCount;
        public SplatCollider[] Colliders => _colliders;
        public SplatSurfaceMaterial CurrentMaterial => _current;
        public string ShapeName => shapeNames != null && _shapeIndex < shapeNames.Length ? shapeNames[_shapeIndex] : "";
        public bool ShapeChanging => _pendingShape >= 0;
        public string PendingStatus => _pendingBuild == null ? "none" : _pendingBuild.Status + (_pendingBuild.Exception != null ? " " + _pendingBuild.Exception.GetBaseException().Message : "");

        SplatLetterRenderer Body => letters != null && letters.Length > 0 ? letters[0] : null;

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
            _shapeIndex = Mathf.Max(0, startShape);
            InitializeIfNeeded();
        }

        void OnEnable()
        {
            if (tuning == null) return;
            tuning.RecenterRequested += Recenter;
            tuning.ToggleRequested += ToggleSummon;
            tuning.ShapeRequested += NextShape;
            tuning.FreezeRequested += ToggleFreeze;
            tuning.StillRequested += SaveStill;
        }

        void OnDisable()
        {
            if (tuning == null) return;
            tuning.RecenterRequested -= Recenter;
            tuning.ToggleRequested -= ToggleSummon;
            tuning.ShapeRequested -= NextShape;
            tuning.FreezeRequested -= ToggleFreeze;
            tuning.StillRequested -= SaveStill;
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
            if (_initialized) return;
            _initialized = true;
            if (presetSlots != null)
            {
                for (int s = 0; s < presetSlots.Length && _current == null; s++)
                    _current = SplatSurfaceMaterial.FromAsset(presetSlots[s]);
            }
            if (_current == null && autoFillSampleSlot0)
                _current = SplatSurfaceMaterial.FromBuilt(PigmentAtlasBuilder.BuildSampleWaterWaveSlot(64, 24, 15f), "sample");
            if (tuning != null) tuning.SetFeel(startFeel != null ? startFeel.feel : SplatFeel.Water);
        }

        public void SetPreview(SplatSurfaceMaterial preview)
        {
            _preview = preview;
        }

        public void ClearPreview()
        {
            _preview = null;
        }

        int ClusterSize => tuning != null ? Mathf.Max(8, tuning.particlesPerCluster) : 300;

        public SplatFeel CurrentFeel => tuning != null ? tuning.CurrentFeel : SplatFeel.Water;

        public void ApplyFeel(SplatFeel feel, string label, Vector3 contactWorld)
        {
            if (tuning != null) tuning.SetFeel(feel);
            SplatLetterRenderer body = Body;
            SplatSimRunner sim = body != null ? body.EnsureSim(ClusterSize) : null;
            if (sim != null && sim.Mode == SplatSimMode.Formed)
                sim.Poke(body.transform.localToWorldMatrix, contactWorld, pokeRadius * body.transform.lossyScale.x, pokeSpeed);
            UnityEngine.Debug.Log($"{Tag} apply feel={label} w={feel.weight:0.##} c={feel.cohesion:0.##} v={feel.viscosity:0.##} b={feel.bounce:0.##} g={feel.grain:0.##} f={feel.fluff:0.##} sq={feel.squish:0.##} pl={feel.plastic:0.##} tk={feel.tack:0.##} th={feel.thicken:0.##}");
        }

        public float FloorY => transform.position.y - (tuning != null ? tuning.floorBelow : 0.45f);

        SplatSimParams CurrentParams()
        {
            if (tuning != null) return tuning.ToSimParams(FloorY);
            SplatSimParams p = SplatSimParams.WaterDroplet;
            p.floorY = FloorY;
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
            UnityEngine.Debug.Log($"{Tag} toggle from={mode} to={SimMode}");
        }

        public void ToggleFreeze()
        {
            SetFrozen(!_frozen);
        }

        public void SetFrozen(bool frozen)
        {
            if (_frozen == frozen) return;
            _frozen = frozen;
            if (stillCapture != null && Body != null) stillCapture.ShowToast(frozen ? "freeze" : "resume", Body.transform, head);
            UnityEngine.Debug.Log($"{Tag} freeze={_frozen}");
        }

        public void StepForward()
        {
            switch (SplatFreezeFlow.Forward(_frozen))
            {
                case SplatStepAction.Freeze:
                    SetFrozen(true);
                    break;
                case SplatStepAction.Save:
                    SaveStill();
                    break;
            }
        }

        public void StepBackward()
        {
            if (SplatFreezeFlow.Backward(_frozen) == SplatStepAction.Resume) SetFrozen(false);
        }

        public void SaveStill()
        {
            if (stillCapture == null || Body == null) return;
            bool started = stillCapture.Capture(head, Body);
            UnityEngine.Debug.Log($"{Tag} still requested started={started} frozen={_frozen} shape={ShapeName}");
        }

        public void NextShape()
        {
            CycleShape(1);
        }

        public void CycleShape(int direction)
        {
            if (shapes == null || shapes.Length == 0 || _pendingShape >= 0 || Body == null) return;
            SetFrozen(false);
            int next = SplatShapeCycle.Step(_shapeIndex, direction, shapes.Length);
            if (next == _shapeIndex || shapes[next] == null) return;
            _pendingShape = next;
            _pendingBuild = SplatLetterRenderer.BuildAsync(shapes[next], splatsPerSquareMeter, minShapeSplats, maxShapeSplats, shapeTextureSize, 1337 + next * 101, ClusterSize);
            _previous = null;
            _applyRadius = 1000f;
            SplatSimRunner sim = Body.EnsureSim(ClusterSize);
            _morph = sim != null && sim.Mode != SplatSimMode.Hidden && sim.Mode != SplatSimMode.Dissolving;
            _morphShape = SplatFeelMap.Morph(CurrentFeel);
            if (_morph) sim.BeginBurst(Body.transform.localToWorldMatrix, _morphShape.burstSpeed);
            else BeginDissolve();
            UnityEngine.Debug.Log($"{Tag} shape change {ShapeName} -> {(shapeNames != null && next < shapeNames.Length ? shapeNames[next] : next.ToString())}");
        }

        void FinishShapeChange()
        {
            if (_pendingShape < 0 || _pendingBuild == null || !_pendingBuild.IsCompleted) return;
            SplatLetterRenderer body = Body;
            SplatSimRunner sim = body != null ? body.Sim : null;
            if (_morph)
            {
                if (sim != null && sim.Mode == SplatSimMode.Bursting && sim.ModeTime < _morphShape.burstSeconds) return;
            }
            else if (sim != null && sim.Mode != SplatSimMode.Hidden) return;
            if (_pendingBuild.IsFaulted || _pendingBuild.Result == null)
            {
                UnityEngine.Debug.Log($"{Tag} shape build failed {_pendingBuild.Exception?.GetBaseException().Message}");
                _pendingShape = -1;
                _pendingBuild = null;
                BeginSummon(_hasEmitter ? _emitterWS : FallbackEmitter());
                return;
            }
            Matrix4x4 bodyToWorld = body.transform.localToWorldMatrix;
            if (_morph && sim != null)
            {
                sim.CopyWorldState(bodyToWorld, out var positions, out var velocities);
                var fromHomes = sim.HomesBody;
                body.ApplyBuild(_pendingBuild.Result, false);
                float ramp = tuning != null ? tuning.gatherRamp : 0.35f;
                body.Sim.SeedFrom(bodyToWorld, fromHomes, positions, velocities, _morphShape.gatherSpread, _morphShape.settleSeconds, ramp);
            }
            else
            {
                body.ApplyBuild(_pendingBuild.Result, true);
                BeginSummon(_hasEmitter ? _emitterWS : FallbackEmitter());
            }
            _shapeIndex = _pendingShape;
            _pendingShape = -1;
            UnityEngine.Debug.Log($"{Tag} shape ready {ShapeName} splats={body.SplatCount} morph={_morph}");
            _pendingBuild = null;
        }

        public float SurfaceDistance(Vector3 worldPoint, out Vector3 nearestWorld)
        {
            nearestWorld = worldPoint;
            SplatLetterRenderer body = Body;
            if (body == null || _pendingShape >= 0) return float.MaxValue;
            SplatSimRunner sim = body.EnsureSim(ClusterSize);
            if (sim == null || sim.Mode != SplatSimMode.Formed) return float.MaxValue;
            Transform t = body.transform;
            float d = sim.NearestHome(t.InverseTransformPoint(worldPoint), out Vector3 nearestLocal);
            nearestWorld = t.TransformPoint(nearestLocal);
            return d * t.lossyScale.x;
        }

        public void ApplyMaterial(SplatSurfaceMaterial material, Vector3 contactWorld)
        {
            SplatLetterRenderer body = Body;
            if (material == null || body == null) return;
            SplatSimRunner sim = body.EnsureSim(ClusterSize);
            Vector3 local = body.transform.InverseTransformPoint(contactWorld);
            if (sim != null) sim.NearestHome(local, out local);
            _previous = _current;
            _current = material;
            _applyOriginOS = local;
            _applyRadius = 0f;
            _applyMax = sim != null ? sim.MaxHomeDistance(local) : 0.3f;
            UnityEngine.Debug.Log($"{Tag} apply material={material.label} from={(_previous != null ? _previous.label : "-")} origin={local} reach={_applyMax:0.000}");
        }

        void AdvanceApply(float dt)
        {
            if (_previous == null) return;
            float speed = tuning != null ? tuning.applySpeed : 0.32f;
            float band = tuning != null ? tuning.applyBand : 0.03f;
            _applyRadius += speed * dt;
            if (SplatApplyWave.Done(_applyRadius, _applyMax, band))
            {
                _previous = null;
                _applyRadius = 1000f;
            }
        }

        public void Recenter()
        {
            if (head == null) return;
            Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            transform.position = head.position + fwd * placeDistance + Vector3.up * placeOffsetY;
            transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            if (shelf != null) shelf.Place(transform.position, fwd);
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

        void FillColliders(int handIndex, in SplatHandPoints hand)
        {
            float tip = tuning != null ? tuning.tipRadius : 0.022f;
            float palm = tuning != null ? tuning.palmRadius : 0.045f;
            int baseIndex = handIndex * SplatHandPoints.Count;
            for (int k = 0; k < SplatHandPoints.Count; k++)
            {
                _colliders[baseIndex + k] = new SplatCollider
                {
                    position = hand.tracked ? hand[k] : Vector3.zero,
                    velocity = _pointVelocity[baseIndex + k],
                    radius = k == SplatHandPoints.Count - 1 ? palm : tip,
                    strength = hand.tracked ? 1f : 0f
                };
            }
        }

        public void UpdateHands(in SplatHandPoints left, in SplatHandPoints right, float timeSeconds, float dt)
        {
            float safeDt = Mathf.Max(dt, 0.001f);
            TrackHand(0, left, safeDt);
            TrackHand(1, right, safeDt);
            FillColliders(0, left);
            FillColliders(1, right);
            _colliderCount = ColliderSlots;

            if (left.tracked)
            {
                _emitterWS = left.Pinch;
                _emitterVelocityWS = 0.5f * (_pointVelocity[0] + _pointVelocity[1]);
                _hasEmitter = true;
            }

            bool bothPinching = left.tracked && right.tracked && left.pinching && right.pinching;
            if (!bothPinching || SimMode != SplatSimMode.Formed)
            {
                _twoHandGrabbing = false;
                return;
            }

            Vector3 lp = left.Pinch;
            Vector3 rp = right.Pinch;
            Vector3 midWS = 0.5f * (lp + rp);
            float span = Mathf.Max(0.04f, Vector3.Distance(lp, rp));
            Vector3 handLine = rp - lp;
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
                return;
            }

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
            bool frozen = _frozen || (tuning != null && tuning.frozen);
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
                UnityEngine.Debug.Log($"{Tag} sim ms={tuning.simMilliseconds:0.00} awake={_awakeSmoothed:0.00} colliders={active} mode={SimMode} shape={ShapeName} frozen={_frozen} activity={activity:E2} wakes={(first != null ? first.WakeCount : 0)}");
            }
        }

        public SplatSurfaceState SurfaceStateFor(SplatSurfaceMaterial material, float timeSeconds)
        {
            return new SplatSurfaceState
            {
                current = material,
                currentFrame = material != null ? material.FrameAt(timeSeconds) : 0f,
                apply = SplatSurfaceState.NoApply,
                applyShape = tuning != null ? tuning.ApplyShape : new Vector4(0.03f, 0.004f, 0.15f, 0f),
                time = timeSeconds,
                triplanarScale = tuning != null ? tuning.triplanarScale : triplanarScale,
                intensity = tuning != null ? tuning.intensity : intensity,
                outline = outlineEnabled
            };
        }

        public void StepAnimationAndMaterials(float timeSeconds)
        {
            InitializeIfNeeded();
            if (letters == null) return;

            SplatLook look = tuning != null ? tuning.ToLook() : SplatLook.Default;
            SplatSurfaceMaterial shown = _preview ?? _current;
            SplatSurfaceState state = SurfaceStateFor(shown, timeSeconds);
            if (_preview == null && _previous != null)
            {
                state.previous = _previous;
                state.previousFrame = _previous.FrameAt(timeSeconds);
                state.apply = new Vector4(_applyOriginOS.x, _applyOriginOS.y, _applyOriginOS.z, _applyRadius);
            }

            for (int i = 0; i < letters.Length; i++)
            {
                SplatLetterRenderer letter = letters[i];
                if (letter == null) continue;
                int overrideMode = tuning != null ? tuning.shaderMode : -1;
                letter.Mode = (SplatShaderMode)(shown != null ? shown.ResolveMode(overrideMode) : Mathf.Max(0, overrideMode));
                letter.SetLook(look);
                letter.ApplyState(state);
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
            Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            PlaceWhenHeadIsReady();
            FinishShapeChange();
            if (!_frozen) _clock += dt;
            AdvanceApply(dt);
            StepSimulation(dt);
            StepAnimationAndMaterials(_clock);
        }
    }
}
