namespace MoodPicker.Hands
{
    public enum Microgesture
    {
        None = 0,
        SwipeLeft = 1,
        SwipeRight = 2,
        SwipeForward = 3,
        SwipeBackward = 4,
        ThumbTap = 5
    }

    public interface IMicrogestureInput
    {
        Microgesture Current(Handedness hand);
    }
}
