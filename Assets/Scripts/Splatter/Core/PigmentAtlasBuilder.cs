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
        public float SeamErrorAfter;
    }

    public static class PigmentAtlasBuilder
    {
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
                SeamErrorAfter = seamFirst
            };
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
                        double w1 = Math.Sin(2.0 * Math.PI * (nx * 2.0 + ny * 1.0) + phase);
                        double w2 = Math.Cos(2.0 * Math.PI * (nx * 1.0 - ny * 3.0) - phase * 1.3);
                        double caustic = Math.Pow(0.5 + 0.25 * (w1 + w2), 2.2);
                        byte r = (byte)Math.Clamp((int)Math.Round(35.0 + 185.0 * caustic), 0, 255);
                        byte g = (byte)Math.Clamp((int)Math.Round(85.0 + 155.0 * caustic), 0, 255);
                        byte b = (byte)Math.Clamp((int)Math.Round(150.0 + 100.0 * caustic), 0, 255);
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
