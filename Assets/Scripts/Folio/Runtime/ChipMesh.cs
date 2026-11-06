using MoodPicker.Tokens;
using UnityEngine;

namespace MoodPicker.Folio
{
    public static class ChipMesh
    {
        public static Mesh Create()
        {
            var data = ChipMeshBuilder.Build();
            var vertices = new Vector3[data.Vertices.Length]; var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length]; var uv2 = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = ToUnity(data.Vertices[i]); normals[i] = ToUnity(data.Normals[i]);
                uv[i] = new Vector2(data.UV[i].X, data.UV[i].Y); uv2[i] = new Vector2(data.UV2[i].X, data.UV2[i].Y);
            }
            var mesh = new Mesh { name = "ORION Chip 3+4", vertices = vertices, normals = normals, uv = uv, uv2 = uv2, subMeshCount = 1 };
            mesh.SetTriangles(data.Triangles, 0); mesh.RecalculateBounds();
            return mesh;
        }

        public static Vector3 ToUnity(FolioVector value) => new Vector3(value.X, value.Y, value.Z);
        public static FolioVector FromUnity(Vector3 value) => new FolioVector(value.x, value.y, value.z);

        public static void Apply(Renderer renderer, MaterialPropertyBlock block, Texture2D tile, int number, bool loop, bool clear = false)
        {
            block.Clear();
            block.SetTexture("_BaseMap", tile != null ? tile : Texture2D.whiteTexture);
            block.SetFloat("_ChipNumber", Mathf.Clamp(number, 1, 12));
            block.SetFloat("_HasLoop", loop ? 1 : 0); block.SetFloat("_Clear", clear ? 1 : 0);
            block.SetColor("_PaperFace", OrionTokens.ToColor(OrionTokens.ColorPaperFaceHex));
            block.SetColor("_PaperEdge", OrionTokens.ToColor(OrionTokens.ColorPaperEdgeHex));
            block.SetColor("_Klein", OrionTokens.ToColor(OrionTokens.ColorInkKleinHex));
            block.SetColor("_Cobalt", OrionTokens.ToColor(OrionTokens.ColorPrintCobaltHex));
            block.SetColor("_Red", OrionTokens.ToColor(OrionTokens.ColorAccentRedHex));
            block.SetColor("_Sky", OrionTokens.ToColor(OrionTokens.ColorAuxSkyHex));
            block.SetVector("_CardSize", new Vector4(ChipMeshBuilder.Width, ChipMeshBuilder.Height, ChipMeshBuilder.Thickness, 0));
            block.SetVector("_TileSize", new Vector4(ChipMeshBuilder.TileWidth, ChipMeshBuilder.TileHeight, ChipMeshBuilder.TileRaise, ChipMeshBuilder.TileCenterY));
            block.SetFloat("_Stroke", OrionTokens.StrokeLinePrimaryMeters);
            block.SetFloat("_Backing", OrionTokens.StrokeLinePrimaryBackingMeters);
            renderer.SetPropertyBlock(block);
        }
    }
}
