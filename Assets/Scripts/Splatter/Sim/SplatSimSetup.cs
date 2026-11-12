using System;
using Unity.Mathematics;

namespace MoodPicker.Splatter.Sim
{
    public sealed class SplatClusters
    {
        public int[] Assignment;
        public float3[] Home;
        public float[] Radius;
        public int2[] Edges;
        public float[] Pin;
        public int4[] ParticleNodes;
        public float4[] ParticleWeights;
        public float[] NodeMass;
        public int Count => Home.Length;
    }

    public static class SplatSimSetup
    {
        public static int ClusterCountFor(int particleCount, int particlesPerCluster)
        {
            return math.clamp(particleCount / math.max(1, particlesPerCluster), 1, 256);
        }

        public static SplatClusters BuildClusters(float3[] homes, int clusterCount, int seedStride = 8)
        {
            int n = homes.Length;
            int k = math.clamp(clusterCount, 1, math.max(1, n));
            var seeds = new float3[k];
            int stride = math.max(1, math.min(seedStride, n / k));
            int m = (n + stride - 1) / stride;
            var minDist = new float[m];
            for (int j = 0; j < m; j++) minDist[j] = float.MaxValue;

            seeds[0] = homes[0];
            for (int c = 1; c < k; c++)
            {
                float3 last = seeds[c - 1];
                int best = 0;
                float bestDist = -1f;
                for (int j = 0; j < m; j++)
                {
                    float d = math.lengthsq(homes[j * stride] - last);
                    if (d < minDist[j]) minDist[j] = d;
                    if (minDist[j] > bestDist)
                    {
                        bestDist = minDist[j];
                        best = j;
                    }
                }
                seeds[c] = homes[best * stride];
            }

            var assignment = new int[n];
            var sum = new float3[k];
            var count = new int[k];
            for (int pass = 0; pass < 2; pass++)
            {
                Array.Clear(sum, 0, k);
                Array.Clear(count, 0, k);
                for (int i = 0; i < n; i++)
                {
                    int best = 0;
                    float bestDist = float.MaxValue;
                    for (int c = 0; c < k; c++)
                    {
                        float d = math.lengthsq(homes[i] - seeds[c]);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            best = c;
                        }
                    }
                    assignment[i] = best;
                    sum[best] += homes[i];
                    count[best]++;
                }
                for (int c = 0; c < k; c++)
                    if (count[c] > 0) seeds[c] = sum[c] / count[c];
            }

            var radius = new float[k];
            for (int i = 0; i < n; i++)
            {
                int c = assignment[i];
                radius[c] = math.max(radius[c], math.distance(homes[i], seeds[c]));
            }

            var clusters = new SplatClusters { Assignment = assignment, Home = seeds, Radius = radius };
            BuildField(clusters, homes);
            return clusters;
        }

        public const int FieldNeighbors = 6;
        public const float PinHeight = 0.25f;

        public static void BuildField(SplatClusters clusters, float3[] homes)
        {
            int k = clusters.Count;
            float3[] node = clusters.Home;
            var neighbors = new int[k * FieldNeighbors];
            var bestD = new float[FieldNeighbors];
            for (int i = 0; i < k; i++)
            {
                for (int m = 0; m < FieldNeighbors; m++)
                {
                    bestD[m] = float.MaxValue;
                    neighbors[i * FieldNeighbors + m] = i;
                }
                for (int j = 0; j < k; j++)
                {
                    if (j == i) continue;
                    float d = math.lengthsq(node[j] - node[i]);
                    for (int m = 0; m < FieldNeighbors; m++)
                    {
                        if (d >= bestD[m]) continue;
                        for (int q = FieldNeighbors - 1; q > m; q--)
                        {
                            bestD[q] = bestD[q - 1];
                            neighbors[i * FieldNeighbors + q] = neighbors[i * FieldNeighbors + q - 1];
                        }
                        bestD[m] = d;
                        neighbors[i * FieldNeighbors + m] = j;
                        break;
                    }
                }
            }

            float low = float.MaxValue, high = float.MinValue;
            for (int i = 0; i < k; i++)
            {
                low = math.min(low, node[i].y);
                high = math.max(high, node[i].y);
            }
            float span = math.max(high - low, 1e-4f);
            var pin = new float[k];
            for (int i = 0; i < k; i++) pin[i] = 1f - math.smoothstep(PinHeight * 0.5f, PinHeight, (node[i].y - low) / span);

            float pLow = float.MaxValue, pHigh = float.MinValue;
            for (int i = 0; i < homes.Length; i++)
            {
                pLow = math.min(pLow, homes[i].y);
                pHigh = math.max(pHigh, homes[i].y);
            }
            float pSpan = math.max(pHigh - pLow, 1e-4f);
            var pn = new int4[homes.Length];
            var pw = new float4[homes.Length];
            for (int i = 0; i < homes.Length; i++)
            {
                int4 idx = new int4(clusters.Assignment[i]);
                float4 dist = new float4(float.MaxValue);
                for (int c = 0; c < k; c++)
                {
                    float d = math.lengthsq(homes[i] - node[c]);
                    if (d >= dist.w) continue;
                    if (d < dist.x) { dist = new float4(d, dist.xyz); idx = new int4(c, idx.xyz); }
                    else if (d < dist.y) { dist = new float4(dist.x, d, dist.yz); idx = new int4(idx.x, c, idx.yz); }
                    else if (d < dist.z) { dist = new float4(dist.xy, d, dist.z); idx = new int4(idx.xy, c, idx.z); }
                    else { dist.w = d; idx.w = c; }
                }
                float4 w = 1f / (math.sqrt(math.min(dist, 1e6f)) + 1e-3f);
                w = math.select(w, 0f, dist >= float.MaxValue);
                w = w * w;
                float free = math.smoothstep(0f, PinHeight, (homes[i].y - pLow) / pSpan);
                pw[i] = w / math.max(math.csum(w), 1e-6f) * free;
                pn[i] = idx;
            }
            var edges = new System.Collections.Generic.HashSet<long>();
            var edgeList = new System.Collections.Generic.List<int2>();
            for (int i = 0; i < k; i++)
                for (int m = 0; m < FieldNeighbors; m++)
                {
                    int j = neighbors[i * FieldNeighbors + m];
                    if (j == i) continue;
                    int a = math.min(i, j), b = math.max(i, j);
                    if (edges.Add((long)a * 100000 + b)) edgeList.Add(new int2(a, b));
                }
            clusters.Edges = edgeList.ToArray();
            clusters.Pin = pin;
            clusters.ParticleNodes = pn;
            clusters.ParticleWeights = pw;
            var mass = new float[k];
            for (int i = 0; i < homes.Length; i++)
            {
                mass[pn[i].x] += pw[i].x;
                mass[pn[i].y] += pw[i].y;
                mass[pn[i].z] += pw[i].z;
                mass[pn[i].w] += pw[i].w;
            }
            clusters.NodeMass = mass;
        }

        public static void FieldLaplacian(int2[] edges, SplatClusterNode[] nodes, float3[] lap)
        {
            for (int c = 0; c < lap.Length; c++) lap[c] = float3.zero;
            foreach (int2 e in edges)
            {
                float3 d = ((nodes[e.y].displacement - nodes[e.y].rest) - (nodes[e.x].displacement - nodes[e.x].rest)) * SplatSimKernel.EdgeOpen(nodes[e.x], nodes[e.y]);
                lap[e.x] += d;
                lap[e.y] -= d;
            }
        }

        public static void FieldStretch(int2[] edges, SplatClusterNode[] nodes, float3[] homeWorld)
        {
            var sum = new float[nodes.Length];
            var count = new int[nodes.Length];
            foreach (int2 e in edges)
            {
                float rest = math.distance(homeWorld[e.x], homeWorld[e.y]);
                float now = math.distance(homeWorld[e.x] + nodes[e.x].displacement, homeWorld[e.y] + nodes[e.y].displacement);
                float r = now / math.max(rest, 1e-5f);
                sum[e.x] += r; count[e.x]++;
                sum[e.y] += r; count[e.y]++;
            }
            for (int c = 0; c < nodes.Length; c++) nodes[c].stretch = count[c] > 0 ? sum[c] / count[c] : 1f;
        }

        public static float SummonArrivals(float3[] homesBody, float[] hash, float[] arrival, in float3x4 bodyToWorld, float3 emitter, float spreadSeconds)
        {
            int n = homesBody.Length;
            float near = float.MaxValue;
            float far = 0f;
            for (int i = 0; i < n; i++)
            {
                float d = math.distance(SplatSimKernel.Transform(bodyToWorld, homesBody[i]), emitter);
                arrival[i] = d;
                near = math.min(near, d);
                far = math.max(far, d);
            }
            float span = math.max(far - near, 1e-4f);
            float last = 0f;
            for (int i = 0; i < n; i++)
            {
                float order = math.saturate(0.7f * (arrival[i] - near) / span + 0.3f * hash[i]);
                arrival[i] = order * spreadSeconds;
                last = math.max(last, arrival[i]);
            }
            return last;
        }

        public static float DissolveArrivals(float3[] homesBody, float[] hash, float[] arrival, float sagSeconds, float spreadSeconds)
        {
            int n = homesBody.Length;
            float low = float.MaxValue;
            float high = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                low = math.min(low, homesBody[i].y);
                high = math.max(high, homesBody[i].y);
            }
            float span = math.max(high - low, 1e-4f);
            float last = 0f;
            for (int i = 0; i < n; i++)
            {
                arrival[i] = sagSeconds + (0.6f * hash[i] + 0.4f * (homesBody[i].y - low) / span) * spreadSeconds;
                last = math.max(last, arrival[i]);
            }
            return last;
        }

        public static void Bounds(float3[] points, out float3 center, out float3 extent)
        {
            float3 lo = new float3(float.MaxValue), hi = new float3(float.MinValue);
            for (int i = 0; i < points.Length; i++)
            {
                lo = math.min(lo, points[i]);
                hi = math.max(hi, points[i]);
            }
            center = (lo + hi) * 0.5f;
            extent = math.max((hi - lo) * 0.5f, new float3(1e-4f));
        }

        public static float3[] BurstVelocities(float3[] homes, float[] hash, float speed)
        {
            Bounds(homes, out float3 center, out float3 extent);
            var v = new float3[homes.Length];
            for (int i = 0; i < homes.Length; i++)
            {
                float3 radial = (homes[i] - center) / extent;
                float3 dir = math.normalizesafe(radial * 0.8f + SplatSimKernel.HashDirection(hash[i]) * 0.6f, SplatSimKernel.HashDirection(hash[i]));
                v[i] = dir * (speed * (0.6f + 0.8f * math.frac(hash[i] * 5.31f)));
            }
            return v;
        }

        public static float GatherArrivals(float3[] homes, float[] hash, float[] arrival, float spreadSeconds)
        {
            Bounds(homes, out float3 center, out float3 extent);
            float last = 0f;
            for (int i = 0; i < homes.Length; i++)
            {
                float d = math.saturate(math.length((homes[i] - center) / extent) / 1.732f);
                arrival[i] = (0.55f * math.frac(hash[i] * 3.17f) + 0.45f * d) * spreadSeconds;
                last = math.max(last, arrival[i]);
            }
            return last;
        }

        public static int[] MapByShape(float3[] fromHomes, float3[] toHomes, float[] toHash, int cells = 12)
        {
            Bounds(fromHomes, out float3 fc, out float3 fe);
            Bounds(toHomes, out float3 tc, out float3 te);
            int Cell(float3 n)
            {
                int3 c = math.clamp((int3)math.floor((n * 0.5f + 0.5f) * cells), 0, cells - 1);
                return (c.z * cells + c.y) * cells + c.x;
            }
            var bucketStart = new int[cells * cells * cells + 1];
            var cellOf = new int[fromHomes.Length];
            for (int i = 0; i < fromHomes.Length; i++)
            {
                cellOf[i] = Cell((fromHomes[i] - fc) / fe);
                bucketStart[cellOf[i] + 1]++;
            }
            for (int c = 0; c < cells * cells * cells; c++) bucketStart[c + 1] += bucketStart[c];
            var fill = new int[cells * cells * cells];
            var sorted = new int[fromHomes.Length];
            for (int i = 0; i < fromHomes.Length; i++) sorted[bucketStart[cellOf[i]] + fill[cellOf[i]]++] = i;

            var map = new int[toHomes.Length];
            for (int i = 0; i < toHomes.Length; i++)
            {
                int c = Cell((toHomes[i] - tc) / te);
                int count = bucketStart[c + 1] - bucketStart[c];
                int pick = (int)(math.frac(toHash[i] * 7.77f) * 65535f);
                map[i] = count > 0 ? sorted[bucketStart[c] + pick % count] : pick % fromHomes.Length;
            }
            return map;
        }

        public static float[] Hashes(float3[] homes)
        {
            var h = new float[homes.Length];
            for (int i = 0; i < h.Length; i++)
            {
                float3 s = math.frac(math.sin(homes[i] * new float3(127.1f, 311.7f, 74.7f)) * 43758.5453f);
                h[i] = math.frac(s.x + s.y + s.z);
            }
            return h;
        }
    }

    public sealed class SplatSimReference
    {
        public readonly float3[] Homes;
        public readonly float[] Hash;
        public readonly float[] Arrival;
        public readonly SplatClusters Clusters;
        public readonly SplatParticle[] Particles;
        public readonly SplatClusterNode[] Nodes;
        public float Activity;

        public void BeginBurst(float speed)
        {
            float3[] v = SplatSimSetup.BurstVelocities(Homes, Hash, speed);
            for (int i = 0; i < Particles.Length; i++) Particles[i].velocity = v[i];
            SplatSimSetup.Bounds(Homes, out float3 center, out float3 extent);
            for (int c = 0; c < Nodes.Length; c++)
                Nodes[c].velocity = math.normalizesafe((Clusters.Home[c] - center) / extent, new float3(0f, 1f, 0f)) * speed;
        }

        public void SeedFrom(float3[] worldPositions, float3[] worldVelocities, int[] map)
        {
            for (int i = 0; i < Particles.Length; i++)
            {
                int j = map[i];
                Particles[i].displacement = worldPositions[j] - Homes[i];
                Particles[i].velocity = worldVelocities[j];
                Particles[i].scale = 1f;
                Particles[i].rest = float3.zero;
            }
            for (int c = 0; c < Nodes.Length; c++) Nodes[c] = default;
        }

        public SplatSimReference(float3[] homes, int clusterCount)
        {
            Homes = homes;
            Hash = SplatSimSetup.Hashes(homes);
            Arrival = new float[homes.Length];
            Clusters = SplatSimSetup.BuildClusters(homes, clusterCount);
            Particles = new SplatParticle[homes.Length];
            Nodes = new SplatClusterNode[Clusters.Count];
            for (int i = 0; i < Particles.Length; i++) Particles[i].scale = 1f;
        }

        public void Step(in SplatSimFrame f, in SplatSimParams p, SplatCollider[] colliders, int colliderCount)
        {
            if (SplatSimKernel.UsesField(p, f.mode))
            {
                StepField(f, p, colliders, colliderCount);
                return;
            }
            int k = Nodes.Length;
            var clusterHomeWorld = new float3[k];
            for (int c = 0; c < k; c++)
            {
                SplatSimKernel.CarryHome(ref Nodes[c].displacement, Clusters.Home[c], f, out clusterHomeWorld[c]);
                var acc = SplatHandAccum.Empty;
                float3 pos = clusterHomeWorld[c] + Nodes[c].displacement;
                for (int j = 0; j < colliderCount; j++)
                    SplatSimKernel.AccumulateCollider(ref acc, pos, Nodes[c].velocity, colliders[j], p, Clusters.Radius[c] * p.clusterReach);
                SplatSimKernel.StepCluster(ref Nodes[c], acc, f, p);
            }

            float activity = 0f;
            for (int i = 0; i < Particles.Length; i++)
            {
                SplatSimKernel.CarryHome(ref Particles[i].displacement, Homes[i], f, out float3 homeWorld);
                var acc = SplatHandAccum.Empty;
                float3 pos = homeWorld + Particles[i].displacement;
                for (int j = 0; j < colliderCount; j++)
                    SplatSimKernel.AccumulateCollider(ref acc, pos, Particles[i].velocity, colliders[j], p, 0f);
                int c = Clusters.Assignment[i];
                SplatSimKernel.StepParticle(ref Particles[i], homeWorld, clusterHomeWorld[c], Nodes[c], Hash[i], Arrival[i], acc, f, p);
                activity = math.max(activity, SplatSimKernel.Activity(Particles[i]));
            }
            Activity = activity;
        }

        void StepField(in SplatSimFrame f, in SplatSimParams p, SplatCollider[] colliders, int colliderCount)
        {
            int k = Nodes.Length;
            var push = new float3[k];
            var lap = new float3[k];
            var homeWorld = new float3[k];
            var touched = new SplatHandAccum[k];
            var nearestD = new float[k];
            var nearestP = new float3[k];
            var nearestV = new float3[k];
            SplatSimParams touch = SplatSimKernel.FieldTouchParams(p);
            for (int c = 0; c < k; c++)
            {
                SplatSimKernel.CarryHome(ref Nodes[c].displacement, Clusters.Home[c], f, out float3 hw);
                homeWorld[c] = hw;
                var acc = SplatHandAccum.Empty;
                float3 pos = hw + Nodes[c].displacement;
                float nearest = float.MaxValue;
                float3 nearPoint = pos, nearVel = float3.zero;
                for (int j = 0; j < colliderCount; j++)
                {
                    SplatSimKernel.AccumulateCollider(ref acc, pos, Nodes[c].velocity, colliders[j], touch, Clusters.Radius[c] * p.clusterReach * p.fieldReach);
                    SplatSimKernel.NearestCollider(colliders[j], pos, ref nearest, ref nearPoint, ref nearVel);
                }
                if (SplatSimKernel.TouchesThroughSurface(p)) acc = SplatHandAccum.Empty;
                push[c] = acc.accel;
                touched[c] = acc;
                nearestD[c] = nearest;
                nearestP[c] = nearPoint;
                nearestV[c] = nearVel;
            }
            if (SplatSimKernel.TouchesThroughSurface(p) && colliderCount > 0)
            {
                for (int i = 0; i < Particles.Length; i++)
                {
                    float3 pos = SplatSimKernel.Transform(f.bodyToWorld, Homes[i]) + Particles[i].displacement;
                    var dot = SplatHandAccum.Empty;
                    for (int j = 0; j < colliderCount; j++)
                        SplatSimKernel.AccumulateCollider(ref dot, pos, Particles[i].velocity, colliders[j], touch, 0f);
                    if (dot.contact <= 0f) continue;
                    int4 n = Clusters.ParticleNodes[i];
                    float4 w = Clusters.ParticleWeights[i];
                    SplatSimKernel.SpreadTouch(ref push[n.x], ref touched[n.x].contact, w.x, Clusters.NodeMass[n.x], dot, p);
                    SplatSimKernel.SpreadTouch(ref push[n.y], ref touched[n.y].contact, w.y, Clusters.NodeMass[n.y], dot, p);
                    SplatSimKernel.SpreadTouch(ref push[n.z], ref touched[n.z].contact, w.z, Clusters.NodeMass[n.z], dot, p);
                    SplatSimKernel.SpreadTouch(ref push[n.w], ref touched[n.w].contact, w.w, Clusters.NodeMass[n.w], dot, p);
                }
            }
            for (int c = 0; c < k; c++)
            {
                float3 pos = homeWorld[c] + Nodes[c].displacement;
                push[c] += SplatSimKernel.FieldTack(ref Nodes[c], pos, Clusters.Pin[c], touched[c], nearestD[c], nearestP[c], nearestV[c], f.dt, p);
                SplatSimKernel.FieldHeal(ref Nodes[c], touched[c].contact, f.dt, p);
                SplatSimKernel.CrackTrigger(ref Nodes[c], touched[c].contact, nearestV[c], f.dt, p);
            }
            if (p.crack > 0f)
            {
                var target = new float[k];
                foreach (int2 e in Clusters.Edges)
                {
                    target[e.y] = math.max(target[e.y], Nodes[e.x].crack * p.crackSpread);
                    target[e.x] = math.max(target[e.x], Nodes[e.y].crack * p.crackSpread);
                }
                for (int c = 0; c < k; c++) Nodes[c].crack = SplatSimKernel.CrackToward(Nodes[c].crack, target[c], f.dt, p);
            }
            float h = f.dt / SplatSimKernel.FieldSubsteps;
            for (int sub = 0; sub < SplatSimKernel.FieldSubsteps; sub++)
            {
                SplatSimSetup.FieldLaplacian(Clusters.Edges, Nodes, lap);
                for (int c = 0; c < k; c++) SplatSimKernel.StepFieldNode(ref Nodes[c], lap[c], Clusters.Pin[c], push[c], h, p);
            }
            if (p.fieldTack > 0f) SplatSimSetup.FieldStretch(Clusters.Edges, Nodes, homeWorld);

            float activity = 0f;
            for (int i = 0; i < Particles.Length; i++)
            {
                SplatSimKernel.CarryHome(ref Particles[i].displacement, Homes[i], f, out float3 particleHome);
                var acc = SplatHandAccum.Empty;
                float3 pos = particleHome + Particles[i].displacement;
                for (int j = 0; j < colliderCount; j++)
                    SplatSimKernel.AccumulateCollider(ref acc, pos, Particles[i].velocity, colliders[j], p, 0f);
                int4 n = Clusters.ParticleNodes[i];
                SplatSimKernel.FieldParticle(ref Particles[i], Nodes[n.x], Nodes[n.y], Nodes[n.z], Nodes[n.w], Clusters.ParticleWeights[i], acc, f, p);
                activity = math.max(activity, SplatSimKernel.Activity(Particles[i]));
            }
            Activity = activity;
        }
    }
}
