using System;
using NUnit.Framework;
using MoodPicker.Splatter.Sim;
using Unity.Mathematics;

namespace MoodPicker.Tests
{
    public class SplatSimTests
    {
        const float Dt = 1f / 72f;

        static float3[] Sheet(int side, float spacing)
        {
            var h = new float3[side * side];
            float half = (side - 1) * spacing * 0.5f;
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                    h[y * side + x] = new float3(x * spacing - half, y * spacing - half, 0f);
            return h;
        }

        static SplatSimFrame Frame(SplatSimMode mode = SplatSimMode.Formed, float modeTime = 0f, float dt = Dt)
        {
            var identity = new float3x4(new float3(1, 0, 0), new float3(0, 1, 0), new float3(0, 0, 1), float3.zero);
            return new SplatSimFrame { bodyToWorld = identity, prevBodyToWorld = identity, dt = dt, mode = (int)mode, modeTime = modeTime };
        }

        static SplatCollider Finger(float3 pos, float3 vel)
        {
            return new SplatCollider { position = pos, velocity = vel, radius = 0.022f, strength = 1f };
        }

        static void Run(SplatSimReference sim, SplatSimParams p, float seconds, SplatSimMode mode = SplatSimMode.Formed)
        {
            var none = new SplatCollider[0];
            int steps = (int)math.round(seconds / Dt);
            for (int i = 0; i < steps; i++) sim.Step(Frame(mode, i * Dt), p, none, 0);
        }

        [Test]
        public void DisplacedParticlesSettleBackHome()
        {
            var sim = new SplatSimReference(Sheet(20, 0.002f), 8);
            for (int i = 0; i < sim.Particles.Length; i++)
            {
                sim.Particles[i].displacement = SplatSimKernel.HashDirection(sim.Hash[i]) * 0.05f;
                sim.Particles[i].velocity = new float3(0.5f, -0.3f, 0.2f);
            }
            Run(sim, SplatSimParams.WaterDroplet, 4f);
            Assert.That(sim.Activity, Is.LessThan(1e-7f));
        }

        [Test]
        public void SweepingFingerLeavesParticlesMovingAlongItsDirection()
        {
            var sim = new SplatSimReference(Sheet(20, 0.002f), 8);
            SplatSimParams p = SplatSimParams.WaterDroplet;
            var c = new SplatCollider[1];
            float speed = 0.8f;
            float x = -0.06f;
            while (x < 0.01f)
            {
                c[0] = Finger(new float3(x, 0f, 0.004f), new float3(speed, 0f, 0f));
                sim.Step(Frame(), p, c, 1);
                x += speed * Dt;
            }
            float along = 0f;
            int touched = 0;
            foreach (SplatParticle s in sim.Particles)
            {
                if (math.lengthsq(s.displacement) < 1e-6f) continue;
                touched++;
                along += s.velocity.x;
            }
            Assert.That(touched, Is.GreaterThan(40));
            Assert.That(along / touched, Is.GreaterThan(0.05f));
        }

        [Test]
        public void RestingFingerOpensAHoleInsteadOfOverlappingParticles()
        {
            var sim = new SplatSimReference(Sheet(30, 0.002f), 12);
            SplatSimParams p = SplatSimParams.WaterDroplet;
            var c = new[] { Finger(float3.zero, float3.zero) };
            for (int i = 0; i < 144; i++) sim.Step(Frame(), p, c, 1);
            int inside = 0;
            for (int i = 0; i < sim.Particles.Length; i++)
                if (math.length(sim.Homes[i] + sim.Particles[i].displacement) < 0.022f * 0.6f) inside++;
            Assert.That(inside, Is.EqualTo(0));
        }

        static float MeanClusterSpread(SplatSimReference sim)
        {
            int k = sim.Clusters.Count;
            var sum = new float3[k];
            var count = new int[k];
            for (int i = 0; i < sim.Particles.Length; i++)
            {
                int cl = sim.Clusters.Assignment[i];
                sum[cl] += sim.Homes[i] + sim.Particles[i].displacement;
                count[cl]++;
            }
            float spread = 0f;
            for (int i = 0; i < sim.Particles.Length; i++)
            {
                int cl = sim.Clusters.Assignment[i];
                spread += math.distance(sim.Homes[i] + sim.Particles[i].displacement, sum[cl] / count[cl]);
            }
            return spread / sim.Particles.Length;
        }

        static float SpreadAfterSweep(float cohesion)
        {
            var sim = new SplatSimReference(Sheet(30, 0.002f), 9);
            SplatSimParams p = SplatSimParams.WaterDroplet;
            p.cohesion = cohesion;
            var c = new SplatCollider[1];
            float speed = 1.2f;
            float x = -0.08f;
            while (x < 0.08f)
            {
                c[0] = Finger(new float3(x, 0f, 0.002f), new float3(speed, 0f, 0f));
                sim.Step(Frame(), p, c, 1);
                x += speed * Dt;
            }
            for (int i = 0; i < 6; i++) sim.Step(Frame(), p, c, 0);
            return MeanClusterSpread(sim);
        }

        [Test]
        public void CohesionKeepsSplashedParticlesInTighterBlobs()
        {
            float loose = SpreadAfterSweep(0f);
            float tight = SpreadAfterSweep(1f);
            Assert.That(tight, Is.LessThan(loose * 0.8f));
        }

        [Test]
        public void SummonStartsHiddenAtEmitterAndEndsFormedWithoutOvershoot()
        {
            var sim = new SplatSimReference(Sheet(20, 0.004f), 8);
            SplatSimParams p = SplatSimParams.WaterDroplet;
            var emitter = new float3(-0.25f, -0.1f, -0.15f);
            SplatSimFrame f0 = Frame(SplatSimMode.Summoning);
            float last = SplatSimSetup.SummonArrivals(sim.Homes, sim.Hash, sim.Arrival, f0.bodyToWorld, emitter, 1.2f);
            Assert.That(last, Is.InRange(0.9f, 1.2f));

            var none = new SplatCollider[0];
            SplatSimFrame f = f0;
            f.emitter = emitter;
            f.modeTime = -Dt;
            sim.Step(f, p, none, 0);
            foreach (SplatParticle s in sim.Particles) Assert.That(s.scale, Is.EqualTo(0f));
            for (int i = 0; i < sim.Particles.Length; i++)
                Assert.That(math.distance(sim.Homes[i] + sim.Particles[i].displacement, emitter), Is.LessThan(0.02f));

            float maxPast = 0f;
            int steps = (int)((last + 2.0f) / Dt);
            for (int i = 0; i < steps; i++)
            {
                f.modeTime = i * Dt;
                sim.Step(f, p, none, 0);
                for (int j = 0; j < sim.Particles.Length; j++)
                {
                    float3 d = sim.Particles[j].displacement;
                    float3 start = emitter - sim.Homes[j];
                    maxPast = math.max(maxPast, -math.dot(d, math.normalize(start)));
                }
            }
            float worst = 0f;
            foreach (SplatParticle s in sim.Particles)
            {
                worst = math.max(worst, math.length(s.displacement));
                Assert.That(s.scale, Is.EqualTo(1f));
            }
            Assert.That(worst, Is.LessThan(0.002f));
            Assert.That(maxPast, Is.LessThan(0.02f));
        }

        [Test]
        public void DissolveReleasesParticlesToFallAndFade()
        {
            var sim = new SplatSimReference(Sheet(20, 0.004f), 8);
            SplatSimParams p = SplatSimParams.WaterDroplet;
            float last = SplatSimSetup.DissolveArrivals(sim.Homes, sim.Hash, sim.Arrival, 0.2f, 0.35f);
            var none = new SplatCollider[0];
            int steps = (int)((last + 1.6f) / Dt);
            float prevMeanY = 0f;
            for (int i = 0; i < steps; i++)
            {
                sim.Step(Frame(SplatSimMode.Dissolving, i * Dt), p, none, 0);
                float meanY = 0f;
                foreach (SplatParticle s in sim.Particles) meanY += s.displacement.y;
                meanY /= sim.Particles.Length;
                Assert.That(meanY, Is.LessThanOrEqualTo(prevMeanY + 1e-6f));
                prevMeanY = meanY;
            }
            foreach (SplatParticle s in sim.Particles) Assert.That(s.scale, Is.LessThan(0.01f));
        }

        [Test]
        public void MovingBodyLeavesParticlesBehindByLagOnly()
        {
            var sim = new SplatSimReference(Sheet(10, 0.004f), 4);
            SplatSimParams p = SplatSimParams.WaterDroplet;
            p.cohesion = 0f;
            SplatSimFrame f = Frame();
            f.bodyToWorld.c3 = new float3(0.05f, 0f, 0f);
            f.lag = 0.15f;
            f.dt = 1e-6f;
            sim.Step(f, p, new SplatCollider[0], 0);
            foreach (SplatParticle s in sim.Particles)
                Assert.That(s.displacement.x, Is.EqualTo(-0.0075f).Within(1e-4f));

            var rigid = new SplatSimReference(Sheet(10, 0.004f), 4);
            f.lag = 0f;
            rigid.Step(f, p, new SplatCollider[0], 0);
            Assert.That(rigid.Activity, Is.LessThan(1e-9f));
        }

        [Test]
        public void StickyParticlesTrailTheFingerAfterContact()
        {
            var sim = new SplatSimReference(Sheet(30, 0.002f), 12);
            SplatSimParams p = SplatSimParams.WaterDroplet;
            p.stickFraction = 0.3f;
            var c = new SplatCollider[1];
            for (int i = 0; i < 36; i++)
            {
                c[0] = Finger(new float3(0f, 0f, 0.004f), float3.zero);
                sim.Step(Frame(), p, c, 1);
            }
            float z = 0.004f;
            for (int i = 0; i < 18; i++)
            {
                z += 0.25f * Dt;
                c[0] = Finger(new float3(0f, 0f, z), new float3(0f, 0f, 0.25f));
                sim.Step(Frame(), p, c, 1);
            }
            int trailing = 0;
            for (int i = 0; i < sim.Particles.Length; i++)
                if (sim.Particles[i].stick > 0.2f && sim.Particles[i].displacement.z > 0.02f) trailing++;
            Assert.That(trailing, Is.GreaterThan(5));
        }

        [Test]
        public void StaysFiniteAtLargestTimeStepWithFastHands()
        {
            var sim = new SplatSimReference(Sheet(24, 0.002f), 10);
            SplatSimParams p = SplatSimParams.WaterDroplet;
            var c = new SplatCollider[2];
            var rng = new System.Random(7);
            for (int i = 0; i < 400; i++)
            {
                var a = new float3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 0.08f;
                var b = new float3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 0.08f;
                c[0] = Finger(a, (a - b) * 60f);
                c[1] = Finger(b, (b - a) * 60f);
                sim.Step(Frame(SplatSimMode.Formed, 0f, SplatSimKernel.MaxDt), p, c, 2);
            }
            foreach (SplatParticle s in sim.Particles)
            {
                Assert.That(math.all(math.isfinite(s.displacement)), Is.True);
                Assert.That(math.length(s.velocity), Is.LessThanOrEqualTo(p.maxSpeed + 1e-3f));
            }
            Run(sim, p, 5f);
            Assert.That(sim.Activity, Is.LessThan(1e-7f));
        }

        [Test]
        public void ClustersCoverEveryParticleWithBoundedRadius()
        {
            float3[] homes = Sheet(40, 0.002f);
            SplatClusters cl = SplatSimSetup.BuildClusters(homes, 16);
            var count = new int[cl.Count];
            foreach (int a in cl.Assignment) count[a]++;
            foreach (int n in count) Assert.That(n, Is.GreaterThan(0));
            foreach (float r in cl.Radius) Assert.That(r, Is.LessThan(0.04f));
        }
    }
}
