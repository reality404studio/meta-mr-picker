using System;

namespace MoodPicker.Splatter
{
    public sealed class SplatWordState
    {
        public const int MaxSlots = 3;

        readonly int[] _assignedSlots;
        readonly SplatShaderMode[] _assignedModes;
        readonly bool[] _selected;

        public int LetterCount { get; }
        public float TriplanarScale { get; private set; } = 1.2f;
        public float Intensity { get; private set; } = 1.0f;
        public bool OutlineEnabled { get; private set; } = true;

        public SplatWordState(int letterCount, int defaultSlot = 0, SplatShaderMode defaultMode = SplatShaderMode.TwoToneCel)
        {
            if (letterCount <= 0) throw new ArgumentOutOfRangeException(nameof(letterCount));
            LetterCount = letterCount;
            _assignedSlots = new int[letterCount];
            _assignedModes = new SplatShaderMode[letterCount];
            _selected = new bool[letterCount];

            int clampedSlot = Math.Clamp(defaultSlot, -1, MaxSlots - 1);
            for (int i = 0; i < letterCount; i++)
            {
                _assignedSlots[i] = clampedSlot;
                _assignedModes[i] = defaultMode;
            }
        }

        public int GetSlot(int letterIndex) => _assignedSlots[letterIndex];
        public SplatShaderMode GetMode(int letterIndex) => _assignedModes[letterIndex];
        public bool IsSelected(int letterIndex) => _selected[letterIndex];

        public int SelectedCount
        {
            get
            {
                int c = 0;
                for (int i = 0; i < LetterCount; i++) if (_selected[i]) c++;
                return c;
            }
        }

        public void ToggleSelect(int letterIndex)
        {
            if (letterIndex < 0 || letterIndex >= LetterCount) return;
            _selected[letterIndex] = !_selected[letterIndex];
        }

        public void ClearSelection()
        {
            Array.Clear(_selected, 0, LetterCount);
        }

        public void ApplySlotToSelectionOrAll(int slotIndex)
        {
            int clamped = Math.Clamp(slotIndex, -1, MaxSlots - 1);
            bool any = SelectedCount > 0;
            for (int i = 0; i < LetterCount; i++)
            {
                if (!any || _selected[i]) _assignedSlots[i] = clamped;
            }
        }

        public void ApplyModeToSelectionOrAll(SplatShaderMode mode)
        {
            bool any = SelectedCount > 0;
            for (int i = 0; i < LetterCount; i++)
            {
                if (!any || _selected[i]) _assignedModes[i] = mode;
            }
        }

        public void SetLetterDirect(int letterIndex, int slotIndex, SplatShaderMode mode)
        {
            if (letterIndex < 0 || letterIndex >= LetterCount) return;
            _assignedSlots[letterIndex] = Math.Clamp(slotIndex, -1, MaxSlots - 1);
            _assignedModes[letterIndex] = mode;
        }

        public void SetSharedParameters(float triplanarScale, float intensity, bool outlineEnabled)
        {
            TriplanarScale = Math.Clamp(triplanarScale, 0.2f, 3.0f);
            Intensity = Math.Clamp(intensity, 0.2f, 2.0f);
            OutlineEnabled = outlineEnabled;
        }
    }
}
