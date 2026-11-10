using System;
using System.Collections.Generic;
using Pigment;

namespace MoodPicker.Splatter
{
    public sealed class BuiltPigmentSlot
    {
        public byte[] AtlasRgba;
        public FlipbookLayout Layout;
        public LoopResult Loop;
        public TwoTonePalette Palette;
        public float SeamErrorBefore;
        public float SeamErrorAfter;
    }

    public static class PigmentAtlasBuilder
    {
        public static float[] DownsampleGray64(byte[] rgba, int tileSize)
        {
            const int G = 64;
            var gray = new float[G * G];
            int step = Math.Max(1, tileSize / G);
            float inv = 1f / (step * step * 255f);
            for (int gy = 0; gy < G; gy++)
            {
                for (int gx = 0; gx < G; gx++)
                {
                    float sum = 0f;
                    int y0 = gy * step;
                    int x0 = gx * step;
                    for (int dy = 0; dy < step; dy++)
                    {
                        int row = (y0 + dy) * tileSize;
                        for (int dx = 0; dx < step; dx++)
                        {
                            int p = (row + x0 + dx) * 4;
                            sum += 0.2126f * rgba[p] + 0.7152f * rgba[p + 1] + 0.0722f * rgba[p + 2];
                        }
                    }
                    gray[gy * G + gx] = sum * inv;
                }
            }
            return gray;
        }

        public static BuiltPigmentSlot BuildFromFrames(
            IReadOnlyList<byte[]> framesRgba,
            IReadOnlyList<float[]> framesGray64,
            int tileSize,
            float sourceFps = 30f,
            float minSec = 1.0f,
            float maxSec = 3.0f,
            int fadeFrames = 8,
            int maxAtlasFrames = 48)
        {
            if (framesRgba == null || framesGray64 == null)
                throw new ArgumentNullException(nameof(framesRgba));
            int n = Math.Min(framesRgba.Count, framesGray64.Count);
            if (n < 4)
                throw new ArgumentException("Need at least 4 frames to build a loop atlas");

            float[] diff = LoopFinder.DiffMatrix(framesGray64);
            LoopResult loop = LoopFinder.Find(diff, n, sourceFps, minSec, maxSec, fadeFrames);
            int fEff = Math.Min(fadeFrames, Math.Max(0, n - loop.End));
            FlipbookLayout layout = FlipbookLayout.For(loop.Length, maxAtlasFrames, tileSize, sourceFps, out int[] picked);

            var atlas = new byte[layout.Width * layout.Height * 4];
            var frameBuf = new byte[tileSize * tileSize * 4];
            float seamBefore = 0f;
            float seamFirst = 0f;

            for (int k = 0; k < layout.Frames; k++)
            {
                int t = picked[k];
                byte[] srcA = framesRgba[loop.Start + t];
                if (t < fEff && loop.End + t < n)
                {
                    float w = LoopFinder.CrossfadeWeight(t, fEff);
                    byte[] srcB = framesRgba[loop.End + t];
                    for (int p = 0; p < frameBuf.Length; p++)
                    {
                        int v = (int)Math.Round(srcB[p] * (1f - w) + srcA[p] * w);
                        frameBuf[p] = (byte)Math.Clamp(v, 0, 255);
                    }
                }
                else
                {
                    Buffer.BlockCopy(srcA, 0, frameBuf, 0, frameBuf.Length);
                }

                if (k == 0) seamBefore = PeriodicTile.SeamError(frameBuf, tileSize);
                PeriodicTile.MakePeriodicRGBA(frameBuf, tileSize);
                if (k == 0) seamFirst = PeriodicTile.SeamError(frameBuf, tileSize);

                layout.PixelRectUnity(k, out int ox, out int oy);
                for (int row = 0; row < tileSize; row++)
                {
                    Buffer.BlockCopy(
                        frameBuf,
                        row * tileSize * 4,
                        atlas,
                        ((oy + row) * layout.Width + ox) * 4,
                        tileSize * 4);
                }
            }

            TwoTonePalette palette = TwoToneExtractor.Extract(atlas, 6000, 10);

            return new BuiltPigmentSlot
            {
                AtlasRgba = atlas,
                Layout = layout,
                Loop = loop,
                Palette = palette,
                SeamErrorBefore = seamBefore,
                SeamErrorAfter = seamFirst
            };
        }

        static double FlowingWaveField(double nx, double ny, double phase)
        {
            double tau = 2.0 * Math.PI;
            double warp = 0.38 * Math.Sin(tau * (nx - ny) - phase) + 0.22 * Math.Cos(tau * (nx + 2.0 * ny) + phase);
            double ribbon1 = Math.Sin(tau * (nx + ny) + warp + phase);
            double ribbon2 = Math.Cos(tau * (2.0 * nx - ny) - warp * 1.2 - phase);
            double v = 0.5 + 0.30 * ribbon1 + 0.20 * ribbon2;
            return Math.Clamp(v, 0.0, 1.0);
        }

        public static BuiltPigmentSlot BuildSampleWaterWaveSlot(int tileSize = 64, int frameCount = 24, float fps = 15f)
        {
            var rgbaList = new List<byte[]>(frameCount);
            var grayList = new List<float[]>(frameCount);

            for (int f = 0; f < frameCount; f++)
            {
                var rgba = new byte[tileSize * tileSize * 4];
                var gray = new float[tileSize * tileSize];
                double phase = 2.0 * Math.PI * f / Math.Max(1, frameCount - 4);

                for (int y = 0; y < tileSize; y++)
                {
                    double ny = (double)y / tileSize;
                    for (int x = 0; x < tileSize; x++)
                    {
                        double nx = (double)x / tileSize;
                        double field = FlowingWaveField(nx, ny, phase);
                        byte r = (byte)Math.Clamp((int)Math.Round(38.0 + 165.0 * field), 0, 255);
                        byte g = (byte)Math.Clamp((int)Math.Round(105.0 + 135.0 * field), 0, 255);
                        byte b = (byte)Math.Clamp((int)Math.Round(180.0 + 72.0 * field), 0, 255);
                        int p = (y * tileSize + x) * 4;
                        rgba[p] = r;
                        rgba[p + 1] = g;
                        rgba[p + 2] = b;
                        rgba[p + 3] = 255;
                        gray[y * tileSize + x] = TwoToneExtractor.LumaSrgb(r / 255f, g / 255f, b / 255f);
                    }
                }
                rgbaList.Add(rgba);
                grayList.Add(gray);
            }

            return BuildFromFrames(rgbaList, grayList, tileSize, fps, 0.8f, 2.0f, 4, 48);
        }

        public static BuiltPigmentSlot BuildSampleSunsetEmberSlot(int tileSize = 64, int frameCount = 24, float fps = 15f)
        {
            var rgbaList = new List<byte[]>(frameCount);
            var grayList = new List<float[]>(frameCount);

            for (int f = 0; f < frameCount; f++)
            {
                var rgba = new byte[tileSize * tileSize * 4];
                var gray = new float[tileSize * tileSize];
                double phase = 2.0 * Math.PI * f / Math.Max(1, frameCount - 4);

                for (int y = 0; y < tileSize; y++)
                {
                    double ny = (double)y / tileSize;
                    for (int x = 0; x < tileSize; x++)
                    {
                        double nx = (double)x / tileSize;
                        double field = FlowingWaveField(ny, nx, phase + 0.9);
                        byte r = (byte)Math.Clamp((int)Math.Round(162.0 + 88.0 * field), 0, 255);
                        byte g = (byte)Math.Clamp((int)Math.Round(72.0 + 145.0 * field), 0, 255);
                        byte b = (byte)Math.Clamp((int)Math.Round(96.0 + 85.0 * field), 0, 255);
                        int p = (y * tileSize + x) * 4;
                        rgba[p] = r;
                        rgba[p + 1] = g;
                        rgba[p + 2] = b;
                        rgba[p + 3] = 255;
                        gray[y * tileSize + x] = TwoToneExtractor.LumaSrgb(r / 255f, g / 255f, b / 255f);
                    }
                }
                rgbaList.Add(rgba);
                grayList.Add(gray);
            }

            return BuildFromFrames(rgbaList, grayList, tileSize, fps, 0.8f, 2.0f, 4, 48);
        }

        public static BuiltPigmentSlot BuildSampleEmeraldCanopySlot(int tileSize = 64, int frameCount = 24, float fps = 15f)
        {
            var rgbaList = new List<byte[]>(frameCount);
            var grayList = new List<float[]>(frameCount);

            for (int f = 0; f < frameCount; f++)
            {
                var rgba = new byte[tileSize * tileSize * 4];
                var gray = new float[tileSize * tileSize];
                double phase = 2.0 * Math.PI * f / Math.Max(1, frameCount - 4);

                for (int y = 0; y < tileSize; y++)
                {
                    double ny = (double)y / tileSize;
                    for (int x = 0; x < tileSize; x++)
                    {
                        double nx = (double)x / tileSize;
                        double field = FlowingWaveField(nx + 0.25, ny + 0.15, phase + 1.8);
                        byte r = (byte)Math.Clamp((int)Math.Round(48.0 + 150.0 * field), 0, 255);
                        byte g = (byte)Math.Clamp((int)Math.Round(125.0 + 122.0 * field), 0, 255);
                        byte b = (byte)Math.Clamp((int)Math.Round(95.0 + 110.0 * field), 0, 255);
                        int p = (y * tileSize + x) * 4;
                        rgba[p] = r;
                        rgba[p + 1] = g;
                        rgba[p + 2] = b;
                        rgba[p + 3] = 255;
                        gray[y * tileSize + x] = TwoToneExtractor.LumaSrgb(r / 255f, g / 255f, b / 255f);
                    }
                }
                rgbaList.Add(rgba);
                grayList.Add(gray);
            }

            return BuildFromFrames(rgbaList, grayList, tileSize, fps, 0.8f, 2.0f, 4, 48);
        }
    }
}
