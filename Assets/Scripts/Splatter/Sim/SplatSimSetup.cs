using System;
using Unity.Mathematics;

namespace MoodPicker.Splatter.Sim
{
    public sealed class SplatClusters
    {
        public int[] Assignment;
        public float3[] Home;
        public float[] Radius;
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

            return new SplatClusters { Assignment = assignment, Home = seeds, Radius = radius };
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
    }
}
