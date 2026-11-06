using System;
using System.Linq;
using MoodPicker.Folio;
using MoodPicker.Tokens;
using NUnit.Framework;

namespace MoodPicker.Tests
{
    public class ChipTests
    {
        [Test]
        public void MeshHas22TrianglesWithSixCardFacesAndFiveTileFaces()
        {
            var m = ChipMeshBuilder.Build();
            Assert.That(m.Triangles.Length / 3, Is.EqualTo(OrionTokens.GeometryChipTrisTotal));
            Assert.That(m.UV2.Count(v => v.X == (float)ChipSurface.Tile), Is.EqualTo(20));
            Assert.That(m.UV2.Count(v => v.X == (float)ChipSurface.PaperFace), Is.EqualTo(4));
            Assert.That(m.Triangles.All(i => i >= 0 && i < m.Vertices.Length), Is.True);
        }

        [Test]
        public void DimensionsAndTileSideDepthMatchTokensWithinTenthMillimeter()
        {
            var m = ChipMeshBuilder.Build();
            Assert.That(m.Vertices.Max(v => v.X)-m.Vertices.Min(v => v.X), Is.EqualTo(OrionTokens.GeometryChipCardMeters0).Within(0.0001));
            Assert.That(m.Vertices.Max(v => v.Y)-m.Vertices.Min(v => v.Y), Is.EqualTo(OrionTokens.GeometryChipCardMeters1).Within(0.0001));
            var tile = m.Vertices.Where((v,i) => m.UV2[i].X == (float)ChipSurface.Tile).ToArray();
            var card = m.Vertices.Where((v,i) => m.UV2[i].X != (float)ChipSurface.Tile).ToArray();
            Assert.That(tile.Max(v=>v.X)-tile.Min(v=>v.X), Is.EqualTo(OrionTokens.GeometryChipTileMeters0).Within(0.0001));
            Assert.That(tile.Max(v=>v.Y)-tile.Min(v=>v.Y), Is.EqualTo(OrionTokens.GeometryChipTileMeters1).Within(0.0001));
            Assert.That(tile.Max(v=>v.Z)-tile.Min(v=>v.Z), Is.EqualTo(OrionTokens.GeometryChipTileRaiseMeters).Within(0.0001));
            Assert.That(card.Max(v=>v.Z)-card.Min(v=>v.Z), Is.EqualTo(OrionTokens.GeometryChipCardThicknessMeters).Within(0.0001));
            Assert.That(tile.Min(v=>v.Z), Is.EqualTo(card.Max(v=>v.Z)).Within(0.0001));
        }

        [Test]
        public void TrianglesAreNonDegenerateOutwardAndNeverMixSurfaceRegions()
        {
            var m = ChipMeshBuilder.Build();
            for (int i=0; i<m.Triangles.Length; i+=3)
            {
                int a=m.Triangles[i], b=m.Triangles[i+1], c=m.Triangles[i+2];
                var cross=FolioVector.Cross(m.Vertices[b]-m.Vertices[a],m.Vertices[c]-m.Vertices[a]);
                Assert.That(cross.Length, Is.GreaterThan(0));
                Assert.That(FolioVector.Dot(cross.Normalized,m.Normals[a]), Is.GreaterThan(0.999));
                Assert.That(m.UV2[b].X, Is.EqualTo(m.UV2[a].X));
                Assert.That(m.UV2[c].X, Is.EqualTo(m.UV2[a].X));
            }
        }
    }
}
