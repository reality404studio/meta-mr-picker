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
        public void JellySpringsBack()
        {
            float jelly = MeanZ(Press(SplatFeel.Jelly, 0.3f, 0.5f, 3f), 0f, 0.018f);
            Assert.That(jelly, Is.GreaterThan(-0.001f));
        }

        static float WorstSpacingError(SplatSimReference sim)
        {
            float worst = 0f;
            for (int y = 0; y < 30; y++)
                for (int x = 0; x < 29; x++)
                {
                    int i = y * 30 + x;
                    float3 a = sim.Homes[i] + sim.Particles[i].displacement;
                    float3 b = sim.Homes[i + 1] + sim.Particles[i + 1].displacement;
                    worst = math.max(worst, math.abs(math.distance(a, b) - 0.004f));
                }
            return worst;
        }

        [Test]
        public void SlimeDentsAsOneSurfaceAndStaysDented()
        {
            SplatSimReference sim = Press(SplatFeel.Slime, 0.3f, 0.5f, 3f);
            Assert.That(MeanZ(sim, 0f, 0.012f), Is.LessThan(-0.002f));
            Assert.That(WorstSpacingError(sim), Is.LessThan(0.002f), "no single dots breaking away");
        }

        [Test]
        public void SlimeStretchesTowardAPullingHandKeepsTheStrandAndGrowsItsSplats()
        {
            var sim = new SplatSimReference(Sheet(30, 0.004f), 12);
            SplatSimParams p = With(SplatFeel.Slime);
            var c = new SplatCollider[1];
            float maxScale = 1f;
            for (int i = 0; i < (int)(0.4f / Dt); i++)
            {
                c[0] = Finger(new float3(0f, 0.03f, 0.018f), float3.zero);
                sim.Step(Frame(), p, c, 1);
            }
            float z = 0.018f;
            for (int i = 0; i < (int)(1.5f / Dt); i++)
            {
                z += 0.03f * Dt;
                c[0] = Finger(new float3(0f, 0.03f, z), new float3(0f, 0f, 0.03f));
                sim.Step(Frame(), p, c, 1);
                foreach (SplatParticle s in sim.Particles) maxScale = math.max(maxScale, s.scale);
            }
            float pulled = MeanZ(sim, 0f, 1f);
            Run(sim, p, 2f);
            float kept = 0f;
            foreach (SplatClusterNode n in sim.Nodes) kept = math.max(kept, n.rest.z);
            Assert.That(pulled, Is.GreaterThan(0.001f), "the surface follows the hand away");
            Assert.That(kept, Is.GreaterThan(0.005f), "part of the strand stays after the hand lets go");
            Assert.That(maxScale, Is.GreaterThan(1.005f), "stretched skin grows its splats to cover gaps");
            Assert.That(maxScale, Is.LessThanOrEqualTo(1.3f + 1e-4f), "but never past the cap that kept the look from turning to flakes");
        }

        static float NodeMoveNearButNotTouching(SplatFeel feel)
        {
            var sim = new SplatSimReference(Sheet(30, 0.004f), 12);
            SplatSimParams p = With(feel);
            var c = new SplatCollider[1];
            float moved = 0f;
            for (int i = 0; i < (int)(1f / Dt); i++)
            {
                float x = math.lerp(-0.04f, 0.04f, i * Dt);
                c[0] = Finger(new float3(x, 0.02f, 0.026f), new float3(0.08f, 0f, 0f));
                sim.Step(Frame(), p, c, 1);
                foreach (SplatClusterNode n in sim.Nodes) moved = math.max(moved, math.length(n.displacement));
            }
            return moved;
        }

        [Test]
        public void SlimeIgnoresAHandThatIsNearButNotTouching()
        {
            Assert.That(NodeMoveNearButNotTouching(SplatFeel.Slime), Is.LessThan(0.0001f));
            Assert.That(NodeMoveNearButNotTouching(SplatFeel.Jelly), Is.GreaterThan(0.0005f), "jelly keeps its approved reach");
        }

        static float MaxNode(SplatSimReference sim, Func<SplatClusterNode, float> of)
        {
            float m = 0f;
            foreach (SplatClusterNode n in sim.Nodes) m = math.max(m, of(n));
            return m;
        }

        [Test]
        public void SlimeShapeStaysReadableAfterManyTouchesAndSlowlyHeals()
        {
            var sim = new SplatSimReference(Sheet(30, 0.004f), 12);
            SplatSimParams p = With(SplatFeel.Slime);
            var c = new SplatCollider[1];
            float worst = 0f;
            for (int touch = 0; touch < 6; touch++)
            {
                float2 at = new float2(math.sin(touch * 2.1f), math.cos(touch * 1.3f)) * 0.03f;
                for (int i = 0; i < (int)(0.8f / Dt); i++)
                {
                    float z = i < 30 ? math.lerp(0.03f, 0.008f, i / 30f) : math.lerp(0.008f, 0.06f, (i - 30) / 28f);
                    c[0] = Finger(new float3(at, z), new float3(0f, 0f, i < 30 ? -0.05f : 0.07f));
                    sim.Step(Frame(), p, c, 1);
                    worst = math.max(worst, MaxNode(sim, n => math.length(n.rest)));
                }
            }
            Assert.That(worst, Is.LessThanOrEqualTo(p.fieldRestMax + 1e-4f), "permanent bend is capped");
            Run(sim, p, 12f);
            Assert.That(MaxNode(sim, n => math.length(n.rest)), Is.LessThan(0.004f), "the letter drifts back while untouched");
        }

        static float StickAfterTouch(float seconds)
        {
            var sim = new SplatSimReference(Sheet(30, 0.004f), 12);
            SplatSimParams p = With(SplatFeel.Slime);
            var c = new SplatCollider[] { Finger(new float3(0f, 0.01f, 0.012f), float3.zero) };
            float best = 0f;
            for (int i = 0; i < (int)math.round(seconds / Dt); i++)
            {
                sim.Step(Frame(), p, c, 1);
                best = math.max(best, MaxNode(sim, n => n.stick));
            }
            return best;
        }

        [Test]
        public void SlimeOnlySticksAfterAHeldTouch()
        {
            Assert.That(StickAfterTouch(0.05f), Is.LessThan(0.3f));
            Assert.That(StickAfterTouch(0.8f), Is.GreaterThan(0.6f));
        }

        [Test]
        public void SlimeLetsGoOnceTheHandIsFarFromTheStuckSkin()
        {
            var sim = new SplatSimReference(Sheet(30, 0.004f), 12);
            SplatSimParams p = With(SplatFeel.Slime);
            var c = new SplatCollider[1];
            for (int i = 0; i < (int)(0.8f / Dt); i++)
            {
                c[0] = Finger(new float3(0f, 0.01f, 0.012f), float3.zero);
                sim.Step(Frame(), p, c, 1);
            }
            Assert.That(MaxNode(sim, n => n.stick), Is.GreaterThan(0.5f));
            for (int i = 0; i < (int)(0.3f / Dt); i++)
            {
                c[0] = Finger(new float3(0f, 0.01f, 0.012f + 0.4f * i * Dt), new float3(0f, 0f, 0.4f));
                sim.Step(Frame(), p, c, 1);
            }
            Assert.That(MaxNode(sim, n => n.stick), Is.EqualTo(0f));
        }

        static SplatSimReference Poke(SplatFeel feel, float speed, out int hit)
        {
            var sim = new SplatSimReference(Sheet(30, 0.004f), 12);
            SplatSimParams p = With(feel);
            var c = new SplatCollider[1];
            float z = 0.024f;
            while (z > 0.012f)
            {
                z -= speed * Dt;
                c[0] = Finger(new float3(0f, 0.01f, z), new float3(0f, 0f, -speed));
                sim.Step(Frame(), p, c, 1);
            }
            hit = 0;
            for (int n = 1; n < sim.Nodes.Length; n++)
                if (sim.Nodes[n].crack > sim.Nodes[hit].crack) hit = n;
            return sim;
        }

        [Test]
        public void StarchCracksUnderAFastPokeAndTheCrackRunsOutward()
        {
            SplatSimReference sim = Poke(SplatFeel.Starch, 0.6f, out int hit);
            SplatSimParams p = With(SplatFeel.Starch);
            Assert.That(sim.Nodes[hit].crack, Is.EqualTo(1f));
            float3 at = sim.Clusters.Home[hit];
            int far = 0;
            for (int n = 0; n < sim.Nodes.Length; n++)
                if (math.distance(sim.Clusters.Home[n], at) > math.distance(sim.Clusters.Home[far], at)) far = n;
            float early = sim.Nodes[far].crack;
            var c = new SplatCollider[] { Finger(new float3(0f, 0.01f, 0.012f), float3.zero) };
            for (int i = 0; i < (int)(0.5f / Dt); i++) sim.Step(Frame(), p, c, 1);
            int mid = -1;
            foreach (int2 e in sim.Clusters.Edges)
                if (e.x == hit || e.y == hit) { mid = e.x == hit ? e.y : e.x; break; }
            Assert.That(sim.Nodes[mid].crack, Is.GreaterThan(0.5f), "the neighbouring plate cracks too");
            Assert.That(sim.Nodes[far].crack, Is.GreaterThan(early), "the crack keeps running outward over time");
            Assert.That(sim.Nodes[far].crack, Is.LessThan(sim.Nodes[mid].crack), "and fades with distance");
        }

        [Test]
        public void StarchSlowPressSinksInWithoutCracking()
        {
            SplatSimReference sim = Poke(SplatFeel.Starch, 0.02f, out _);
            Assert.That(MaxNode(sim, n => n.crack), Is.EqualTo(0f));
        }

        [Test]
        public void StarchCracksOpenAsGapsAlongPlateEdges()
        {
            SplatSimReference sim = Poke(SplatFeel.Starch, 0.6f, out int hit);
            float edgeScale = 1f, inner = 0f;
            for (int i = 0; i < sim.Particles.Length; i++)
            {
                if (sim.Clusters.ParticleNodes[i].x != hit) continue;
                float edge = SplatSimKernel.PlateEdge(sim.Clusters.ParticleWeights[i]);
                if (edge > 0.9f) edgeScale = math.min(edgeScale, sim.Particles[i].scale);
                if (edge < 0.1f) inner = math.max(inner, sim.Particles[i].scale);
            }
            Assert.That(edgeScale, Is.LessThan(0.3f));
            Assert.That(inner, Is.GreaterThan(0.9f));
        }

        [Test]
        public void StarchCracksMeltBackWhenLeftAlone()
        {
            SplatSimReference sim = Poke(SplatFeel.Starch, 0.6f, out _);
            Run(sim, With(SplatFeel.Starch), 6f);
            Assert.That(MaxNode(sim, n => n.crack), Is.LessThan(0.05f));
        }

        [Test]
        public void JellyAndSlimeNeverCrack()
        {
            Assert.That(With(SplatFeel.Jelly).crack, Is.EqualTo(0f));
            Assert.That(With(SplatFeel.Slime).crack, Is.EqualTo(0f));
            Assert.That(MaxNode(Poke(SplatFeel.Jelly, 0.6f, out _), n => n.crack), Is.EqualTo(0f));
        }

        static void Stroke(SplatSimReference sim, SplatSimParams p, float dir)
        {
            var c = new SplatCollider[1];
            float x = -0.05f * dir;
            while (x * dir < 0.05f)
            {
                c[0] = Finger(new float3(x, 0f, 0.026f), new float3(0.25f * dir, 0f, 0f));
                sim.Step(Frame(), p, c, 1);
                x += 0.25f * dir * Dt;
            }
        }

        static float CombAlongX(SplatSimReference sim, out float far, out float moved)
        {
            float along = 0f;
            int n = 0;
            far = 0f;
            moved = 0f;
            for (int i = 0; i < sim.Particles.Length; i++)
            {
                SplatParticle s = sim.Particles[i];
                moved = math.max(moved, math.length(s.displacement));
                if (math.abs(sim.Homes[i].y) < 0.008f) { along += s.velocity.x; n++; }
                if (sim.Homes[i].y > 0.045f) far = math.max(far, math.length(s.velocity));
            }
            return along / math.max(1, n);
        }

        [Test]
        public void FurStrokeLaysHairAlongTheHandWithoutMovingTheDots()
        {
            var sim = new SplatSimReference(Sheet(30, 0.004f), 12);
            SplatSimParams p = With(SplatFeel.Fur);
            Stroke(sim, p, 1f);
            float along = CombAlongX(sim, out float far, out float moved);
            Assert.That(along, Is.GreaterThan(0.6f * p.combMax), "stroked hair lies along the hand");
            Assert.That(far, Is.LessThan(0.05f), "hair the hand never reached stays as it was");
            Assert.That(moved, Is.LessThan(0.0005f), "the dots themselves do not fly");
        }

        [Test]
        public void FurKeepsTheStrokeAndCanBeBrushedBack()
        {
            var sim = new SplatSimReference(Sheet(30, 0.004f), 12);
            SplatSimParams p = With(SplatFeel.Fur);
            Stroke(sim, p, 1f);
            float before = CombAlongX(sim, out _, out _);
            Run(sim, p, 1f);
            Assert.That(CombAlongX(sim, out _, out _), Is.GreaterThan(before * 0.9f), "the brushed direction stays");
            Stroke(sim, p, -1f);
            Assert.That(CombAlongX(sim, out _, out _), Is.LessThan(-0.4f * p.combMax), "brushing back turns it around");
        }

        [Test]
        public void OnlyFluffyFeelsComb()
        {
            Assert.That(With(SplatFeel.Fur).comb, Is.EqualTo(1f));
            foreach (SplatFeel f in new[] { SplatFeel.Water, SplatFeel.Sand, SplatFeel.Jelly, SplatFeel.Slime, SplatFeel.Starch })
                Assert.That(With(f).comb, Is.EqualTo(0f));
        }

        [Test]
        public void JellyStaysWithoutTackRestOrGrowth()
        {
            SplatSimParams p = With(SplatFeel.Jelly);
            Assert.That(p.fieldTack, Is.EqualTo(0f));
            Assert.That(p.fieldReach, Is.EqualTo(1f));
            Assert.That(p.plastic, Is.EqualTo(0f));
            Assert.That(SplatFeelMap.Look(SplatFeel.Jelly).detach, Is.EqualTo(0.012f));
            Assert.That(SplatFeelMap.Look(SplatFeel.Slime).detach, Is.GreaterThan(0.3f));
            Assert.That(SplatFeelMap.Look(SplatFeel.Jelly).stretchMax, Is.EqualTo(4f));
            Assert.That(SplatFeelMap.Look(SplatFeel.Slime).stretchMax, Is.LessThan(1f));
        }

        static float PeakNodeSpeed(SplatFeel feel, float speed)
        {
            var sim = new SplatSimReference(Sheet(30, 0.004f), 12);
            SplatSimParams p = With(feel);
            var c = new SplatCollider[1];
            float z = 0.024f, peak = 0f;
            while (z > 0.012f)
            {
                z -= speed * Dt;
                c[0] = Finger(new float3(0f, 0.01f, z), new float3(0f, 0f, -speed));
                sim.Step(Frame(), p, c, 1);
                peak = math.max(peak, MaxNode(sim, n => math.length(n.velocity)));
            }
            return peak;
        }

        [Test]
        public void StarchPushesBackHarderAgainstAFastPokeThanASlowPress()
        {
            SplatFeel runny = SplatFeel.Starch;
            runny.thicken = 0f;
            Assert.That(PeakNodeSpeed(SplatFeel.Starch, 0.6f), Is.GreaterThan(PeakNodeSpeed(runny, 0.6f) * 1.5f), "a fast hit meets a firmer surface");
            Assert.That(PeakNodeSpeed(SplatFeel.Starch, 0.02f), Is.LessThan(PeakNodeSpeed(runny, 0.02f) * 0.5f), "a slow press sinks in instead of shoving");
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
                Assert.That(new[] { p.field, p.plastic, p.thicken, p.crack }, Is.EqualTo(new[] { 0f, 0f, 0f, 0f }));
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
