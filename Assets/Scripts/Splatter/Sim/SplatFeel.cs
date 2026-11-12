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
        public float fluff;
        public float squish;
        public float plastic;
        public float tack;
        public float thicken;

        public static SplatFeel Water => new SplatFeel { weight = 0.2f, cohesion = 0.2f, viscosity = 0.1f, bounce = 0.2f, grain = 1f };
        public static SplatFeel Sand => new SplatFeel { weight = 0.7f, cohesion = 0.3f, viscosity = 0.7f, bounce = 0.8f, grain = 1f };
        public static SplatFeel Leaf => new SplatFeel { weight = 0.3f, cohesion = 0.25f, viscosity = 0.55f, bounce = 0.35f, grain = 0.65f };
        public static SplatFeel Jelly => new SplatFeel { weight = 0.4f, cohesion = 1f, viscosity = 0.25f, bounce = 0.9f, grain = 0f, squish = 1f };
        public static SplatFeel Slime => new SplatFeel { weight = 0.5f, cohesion = 0.9f, viscosity = 0.8f, bounce = 0.3f, grain = 0f, squish = 1f, plastic = 0.8f, tack = 0.8f };
        public static SplatFeel Starch => new SplatFeel { weight = 0.8f, cohesion = 0.5f, viscosity = 0.6f, bounce = 0.2f, grain = 0.8f, squish = 1f, plastic = 0.5f, thicken = 1f };
        public static SplatFeel Fur => new SplatFeel { weight = 0.1f, cohesion = 0.7f, viscosity = 0.2f, bounce = 0.7f, grain = 1f, fluff = 1f };

        public SplatFeel Clamped => new SplatFeel
        {
            weight = math.saturate(weight),
            cohesion = math.saturate(cohesion),
            viscosity = math.saturate(viscosity),
            bounce = math.saturate(bounce),
            grain = math.saturate(grain),
            fluff = math.saturate(fluff),
            squish = math.saturate(squish),
            plastic = math.saturate(plastic),
            tack = math.saturate(tack),
            thicken = math.saturate(thicken)
        };
    }

    public struct SplatFeelLook
    {
        public float grain;
        public float stretchMax;
        public float stretchSeconds;
        public float fluff;
        public float detach;
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
            p.stickFraction = 0.25f * f.cohesion * (1f - f.grain) + 0.35f * f.tack;
            p.stickDecay = math.lerp(1.2f, 0.25f, f.tack);
            p.stickReach = math.lerp(0.06f, 0.12f, f.tack);
            p.field = f.squish;
            p.fieldStiffness = math.lerp(150f, 900f, f.bounce);
            p.fieldAnchor = math.lerp(2f, 30f, f.bounce);
            p.fieldPin = 3000f;
            p.fieldDamping = math.lerp(0.6f, 6f, f.viscosity);
            p.fieldTack = f.tack;
            p.fieldTackBreak = 0.035f;
            p.fieldReach = f.tack > 0f || f.thicken > 0f ? 0f : 1f;
            p.fieldTouchGain = 6f;
            p.fieldRestMax = 0.015f;
            p.fieldHeal = 0.15f;
            p.fieldStickRate = 2.5f;
            p.fieldTackPull = 0.5f;
            p.crack = f.thicken * math.step(0.5f, f.squish);
            p.crackSpeed = 0.25f;
            p.crackSpread = 0.8f;
            p.crackRate = 10f;
            p.crackHeal = 0.3f;
            p.plastic = math.lerp(0f, 10f, f.plastic);
            p.plasticYield = 0.02f * (1f - f.plastic) * (1f - f.plastic) + 0.001f;
            p.plasticMax = 0.08f;
            p.thicken = f.thicken;
            p.thickenSpeed = 0.35f;
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
            float slime = math.step(1e-3f, f.tack) * math.step(0.5f, f.squish);
            return new SplatFeelLook
            {
                grain = math.lerp(1f, 0.45f, f.grain),
                stretchMax = math.lerp(4f, 0f, f.grain) * (1f - 0.85f * slime),
                stretchSeconds = math.lerp(0.012f, 0f, f.grain) * (1f - 0.85f * slime),
                fluff = f.fluff,
                detach = math.lerp(0.012f, 0.5f, math.step(0.5f, f.squish) * math.step(1e-3f, f.tack + f.thicken))
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
