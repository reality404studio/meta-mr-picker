using NUnit.Framework;
using UnityEngine;

namespace MoodPicker.Splatter.Patisserie.Tests
{
    public class PatisserieCoreTests
    {
        [Test]
        public void NozzleProfile_Star6_ProducesValidNormalsAndNoNaN()
        {
            var profile = IcingNozzleProfile.Star6;
            Vector3 Tk = Vector3.forward;
            Vector3 Nk = Vector3.up;
            Vector3 Bk = Vector3.right;
            Vector3 cakeNormal = Vector3.up;

            for (int i = 0; i < profile.splatsPerRing; i++)
            {
                float theta = (2f * Mathf.PI * i) / profile.splatsPerRing;
                profile.EvaluateRingPoint(
                    theta,
                    1.0f,
                    Tk,
                    Nk,
                    Bk,
                    cakeNormal,
                    out Vector3 offset,
                    out Vector3 normal,
                    out float scaleU,
                    out float scaleV);

                Assert.IsFalse(float.IsNaN(offset.x) || float.IsNaN(offset.y) || float.IsNaN(offset.z), "Offset has NaN");
                Assert.IsFalse(float.IsNaN(normal.x) || float.IsNaN(normal.y) || float.IsNaN(normal.z), "Normal has NaN");
                Assert.AreEqual(1f, normal.magnitude, 1e-3f, "Normal is not normalized");
                Assert.Greater(scaleU, 0f, "ScaleU must be positive");
                Assert.Greater(scaleV, 0f, "ScaleV must be positive");
            }
        }

        [Test]
        public void ParallelTransport_MaintainsSmoothFramesAlongRosette()
        {
            Vector3 cakeNormal = Vector3.up;
            var points = new Vector3[36];
            for (int i = 0; i < 36; i++)
            {
                float angle = i * 10f * Mathf.Deg2Rad;
                float r = 0.05f - (i * 0.001f);
                points[i] = new Vector3(Mathf.Cos(angle) * r, i * 0.001f, Mathf.Sin(angle) * r);
            }

            ParallelFrame frame = ParallelFrame.Initialize(points[0], points[1], cakeNormal);

            for (int i = 1; i < points.Length; i++)
            {
                Vector3 prevN = frame.Normal;
                frame = frame.Advance(points[i]);

                float dotN = Vector3.Dot(prevN, frame.Normal);
                Assert.Greater(dotN, 0.85f, $"Parallel transport flipped or jerked at step {i}: dot={dotN}");
                Assert.AreEqual(1f, frame.Tangent.magnitude, 1e-3f);
                Assert.AreEqual(1f, frame.Normal.magnitude, 1e-3f);
                Assert.AreEqual(1f, frame.Binormal.magnitude, 1e-3f);
            }
        }

        [Test]
        public void SampleRing_SqueezeScalesRadiiCorrectly()
        {
            var profile = IcingNozzleProfile.Round;
            var frame = ParallelFrame.Initialize(Vector3.zero, Vector3.forward, Vector3.up);

            SplatSample[] lowPinch = IcingTubeSampler.SampleRing(frame, profile, 0.4f, Vector3.up);
            SplatSample[] highPinch = IcingTubeSampler.SampleRing(frame, profile, 1.2f, Vector3.up);

            Assert.AreEqual(profile.splatsPerRing, lowPinch.Length);
            Assert.AreEqual(profile.splatsPerRing, highPinch.Length);

            float lowDist = Vector3.Distance(new Vector3(lowPinch[0].CenterX, lowPinch[0].CenterY, lowPinch[0].CenterZ), Vector3.zero);
            float highDist = Vector3.Distance(new Vector3(highPinch[0].CenterX, highPinch[0].CenterY, highPinch[0].CenterZ), Vector3.zero);

            Assert.Greater(highDist, lowDist * 2f, "High pinch should result in larger radius than low pinch");
        }

        [Test]
        public void SampleStrokePath_AllPresetsProduceValidSamples()
        {
            var path = new Vector3[]
            {
                new Vector3(0, 0, 0),
                new Vector3(0, 0, 0.02f),
                new Vector3(0.01f, 0, 0.04f)
            };
            var squeezes = new float[] { 1f, 1f, 0.5f };

            IcingNozzleProfile[] presets = {
                IcingNozzleProfile.Star6,
                IcingNozzleProfile.FrenchStar8,
                IcingNozzleProfile.Round,
                IcingNozzleProfile.PetalRibbon
            };

            foreach (var preset in presets)
            {
                SplatSample[] samples = IcingTubeSampler.SampleStrokePath(path, squeezes, preset, Vector3.up);
                Assert.Greater(samples.Length, 0, $"Preset {preset.type} produced 0 samples");

                for (int i = 0; i < samples.Length; i++)
                {
                    Assert.IsFalse(float.IsNaN(samples[i].CenterX), "Sample pos has NaN");
                    Assert.AreEqual(0, samples[i].IsOutline, "IsOutline should be 0 for standard modes");
                }
            }
        }
    }
}
