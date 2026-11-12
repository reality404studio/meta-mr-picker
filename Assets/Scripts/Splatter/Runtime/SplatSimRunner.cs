using System;
using MoodPicker.Splatter.Sim;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MoodPicker.Splatter
{
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    struct SplatClusterJob : IJob
    {
        [ReadOnly] public NativeArray<float3> home;
        [ReadOnly] public NativeArray<float> radius;
        [ReadOnly] public NativeArray<SplatCollider> colliders;
        [ReadOnly] public NativeArray<int2> edges;
        [ReadOnly] public NativeArray<float> pin;
        [ReadOnly] public NativeArray<float3> particleHome;
        [ReadOnly] public NativeArray<float4> particleState;
        [ReadOnly] public NativeArray<int4> fieldNodes;
        [ReadOnly] public NativeArray<float4> fieldWeights;
        [ReadOnly] public NativeArray<float> nodeMass;
        public NativeArray<float3> homeWorld;
        public NativeArray<SplatClusterNode> nodes;
        public SplatSimFrame frame;
        public SplatSimParams p;
        public int colliderCount;

        public void Execute()
        {
            if (SplatSimKernel.UsesField(p, frame.mode))
            {
                ExecuteField();
                return;
            }
            for (int c = 0; c < nodes.Length; c++)
            {
                SplatClusterNode node = nodes[c];
                SplatSimKernel.CarryHome(ref node.displacement, home[c], frame, out float3 hw);
                homeWorld[c] = hw;
                SplatHandAccum acc = SplatHandAccum.Empty;
                float3 pos = hw + node.displacement;
                float reach = radius[c] * p.clusterReach;
                for (int j = 0; j < colliderCount; j++)
                    SplatSimKernel.AccumulateCollider(ref acc, pos, node.velocity, colliders[j], p, reach);
                SplatSimKernel.StepCluster(ref node, acc, frame, p);
                nodes[c] = node;
            }
        }

        void ExecuteField()
        {
            int k = nodes.Length;
            var push = new NativeArray<float3>(k, Allocator.Temp);
            var touched = new NativeArray<SplatHandAccum>(k, Allocator.Temp);
            var nearestD = new NativeArray<float>(k, Allocator.Temp);
            var nearestP = new NativeArray<float3>(k, Allocator.Temp);
            var nearestV = new NativeArray<float3>(k, Allocator.Temp);
            bool throughSurface = SplatSimKernel.TouchesThroughSurface(p);
            SplatSimParams touch = SplatSimKernel.FieldTouchParams(p);
            var lap = new NativeArray<float3>(k, Allocator.Temp);
            for (int c = 0; c < k; c++)
            {
                SplatClusterNode node = nodes[c];
                SplatSimKernel.CarryHome(ref node.displacement, home[c], frame, out float3 hw);
                homeWorld[c] = hw;
                SplatHandAccum acc = SplatHandAccum.Empty;
                float3 pos = hw + node.displacement;
                float reach = radius[c] * p.clusterReach * p.fieldReach;
                float nearest = float.MaxValue;
                float3 nearPoint = pos, nearVel = float3.zero;
                for (int j = 0; j < colliderCount; j++)
                {
                    SplatSimKernel.AccumulateCollider(ref acc, pos, node.velocity, colliders[j], touch, reach);
                    SplatSimKernel.NearestCollider(colliders[j], pos, ref nearest, ref nearPoint, ref nearVel);
                }
                if (throughSurface) acc = SplatHandAccum.Empty;
                push[c] = acc.accel;
                touched[c] = acc;
                nearestD[c] = nearest;
                nearestP[c] = nearPoint;
                nearestV[c] = nearVel;
                nodes[c] = node;
            }
            if (throughSurface && colliderCount > 0)
            {
                for (int i = 0; i < particleHome.Length; i++)
                {
                    float4 a = particleState[i * 2];
                    float4 b = particleState[i * 2 + 1];
                    float3 pos = SplatSimKernel.Transform(frame.bodyToWorld, particleHome[i]) + a.xyz;
                    SplatHandAccum dot = SplatHandAccum.Empty;
                    for (int j = 0; j < colliderCount; j++)
                        SplatSimKernel.AccumulateCollider(ref dot, pos, b.xyz, colliders[j], touch, 0f);
                    if (dot.contact <= 0f) continue;
                    int4 n = fieldNodes[i];
                    float4 w = fieldWeights[i];
                    Spread(push, touched, n.x, w.x, dot);
                    Spread(push, touched, n.y, w.y, dot);
                    Spread(push, touched, n.z, w.z, dot);
                    Spread(push, touched, n.w, w.w, dot);
                }
            }
            for (int c = 0; c < k; c++)
            {
                SplatClusterNode node = nodes[c];
                float3 pos = homeWorld[c] + node.displacement;
                push[c] = push[c] + SplatSimKernel.FieldTack(ref node, pos, pin[c], touched[c], nearestD[c], nearestP[c], nearestV[c], frame.dt, p);
                SplatSimKernel.FieldHeal(ref node, touched[c].contact, frame.dt, p);
                SplatSimKernel.CrackTrigger(ref node, touched[c].contact, nearestV[c], frame.dt, p);
                nodes[c] = node;
            }
            if (p.crack > 0f)
            {
                var target = new NativeArray<float>(k, Allocator.Temp);
                for (int e = 0; e < edges.Length; e++)
                {
                    int2 ab = edges[e];
                    target[ab.y] = math.max(target[ab.y], nodes[ab.x].crack * p.crackSpread);
                    target[ab.x] = math.max(target[ab.x], nodes[ab.y].crack * p.crackSpread);
                }
                for (int c = 0; c < k; c++)
                {
                    SplatClusterNode node = nodes[c];
                    node.crack = SplatSimKernel.CrackToward(node.crack, target[c], frame.dt, p);
                    nodes[c] = node;
                }
                target.Dispose();
            }
            float h = frame.dt / SplatSimKernel.FieldSubsteps;
            for (int sub = 0; sub < SplatSimKernel.FieldSubsteps; sub++)
            {
                for (int c = 0; c < k; c++) lap[c] = float3.zero;
                for (int e = 0; e < edges.Length; e++)
                {
                    int2 ab = edges[e];
                    float3 d = ((nodes[ab.y].displacement - nodes[ab.y].rest) - (nodes[ab.x].displacement - nodes[ab.x].rest)) * SplatSimKernel.EdgeOpen(nodes[ab.x], nodes[ab.y]);
                    lap[ab.x] += d;
                    lap[ab.y] -= d;
                }
                for (int c = 0; c < k; c++)
                {
                    SplatClusterNode node = nodes[c];
                    SplatSimKernel.StepFieldNode(ref node, lap[c], pin[c], push[c], h, p);
                    nodes[c] = node;
                }
            }
            if (p.fieldTack > 0f)
            {
                var sum = new NativeArray<float>(k, Allocator.Temp);
                var count = new NativeArray<int>(k, Allocator.Temp);
                for (int e = 0; e < edges.Length; e++)
                {
                    int2 ab = edges[e];
                    float rest = math.distance(homeWorld[ab.x], homeWorld[ab.y]);
                    float now = math.distance(homeWorld[ab.x] + nodes[ab.x].displacement, homeWorld[ab.y] + nodes[ab.y].displacement);
                    float r = now / math.max(rest, 1e-5f);
                    sum[ab.x] += r; count[ab.x]++;
                    sum[ab.y] += r; count[ab.y]++;
                }
                for (int c = 0; c < k; c++)
                {
                    SplatClusterNode node = nodes[c];
                    node.stretch = count[c] > 0 ? sum[c] / count[c] : 1f;
                    nodes[c] = node;
                }
                sum.Dispose();
                count.Dispose();
            }
            push.Dispose();
            lap.Dispose();
            touched.Dispose();
            nearestD.Dispose();
            nearestP.Dispose();
            nearestV.Dispose();
        }

        void Spread(NativeArray<float3> push, NativeArray<SplatHandAccum> touched, int node, float weight, in SplatHandAccum dot)
        {
            float3 accel = push[node];
            SplatHandAccum t = touched[node];
            SplatSimKernel.SpreadTouch(ref accel, ref t.contact, weight, nodeMass[node], dot, p);
            push[node] = accel;
            touched[node] = t;
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    struct SplatParticleJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> home;
        [ReadOnly] public NativeArray<float> hash;
        [ReadOnly] public NativeArray<float> arrival;
        [ReadOnly] public NativeArray<int> cluster;
        [ReadOnly] public NativeArray<float3> clusterHomeWorld;
        [ReadOnly] public NativeArray<SplatClusterNode> nodes;
        [ReadOnly] public NativeArray<SplatCollider> colliders;
        [ReadOnly] public NativeArray<int4> fieldNodes;
        [ReadOnly] public NativeArray<float4> fieldWeights;
        [NativeDisableParallelForRestriction] public NativeArray<float4> texels;
        public NativeArray<float> stick;
        public NativeArray<float3> rest;
        public SplatSimFrame frame;
        public SplatSimParams p;
        public int colliderCount;

        public void Execute(int i)
        {
            float4 a = texels[i * 2];
            float4 b = texels[i * 2 + 1];
            var s = new SplatParticle { displacement = a.xyz, scale = a.w, velocity = b.xyz, glow = b.w, stick = stick[i], rest = rest[i] };
            SplatSimKernel.CarryHome(ref s.displacement, home[i], frame, out float3 hw);
            SplatHandAccum acc = SplatHandAccum.Empty;
            float3 pos = hw + s.displacement;
            for (int j = 0; j < colliderCount; j++)
                SplatSimKernel.AccumulateCollider(ref acc, pos, s.velocity, colliders[j], p, 0f);
            if (SplatSimKernel.UsesField(p, frame.mode))
            {
                int4 n = fieldNodes[i];
                SplatSimKernel.FieldParticle(ref s, nodes[n.x], nodes[n.y], nodes[n.z], nodes[n.w], fieldWeights[i], acc, frame, p);
            }
            else
            {
                int c = cluster[i];
                SplatSimKernel.StepParticle(ref s, hw, clusterHomeWorld[c], nodes[c], hash[i], arrival[i], acc, frame, p);
            }
            texels[i * 2] = new float4(s.displacement, s.scale);
            texels[i * 2 + 1] = new float4(s.velocity, s.glow);
            stick[i] = s.stick;
            rest[i] = s.rest;
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    struct SplatActivityJob : IJob
    {
        [ReadOnly] public NativeArray<float4> texels;
        public NativeArray<float> result;
        public int count;

        public void Execute()
        {
            float offset = 0f;
            float speed = 0f;
            float visible = 0f;
            for (int i = 0; i < count; i++)
            {
                float4 a = texels[i * 2];
                float4 b = texels[i * 2 + 1];
                offset = math.max(offset, math.lengthsq(a.xyz));
                speed = math.max(speed, math.lengthsq(b.xyz));
                visible = math.max(visible, a.w);
            }
            result[0] = math.max(offset, speed * 0.01f);
            result[1] = visible;
            result[2] = speed;
            result[3] = offset;
        }
    }

    public sealed class SplatSimPlan
    {
        public float3[] homes;
        public float[] hash;
        public SplatClusters clusters;
        public int perCluster;
    }

    public sealed class SplatSimRunner : IDisposable
    {
        public const int TextureWidth = 256;
        public const int MaxColliders = 16;
        const float SleepSpeedSq = 4e-6f;
        const float RestOffsetSq = 1e-8f;
        const int SleepFrames = 12;

        readonly int _count;
        readonly float3[] _homesManaged;
        readonly float[] _hashManaged;
        readonly float[] _arrivalManaged;
        readonly float3 _boundsCenter;
        readonly float _boundsRadius;
        readonly float _maxClusterRadius;

        NativeArray<float3> _home;
        NativeArray<float> _hash;
        NativeArray<float> _arrival;
        NativeArray<float> _stick;
        NativeArray<float3> _rest;
        NativeArray<int> _cluster;
        NativeArray<float3> _clusterHome;
        NativeArray<float3> _clusterHomeWorld;
        NativeArray<float> _clusterRadius;
        NativeArray<SplatClusterNode> _nodes;
        NativeArray<int2> _edges;
        NativeArray<float> _pin;
        NativeArray<int4> _fieldNodes;
        NativeArray<float4> _fieldWeights;
        NativeArray<float> _nodeMass;
        NativeArray<float4> _texels;
        NativeArray<SplatCollider> _colliders;
        NativeArray<float> _result;
        Texture2D _texture;
        float3x4 _prevBody;
        bool _hasPrevBody;
        bool _keepPose;
        bool _asleep = true;
        bool _dirty;
        int _quietFrames;
        float _modeTime;
        float _modeEnd;

        public SplatSimMode Mode { get; private set; } = SplatSimMode.Formed;
        public bool Frozen { get; set; }
        public bool Asleep => _asleep;
        public bool DrivesShader => !_asleep || Mode != SplatSimMode.Formed || _keepPose;
        public Texture2D Texture => _texture;
        public int Count => _count;
        public int ClusterCount => _nodes.Length;
        public float Activity { get; private set; }
        public int LastColliderCount { get; private set; }
        public bool LastMoved { get; private set; }
        public bool LastTouching { get; private set; }
        public int WakeCount { get; private set; }

        public Vector3 HomeCenter => _boundsCenter;
        public float HomeRadius => _boundsRadius;

        public float NearestHome(Vector3 bodyPoint, out Vector3 nearest)
        {
            float3 p = bodyPoint;
            float best = float.MaxValue;
            int index = -1;
            for (int i = 0; i < _count; i++)
            {
                float d = math.lengthsq(_homesManaged[i] - p);
                if (d < best)
                {
                    best = d;
                    index = i;
                }
            }
            nearest = index >= 0 ? (Vector3)_homesManaged[index] : bodyPoint;
            return index >= 0 ? math.sqrt(best) : float.MaxValue;
        }

        public float MaxHomeDistance(Vector3 bodyPoint)
        {
            float3 p = bodyPoint;
            float best = 0f;
            for (int i = 0; i < _count; i++) best = math.max(best, math.lengthsq(_homesManaged[i] - p));
            return math.sqrt(best);
        }

        public static Vector2 TexelFor(int particle)
        {
            int t = particle * 2;
            return new Vector2(t % TextureWidth, t / TextureWidth);
        }

        public static SplatSimPlan Plan(Vector3[] homesBody, int particlesPerCluster)
        {
            var homes = new float3[homesBody.Length];
            for (int i = 0; i < homes.Length; i++) homes[i] = homesBody[i];
            return new SplatSimPlan
            {
                homes = homes,
                hash = SplatSimSetup.Hashes(homes),
                clusters = SplatSimSetup.BuildClusters(homes, SplatSimSetup.ClusterCountFor(homes.Length, particlesPerCluster)),
                perCluster = particlesPerCluster
            };
        }

        public SplatSimRunner(Vector3[] homesBody, int particlesPerCluster) : this(Plan(homesBody, particlesPerCluster))
        {
        }

        public SplatSimRunner(SplatSimPlan plan)
        {
            _count = plan.homes.Length;
            _homesManaged = plan.homes;
            float3 lo = new float3(float.MaxValue);
            float3 hi = new float3(float.MinValue);
            for (int i = 0; i < _count; i++)
            {
                lo = math.min(lo, _homesManaged[i]);
                hi = math.max(hi, _homesManaged[i]);
            }
            _boundsCenter = (lo + hi) * 0.5f;
            _boundsRadius = math.length(hi - lo) * 0.5f;
            _hashManaged = plan.hash;
            _arrivalManaged = new float[_count];
            SplatClusters clusters = plan.clusters;

            _home = new NativeArray<float3>(_homesManaged, Allocator.Persistent);
            _hash = new NativeArray<float>(_hashManaged, Allocator.Persistent);
            _arrival = new NativeArray<float>(_count, Allocator.Persistent);
            _stick = new NativeArray<float>(_count, Allocator.Persistent);
            _rest = new NativeArray<float3>(_count, Allocator.Persistent);
            _cluster = new NativeArray<int>(clusters.Assignment, Allocator.Persistent);
            _clusterHome = new NativeArray<float3>(clusters.Home, Allocator.Persistent);
            _clusterHomeWorld = new NativeArray<float3>(clusters.Count, Allocator.Persistent);
            _clusterRadius = new NativeArray<float>(clusters.Radius, Allocator.Persistent);
            _nodes = new NativeArray<SplatClusterNode>(clusters.Count, Allocator.Persistent);
            _edges = new NativeArray<int2>(clusters.Edges, Allocator.Persistent);
            _pin = new NativeArray<float>(clusters.Pin, Allocator.Persistent);
            _fieldNodes = new NativeArray<int4>(clusters.ParticleNodes, Allocator.Persistent);
            _fieldWeights = new NativeArray<float4>(clusters.ParticleWeights, Allocator.Persistent);
            _nodeMass = new NativeArray<float>(clusters.NodeMass, Allocator.Persistent);
            _colliders = new NativeArray<SplatCollider>(MaxColliders, Allocator.Persistent);
            _result = new NativeArray<float>(4, Allocator.Persistent);
            for (int c = 0; c < clusters.Count; c++) _maxClusterRadius = math.max(_maxClusterRadius, clusters.Radius[c]);

            int height = math.max(1, (_count * 2 + TextureWidth - 1) / TextureWidth);
            _texels = new NativeArray<float4>(TextureWidth * height, Allocator.Persistent);
            _texture = new Texture2D(TextureWidth, height, TextureFormat.RGBAFloat, false, true)
            {
                name = "SplatSimState",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
            ResetState(1f);
            Upload();
        }

        void ResetState(float scale)
        {
            _keepPose = false;
            for (int i = 0; i < _count; i++)
            {
                _texels[i * 2] = new float4(0f, 0f, 0f, scale);
                _texels[i * 2 + 1] = float4.zero;
                _stick[i] = 0f;
                _rest[i] = float3.zero;
            }
            for (int c = 0; c < _nodes.Length; c++) _nodes[c] = default;
        }

        void Upload()
        {
            _texture.SetPixelData(_texels, 0);
            _texture.Apply(false, false);
            _dirty = false;
        }

        public void BeginSummon(Matrix4x4 bodyToWorld, Vector3 emitter, float spreadSeconds, float settleSeconds)
        {
            float last = SplatSimSetup.SummonArrivals(_homesManaged, _hashManaged, _arrivalManaged, ToBody(bodyToWorld), emitter, spreadSeconds);
            _arrival.CopyFrom(_arrivalManaged);
            ResetState(0f);
            Mode = SplatSimMode.Summoning;
            _modeTime = 0f;
            _modeEnd = last + settleSeconds;
            Wake();
        }

        public float3[] HomesBody => _homesManaged;
        public float ModeTime => _modeTime;

        public void BeginBurst(Matrix4x4 bodyToWorld, float speed)
        {
            float3[] v = SplatSimSetup.BurstVelocities(_homesManaged, _hashManaged, speed);
            for (int i = 0; i < _count; i++)
            {
                Vector3 w = bodyToWorld.MultiplyVector(v[i]);
                float4 a = _texels[i * 2];
                _texels[i * 2] = new float4(a.xyz, math.max(a.w, 1f));
                _texels[i * 2 + 1] = new float4(w.x, w.y, w.z, 0f);
            }
            float3 center = _boundsCenter;
            for (int c = 0; c < _nodes.Length; c++)
            {
                Vector3 dir = bodyToWorld.MultiplyVector(math.normalizesafe(_clusterHome[c] - center, new float3(0f, 1f, 0f)));
                _nodes[c] = new SplatClusterNode { displacement = _nodes[c].displacement, velocity = (float3)(Vector3)dir * speed };
            }
            Mode = SplatSimMode.Bursting;
            _modeTime = 0f;
            _modeEnd = float.MaxValue;
            Wake();
        }

        public void Poke(Matrix4x4 bodyToWorld, Vector3 worldPoint, float radius, float speed)
        {
            float3 p = worldPoint;
            float r = math.max(radius, 1e-3f);
            for (int i = 0; i < _count; i++)
            {
                float3 home = bodyToWorld.MultiplyPoint3x4(_homesManaged[i]);
                float4 a = _texels[i * 2];
                float3 offset = home + a.xyz - p;
                float d = math.length(offset);
                if (d >= r) continue;
                float w = 1f - d / r;
                float3 dir = math.normalizesafe(offset, SplatSimKernel.HashDirection(_hashManaged[i]));
                float4 b = _texels[i * 2 + 1];
                _texels[i * 2 + 1] = new float4(b.xyz + dir * (speed * w * (0.6f + 0.8f * _hashManaged[i])), math.max(b.w, w));
            }
            _keepPose = true;
            Wake();
        }

        public void CopyWorldState(Matrix4x4 bodyToWorld, out float3[] positions, out float3[] velocities)
        {
            positions = new float3[_count];
            velocities = new float3[_count];
            for (int i = 0; i < _count; i++)
            {
                float4 a = _texels[i * 2];
                positions[i] = (float3)bodyToWorld.MultiplyPoint3x4(_homesManaged[i]) + a.xyz;
                velocities[i] = _texels[i * 2 + 1].xyz;
            }
        }

        public void SeedFrom(Matrix4x4 bodyToWorld, float3[] fromHomesBody, float3[] worldPositions, float3[] worldVelocities, float spreadSeconds, float settleSeconds, float ramp)
        {
            int[] map = SplatSimSetup.MapByShape(fromHomesBody, _homesManaged, _hashManaged);
            for (int i = 0; i < _count; i++)
            {
                int j = map[i];
                float3 home = bodyToWorld.MultiplyPoint3x4(_homesManaged[i]);
                _texels[i * 2] = new float4(worldPositions[j] - home, 1f);
                _texels[i * 2 + 1] = new float4(worldVelocities[j], 0f);
                _stick[i] = 0f;
                _rest[i] = float3.zero;
            }
            for (int c = 0; c < _nodes.Length; c++) _nodes[c] = default;
            float last = SplatSimSetup.GatherArrivals(_homesManaged, _hashManaged, _arrivalManaged, spreadSeconds);
            _arrival.CopyFrom(_arrivalManaged);
            _prevBody = ToBody(bodyToWorld);
            _hasPrevBody = true;
            Mode = SplatSimMode.Gathering;
            _modeTime = 0f;
            _modeEnd = last + ramp + settleSeconds;
            Wake();
            Upload();
        }

        public void BeginDissolve(float sagSeconds, float spreadSeconds, float fallSeconds)
        {
            float last = SplatSimSetup.DissolveArrivals(_homesManaged, _hashManaged, _arrivalManaged, sagSeconds, spreadSeconds);
            _arrival.CopyFrom(_arrivalManaged);
            Mode = SplatSimMode.Dissolving;
            _modeTime = 0f;
            _modeEnd = last + fallSeconds;
            Wake();
        }

        public void Hide()
        {
            Mode = SplatSimMode.Hidden;
            ResetState(0f);
            Upload();
            _asleep = true;
        }

        public void ShowFormed()
        {
            Mode = SplatSimMode.Formed;
            ResetState(1f);
            Upload();
            _asleep = true;
        }

        void Wake()
        {
            WakeCount++;
            _asleep = false;
            _quietFrames = 0;
        }

        static float3x4 ToBody(Matrix4x4 m)
        {
            return new float3x4(
                new float3(m.m00, m.m10, m.m20),
                new float3(m.m01, m.m11, m.m21),
                new float3(m.m02, m.m12, m.m22),
                new float3(m.m03, m.m13, m.m23));
        }

        static bool Moved(in float3x4 a, in float3x4 b)
        {
            float3 dp = math.abs(a.c3 - b.c3);
            float3 dr = math.abs(a.c0 - b.c0) + math.abs(a.c1 - b.c1) + math.abs(a.c2 - b.c2);
            return math.cmax(dp) > 2e-4f || math.cmax(dr) > 2e-3f;
        }

        int GatherColliders(SplatCollider[] source, int sourceCount, in float3x4 body, in SplatSimParams p, out bool touching)
        {
            touching = false;
            if (source == null) return 0;
            float3 center = SplatSimKernel.Transform(body, _boundsCenter);
            float scale = math.length(body.c0);
            float bounds = _boundsRadius * scale;
            int n = 0;
            for (int j = 0; j < sourceCount && n < MaxColliders; j++)
            {
                SplatCollider c = source[j];
                if (c.strength <= 0f) continue;
                float dist = math.distance(c.position, center);
                float reach = (c.radius + _maxClusterRadius * scale * p.clusterReach) * p.attractShell;
                if (dist < bounds + reach) touching = true;
                if (dist < bounds + reach + 0.3f) _colliders[n++] = c;
            }
            return n;
        }

        public void Step(Matrix4x4 bodyToWorld, float dt, in SplatSimParams p, SplatCollider[] colliders, int colliderCount, Vector3 emitter, Vector3 emitterVelocity)
        {
            if (Mode == SplatSimMode.Hidden || Frozen) return;

            float3x4 body = ToBody(bodyToWorld);
            if (!_hasPrevBody)
            {
                _prevBody = body;
                _hasPrevBody = true;
            }
            bool moved = Moved(body, _prevBody);
            int active = GatherColliders(colliders, colliderCount, body, p, out bool touching);
            LastColliderCount = active;
            LastMoved = moved;
            LastTouching = touching;

            if (_asleep)
            {
                if (!touching && !(moved && p.carry < 0.999f))
                {
                    _prevBody = body;
                    return;
                }
                Wake();
            }

            float step = math.min(dt, SplatSimKernel.MaxDt);
            _modeTime += step;
            var frame = new SplatSimFrame
            {
                bodyToWorld = body,
                prevBodyToWorld = _prevBody,
                emitter = emitter,
                emitterVelocity = emitterVelocity,
                dt = step,
                modeTime = _modeTime,
                lag = moved && Mode == SplatSimMode.Formed ? math.saturate(1f - p.carry) : 0f,
                mode = (int)Mode
            };
            _prevBody = body;

            JobHandle clusterHandle = new SplatClusterJob
            {
                home = _clusterHome,
                radius = _clusterRadius,
                colliders = _colliders,
                edges = _edges,
                pin = _pin,
                particleHome = _home,
                particleState = _texels,
                fieldNodes = _fieldNodes,
                fieldWeights = _fieldWeights,
                nodeMass = _nodeMass,
                homeWorld = _clusterHomeWorld,
                nodes = _nodes,
                frame = frame,
                p = p,
                colliderCount = active
            }.Schedule();

            JobHandle particleHandle = new SplatParticleJob
            {
                home = _home,
                hash = _hash,
                arrival = _arrival,
                cluster = _cluster,
                clusterHomeWorld = _clusterHomeWorld,
                nodes = _nodes,
                colliders = _colliders,
                fieldNodes = _fieldNodes,
                fieldWeights = _fieldWeights,
                texels = _texels,
                stick = _stick,
                rest = _rest,
                frame = frame,
                p = p,
                colliderCount = active
            }.Schedule(_count, 512, clusterHandle);

            new SplatActivityJob { texels = _texels, result = _result, count = _count }.Schedule(particleHandle).Complete();
            Activity = _result[0];
            _dirty = true;

            if ((Mode == SplatSimMode.Summoning || Mode == SplatSimMode.Gathering) && _modeTime >= _modeEnd)
            {
                Mode = SplatSimMode.Formed;
            }
            else if (Mode == SplatSimMode.Dissolving && (_modeTime >= _modeEnd || _result[1] < 0.01f))
            {
                Hide();
                return;
            }
            else if (Mode == SplatSimMode.Formed)
            {
                _quietFrames = !touching && _result[2] < SleepSpeedSq ? _quietFrames + 1 : 0;
                if (_quietFrames >= SleepFrames)
                {
                    if (_result[3] < RestOffsetSq) ResetState(1f);
                    else _keepPose = true;
                    _asleep = true;
                }
            }

            if (_dirty) Upload();
        }

        public void Dispose()
        {
            if (_home.IsCreated) _home.Dispose();
            if (_hash.IsCreated) _hash.Dispose();
            if (_arrival.IsCreated) _arrival.Dispose();
            if (_stick.IsCreated) _stick.Dispose();
            if (_rest.IsCreated) _rest.Dispose();
            if (_cluster.IsCreated) _cluster.Dispose();
            if (_clusterHome.IsCreated) _clusterHome.Dispose();
            if (_clusterHomeWorld.IsCreated) _clusterHomeWorld.Dispose();
            if (_clusterRadius.IsCreated) _clusterRadius.Dispose();
            if (_nodes.IsCreated) _nodes.Dispose();
            if (_edges.IsCreated) _edges.Dispose();
            if (_pin.IsCreated) _pin.Dispose();
            if (_fieldNodes.IsCreated) _fieldNodes.Dispose();
            if (_fieldWeights.IsCreated) _fieldWeights.Dispose();
            if (_nodeMass.IsCreated) _nodeMass.Dispose();
            if (_texels.IsCreated) _texels.Dispose();
            if (_colliders.IsCreated) _colliders.Dispose();
            if (_result.IsCreated) _result.Dispose();
            if (_texture != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_texture);
                else UnityEngine.Object.DestroyImmediate(_texture);
                _texture = null;
            }
        }
    }
}
