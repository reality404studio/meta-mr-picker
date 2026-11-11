using System;
using Unity.Mathematics;

namespace MoodPicker.Splatter.Sim
{
    [Serializable]
    public struct SplatFeel
    {
        public float weight;
        public float cohesion;
        public float viscosity;
        public float bounce;
        public float grain;

        public static SplatFeel Water => new SplatFeel { weight = 0.2f, cohesion = 0.2f, viscosity = 0.1f, bounce = 0.2f, grain = 1f };
        public static SplatFeel Sand => new SplatFeel { weight = 0.85f, cohesion = 0.12f, viscosity = 0.75f, bounce = 0.1f, grain = 0.85f };
        public static SplatFeel Leaf => new SplatFeel { weight = 0.3f, cohesion = 0.25f, viscosity = 0.55f, bounce = 0.35f, grain = 0.65f };
        public static SplatFeel Jelly => new SplatFeel { weight = 0.4f, cohesion = 1f, viscosity = 0.45f, bounce = 0.95f, grain = 0f };

        public SplatFeel Clamped => new SplatFeel
        {
            weight = math.saturate(weight),
            cohesion = math.saturate(cohesion),
            viscosity = math.saturate(viscosity),
            bounce = math.saturate(bounce),
            grain = math.saturate(grain)
        };
    }

    public struct SplatFeelLook
    {
        public float grain;
        public float stretchMax;
        public float stretchSeconds;
    }

    public struct SplatTransitionShape
    {
        public float burstSpeed;
        public float burstSeconds;
        public float gatherSpread;
        public float settleSeconds;
    }

    public static class SplatFeelMap
    {
        public static void Apply(in SplatFeel feel, ref SplatSimParams p)
        {
            SplatFeel f = feel.Clamped;
            p.homeStiffness = math.lerp(60f, 320f, f.bounce);
            p.homeDamping = math.lerp(1.5f, 16f, f.viscosity);
            p.maxSpringAccel = math.lerp(40f, 14f, f.weight);
            p.cohesion = f.cohesion;
            p.clusterStiffness = math.lerp(40f, 160f, f.cohesion);
            p.clusterDamping = math.lerp(1.5f, 8f, f.viscosity);
            p.clusterViscosity = math.lerp(0f, 50f, f.cohesion);
            p.clusterGather = math.lerp(0.9f, 0.45f, f.cohesion);
            p.handPush = math.lerp(90f, 40f, f.weight);
            p.handDrag = math.lerp(6f, 30f, f.viscosity);
            p.stickFraction = 0.25f * f.cohesion * (1f - f.grain);
            p.weight = f.weight * 3f;
            p.fallGravity = math.lerp(1.2f, 6f, f.weight);
            p.fallDamping = math.lerp(0.2f, 1.5f, f.viscosity);
            p.bounce = f.bounce * 0.6f;
            p.burstDamping = math.lerp(3.5f, 7f, f.viscosity);
            p.burstGravity = math.lerp(0f, 5f, f.weight);
            p.gatherRamp = math.lerp(0.18f, 0.6f, f.viscosity);
        }

        public static SplatFeelLook Look(in SplatFeel feel)
        {
            SplatFeel f = feel.Clamped;
            return new SplatFeelLook
            {
                grain = math.lerp(1f, 0.45f, f.grain),
                stretchMax = math.lerp(4f, 0f, f.grain),
                stretchSeconds = math.lerp(0.012f, 0f, f.grain)
            };
        }

        public static SplatTransitionShape Morph(in SplatFeel feel)
        {
            SplatFeel f = feel.Clamped;
            return new SplatTransitionShape
            {
                burstSpeed = math.lerp(0.3f, 0.16f, f.weight) * math.lerp(1.1f, 0.85f, f.viscosity),
                burstSeconds = math.lerp(0.32f, 0.5f, f.weight),
                gatherSpread = math.lerp(0.55f, 1.1f, f.grain),
                settleSeconds = math.lerp(0.5f, 1.1f, f.viscosity)
            };
        }
    }
}
