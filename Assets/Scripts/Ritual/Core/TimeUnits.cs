using System;

namespace MoodPicker.Ritual
{
    public static class TimeUnits
    {
        public const int MinutesPerUnit = 10;

        public static int Step(float rawLength, int current, float unitLength, float hysteresis, int maxUnits)
        {
            if (unitLength <= 0f || maxUnits <= 0) return 0;
            float x = Math.Max(0f, rawLength) / unitLength;
            int next = Math.Max(0, Math.Min(current, maxUnits));
            while (next < maxUnits && x >= next + 0.5f + hysteresis) next++;
            while (next > 0 && x <= next - 0.5f - hysteresis) next--;
            return next;
        }

        public static int NodeStrength(int boundary)
        {
            if (boundary <= 0) return 0;
            if (boundary % 6 == 0) return 2;
            if (boundary % 3 == 0) return 1;
            return 0;
        }

        public static int Minutes(int units) => units * MinutesPerUnit;

        public static string Label(int units)
        {
            int m = Minutes(units);
            int h = m / 60;
            int r = m % 60;
            if (h == 0) return r + "분";
            return r == 0 ? h + "시간" : h + "시간 " + r + "분";
        }

        public static string LabelEn(int units)
        {
            int m = Minutes(units);
            int h = m / 60;
            int r = m % 60;
            if (h == 0) return $"{r:00}m";
            return $"{h:00}h {r:00}m";
        }

        public static string ShortEn(int units)
        {
            int m = Minutes(units);
            int h = m / 60;
            int r = m % 60;
            if (h == 0) return $"{r}m";
            return r == 0 ? $"{h}h" : $"{h}h {r:00}m";
        }
    }

    public struct TimePiece
    {
        public string category;
        public int units;
        public int mergedCount;
    }
}
