using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MoodPicker.Catch;
using NUnit.Framework;
using UnityEngine;

namespace MoodPicker.Tests
{
    public static class CatchImages
    {
        public static string Dir => Path.Combine(Application.dataPath, "Tests", "EditMode", "Fixtures", "Catch");

        public static Color32[] Load(string name, out int w, out int h)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(tex.LoadImage(File.ReadAllBytes(Path.Combine(Dir, name))), name);
            w = tex.width; h = tex.height;
            var px = tex.GetPixels32();
            UnityEngine.Object.DestroyImmediate(tex);
            return px;
        }

        public static Color32[] Noise(int w, int h, int seed, Func<double, double, double> light)
        {
            var rnd = new System.Random(seed);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double l = light((x + 0.5) / w, 1.0 - (y + 0.5) / h);
                    double n = rnd.NextDouble() * 0.12;
                    byte r = (byte)Math.Round(255 * Math.Min(1, 0.15 + 0.7 * l + n));
                    byte g = (byte)Math.Round(255 * Math.Min(1, 0.12 + 0.55 * l + n));
                    byte b = (byte)Math.Round(255 * Math.Min(1, 0.25 + 0.35 * l + n));
                    px[y * w + x] = new Color32(r, g, b, 255);
                }
            return px;
        }

        public static string Hash(Color32[] px)
        {
            var bytes = new byte[px.Length * 4];
            for (int i = 0; i < px.Length; i++) { bytes[i * 4] = px[i].r; bytes[i * 4 + 1] = px[i].g; bytes[i * 4 + 2] = px[i].b; bytes[i * 4 + 3] = px[i].a; }
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes));
        }

        public static Color HexColor(string hex)
        {
            Assert.That(ColorUtility.TryParseHtmlString(hex, out Color c), hex);
            return c;
        }
    }

    public class CatchCropTests
    {
        static readonly Vector2Int Res = new Vector2Int(1280, 960);
        const float Fx = 600f, Cx = 640f, Cy = 480f;

        static Vector2 Project(Vector3 p) => new Vector2((p.x / p.z * Fx + Cx) / Res.x, (p.y / p.z * Fx + Cy) / Res.y);

        [Test]
        public void Crop_KnownWorldSquareMapsToExpectedPixelRect()
        {
            var sq = new CatchSquareView { center = new Vector3(0.1f, 0.05f, 0.5f), rotation = Quaternion.identity, size = 0.2f };
            var vp = sq.Corners().Select(Project).ToArray();
            Assert.That(CropMath.TrySquare(vp, Res, 0.125f, 32, out CropRect c));
            Assert.That(c.side, Is.EqualTo(240).Within(2));
            Assert.That(c.overlap, Is.EqualTo(30));
            Assert.That(c.Still.x, Is.EqualTo(640).Within(2));
            Assert.That(c.Still.y, Is.EqualTo(420).Within(2));
            Assert.That(c.big.y, Is.EqualTo(c.Still.y - c.overlap));
            Assert.That(c.big.width, Is.EqualTo(c.side + c.overlap));
        }

        [Test]
        public void Crop_PixelsAreSourcePixels()
        {
            var src = CatchImages.Noise(Res.x, Res.y, 11, (u, v) => u);
            var sq = new CatchSquareView { center = new Vector3(-0.05f, 0.02f, 0.45f), rotation = Quaternion.identity, size = 0.15f };
            Assert.That(CropMath.TrySquare(sq.Corners().Select(Project).ToArray(), Res, 0.125f, 32, out CropRect c));
            var big = CropMath.Crop(src, Res.x, Res.y, c.big);
            var still = CropMath.StillFromBig(big, c);
            RectInt s = c.Still;
            for (int y = 0; y < s.height; y++)
                for (int x = 0; x < s.width; x++)
                    Assert.That(still[y * s.width + x], Is.EqualTo(src[(s.y + y) * Res.x + s.x + x]), $"({x},{y})");
        }

        [Test]
        public void Crop_NearEdgeStaysInsideFrameAndOversizeShrinks()
        {
            var edge = new CatchSquareView { center = new Vector3(0.38f, 0.28f, 0.5f), rotation = Quaternion.identity, size = 0.2f };
            Assert.That(CropMath.TrySquare(edge.Corners().Select(Project).ToArray(), Res, 0.125f, 32, out CropRect a));
            Assert.That(a.big.xMin >= 0 && a.big.yMin >= 0 && a.big.xMax <= Res.x && a.big.yMax <= Res.y, a.big.ToString());
            var huge = new CatchSquareView { center = new Vector3(0f, 0f, 0.3f), rotation = Quaternion.identity, size = 0.6f };
            Assert.That(CropMath.TrySquare(huge.Corners().Select(Project).ToArray(), Res, 0.125f, 32, out CropRect b));
            Assert.That(b.BigSize, Is.LessThanOrEqualTo(Res.y));
            var behind = new[] { new Vector2(float.NaN, 0f) };
            Assert.That(CropMath.TrySquare(behind, Res, 0.125f, 32, out _), Is.False);
        }

        static string RuntimeSource() =>
            string.Join("\n", Directory.GetFiles(Path.Combine(Application.dataPath, "Scripts", "Catch"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));

        [Test]
        public void Crop_RuntimeReadsOnlyTheCropRegionAsynchronously()
        {
            string src = RuntimeSource();
            foreach (string banned in new[] { "WaitForCompletion", "GetColors(", "ReadPixels", "GetPixels(" })
                Assert.That(src.Contains(banned), Is.False, banned + " 금지: 동기 GPU 읽기 / 전체 프레임 CPU 복사");
            var calls = Regex.Matches(src, @"AsyncGPUReadback\.Request\w*\(([^;]*)\);");
            Assert.That(calls.Count, Is.GreaterThan(0), "AsyncGPUReadback 으로 읽어야 함");
            foreach (Match m in calls)
                Assert.That(m.Groups[1].Value.Count(ch => ch == ','), Is.GreaterThanOrEqualTo(8), "영역(x,w,y,h) 지정 오버로드만: " + m.Value);
        }
    }

    public class CatchProjectionTests
    {
        static readonly Vector2Int Res = new Vector2Int(1280, 960);
        const float F = 870f;
        static readonly Pose Eye = new Pose(new Vector3(0f, 1.2f, 0f), Quaternion.identity);
        static readonly Vector3 LensOffset = new Vector3(0.032f, -0.02f, 0.07f);

        static Vector2 Camera(Vector3 w)
        {
            Vector3 p = w - (Eye.position + LensOffset);
            return new Vector2((p.x / p.z * F + Res.x * 0.5f) / Res.x, (p.y / p.z * F + Res.y * 0.5f) / Res.y);
        }

        static int ExpectedSide(float size, float eyeDistance) => Mathf.RoundToInt(size / eyeDistance * F);

        static CropRect Crop(Vector3[] pts)
        {
            Assert.That(CropMath.TrySquare(pts.Select(Camera).ToArray(), Res, 0.125f, 32, out CropRect c));
            return c;
        }

        [TestCase(0.116f, 0.21f)]
        [TestCase(0.161f, 0.25f)]
        [TestCase(0.170f, 0.30f)]
        [TestCase(0.10f, 0.40f)]
        public void Projection_CropMatchesWhatTheEyeSeesThroughTheSquare(float size, float distance)
        {
            var sq = CatchRay.Square(Eye, distance, size);
            var c = Crop(CatchProjection.ThroughSquare(Eye.position, sq.Corners(), 3f));
            int expected = ExpectedSide(size, distance);
            Assert.That(c.side, Is.EqualTo(expected).Within(Mathf.CeilToInt(expected * 0.04f)), $"side {c.side} vs eye {expected}");
            Assert.That(c.Still.center.x, Is.EqualTo(Res.x * 0.5f).Within(Res.x * 0.02f));
            Assert.That(c.Still.center.y, Is.EqualTo(Res.y * 0.5f).Within(Res.y * 0.02f));
        }

        [Test]
        public void Projection_NearSquareProjectedDirectlyIsTheDeviceBug()
        {
            var sq = CatchRay.Square(Eye, 0.21f, 0.116f);
            int near = Crop(sq.Corners()).side;
            int far = Crop(CatchProjection.ThroughSquare(Eye.position, sq.Corners(), 3f)).side;
            Assert.That(near, Is.GreaterThan(far * 1.25f), "픽스처가 실기 증상(넓게 찍힘)을 재현해야 함");
        }

        [Test]
        public void Projection_RuntimeProjectsThroughTheSquare()
        {
            string src = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Catch", "Runtime", "CatchController.cs"));
            Assert.That(src, Does.Contain("CatchProjection.ThroughSquare("));
        }
    }

    public class CelExtractTests
    {
        static CelResult Run(Color32[] px, int w, int h, int k = 3) => CelExtract.Run(px, w, h, 64, k, 20, 2f);

        [Test]
        public void Cel_YunseulMatchesMacLab()
        {
            var px = CatchImages.Load("yunseul_still.png", out int w, out int h);
            var r = Run(px, w, h);
            var lab = JsonUtility.FromJson<LabPaint>(File.ReadAllText(Path.Combine(CatchImages.Dir, "yunseul_paint_python.json")));
            AssertClose(r.Light, CatchImages.HexColor(lab.lightTone), "light");
            AssertClose(r.Mid, CatchImages.HexColor(lab.midTone), "mid");
            AssertClose(r.Shadow, CatchImages.HexColor(lab.shadowTone), "shadow");
            for (int i = 0; i < 3; i++) Assert.That(r.lightDirView[i], Is.EqualTo(lab.lightDirView[i]).Within(0.02f), "lightDir " + i);
        }

        [Serializable] class LabPaint { public string lightTone, midTone, shadowTone; public float[] lightDirView; }

        static void AssertClose(Color a, Color b, string what)
        {
            Assert.That(Mathf.Abs(a.r - b.r) * 255, Is.LessThanOrEqualTo(2f), what + " r");
            Assert.That(Mathf.Abs(a.g - b.g) * 255, Is.LessThanOrEqualTo(2f), what + " g");
            Assert.That(Mathf.Abs(a.b - b.b) * 255, Is.LessThanOrEqualTo(2f), what + " b");
        }

        [Test]
        public void Cel_LightDirectionSignFollowsBrightSide()
        {
            var left = CatchImages.Noise(256, 256, 3, (u, v) => 1 - u);
            var rl = Run(left, 256, 256);
            Assert.That(rl.lightDirView.x, Is.LessThan(-0.2f), "left-lit → x<0");
            Assert.That(rl.lightDirView.z, Is.LessThan(0f));

            var lowerRight = CatchImages.Noise(256, 256, 4, (u, v) => (u + v) * 0.5);
            var rb = Run(lowerRight, 256, 256);
            Assert.That(rb.lightDirView.x, Is.GreaterThan(0.2f), "lower-right → x>0");
            Assert.That(rb.lightDirView.y, Is.LessThan(-0.2f), "lower-right → y<0");
        }

        [Test]
        public void Cel_TwoOrThreeTonesOrderedDarkToLight()
        {
            var px = CatchImages.Noise(200, 200, 5, (u, v) => v);
            foreach (int k in new[] { 2, 3 })
            {
                var r = Run(px, 200, 200, k);
                Assert.That(r.tones.Length, Is.EqualTo(k));
                for (int i = 1; i < k; i++)
                    Assert.That(Lum(r.tones[i]), Is.GreaterThan(Lum(r.tones[i - 1])));
            }
        }

        static float Lum(Color c) => c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;

        [Test]
        public void Cel_SameInputSameResult()
        {
            var a = Run(CatchImages.Noise(128, 128, 9, (u, v) => u * v), 128, 128);
            var b = Run(CatchImages.Noise(128, 128, 9, (u, v) => u * v), 128, 128);
            Assert.That(b.tones, Is.EqualTo(a.tones));
            Assert.That(b.lightDirView, Is.EqualTo(a.lightDirView));
        }

        [Test]
        public void Cel_DoesNotTouchSourcePixels()
        {
            var px = CatchImages.Load("yunseul_still.png", out int w, out int h);
            string before = CatchImages.Hash(px);
            Run(px, w, h);
            Assert.That(CatchImages.Hash(px), Is.EqualTo(before));
        }
    }

    public class SeamlessTileTests
    {
        [Test]
        public void Tile_TwoByTwoSeamNotAboveInteriorAndCenterIsOriginal()
        {
            var big = CatchImages.Load("yunseul_big.png", out int bw, out int bh);
            Assert.That(bw, Is.EqualTo(bh));
            int s = 480, b = bw - s;
            Assert.That(b, Is.EqualTo(CropMath.Overlap(s, 0.125f)));
            var c = new CropRect { big = new RectInt(0, 0, bw, bw), side = s, overlap = b };
            var still = CropMath.StillFromBig(big, c);
            var tile = SeamlessTile.FromBig(big, c);

            double raw = SeamlessTile.SeamRatio(still, s), seam = SeamlessTile.SeamRatio(tile, s);
            Assert.That(seam, Is.LessThanOrEqualTo(1.0), $"seam/interior {seam:0.000}");
            Assert.That(seam, Is.LessThan(raw), $"raw {raw:0.000}");

            for (int y = 0; y < s - b; y++)
                for (int x = b; x < s; x++)
                    Assert.That(tile[y * s + x], Is.EqualTo(still[y * s + x]), $"band 밖 ({x},{y}) 는 원본");
        }

        [Test]
        public void Tile_MatchesMacLabWithinOneLevel()
        {
            var big = CatchImages.Load("yunseul_big.png", out int bw, out _);
            var tile = SeamlessTile.FromBig(big, bw, 480, bw - 480);
            var lab = CatchImages.Load("yunseul_tile_python.png", out int lw, out _);
            Assert.That(lw, Is.EqualTo(480));
            int worst = 0;
            for (int i = 0; i < tile.Length; i++)
                worst = Math.Max(worst, Math.Max(Math.Abs(tile[i].r - lab[i].r), Math.Max(Math.Abs(tile[i].g - lab[i].g), Math.Abs(tile[i].b - lab[i].b))));
            Assert.That(worst, Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void Tile_RejectsWrongSizes()
        {
            Assert.Throws<ArgumentException>(() => SeamlessTile.FromBig(new Color32[100], 10, 10, 0));
            Assert.Throws<ArgumentException>(() => SeamlessTile.FromBig(new Color32[99], 10, 8, 2));
        }
    }
}
