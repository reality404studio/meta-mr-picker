using System;

namespace MoodPicker.Folio
{
    public readonly struct FolioVector
    {
        public readonly float X, Y, Z;
        public FolioVector(float x, float y, float z) { X = x; Y = y; Z = z; }
        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        public FolioVector Normalized => Length > 0 ? this * (1f / Length) : default;
        public static FolioVector operator +(FolioVector a, FolioVector b) => new FolioVector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static FolioVector operator -(FolioVector a, FolioVector b) => new FolioVector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static FolioVector operator *(FolioVector a, float s) => new FolioVector(a.X * s, a.Y * s, a.Z * s);
        public static FolioVector Cross(FolioVector a, FolioVector b) => new FolioVector(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public static float Dot(FolioVector a, FolioVector b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    }
}
