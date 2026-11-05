namespace MoodPicker.HandStates
{
    public enum HandState { Bare, Paint, Stamp, Dropper }

    public enum HandEventKind
    {
        PortalDipPaint, Washed, GrabStamp, GrabDropper, ReleaseTool,
        GrabLetter, ReleaseLetter, TapScreen, LetterTouch, StampFired, DropperPicked
    }

    public enum HandAction
    {
        TapScreen, GrabLetter, TouchLetterPulse, ThrowWord, StirBench,
        FillLetter, Capture, Pick, DropDrop
    }

    public readonly struct HandColor
    {
        public float R { get; }
        public float G { get; }
        public float B { get; }
        public float A { get; }

        public HandColor(float r, float g, float b, float a = 1f)
        {
            R = r; G = g; B = b; A = a;
        }
    }

    public readonly struct HandEvent
    {
        public HandEventKind Kind { get; }
        public HandColor? Color { get; }

        public HandEvent(HandEventKind kind, HandColor? color = null)
        {
            Kind = kind; Color = color;
        }
    }

    public readonly struct HandSnapshot
    {
        public HandState Mode { get; }
        public HandColor? PaintColor { get; }
        public HandColor? LastPickedColor { get; }

        internal HandSnapshot(HandState mode, HandColor? paintColor, HandColor? lastPickedColor)
        {
            Mode = mode; PaintColor = paintColor; LastPickedColor = lastPickedColor;
        }
    }
}
