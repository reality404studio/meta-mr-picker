using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoodPicker.Catch
{
    public struct CropRect
    {
        public RectInt big;
        public int side;
        public int overlap;

        public RectInt Still => new RectInt(big.x, big.y + overlap, side, side);
        public int BigSize => side + overlap;
    }

    public static class CropMath
    {
        public static int Overlap(int side, float frac) => Mathf.Max(1, Mathf.RoundToInt(side * frac));

        public static bool TrySquare(IReadOnlyList<Vector2> viewportCorners, Vector2Int res, float overlapFrac, int minPixels, out CropRect crop)
        {
            crop = default;
            if (viewportCorners == null || viewportCorners.Count == 0 || res.x <= 0 || res.y <= 0) return false;
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (Vector2 v in viewportCorners)
            {
                if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.x) || float.IsInfinity(v.y)) return false;
                float px = v.x * res.x, py = v.y * res.y;
                x0 = Mathf.Min(x0, px); x1 = Mathf.Max(x1, px);
                y0 = Mathf.Min(y0, py); y1 = Mathf.Max(y1, py);
            }
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
            if (cx < 0 || cy < 0 || cx > res.x || cy > res.y) return false;

            int side = Mathf.RoundToInt(Mathf.Max(x1 - x0, y1 - y0));
            int limit = Mathf.Min(res.x, res.y);
            while (side > 0 && side + Overlap(side, overlapFrac) > limit) side--;
            if (side < minPixels) return false;
            int b = Overlap(side, overlapFrac);

            int sx = Mathf.RoundToInt(cx - side * 0.5f);
            int sy = Mathf.RoundToInt(cy - side * 0.5f);
            int bx = Mathf.Clamp(sx, 0, res.x - (side + b));
            int by = Mathf.Clamp(sy - b, 0, res.y - (side + b));
            crop = new CropRect { big = new RectInt(bx, by, side + b, side + b), side = side, overlap = b };
            return true;
        }

        public static Color32[] Crop(Color32[] src, int width, int height, RectInt r)
        {
            if (src == null || src.Length != width * height) throw new ArgumentException("source size");
            if (r.xMin < 0 || r.yMin < 0 || r.xMax > width || r.yMax > height) throw new ArgumentOutOfRangeException(nameof(r));
            var dst = new Color32[r.width * r.height];
            for (int y = 0; y < r.height; y++)
                Array.Copy(src, (r.y + y) * width + r.x, dst, y * r.width, r.width);
            return dst;
        }

        public static Color32[] StillFromBig(Color32[] big, CropRect c) =>
            Crop(big, c.BigSize, c.BigSize, new RectInt(0, c.overlap, c.side, c.side));

        public static Color32[] FlipRows(Color32[] px, int width, int height)
        {
            var dst = new Color32[px.Length];
            for (int y = 0; y < height; y++)
                Array.Copy(px, y * width, dst, (height - 1 - y) * width, width);
            return dst;
        }
    }
}
