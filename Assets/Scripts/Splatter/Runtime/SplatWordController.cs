using Pigment;
using UnityEngine;

namespace MoodPicker.Splatter
{
    public sealed class SplatWordController : MonoBehaviour
    {
        [SerializeField] SplatLetterRenderer[] letters;
        [SerializeField] float letterSize = 0.085f;
        [SerializeField] float triplanarScale = 1.2f;
        [SerializeField] float intensity = 1.0f;
        [SerializeField] bool outlineEnabled = true;
        [SerializeField] bool autoFillSampleSlot0 = true;

        readonly BuiltPigmentSlot[] _slots = new BuiltPigmentSlot[SplatWordState.MaxSlots];
        readonly Texture2D[] _slotTextures = new Texture2D[SplatWordState.MaxSlots];
        Vector3[] _baseLocalPositions;
        SplatWordState _state;

        public SplatWordState State => _state;

        void Awake()
        {
            InitializeIfNeeded();
        }

        public void InitializeIfNeeded()
        {
            if (letters == null || letters.Length == 0)
                letters = GetComponentsInChildren<SplatLetterRenderer>();

            int count = letters != null ? letters.Length : 0;
            if (count > 0 && _state == null)
            {
                _state = new SplatWordState(count, 0, SplatShaderMode.TwoToneCel);
                _state.SetSharedParameters(triplanarScale, intensity, outlineEnabled);
                _baseLocalPositions = new Vector3[count];
                for (int i = 0; i < count; i++)
                {
                    if (letters[i] != null) _baseLocalPositions[i] = letters[i].transform.localPosition;
                }
            }

            if (autoFillSampleSlot0 && _slots[0] == null)
            {
                SetSlot(0, PigmentAtlasBuilder.BuildSampleWaterWaveSlot(64, 24, 15f));
            }
        }

        public void SetSlot(int slotIndex, BuiltPigmentSlot built)
        {
            if (slotIndex < 0 || slotIndex >= SplatWordState.MaxSlots || built == null) return;
            _slots[slotIndex] = built;

            if (_slotTextures[slotIndex] != null)
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
        }

        public void StepAnimationAndMaterials(float timeSeconds)
        {
            InitializeIfNeeded();
            if (letters == null || _state == null) return;

            _state.SetSharedParameters(triplanarScale, intensity, outlineEnabled);

            int[] slotFrames = new int[SplatWordState.MaxSlots];
            for (int s = 0; s < SplatWordState.MaxSlots; s++)
            {
                BuiltPigmentSlot slot = _slots[s];
                slotFrames[s] = slot != null
                    ? LetterPoseMath.SlotFrame(timeSeconds, slot.Layout.Fps, slot.Layout.Frames)
                    : 0;
            }

            for (int i = 0; i < letters.Length; i++)
            {
                SplatLetterRenderer letter = letters[i];
                if (letter == null) continue;

                float rotYDeg = LetterPoseMath.RotationYRadians(timeSeconds, i) * Mathf.Rad2Deg;
                float bobY = LetterPoseMath.BobOffsetY(timeSeconds, i, letterSize);
                Vector3 basePos = _baseLocalPositions != null && i < _baseLocalPositions.Length
                    ? _baseLocalPositions[i]
                    : letter.transform.localPosition;

                letter.transform.localPosition = new Vector3(basePos.x, basePos.y + bobY, basePos.z);
                letter.transform.localRotation = Quaternion.Euler(0f, rotYDeg, 0f);

                int slotIdx = _state.GetSlot(i);
                letter.AssignedSlot = slotIdx;
                letter.Mode = _state.GetMode(i);

                bool hasSlot = slotIdx >= 0 && slotIdx < SplatWordState.MaxSlots && _slots[slotIdx] != null && _slotTextures[slotIdx] != null;
                if (hasSlot)
                {
                    BuiltPigmentSlot slot = _slots[slotIdx];
                    letter.ApplyPigmentState(
                        _slotTextures[slotIdx],
                        slot.Layout,
                        slotFrames[slotIdx],
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
                        0,
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
            StepAnimationAndMaterials(Time.time);
        }

        void OnDestroy()
        {
            for (int i = 0; i < _slotTextures.Length; i++)
            {
                if (_slotTextures[i] != null)
                {
                    if (Application.isPlaying) Destroy(_slotTextures[i]);
                    else DestroyImmediate(_slotTextures[i]);
                }
            }
        }
    }
}
