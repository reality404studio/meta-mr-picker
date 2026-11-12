using Unity.Mathematics;

namespace MoodPicker.Splatter.Sim
{
    public enum SplatSimMode
    {
        Formed = 0,
        Summoning = 1,
        Dissolving = 2,
        Hidden = 3,
        Bursting = 4,
        Gathering = 5
    }

    public struct SplatSimParams
    {
        public float homeStiffness;
        public float homeDamping;
        public float stiffnessJitter;
        public float maxSpringAccel;
        public float cohesion;
        public float clusterStiffness;
        public float clusterDamping;
        public float clusterGather;
        public float gatherDistance;
        public float clusterReach;
        public float clusterViscosity;
        public float handPush;
        public float handDrag;
        public float handAttract;
        public float attractShell;
        public float stickFraction;
        public float stickGain;
        public float stickDecay;
        public float stickPull;
        public float stickReach;
        public float glowDecay;
        public float weight;
        public float fallGravity;
        public float fallDamping;
        public float bounce;
        public float floorY;
        public float maxSpeed;
        public float carry;
        public float summonSpeed;
        public float summonEase;
        public float summonResponse;
        public float summonSwirl;
        public float appearSeconds;
        public float fadeDistance;
        public float burstDamping;
        public float burstGravity;
        public float gatherRamp;
        public float field;
        public float fieldStiffness;
        public float fieldAnchor;
        public float fieldPin;
        public float fieldDamping;
        public float fieldTack;
        public float fieldTackBreak;
        public float fieldReach;
        public float fieldTouchGain;
        public float fieldRestMax;
        public float fieldHeal;
        public float fieldStickRate;
        public float fieldTackPull;
        public float crack;
        public float crackSpeed;
        public float crackSpread;
        public float crackRate;
        public float crackHeal;
        public float plastic;
        public float plasticYield;
        public float plasticMax;
        public float thicken;
        public float thickenSpeed;

        public static SplatSimParams WaterDroplet => new SplatSimParams
        {
            homeStiffness = 180f,
            homeDamping = 5f,
            stiffnessJitter = 0.35f,
            maxSpringAccel = 25f,
            cohesion = 0.7f,
            clusterStiffness = 90f,
            clusterDamping = 3f,
            clusterGather = 0.55f,
            gatherDistance = 0.03f,
            clusterReach = 0.6f,
            clusterViscosity = 25f,
            handPush = 60f,
            handDrag = 18f,
            handAttract = 1.2f,
            attractShell = 2.2f,
            stickFraction = 0.12f,
            stickGain = 8f,
            stickDecay = 1.2f,
            stickPull = 220f,
            stickReach = 0.06f,
            glowDecay = 5f,
            weight = 0f,
            fallGravity = 2.4f,
            fallDamping = 0.4f,
            bounce = 0.3f,
            floorY = -10f,
            maxSpeed = 4f,
            carry = 0.85f,
            summonSpeed = 0.9f,
            summonEase = 6f,
            summonResponse = 30f,
            summonSwirl = 0.6f,
            appearSeconds = 0.12f,
            fadeDistance = 0.5f,
            burstDamping = 2.2f,
            burstGravity = 0.4f,
            gatherRamp = 0.35f
        };
    }

    public struct SplatCollider
    {
        public float3 position;
        public float radius;
        public float3 velocity;
        public float strength;
    }

    public struct SplatSimFrame
    {
        public float3x4 bodyToWorld;
        public float3x4 prevBodyToWorld;
        public float3 emitter;
        public float3 emitterVelocity;
        public float dt;
        public float modeTime;
        public float lag;
        public int mode;
    }

    public struct SplatParticle
    {
        public float3 displacement;
        public float scale;
        public float3 velocity;
        public float glow;
        public float stick;
        public float3 rest;
    }

    public struct SplatClusterNode
    {
        public float3 displacement;
        public float3 velocity;
        public float3 rest;
        public float stick;
        public float stretch;
        public float crack;
    }

    public struct SplatHandAccum
    {
        public float3 accel;
        public float3 nearestPoint;
        public float3 nearestVelocity;
        public float nearestDistance;
        public float contact;
        public float glow;

        public static SplatHandAccum Empty => new SplatHandAccum { nearestDistance = float.MaxValue };
    }
}
