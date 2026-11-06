using System;
using MoodPicker.Tokens;

namespace MoodPicker.HandStates
{
    public sealed class HandStyleSettings
    {
        public const float RimPower = OrionTokens.StrokeHandRimPower;
        public const float InflateMm = OrionTokens.StrokeHandInflateMm;
        public const float InflateMeters = OrionTokens.StrokeHandInflateMeters;
        public const float PassthroughWorkspace = OrionTokens.EnvPassthroughWorkspace;
        public const float PassthroughHero = OrionTokens.EnvPassthroughHero;
        public const float PassthroughTransitionSeconds = OrionTokens.EnvPassthroughTransitionSeconds;

        public HandColor Bare { get; set; } = new HandColor(OrionTokens.ColorPaperFaceR, OrionTokens.ColorPaperFaceG, OrionTokens.ColorPaperFaceB);
        public HandColor Stamp { get; set; } = new HandColor(OrionTokens.ColorPrintCobaltR, OrionTokens.ColorPrintCobaltG, OrionTokens.ColorPrintCobaltB);

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
