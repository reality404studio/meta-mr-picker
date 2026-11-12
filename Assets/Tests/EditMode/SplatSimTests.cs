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
    
        static void Burst(SplatSimReference sim, SplatSimParams p, float speed, float seconds)
        {
            sim.BeginBurst(speed);
            var none = new SplatCollider[0];
            int steps = (int)math.round(seconds / Dt);
            for (int i = 0; i < steps; i++) sim.Step(Frame(SplatSimMode.Bursting, i * Dt), p, none, 0);
        }

        static float MeanDisplacement(SplatSimReference sim)
        {
            float sum = 0f;
            foreach (SplatParticle s in sim.Particles) sum += math.length(s.displacement);
            return sum / sim.Particles.Length;
        }

        static SplatFeel Mid => new SplatFeel { weight = 0.5f, cohesion = 0.5f, viscosity = 0.5f, bounce = 0.5f, grain = 0.5f };

        static SplatSimParams With(SplatFeel f)
        {
            SplatSimParams p = SplatSimParams.WaterDroplet;
            SplatFeelMap.Apply(f, ref p);
            return p;
        }

        [Test]
        public void EachFeelValuePushesItsOwnDirection()
        {
            SplatFeel lo = Mid, hi = Mid;
            lo.weight = 0.1f; hi.weight = 0.9f;
            Assert.That(With(hi).fallGravity, Is.GreaterThan(With(lo).fallGravity));
            Assert.That(With(hi).burstGravity, Is.GreaterThan(With(lo).burstGravity));
            lo = Mid; hi = Mid; lo.cohesion = 0.1f; hi.cohesion = 0.9f;
            Assert.That(With(hi).cohesion, Is.GreaterThan(With(lo).cohesion));
            Assert.That(With(hi).clusterViscosity, Is.GreaterThan(With(lo).clusterViscosity));
            lo = Mid; hi = Mid; lo.viscosity = 0.1f; hi.viscosity = 0.9f;
            Assert.That(With(hi).homeDamping, Is.GreaterThan(With(lo).homeDamping));
            lo = Mid; hi = Mid; lo.bounce = 0.1f; hi.bounce = 0.9f;
            Assert.That(With(hi).homeStiffness, Is.GreaterThan(With(lo).homeStiffness));
            lo = Mid; hi = Mid; lo.grain = 0.1f; hi.grain = 0.9f;
            Assert.That(SplatFeelMap.Look(hi).grain, Is.LessThan(SplatFeelMap.Look(lo).grain));
            Assert.That(SplatFeelMap.Look(hi).stretchMax, Is.LessThan(SplatFeelMap.Look(lo).stretchMax));
        }

        static SplatSimReference Press(SplatFeel feel, float pressSeconds, float holdSeconds, float afterSeconds)
        {
            var sim = new SplatSimReference(Sheet(30, 0.004f), 12);
            SplatSimParams p = With(feel);
            var c = new SplatCollider[1];
            float speed = 0.018f / pressSeconds;
            int press = (int)math.round(pressSeconds / Dt);
            int hold = (int)math.round(holdSeconds / Dt);
            for (int i = 0; i < press + hold; i++)
            {
                bool moving = i < press;
                float z = math.lerp(0.03f, 0.012f, math.saturate((float)i / press));
                c[0] = Finger(new float3(0f, 0f, z), moving ? new float3(0f, 0f, -speed) : float3.zero);
                sim.Step(Frame(), p, c, 1);
            }
            Run(sim, p, afterSeconds);
            return sim;
        }

        static float MeanZ(SplatSimReference sim, float rMin, float rMax)
        {
            float sum = 0f;
            int n = 0;
            for (int i = 0; i < sim.Particles.Length; i++)
            {
                float r = math.length(sim.Homes[i].xy);
                if (r < rMin || r >= rMax) continue;
                sum += sim.Particles[i].displacement.z;
                n++;
            }
            return sum / math.max(1, n);
        }

        [Test]
        public void PuddingDentsAsOneSurfaceWithoutOpeningAHole()
        {
            SplatSimReference sim = Press(SplatFeel.Jelly, 0.3f, 0.5f, 0f);
            Assert.That(MeanZ(sim, 0f, 0.012f), Is.LessThan(-0.002f));
            float worst = 0f;
            for (int y = 0; y < 30; y++)
                for (int x = 0; x < 29; x++)
                {
                    int i = y * 30 + x;
                    float3 a = sim.Homes[i] + sim.Particles[i].displacement;
                    float3 b = sim.Homes[i + 1] + sim.Particles[i + 1].displacement;
                    worst = math.max(worst, math.abs(math.distance(a, b) - 0.004f));
                }
            Assert.That(worst, Is.LessThan(0.002f), "neighbouring dots keep their spacing: no hole, no clumping");
        }

        [Test]
        public void PuddingTopSwaysWhileTheBottomStaysOnThePlate()
        {
            var sim = new SplatSimReference(Sheet(30, 0.004f), 12);
            SplatSimParams p = With(SplatFeel.Jelly);
            for (int c = 0; c < sim.Nodes.Length; c++) if (sim.Clusters.Home[c].y > 0.02f) sim.Nodes[c].velocity = new float3(0.3f, 0f, 0f);
            float top = 0f, bottom = 0f;
            for (int step = 0; step < (int)(1f / Dt); step++)
            {
                sim.Step(Frame(), p, new SplatCollider[0], 0);
                for (int i = 0; i < sim.Particles.Length; i++)
                {
                    float y = sim.Homes[i].y;
                    if (y > 0.03f) top = math.max(top, math.abs(sim.Particles[i].displacement.x));
                    if (y < -0.05f) bottom = math.max(bottom, math.abs(sim.Particles[i].displacement.x));
                }
            }
            Assert.That(top, Is.GreaterThan(bottom * 3f));
            Run(sim, p, 5f);
            Assert.That(sim.Activity, Is.LessThan(1e-6f));
        }

        [Test]
        public void EveryDotFollowsFourWeightedSkeletonPoints()
        {
            var sim = new SplatSimReference(Sheet(30, 0.004f), 12);
            for (int i = 0; i < sim.Homes.Length; i++)
            {
                float sum = math.csum(sim.Clusters.ParticleWeights[i]);
                if (sim.Homes[i].y > -0.02f) Assert.That(sum, Is.EqualTo(1f).Within(1e-4f));
                if (sim.Homes[i].y < -0.057f) Assert.That(sum, Is.LessThan(1e-4f), "the row on the plate does not move");
            }
            Assert.That(sim.Clusters.Edges.Length, Is.GreaterThanOrEqualTo(sim.Nodes.Length * SplatSimSetup.FieldNeighbors / 2));
            foreach (int2 e in sim.Clusters.Edges) Assert.That(e.x, Is.LessThan(e.y));
            Assert.That(sim.Clusters.Pin, Has.Some.GreaterThan(0.99f));
            Assert.That(sim.Clusters.Pin, Has.Some.LessThan(0.01f));
        }

        [Test]
        public void JellySpringsBackButSlimeKeepsTheDent()
        {
            float jelly = MeanZ(Press(SplatFeel.Jelly, 0.3f, 0.5f, 3f), 0f, 0.018f);
            float slime = MeanZ(Press(SplatFeel.Slime, 0.3f, 0.5f, 3f), 0f, 0.018f);
            Assert.That(jelly, Is.GreaterThan(-0.001f));
            Assert.That(slime, Is.LessThan(-0.001f));
        }

        [Test]
        public void StarchPushesBackHarderAgainstAFastPokeThanASlowPress()
        {
            float fast = MeanZ(Press(SplatFeel.Starch, 0.03f, 0.02f, 0f), 0f, 0.018f);
            float slow = MeanZ(Press(SplatFeel.Starch, 1.2f, 0.02f, 0f), 0f, 0.018f);
            Assert.That(fast, Is.LessThan(slow - 0.001f));
        }

        [Test]
        public void StarchIsFirmWhenHitFastAndSoftWhenPressedSlowly()
        {
            SplatSimParams starch = With(SplatFeel.Starch);
            SplatSimParams water = With(SplatFeel.Water);
            Assert.That(SplatSimKernel.Firmness(1.5f, starch), Is.GreaterThan(2f));
            Assert.That(SplatSimKernel.Firmness(0.02f, starch), Is.LessThan(0.4f));
            Assert.That(SplatSimKernel.Firmness(1.5f, water), Is.EqualTo(1f));
            Assert.That(SplatSimKernel.Firmness(0.02f, water), Is.EqualTo(1f));
        }

        [Test]
        public void OlderFeelsGainNoSquishPlasticTackOrThicken()
        {
            foreach (SplatFeel f in new[] { SplatFeel.Water, SplatFeel.Sand, SplatFeel.Fur })
            {
                SplatSimParams p = With(f);
                Assert.That(new[] { p.field, p.plastic, p.thicken }, Is.EqualTo(new[] { 0f, 0f, 0f }));
                Assert.That(p.stickDecay, Is.EqualTo(1.2f));
                Assert.That(p.stickReach, Is.EqualTo(0.06f));
            }
        }

        [Test]
        public void WaterIsWhatTheHumanFoundInTheHeadset()
        {
            SplatFeel w = SplatFeel.Water;
            Assert.That(new[] { w.weight, w.cohesion, w.viscosity, w.bounce, w.grain }, Is.EqualTo(new[] { 0.2f, 0.2f, 0.1f, 0.2f, 1f }));
        }

        [Test]
        public void SandIsWhatTheHumanFoundInTheHeadset()
        {
            SplatFeel f = SplatFeel.Sand;
            Assert.That(new[] { f.weight, f.cohesion, f.viscosity, f.bounce, f.grain, f.fluff }, Is.EqualTo(new[] { 0.7f, 0.3f, 0.7f, 0.8f, 1f, 0f }));
        }

        [Test]
        public void FurIsWhatTheHumanFoundInTheHeadset()
        {
            SplatFeel f = SplatFeel.Fur;
            Assert.That(new[] { f.weight, f.cohesion, f.viscosity, f.bounce, f.grain, f.fluff }, Is.EqualTo(new[] { 0.1f, 0.7f, 0.2f, 0.7f, 1f, 1f }));
        }

        [Test]
        public void CohesiveBurstHoldsTogetherInBlobsWhileLooseOneScatters()
        {
            SplatFeel tight = Mid, loose = Mid;
            tight.cohesion = 1f;
            loose.cohesion = 0f;
            var w = new SplatSimReference(Sheet(30, 0.003f), 9);
            var g = new SplatSimReference(Sheet(30, 0.003f), 9);
            Burst(w, With(tight), 0.6f, 0.4f);
            Burst(g, With(loose), 0.6f, 0.4f);
            Assert.That(MeanDisplacement(w), Is.GreaterThan(0.02f));
            Assert.That(MeanClusterSpread(w), Is.LessThan(MeanClusterSpread(g) * 0.8f));
        }

        [Test]
        public void ShapeMappingKeepsRelativePlacement()
        {
            float3[] from = Sheet(20, 0.004f);
            var to = new float3[from.Length];
            for (int i = 0; i < to.Length; i++) to[i] = from[i] * 1.5f + new float3(0.3f, 0f, 0f);
            int[] map = SplatSimSetup.MapByShape(from, to, SplatSimSetup.Hashes(to));
            float err = 0f;
            for (int i = 0; i < to.Length; i++) err += math.length((to[i] - new float3(0.3f, 0f, 0f)) / 1.5f - from[map[i]]);
            Assert.That(err / to.Length, Is.LessThan(0.006f));
        }

        [Test]
        public void MorphBurstsInPlaceThenGathersIntoTheNewShape()
        {
            SplatSimParams p = SplatSimParams.WaterDroplet;
            SplatFeelMap.Apply(SplatFeel.Water, ref p);
            p.weight = 0f;
            var oldSim = new SplatSimReference(Sheet(24, 0.004f), 8);
            Burst(oldSim, p, 0.6f, 0.35f);
            Assert.That(MeanDisplacement(oldSim), Is.GreaterThan(0.03f));

            var worldPos = new float3[oldSim.Particles.Length];
            var worldVel = new float3[oldSim.Particles.Length];
            for (int i = 0; i < worldPos.Length; i++)
            {
                worldPos[i] = oldSim.Homes[i] + oldSim.Particles[i].displacement;
                worldVel[i] = oldSim.Particles[i].velocity;
            }
            var ring = new float3[700];
            for (int i = 0; i < ring.Length; i++)
            {
                float a = i * 6.2831853f / ring.Length;
                ring[i] = new float3(math.cos(a), math.sin(a), 0f) * (0.04f + 0.006f * (i % 3));
            }
            var newSim = new SplatSimReference(ring, 6);
            newSim.SeedFrom(worldPos, worldVel, SplatSimSetup.MapByShape(oldSim.Homes, ring, newSim.Hash));
            float last = SplatSimSetup.GatherArrivals(newSim.Homes, newSim.Hash, newSim.Arrival, 0.6f);
            Assert.That(MeanDisplacement(newSim), Is.GreaterThan(0.02f));

            var none = new SplatCollider[0];
            int steps = (int)((last + 3f) / Dt);
            for (int i = 0; i < steps; i++) newSim.Step(Frame(SplatSimMode.Gathering, i * Dt), p, none, 0);
            Assert.That(MeanDisplacement(newSim), Is.LessThan(0.002f));
            foreach (SplatParticle s in newSim.Particles) Assert.That(math.all(math.isfinite(s.displacement)), Is.True);
        }
    }
}
