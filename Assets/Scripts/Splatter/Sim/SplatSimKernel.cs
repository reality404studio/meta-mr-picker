using Unity.Mathematics;

namespace MoodPicker.Splatter.Sim
{
    public static class SplatSimKernel
    {
        public const float MaxDt = 1f / 30f;

        public static float3 Transform(in float3x4 m, float3 p)
        {
            return m.c0 * p.x + m.c1 * p.y + m.c2 * p.z + m.c3;
        }

        public static float3 HashDirection(float hash)
        {
            float a = hash * 6.2831853f * 7f;
            float z = math.frac(hash * 13.37f) * 2f - 1f;
            float r = math.sqrt(math.max(0f, 1f - z * z));
            return new float3(r * math.cos(a), z, r * math.sin(a));
        }

        public static bool IsSticky(float hash, float fraction)
        {
            return math.frac(hash * 7.31f + 0.17f) < fraction;
        }

        public static float3 ClampLength(float3 v, float max)
        {
            float sq = math.lengthsq(v);
            if (sq <= max * max) return v;
            return v * (max / math.sqrt(sq));
        }

        public static void CarryHome(ref float3 displacement, float3 homeBody, in SplatSimFrame f, out float3 homeWorld)
        {
            homeWorld = Transform(f.bodyToWorld, homeBody);
            if (f.lag > 0f)
                displacement -= (homeWorld - Transform(f.prevBodyToWorld, homeBody)) * f.lag;
        }

        public static void AccumulateCollider(
            ref SplatHandAccum acc,
            float3 position,
            float3 velocity,
            in SplatCollider c,
            in SplatSimParams p,
            float extraRadius)
        {
            float radius = c.radius + extraRadius;
            float outer = radius * p.attractShell;
            float3 r = position - c.position;
            float distSq = math.lengthsq(r);
            if (distSq >= outer * outer) return;

            float dist = math.sqrt(distSq);
            float3 n = dist > 1e-5f ? r / dist : new float3(0f, 1f, 0f);
            if (dist < acc.nearestDistance)
            {
                acc.nearestDistance = dist;
                acc.nearestPoint = c.position + n * radius;
                acc.nearestVelocity = c.velocity;
            }

            if (dist < radius)
            {
                float w = 1f - dist / radius;
                float firm = Firmness(math.length(c.velocity - velocity), p);
                acc.accel += (n * (p.handPush * w) + (c.velocity - velocity) * (p.handDrag * w)) * (c.strength * firm);
                acc.contact = math.max(acc.contact, w);
                acc.glow = 1f;
            }
            else
            {
                float s = 1f - (dist - radius) / math.max(outer - radius, 1e-5f);
                acc.accel += ((c.velocity - velocity) * (p.handDrag * 0.2f * s) - n * (p.handAttract * s)) * c.strength;
                acc.glow = math.max(acc.glow, s);
            }
        }

        public static float Firmness(float relativeSpeed, in SplatSimParams p)
        {
            if (p.thicken <= 0f) return 1f;
            float fast = math.saturate(relativeSpeed / math.max(p.thickenSpeed, 1e-3f));
            return math.lerp(1f, math.lerp(0.15f, 3f, fast), p.thicken);
        }

        public static void StepCluster(
            ref SplatClusterNode node,
            in SplatHandAccum hand,
            in SplatSimFrame f,
            in SplatSimParams p)
        {
            bool bursting = f.mode == (int)SplatSimMode.Bursting;
            bool springing = f.mode == (int)SplatSimMode.Formed || f.mode == (int)SplatSimMode.Gathering;
            if (!bursting && !springing)
            {
                node.displacement = float3.zero;
                node.velocity = float3.zero;
                return;
            }

            float3 a = hand.accel;
            if (springing) a += ClampLength(-node.displacement * p.clusterStiffness, p.maxSpringAccel);
            a.y -= bursting ? p.burstGravity : p.weight;
            node.velocity += a * f.dt;
            node.velocity *= math.exp(-(bursting ? p.burstDamping : p.clusterDamping) * f.dt);
            node.velocity = ClampLength(node.velocity, p.maxSpeed);
            node.displacement += node.velocity * f.dt;
        }

        public static void StepParticle(
            ref SplatParticle s,
            float3 homeWorld,
            float3 clusterHomeWorld,
            in SplatClusterNode cluster,
            float hash,
            float arrival,
            in SplatHandAccum hand,
            in SplatSimFrame f,
            in SplatSimParams p)
        {
            if (f.mode == (int)SplatSimMode.Hidden)
            {
                s = default;
                return;
            }

            float3 pos = homeWorld + s.displacement;

            if (f.mode == (int)SplatSimMode.Summoning)
            {
                if (f.modeTime < arrival)
                {
                    s.displacement = f.emitter + HashDirection(hash) * (0.004f + 0.008f * hash) - homeWorld;
                    s.velocity = f.emitterVelocity;
                    s.scale = 0f;
                    s.glow = 0f;
                    s.stick = 0f;
                    s.rest = float3.zero;
                    return;
                }

                float3 to = -s.displacement;
                float dist = math.length(to);
                float3 dir = dist > 1e-5f ? to / dist : float3.zero;
                float3 side = math.cross(dir, HashDirection(hash));
                float3 desired = dir * math.min(p.summonSpeed, dist * p.summonEase)
                               + side * (p.summonSwirl * math.min(p.summonSpeed, dist * p.summonEase) * math.saturate(dist * 8f));
                float3 a = (desired - s.velocity) * p.summonResponse + hand.accel;
                s.velocity = ClampLength(s.velocity + a * f.dt, p.maxSpeed);
                s.displacement += s.velocity * f.dt;
                s.scale = math.saturate((f.modeTime - arrival) / math.max(p.appearSeconds, 1e-3f));
                s.glow = math.max(hand.glow, math.saturate(dist * 12f));
                s.stick = 0f;
                s.rest = float3.zero;
                return;
            }

            bool released = f.mode == (int)SplatSimMode.Dissolving && f.modeTime >= arrival;
            bool bursting = f.mode == (int)SplatSimMode.Bursting;
            float3 accel = hand.accel;
            float damping;

            if (bursting)
            {
                float3 rel = homeWorld - clusterHomeWorld;
                float3 cohesive = clusterHomeWorld + cluster.displacement + rel * p.clusterGather;
                accel += ClampLength((cohesive - pos) * (p.clusterStiffness * p.cohesion), p.maxSpringAccel * 2f);
                accel += (cluster.velocity - s.velocity) * (p.clusterViscosity * p.cohesion);
                accel.y -= p.burstGravity;
                damping = p.burstDamping;
                s.stick = 0f;
                s.rest = float3.zero;
            }
            else if (released)
            {
                float3 drift = HashDirection(hash);
                accel += new float3(drift.x, -1f, drift.z * 0.6f) * new float3(p.fallGravity * 0.12f, p.fallGravity, p.fallGravity * 0.12f);
                damping = p.fallDamping;
                s.stick = 0f;
                s.rest = float3.zero;
            }
            else
            {
                float3 rel = homeWorld - clusterHomeWorld;
                float gather = math.lerp(1f, p.clusterGather, math.saturate(math.length(cluster.displacement) / math.max(p.gatherDistance, 1e-4f)));
                float3 cohesive = clusterHomeWorld + cluster.displacement + rel * gather;
                float3 target = math.lerp(homeWorld, cohesive, p.cohesion) + s.rest;
                float k = p.homeStiffness * (1f + p.stiffnessJitter * (hash * 2f - 1f));
                if (f.mode == (int)SplatSimMode.Gathering)
                    k *= math.saturate((f.modeTime - arrival) / math.max(p.gatherRamp, 1e-3f));
                float3 spring = ClampLength((target - pos) * k, p.maxSpringAccel);

                if (IsSticky(hash, p.stickFraction))
                {
                    float near = math.saturate(1f - math.length(s.displacement) / math.max(p.stickReach, 1e-4f));
                    s.stick = math.min(1f, s.stick + hand.glow * near * p.stickGain * f.dt);
                }
                s.stick *= math.exp(-p.stickDecay * f.dt);

                accel += spring * (1f - s.stick);
                accel += (cluster.velocity - s.velocity) * (p.clusterViscosity * p.cohesion * (1f - s.stick));
                if (s.stick > 0.01f && hand.nearestDistance < float.MaxValue)
                    accel += ((hand.nearestPoint - pos) * p.stickPull + (hand.nearestVelocity - s.velocity) * (p.handDrag * 2f)) * s.stick;
                accel.y -= p.weight;
                damping = p.homeDamping;

                if (p.plastic > 0f && f.mode == (int)SplatSimMode.Formed)
                {
                    float3 strain = s.displacement - s.rest;
                    float len = math.length(strain);
                    float over = len - p.plasticYield;
                    if (over > 0f)
                        s.rest = ClampLength(s.rest + strain / len * (over * math.saturate(p.plastic * f.dt)), p.plasticMax);
                }
            }

            s.velocity += accel * f.dt;
            s.velocity *= math.exp(-damping * f.dt);
            s.velocity = ClampLength(s.velocity, p.maxSpeed);
            s.displacement += s.velocity * f.dt;

            float y = homeWorld.y + s.displacement.y;
            if (y < p.floorY)
            {
                s.displacement.y = p.floorY - homeWorld.y;
                s.velocity.y = math.abs(s.velocity.y) * p.bounce;
                s.velocity.xz *= 0.8f;
            }

            s.glow = math.max(hand.glow, s.glow * math.exp(-p.glowDecay * f.dt));
            s.scale = released
                ? 1f - math.smoothstep(p.fadeDistance * 0.5f, p.fadeDistance, math.length(s.displacement))
                : 1f;
        }

        public const int FieldSubsteps = 4;
        public const float FieldMaxOffset = 0.06f;

        public static bool UsesField(in SplatSimParams p, int mode)
        {
            return p.field > 0.5f && mode == (int)SplatSimMode.Formed;
        }

        public static void StepFieldNode(ref SplatClusterNode node, float3 laplacian, float pin, float3 handAccel, float h, in SplatSimParams p)
        {
            float3 a = handAccel + laplacian * p.fieldStiffness - node.displacement * (p.fieldAnchor + pin * p.fieldPin);
            node.velocity += a * h;
            node.velocity *= math.exp(-p.fieldDamping * h);
            node.velocity = ClampLength(node.velocity, p.maxSpeed);
            node.displacement = ClampLength(node.displacement + node.velocity * h, FieldMaxOffset);
        }

        public static void FieldParticle(
            ref SplatParticle s,
            in SplatClusterNode a,
            in SplatClusterNode b,
            in SplatClusterNode c,
            in SplatClusterNode d,
            float4 w,
            in SplatHandAccum hand,
            in SplatSimFrame f,
            in SplatSimParams p)
        {
            s.displacement = a.displacement * w.x + b.displacement * w.y + c.displacement * w.z + d.displacement * w.w;
            s.velocity = a.velocity * w.x + b.velocity * w.y + c.velocity * w.z + d.velocity * w.w;
            s.scale = 1f;
            s.stick = 0f;
            s.rest = float3.zero;
            s.glow = math.max(hand.glow, s.glow * math.exp(-p.glowDecay * f.dt));
        }

        public static float Activity(in SplatParticle s)
        {
            return math.max(math.lengthsq(s.displacement), math.lengthsq(s.velocity) * 0.01f);
        }
    }
}
