namespace MoodPicker.Hands
{
    public interface IPinchGate
    {
        bool SuppressesPinch(float t);
    }

    public static class PinchGate
    {
        public static HandFrame Apply(HandFrame f, IPinchGate gate)
        {
            if (gate == null || !gate.SuppressesPinch(f.t)) return f;
            f.left.tracked = false;
            f.right.tracked = false;
            return f;
        }
    }
}
