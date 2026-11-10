using Pigment;
using UnityEngine;

namespace MoodPicker.Splatter
{
    public enum SplatMagicState
    {
        Formed = 0,
        Summoning = 1,
        Dissolving = 2,
        Hidden = 3
    }

    public sealed class SplatWordController : MonoBehaviour
    {
        [SerializeField] SplatLetterRenderer[] letters;
        [SerializeField] SplatPigmentSlotAsset[] presetSlots;
        [SerializeField] Transform head;
        [SerializeField] float placeDistance = 0.38f;
        [SerializeField] float placeOffsetY = -0.06f;
        [SerializeField] float letterSize = 0.12f;
        [SerializeField] float triplanarScale = 1.15f;
        [SerializeField] float intensity = 1.15f;
        [SerializeField] bool outlineEnabled = false;
        [SerializeField] bool autoFillSampleSlot0 = true;

        readonly BuiltPigmentSlot[] _slots = new BuiltPigmentSlot[SplatWordState.MaxSlots];
        readonly Texture2D[] _slotTextures = new Texture2D[SplatWordState.MaxSlots];
        readonly bool[] _ownsTexture = new bool[SplatWordState.MaxSlots];
        readonly float[] _slotFrames = new float[SplatWordState.MaxSlots];
        readonly Vector4[] _rippleHitOS =
        {
            new Vector4(0f, 0f, 0f, -10f),
            new Vector4(0f, 0f, 0f, -10f),
            new Vector4(0f, 0f, 0f, -10f),
            new Vector4(0f, 0f, 0f, -10f)
        };

        Vector3[] _baseLocalPositions;
        Quaternion[] _baseLocalRotations;
        SplatWordState _state;
        int _headAlignFrames;
        bool _headAligned;
        bool _userInteracted;

        float _wordScale = 1f;
        bool _twoHandGrabbing;
        Vector3 _grabStartMidWS;
        float _grabStartSpan = 0.2f;
        Vector3 _grabStartWordPos;
        float _grabStartWordScale = 1f;
        float _grabStartYawDeg;
        Quaternion _grabStartWordRot = Quaternion.identity;

        public const float SummonSeconds = 3.4f;
        public const float DissolveSeconds = 1.7f;

        SplatMagicState _magicState = SplatMagicState.Formed;
        float _magicStartTime = -10f;
        float _magicDuration = 1.35f;
        Vector3 _lastLeftPinchWS;
        bool _hasLeftPinchWS;
        bool _wasLeftPinching;
        float _lastLeftPinchToggleTime = -10f;

        bool _hadLeftPos;
        bool _hadRightPos;
        Vector3 _prevLeftTipWS;
        Vector3 _prevRightTipWS;
        Vector3 _leftVelWS;
        Vector3 _rightVelWS;
        Vector4 _handLeftWS;
        Vector4 _handRightWS;
        Vector4 _handLeftVelWS = new Vector4(0f, 0f, 0f, 0.075f);
        Vector4 _handRightVelWS = new Vector4(0f, 0f, 0f, 0.075f);
        int _nextRippleIndex;
        float _lastLeftRippleTime = -10f;
        float _lastRightRippleTime = -10f;
        bool _externalHandFeed;

        public SplatMagicState MagicState => _magicState;
        public SplatWordState State => _state;
        public float WordScale => _wordScale;
        public bool IsTwoHandGrabbing => _twoHandGrabbing;

        void Awake()
        {
            _wordScale = Mathf.Max(0.1f, transform.localScale.x);
            _magicState = SplatMagicState.Summoning;
            _magicStartTime = Time.time;
            _magicDuration = SummonSeconds;
            InitializeIfNeeded();
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
                _baseLocalPositions = new Vector3[count];
                _baseLocalRotations = new Quaternion[count];
                for (int i = 0; i < count; i++)
                {
                    if (letters[i] != null)
                    {
                        _baseLocalPositions[i] = letters[i].transform.localPosition;
                        _baseLocalRotations[i] = letters[i].transform.localRotation;
                        _state.SetLetterDirect(i, letters[i].AssignedSlot, letters[i].Mode);
                    }
                }
            }

            if (presetSlots != null)
            {
                for (int s = 0; s < presetSlots.Length && s < SplatWordState.MaxSlots; s++)
                {
                    if (presetSlots[s] != null && presetSlots[s].atlasTexture != null && _slots[s] == null)
                    {
                        SetSlotAsset(s, presetSlots[s]);
                    }
                }
            }

            if (autoFillSampleSlot0 && _slots[0] == null)
            {
                SetSlot(0, PigmentAtlasBuilder.BuildSampleWaterWaveSlot(64, 24, 15f));
            }
        }

        public void SetSlotAsset(int slotIndex, SplatPigmentSlotAsset asset)
        {
            if (slotIndex < 0 || slotIndex >= SplatWordState.MaxSlots || asset == null || asset.atlasTexture == null) return;
            if (_ownsTexture[slotIndex] && _slotTextures[slotIndex] != null)
            {
                if (Application.isPlaying) Destroy(_slotTextures[slotIndex]);
                else DestroyImmediate(_slotTextures[slotIndex]);
            }
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
            _slots[slotIndex] = built;

            if (_ownsTexture[slotIndex] && _slotTextures[slotIndex] != null)
            {
                if (Application.isPlaying) Destroy(_slotTextures[slotIndex]);
                else DestroyImmediate(_slotTextures[slotIndex]);
            }

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

        public void TriggerMagicToggle()
        {
            float now = Time.time;
            if (_magicState == SplatMagicState.Formed || _magicState == SplatMagicState.Summoning)
            {
                _magicState = SplatMagicState.Dissolving;
                _magicStartTime = now;
                _magicDuration = DissolveSeconds;
            }
            else
            {
                _magicState = SplatMagicState.Summoning;
                _magicStartTime = now;
                _magicDuration = SummonSeconds;
            }
        }

        Vector3 ResolveSummonOrigin()
        {
            if (_hasLeftPinchWS) return _lastLeftPinchWS;
            if (head != null) return head.position + head.forward * 0.22f + Vector3.down * 0.14f;
            return transform.position + Vector3.down * 0.25f;
        }

        public void UpdateHandInteraction(
            bool leftTracked,
            Vector3 leftEffectorWS,
            Vector3 leftPinchWS,
            bool leftPinching,
            bool rightTracked,
            Vector3 rightEffectorWS,
            Vector3 rightPinchWS,
            bool rightPinching,
            float timeSeconds,
            float dt)
        {
            _externalHandFeed = true;
            float safeDt = Mathf.Max(dt, 0.001f);

            if (leftTracked)
            {
                _lastLeftPinchWS = leftPinchWS;
                _hasLeftPinchWS = true;
                Vector3 rawVel = _hadLeftPos ? (leftEffectorWS - _prevLeftTipWS) / safeDt : Vector3.zero;
                _leftVelWS = Vector3.Lerp(_leftVelWS, Vector3.ClampMagnitude(rawVel, 3.0f), 1f - Mathf.Exp(-20f * safeDt));
                _prevLeftTipWS = leftEffectorWS;
                _hadLeftPos = true;
            }
            else
            {
                _hadLeftPos = false;
                _leftVelWS = Vector3.zero;
            }

            if (rightTracked)
            {
                Vector3 rawVel = _hadRightPos ? (rightEffectorWS - _prevRightTipWS) / safeDt : Vector3.zero;
                _rightVelWS = Vector3.Lerp(_rightVelWS, Vector3.ClampMagnitude(rawVel, 3.0f), 1f - Mathf.Exp(-20f * safeDt));
                _prevRightTipWS = rightEffectorWS;
                _hadRightPos = true;
            }
            else
            {
                _hadRightPos = false;
                _rightVelWS = Vector3.zero;
            }

            bool bothPinching = leftTracked && rightTracked && leftPinching && rightPinching;
            if (bothPinching && _magicState == SplatMagicState.Formed)
            {
                Vector3 midWS = 0.5f * (leftPinchWS + rightPinchWS);
                float span = Mathf.Max(0.04f, Vector3.Distance(leftPinchWS, rightPinchWS));
                Vector3 handLine = rightPinchWS - leftPinchWS;
                handLine.y = 0f;
                float currentYaw = handLine.sqrMagnitude > 1e-4f
                    ? Mathf.Atan2(handLine.z, handLine.x) * Mathf.Rad2Deg
                    : 0f;

                if (!_twoHandGrabbing)
                {
                    float distToWord = Vector3.Distance(midWS, transform.position);
                    float maxGrabDist = Mathf.Max(0.55f, 0.65f * _wordScale);
                    if (distToWord <= maxGrabDist)
                    {
                        _twoHandGrabbing = true;
                        _userInteracted = true;
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

                    Vector3 targetPos = _grabStartWordPos + (midWS - _grabStartMidWS);
                    float posFollow = 1f - Mathf.Exp(-28f * safeDt);
                    transform.position = Vector3.Lerp(transform.position, targetPos, posFollow);

                    if (handLine.sqrMagnitude > 1e-4f)
                    {
                        float yawDelta = Mathf.DeltaAngle(_grabStartYawDeg, currentYaw);
                        Quaternion targetRot = Quaternion.Euler(0f, -yawDelta, 0f) * _grabStartWordRot;
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, posFollow);
                    }
                }

                float radiusSuppressed = 0.075f * _wordScale;
                _handLeftWS = Vector4.zero;
                _handRightWS = Vector4.zero;
                _handLeftVelWS = new Vector4(0f, 0f, 0f, radiusSuppressed);
                _handRightVelWS = new Vector4(0f, 0f, 0f, radiusSuppressed);
                return;
            }

            _twoHandGrabbing = false;

            bool leftOnlyPinch = leftTracked && leftPinching && !(rightTracked && rightPinching);
            bool leftTriggered = leftOnlyPinch && !_wasLeftPinching;
            _wasLeftPinching = leftOnlyPinch;

            if (leftTriggered && !_twoHandGrabbing && (timeSeconds - _lastLeftPinchToggleTime >= 0.45f))
            {
                _lastLeftPinchToggleTime = timeSeconds;
                _userInteracted = true;
                if (_magicState == SplatMagicState.Formed || _magicState == SplatMagicState.Summoning)
                {
                    _magicState = SplatMagicState.Dissolving;
                    _magicStartTime = timeSeconds;
                    _magicDuration = DissolveSeconds;
                }
                else
                {
                    _magicState = SplatMagicState.Summoning;
                    _magicStartTime = timeSeconds;
                    _magicDuration = SummonSeconds;
                }
            }

            if (_magicState != SplatMagicState.Formed)
            {
                _handLeftWS = Vector4.zero;
                _handRightWS = Vector4.zero;
                _handLeftVelWS = new Vector4(0f, 0f, 0f, 0.075f * _wordScale);
                _handRightVelWS = new Vector4(0f, 0f, 0f, 0.075f * _wordScale);
                return;
            }

            float splashRadiusWS = 0.075f * _wordScale;
            _handLeftWS = leftTracked ? new Vector4(leftEffectorWS.x, leftEffectorWS.y, leftEffectorWS.z, 1f) : Vector4.zero;
            _handRightWS = rightTracked ? new Vector4(rightEffectorWS.x, rightEffectorWS.y, rightEffectorWS.z, 1f) : Vector4.zero;
            _handLeftVelWS = new Vector4(_leftVelWS.x, _leftVelWS.y, _leftVelWS.z, splashRadiusWS);
            _handRightVelWS = new Vector4(_rightVelWS.x, _rightVelWS.y, _rightVelWS.z, splashRadiusWS);

            if (letters != null && letters.Length > 0)
            {
                float hitThreshold = 0.12f * _wordScale;

                if (leftTracked && timeSeconds - _lastLeftRippleTime >= 0.085f)
                {
                    for (int i = 0; i < letters.Length; i++)
                    {
                        if (letters[i] == null) continue;
                        Transform lt = letters[i].transform;
                        float distL = Vector3.Distance(leftEffectorWS, lt.position);
                        if (distL <= hitThreshold && (_leftVelWS.sqrMagnitude > 0.002f || timeSeconds - _lastLeftRippleTime > 0.35f))
                        {
                            _userInteracted = true;
                            Vector3 hitOS = lt.InverseTransformPoint(leftEffectorWS);
                            _rippleHitOS[_nextRippleIndex] = new Vector4(hitOS.x, hitOS.y, hitOS.z, timeSeconds);
                            _nextRippleIndex = (_nextRippleIndex + 1) & 3;
                            _lastLeftRippleTime = timeSeconds;
                            break;
                        }
                    }
                }

                if (rightTracked && timeSeconds - _lastRightRippleTime >= 0.085f)
                {
                    for (int i = 0; i < letters.Length; i++)
                    {
                        if (letters[i] == null) continue;
                        Transform lt = letters[i].transform;
                        float distR = Vector3.Distance(rightEffectorWS, lt.position);
                        if (distR <= hitThreshold && (_rightVelWS.sqrMagnitude > 0.002f || timeSeconds - _lastRightRippleTime > 0.35f))
                        {
                            _userInteracted = true;
                            Vector3 hitOS = lt.InverseTransformPoint(rightEffectorWS);
                            _rippleHitOS[_nextRippleIndex] = new Vector4(hitOS.x, hitOS.y, hitOS.z, timeSeconds);
                            _nextRippleIndex = (_nextRippleIndex + 1) & 3;
                            _lastRightRippleTime = timeSeconds;
                            break;
                        }
                    }
                }
            }
        }

        public void TriggerSampleSplash(Vector3 hitPosOS, Vector3 handPosWS, Vector3 handVelWS, float timeSeconds)
        {
            float splashRadiusWS = 0.075f * _wordScale;
            _handRightWS = new Vector4(handPosWS.x, handPosWS.y, handPosWS.z, 1f);
            _handRightVelWS = new Vector4(handVelWS.x, handVelWS.y, handVelWS.z, splashRadiusWS);
            _rippleHitOS[0] = new Vector4(hitPosOS.x, hitPosOS.y, hitPosOS.z, timeSeconds - 0.16f);
            _rippleHitOS[1] = new Vector4(hitPosOS.x * 0.5f, hitPosOS.y * 0.5f, hitPosOS.z, timeSeconds - 0.05f);
        }

        Vector4 BuildRippleShaderVector(int idx, float timeSeconds)
        {
            Vector4 raw = _rippleHitOS[idx];
            float age = raw.w < -1f ? -1f : (timeSeconds - raw.w);
            if (age < 0f || age > 1.8f) age = -1f;
            return new Vector4(raw.x, raw.y, raw.z, age);
        }

        public void StepAnimationAndMaterials(float timeSeconds)
        {
            InitializeIfNeeded();
            if (letters == null || _state == null) return;

            _state.SetSharedParameters(triplanarScale, intensity, outlineEnabled);

            for (int s = 0; s < SplatWordState.MaxSlots; s++)
            {
                BuiltPigmentSlot slot = _slots[s];
                _slotFrames[s] = slot != null
                    ? LetterPoseMath.SlotFrameContinuous(timeSeconds, slot.Layout.Fps, slot.Layout.Frames)
                    : 0f;
            }

            Vector4 r0 = BuildRippleShaderVector(0, timeSeconds);
            Vector4 r1 = BuildRippleShaderVector(1, timeSeconds);
            Vector4 r2 = BuildRippleShaderVector(2, timeSeconds);
            Vector4 r3 = BuildRippleShaderVector(3, timeSeconds);

            Vector4 magicVec = Vector4.zero;
            Vector3 summonOrigin = Vector3.zero;
            if (_magicState == SplatMagicState.Formed)
            {
                magicVec = new Vector4(1f, 0f, 0f, 0f);
            }
            else if (_magicState == SplatMagicState.Summoning)
            {
                float p = Mathf.Clamp01((timeSeconds - _magicStartTime) / Mathf.Max(0.1f, _magicDuration));
                if (p >= 1f)
                {
                    _magicState = SplatMagicState.Formed;
                    magicVec = new Vector4(1f, 0f, 0f, 0f);
                }
                else
                {
                    float flash = 0f;
                    if (p >= 0.87f)
                    {
                        float fNorm = (p - 0.87f) / 0.13f;
                        flash = Mathf.Sin(fNorm * Mathf.PI) * 1.2f;
                    }
                    magicVec = new Vector4(p, 1f, flash, timeSeconds - _magicStartTime);
                    summonOrigin = ResolveSummonOrigin();
                }
            }
            else if (_magicState == SplatMagicState.Dissolving)
            {
                float p = Mathf.Clamp01((timeSeconds - _magicStartTime) / Mathf.Max(0.1f, _magicDuration));
                if (p >= 1f)
                {
                    _magicState = SplatMagicState.Hidden;
                    magicVec = new Vector4(0f, 3f, 0f, 0f);
                }
                else
                {
                    magicVec = new Vector4(p, 2f, 0f, timeSeconds - _magicStartTime);
                }
            }
            else if (_magicState == SplatMagicState.Hidden)
            {
                magicVec = new Vector4(0f, 3f, 0f, 0f);
            }

            float rotScale = letters.Length == 1 ? 0.15f : 0.18f;

            for (int i = 0; i < letters.Length; i++)
            {
                SplatLetterRenderer letter = letters[i];
                if (letter == null) continue;

                float rotYDeg = LetterPoseMath.RotationYRadians(timeSeconds, i) * Mathf.Rad2Deg * rotScale;
                float bobY = _twoHandGrabbing ? 0f : LetterPoseMath.BobOffsetY(timeSeconds, i, letterSize) * 0.45f;
                Vector3 basePos = _baseLocalPositions != null && i < _baseLocalPositions.Length
                    ? _baseLocalPositions[i]
                    : letter.transform.localPosition;
                Quaternion baseRot = _baseLocalRotations != null && i < _baseLocalRotations.Length
                    ? _baseLocalRotations[i]
                    : Quaternion.identity;

                letter.transform.localPosition = new Vector3(basePos.x, basePos.y + bobY, basePos.z);
                letter.transform.localRotation = baseRot * Quaternion.Euler(0f, rotYDeg, 0f);

                int slotIdx = _state.GetSlot(i);
                letter.AssignedSlot = slotIdx;
                letter.Mode = _state.GetMode(i);
                letter.SetMagicState(magicVec);
                letter.SetSummonOrigin(summonOrigin);
                letter.SetHandWaterState(_handLeftWS, _handRightWS, _handLeftVelWS, _handRightVelWS, r0, r1, r2, r3);

                bool hasSlot = slotIdx >= 0 && slotIdx < SplatWordState.MaxSlots && _slots[slotIdx] != null && _slotTextures[slotIdx] != null;
                if (hasSlot)
                {
                    BuiltPigmentSlot slot = _slots[slotIdx];
                    letter.ApplyPigmentState(
                        _slotTextures[slotIdx],
                        slot.Layout,
                        _slotFrames[slotIdx],
                        true,
                        slot.Palette,
                        _state.TriplanarScale,
                        _state.Intensity,
                        _state.OutlineEnabled);
                }
                else
                {
                    letter.ApplyPigmentState(
                        null,
                        default,
                        0f,
                        false,
                        default,
                        _state.TriplanarScale,
                        _state.Intensity,
                        _state.OutlineEnabled);
                }
            }
        }

        void Update()
        {
            if (head != null && !_twoHandGrabbing)
            {
                Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
                fwd.Normalize();
                Vector3 idealPos = head.position + fwd * placeDistance + Vector3.up * placeOffsetY;
                Quaternion idealRot = Quaternion.LookRotation(fwd, Vector3.up);

                if (!_headAligned)
                {
                    _headAlignFrames++;
                    transform.position = idealPos;
                    transform.rotation = idealRot;
                    if (head.position.sqrMagnitude > 1e-4f || _headAlignFrames >= 20)
                    {
                        _headAligned = true;
                    }
                }
                else if (!_userInteracted || Vector3.Distance(transform.position, idealPos) > 0.45f)
                {
                    float follow = 1f - Mathf.Exp(-6f * Mathf.Max(Time.deltaTime, 0.001f));
                    transform.position = Vector3.Lerp(transform.position, idealPos, follow);
                    transform.rotation = Quaternion.Slerp(transform.rotation, idealRot, follow);
                }
            }
            StepAnimationAndMaterials(Time.time);
        }

        void OnDestroy()
        {
            for (int i = 0; i < _slotTextures.Length; i++)
            {
                if (_ownsTexture[i] && _slotTextures[i] != null)
                {
                    if (Application.isPlaying) Destroy(_slotTextures[i]);
                    else DestroyImmediate(_slotTextures[i]);
                }
            }
        }
    }
}
