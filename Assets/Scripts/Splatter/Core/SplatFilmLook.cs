namespace MoodPicker.Splatter
{
    public static class SplatFilmLook
    {
        public const int HandLayer = 26;
        public const string ToggleRow = "Film VR";
        public const string BackgroundRow = "Film bg";

        public static float ClearAlpha(bool film) => film ? 1f : 0f;

        public static float ClearGrey(bool film, float background) => film ? Clamp01(background) : 0f;

        public static int CullingMask(int mask, bool film)
        {
            int bit = 1 << HandLayer;
            return film ? mask | bit : mask & ~bit;
        }

        public static float PassthroughBrightness(bool film, float roomBrightness) => film ? 0f : roomBrightness;

        public static float PassthroughContrast(bool film, float roomContrast) => film ? 0f : roomContrast;

        public static float PassthroughSaturation(bool film, float roomSaturation) => film ? 0f : roomSaturation;

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
