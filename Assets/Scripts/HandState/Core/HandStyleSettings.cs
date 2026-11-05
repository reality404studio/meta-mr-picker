using System;

namespace MoodPicker.HandStates
{
    public sealed class HandStyleSettings
    {
        public HandColor Bare { get; set; } = new HandColor(240f / 255f, 238f / 255f, 230f / 255f);
        public HandColor Stamp { get; set; } = new HandColor(37f / 255f, 89f / 255f, 180f / 255f);

        public HandColor ColorFor(HandSnapshot state)
        {
            switch (state.Mode)
            {
                case HandState.Bare: return Bare;
                case HandState.Paint: return state.PaintColor ?? Bare;
                case HandState.Stamp: return Stamp;
                case HandState.Dropper: return state.LastPickedColor ?? Bare;
                default: throw new ArgumentOutOfRangeException(nameof(state));
            }
        }
    }
}
