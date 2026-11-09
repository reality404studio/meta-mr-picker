using System;
using System.Collections.Generic;
using NUnit.Framework;
using Pigment;
using MoodPicker.Splatter;

namespace MoodPicker.Tests
{
    public class SplatterCoreTests
    {
        [Test]
        public void BaselineExcludesZeroDiffDuplicateFrames()
        {
            var frames = new List<float[]>
            {
                new float[] { 0.1f, 0.2f },
                new float[] { 0.1f, 0.2f },
                new float[] { 0.1f, 0.2f },
                new float[] { 0.5f, 0.6f }
            };
            float[] d = LoopFinder.DiffMatrix(frames);
            float baseVal = LoopFinder.Baseline(d, frames.Count);
            Assert.That(baseVal, Is.EqualTo(0.4f).Within(1e-5f));
        }

        [Test]
        public void FlipbookLayoutCapsFramesAt48AndScalesFps()
        {
            int[] picked;
            FlipbookLayout lay = FlipbookLayout.For(90, 48, 256, 30f, out picked);
            Assert.That(lay.Frames, Is.EqualTo(48));
            Assert.That(lay.Cols, Is.EqualTo(7));
            Assert.That(lay.Rows, Is.EqualTo(7));
            Assert.That(lay.Fps, Is.EqualTo(16f).Within(1e-4f));
            Assert.That(picked.Length, Is.EqualTo(48));
        }

        [Test]
        public void PeriodicTileReducesBoundarySeamErrorBelowOne()
        {
            int n = 32;
            var rgba = new byte[n * n * 4];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int idx = (y * n + x) * 4;
                    double wave = 28.0 * Math.Sin(2.0 * Math.PI * x * 4.0 / n) + 28.0 * Math.Cos(2.0 * Math.PI * y * 4.0 / n);
                    byte v = (byte)Math.Clamp((int)Math.Round(70.0 + wave + x * 2.5 + y * 2.5), 0, 255);
                    rgba[idx] = v;
                    rgba[idx + 1] = v;
                    rgba[idx + 2] = v;
                    rgba[idx + 3] = 255;
                }
            }
            float before = PeriodicTile.SeamError(rgba, n);
            PeriodicTile.MakePeriodicRGBA(rgba, n);
            float after = PeriodicTile.SeamError(rgba, n);
            Assert.That(before, Is.GreaterThan(2.0f));
            Assert.That(after, Is.LessThan(1.0f));
        }

        [Test]
        public void TwoToneExtractorSeparatesShadowAndLightAndMidpointThreshold()
        {
            var rgba = new byte[400 * 4];
            for (int i = 0; i < 400; i++)
            {
                byte v = i < 200 ? (byte)25 : (byte)230;
                rgba[i * 4] = v;
                rgba[i * 4 + 1] = v;
                rgba[i * 4 + 2] = v;
                rgba[i * 4 + 3] = 255;
            }
            TwoTonePalette pal = TwoToneExtractor.Extract(rgba, 400, 10);
            Assert.That(pal.ShadowR, Is.LessThan(0.2f));
            Assert.That(pal.LightR, Is.GreaterThan(0.8f));
            Assert.That(pal.Threshold, Is.EqualTo(0.5f).Within(0.05f));
        }

        [Test]
        public void LetterPoseMathFollowsBriefEquations()
        {
            Assert.That(LetterPoseMath.Phase(2), Is.EqualTo(1.8f).Within(1e-5f));
            Assert.That(LetterPoseMath.SlotFrame(1.5f, 10f, 12), Is.EqualTo(3));
            Assert.That(LetterPoseMath.RotationYRadians(0f, 0), Is.EqualTo(0f).Within(1e-6f));
        }
    }
}
