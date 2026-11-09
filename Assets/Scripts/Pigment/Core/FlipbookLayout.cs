using System;

namespace Pigment
{
    public struct FlipbookLayout
    {
        public int Frames, Cols, Rows, Tile;
        public float Fps;

        public static FlipbookLayout For(int loopLength, int maxFrames, int tile, float sourceFps, out int[] pickedIndices)
        {
            int n = Math.Min(loopLength, maxFrames);
            pickedIndices = new int[n];
            for (int t = 0; t < n; t++) pickedIndices[t] = (int)((long)t * loopLength / n);
            int cols = (int)Math.Ceiling(Math.Sqrt(n));
            int rows = (int)Math.Ceiling(n / (double)cols);
            return new FlipbookLayout { Frames = n, Cols = cols, Rows = rows, Tile = tile, Fps = sourceFps * n / loopLength };
        }

        public int Width { get { return Cols * Tile; } }
        public int Height { get { return Rows * Tile; } }

        public void PixelRectUnity(int k, out int x, out int y)
        {
            int col = k % Cols, row = k / Cols;
            x = col * Tile;
            y = (Rows - 1 - row) * Tile;
        }

        public void UvRect(int k, out float ox, out float oy, out float sx, out float sy)
        {
            int col = k % Cols, row = k / Cols;
            sx = 1f / Cols; sy = 1f / Rows;
            ox = col * sx; oy = (Rows - 1 - row) * sy;
        }
    }
}
