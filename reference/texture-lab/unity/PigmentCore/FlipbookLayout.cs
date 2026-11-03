// FlipbookLayout — 루프 프레임을 아틀라스(스프라이트 시트) 격자에 배치하는 규칙.
// 파일(PNG)은 "행 우선, 왼쪽 위부터" (texlab.py, web/pigment-lab 과 같음).
// Unity Texture2D 의 픽셀 좌표는 왼쪽 아래가 원점이라 y 를 뒤집어야 한다 → PixelRectUnity.
using System;

namespace Pigment
{
    public struct FlipbookLayout
    {
        public int Frames, Cols, Rows, Tile;
        public float Fps;

        /// loopLength 프레임 루프를 maxFrames 이하로 고르게 솎아 배치. fps 는 솎은 만큼 낮아진다.
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

        /// 프레임 k 의 왼쪽 아래 픽셀 좌표 (Unity Texture2D / Graphics.CopyTexture 용).
        public void PixelRectUnity(int k, out int x, out int y)
        {
            int col = k % Cols, row = k / Cols;      // row 0 = 파일 맨 위
            x = col * Tile;
            y = (Rows - 1 - row) * Tile;
        }

        /// 셰이더용: 프레임 k 의 UV 오프셋(왼쪽 아래)과 스케일. uv = offset + frac(tileUV) * scale
        public void UvRect(int k, out float ox, out float oy, out float sx, out float sy)
        {
            int col = k % Cols, row = k / Cols;
            sx = 1f / Cols; sy = 1f / Rows;
            ox = col * sx; oy = (Rows - 1 - row) * sy;
        }
    }
}
