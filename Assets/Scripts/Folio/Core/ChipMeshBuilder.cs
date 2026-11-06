using System;
using System.Collections.Generic;
using MoodPicker.Tokens;

namespace MoodPicker.Folio
{
    public enum ChipSurface { PaperFace = 0, PaperEdge = 1, Tile = 2 }

    public sealed class ChipMeshData
    {
        public FolioVector[] Vertices { get; }
        public FolioVector[] Normals { get; }
        public FolioVector[] UV { get; }
        public FolioVector[] UV2 { get; }
        public int[] Triangles { get; }
        internal ChipMeshData(List<FolioVector> vertices, List<FolioVector> normals,
            List<FolioVector> uv, List<FolioVector> uv2, List<int> triangles)
        {
            Vertices = vertices.ToArray(); Normals = normals.ToArray();
            UV = uv.ToArray(); UV2 = uv2.ToArray(); Triangles = triangles.ToArray();
        }
    }

    public static class ChipMeshBuilder
    {
        public const float Width = OrionTokens.GeometryChipCardMeters0;
        public const float Height = OrionTokens.GeometryChipCardMeters1;
        public const float Thickness = OrionTokens.GeometryChipCardThicknessMeters;
        public const float TileWidth = OrionTokens.GeometryChipTileMeters0;
        public const float TileHeight = OrionTokens.GeometryChipTileMeters1;
        public const float TileRaise = OrionTokens.GeometryChipTileRaiseMeters;
        public const float TileCenterY = (Height - TileHeight) / 4f;
        public const float FrontZ = Thickness + TileRaise;

        public static ChipMeshData Build()
        {
            var v = new List<FolioVector>(); var n = new List<FolioVector>();
            var uv = new List<FolioVector>(); var uv2 = new List<FolioVector>(); var tris = new List<int>();
            void Face(FolioVector center, FolioVector right, FolioVector up, ChipSurface surface)
            {
                int start = v.Count;
                var normal = FolioVector.Cross(right, up).Normalized;
                var points = new[] { center - right - up, center + right - up, center + right + up, center - right + up };
                for (int i = 0; i < 4; i++)
                {
                    v.Add(points[i]); n.Add(normal);
                    uv.Add(new FolioVector(0.5f - points[i].X / Width, points[i].Y / Height + 0.5f, 0));
                    uv2.Add(new FolioVector((float)surface, 0, 0));
                }
                tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            void Box(float width, float height, float low, float high, float cy, bool tile)
            {
                float x = width / 2, y = height / 2, z = (high + low) / 2, dz = (high - low) / 2;
                var side = tile ? ChipSurface.Tile : ChipSurface.PaperEdge;
                Face(new FolioVector(0, cy, high), new FolioVector(x, 0, 0), new FolioVector(0, y, 0), tile ? ChipSurface.Tile : ChipSurface.PaperFace);
                if (!tile) Face(new FolioVector(0, cy, low), new FolioVector(-x, 0, 0), new FolioVector(0, y, 0), side);
                Face(new FolioVector(x, cy, z), new FolioVector(0, 0, -dz), new FolioVector(0, y, 0), side);
                Face(new FolioVector(-x, cy, z), new FolioVector(0, 0, dz), new FolioVector(0, y, 0), side);
                Face(new FolioVector(0, cy + y, z), new FolioVector(x, 0, 0), new FolioVector(0, 0, -dz), side);
                Face(new FolioVector(0, cy - y, z), new FolioVector(x, 0, 0), new FolioVector(0, 0, dz), side);
            }
            Box(Width, Height, 0, Thickness, 0, false);
            Box(TileWidth, TileHeight, Thickness, FrontZ, TileCenterY, true);
            if (tris.Count / 3 > OrionTokens.GeometryChipTrisTotal) throw new InvalidOperationException("chip triangle budget exceeded");
            return new ChipMeshData(v, n, uv, uv2, tris);
        }
    }
}
