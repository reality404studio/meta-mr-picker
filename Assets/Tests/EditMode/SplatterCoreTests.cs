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

        [Test]
        public void MeshSplatSamplerDistributesByTriangleAreaAndCreatesOutlineLayer()
        {
            var pos = new float[]
            {
                0f, 0f, 0f,
                1f, 0f, 0f,
                0f, 1f, 0f,
                10f, 0f, 0f,
                12f, 0f, 0f,
                10f, 2f, 0f
            };
            var nrm = new float[]
            {
                0f, 0f, 1f,
                0f, 0f, 1f,
                0f, 0f, 1f,
                0f, 0f, 1f,
                0f, 0f, 1f,
                0f, 0f, 1f
            };
            var tris = new int[] { 0, 1, 2, 3, 4, 5 };
            SplatSample[] samples = MeshSplatSampler.SampleMesh(pos, nrm, tris, 1000, 0.1f, true, 42);
            Assert.That(samples.Length, Is.EqualTo(2000));

            int smallTriCount = 0;
            int largeTriCount = 0;
            for (int i = 0; i < 1000; i++)
            {
                SplatSample s = samples[i];
                Assert.That(s.IsOutline, Is.EqualTo(0));
                Assert.That(s.ScaleNormal, Is.LessThanOrEqualTo(s.ScaleTangentU * 0.1f + 1e-6f));
                float qNorm = s.RotX * s.RotX + s.RotY * s.RotY + s.RotZ * s.RotZ + s.RotW * s.RotW;
                Assert.That(qNorm, Is.EqualTo(1f).Within(1e-4f));
                if (s.CenterX < 5f) smallTriCount++;
                else largeTriCount++;

                SplatSample o = samples[1000 + i];
                Assert.That(o.IsOutline, Is.EqualTo(1));
                Assert.That(o.CenterZ - s.CenterZ, Is.EqualTo(0.018f * 0.1f).Within(1e-5f));
            }
            Assert.That(smallTriCount, Is.InRange(170, 230));
            Assert.That(largeTriCount, Is.InRange(770, 830));
        }

        [Test]
        public void WeldedSmoothNormalsAverageHardCornerVertices()
        {
            var pos = new float[]
            {
                0f, 0f, 0f,  1f, 0f, 0f,  0f, 1f, 0f,
                0f, 0f, 0f,  0f, 1f, 0f,  0f, 0f, 1f
            };
            var tris = new int[] { 0, 1, 2, 3, 4, 5 };
            float[] smooth = MeshSplatSampler.ComputeWeldedSmoothNormals(pos, tris, 1e-4f);
            Assert.That(smooth[0], Is.EqualTo(smooth[9]).Within(1e-5f));
            Assert.That(smooth[1], Is.EqualTo(smooth[10]).Within(1e-5f));
            Assert.That(smooth[2], Is.EqualTo(smooth[11]).Within(1e-5f));
            Assert.That(Math.Abs(smooth[0]), Is.GreaterThan(0.1f));
            Assert.That(Math.Abs(smooth[2]), Is.GreaterThan(0.1f));
        }

        [Test]
        public void SplatCloudMeshBuilderGeneratesQuadVerticesAndPreservesSplatCenter()
        {
            MeshSplatSampler.NormalToQuaternion(0f, 0f, 1f, out float qx, out float qy, out float qz, out float qw);
            var samples = new SplatSample[]
            {
                new SplatSample
                {
                    CenterX = 1.5f, CenterY = -0.5f, CenterZ = 2.0f,
                    NormalX = 0f, NormalY = 0f, NormalZ = 1f,
                    ScaleTangentU = 0.01f, ScaleTangentV = 0.01f, ScaleNormal = 0.0008f,
                    RotX = qx, RotY = qy, RotZ = qz, RotW = qw,
                    IsOutline = 1
                }
            };
            SplatCloudMeshData data = SplatCloudMeshBuilder.Build(samples);
            Assert.That(data.SplatCount, Is.EqualTo(1));
            Assert.That(data.VerticesXYZ.Length, Is.EqualTo(12));
            Assert.That(data.Indices.Length, Is.EqualTo(6));
            for (int c = 0; c < 4; c++)
            {
                Assert.That(data.CenterAndFlagsXYZW[c * 4], Is.EqualTo(1.5f).Within(1e-5f));
                Assert.That(data.CenterAndFlagsXYZW[c * 4 + 1], Is.EqualTo(-0.5f).Within(1e-5f));
                Assert.That(data.CenterAndFlagsXYZW[c * 4 + 2], Is.EqualTo(2.0f).Within(1e-5f));
                Assert.That(data.CenterAndFlagsXYZW[c * 4 + 3], Is.EqualTo(1.0f).Within(1e-5f));
            }
        }

        [Test]
        public void PigmentAtlasBuilderProducesSeamlessWaterWaveSlotAndTwoTonePalette()
        {
            BuiltPigmentSlot slot = PigmentAtlasBuilder.BuildSampleWaterWaveSlot(32, 16, 15f);
            Assert.That(slot.Layout.Frames, Is.GreaterThanOrEqualTo(6));
            Assert.That(slot.SeamErrorAfter, Is.LessThan(1.0f));
            Assert.That(slot.Palette.LightB, Is.GreaterThan(slot.Palette.ShadowB));
            Assert.That(slot.Palette.Threshold, Is.InRange(0.15f, 0.85f));
        }

        [Test]
        public void SplatWordStateAppliesSlotAndModeToSelectionOrAllWhenNoneSelected()
        {
            var state = new SplatWordState(5, 0, SplatShaderMode.TwoToneCel);
            state.ApplyModeToSelectionOrAll(SplatShaderMode.Glint);
            for (int i = 0; i < 5; i++)
                Assert.That(state.GetMode(i), Is.EqualTo(SplatShaderMode.Glint));

            state.ToggleSelect(1);
            state.ToggleSelect(3);
            state.ApplySlotToSelectionOrAll(2);
            state.ApplyModeToSelectionOrAll(SplatShaderMode.Chrome);

            Assert.That(state.GetSlot(0), Is.EqualTo(0));
            Assert.That(state.GetSlot(1), Is.EqualTo(2));
            Assert.That(state.GetSlot(2), Is.EqualTo(0));
            Assert.That(state.GetSlot(3), Is.EqualTo(2));
            Assert.That(state.GetSlot(4), Is.EqualTo(0));

            Assert.That(state.GetMode(0), Is.EqualTo(SplatShaderMode.Glint));
            Assert.That(state.GetMode(1), Is.EqualTo(SplatShaderMode.Chrome));
            Assert.That(state.GetMode(3), Is.EqualTo(SplatShaderMode.Chrome));
        }
    }
}
