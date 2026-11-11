using System;
using UnityEngine;

namespace MoodPicker.Splatter.Patisserie
{
    public enum NozzleType
    {
        Star6Tip,
        FrenchStar8Tip,
        RoundTip,
        PetalRibbonTip
    }

    [Serializable]
    public struct IcingNozzleProfile
    {
        public NozzleType type;
        public float baseRadius;
        public int fluteCount;
        public float fluteDepth;
        public int splatsPerRing;
        public float advanceSpacing;
        public float ellipseAspectX;
        public float ellipseAspectY;
        public float squashFactor;

        public static IcingNozzleProfile Star6 => new IcingNozzleProfile
        {
            type = NozzleType.Star6Tip,
            baseRadius = 0.009f,
            fluteCount = 6,
            fluteDepth = 0.24f,
            splatsPerRing = 20,
            advanceSpacing = 0.004f,
            ellipseAspectX = 1f,
            ellipseAspectY = 1f,
            squashFactor = 0.75f
        };

        public static IcingNozzleProfile FrenchStar8 => new IcingNozzleProfile
        {
            type = NozzleType.FrenchStar8Tip,
            baseRadius = 0.010f,
            fluteCount = 8,
            fluteDepth = 0.16f,
            splatsPerRing = 24,
            advanceSpacing = 0.0045f,
            ellipseAspectX = 1f,
            ellipseAspectY = 1f,
            squashFactor = 0.75f
        };

        public static IcingNozzleProfile Round => new IcingNozzleProfile
        {
            type = NozzleType.RoundTip,
            baseRadius = 0.008f,
            fluteCount = 0,
            fluteDepth = 0f,
            splatsPerRing = 18,
            advanceSpacing = 0.0036f,
            ellipseAspectX = 1f,
            ellipseAspectY = 1f,
            squashFactor = 0.75f
        };

        public static IcingNozzleProfile PetalRibbon => new IcingNozzleProfile
        {
            type = NozzleType.PetalRibbonTip,
            baseRadius = 0.007f,
            fluteCount = 0,
            fluteDepth = 0f,
            splatsPerRing = 20,
            advanceSpacing = 0.003f,
            ellipseAspectX = 1.6f,
            ellipseAspectY = 0.35f,
            squashFactor = 0.85f
        };

        public void EvaluateRingPoint(
            float theta,
            float squeeze,
            Vector3 Tk,
            Vector3 Nk,
            Vector3 Bk,
            Vector3 cakeNormalWS,
            out Vector3 localOffset,
            out Vector3 normalSplat,
            out float scaleU,
            out float scaleV)
        {
            float p = Mathf.Clamp(squeeze, 0.1f, 1.5f);
            float cosK = fluteCount > 0 ? Mathf.Cos(fluteCount * theta) : 1f;
            float sinK = fluteCount > 0 ? Mathf.Sin(fluteCount * theta) : 0f;

            float rBase = baseRadius * p * (1f + fluteDepth * cosK);
            float rPrime = fluteCount > 0 ? -baseRadius * p * fluteDepth * fluteCount * sinK : 0f;

            float cosTheta = Mathf.Cos(theta);
            float sinTheta = Mathf.Sin(theta);

            float rx = rBase * ellipseAspectX;
            float ry = rBase * ellipseAspectY;
            float rEff = Mathf.Sqrt(cosTheta * cosTheta * rx * rx + sinTheta * sinTheta * ry * ry);

            Vector3 d = cosTheta * Nk + sinTheta * Bk;
            Vector3 dPerp = -sinTheta * Nk + cosTheta * Bk;

            float dotCake = Vector3.Dot(d, cakeNormalWS);
            float squash = (dotCake < 0f) ? Mathf.Lerp(1f, squashFactor, -dotCake) : 1f;

            localOffset = d * (rEff * squash);

            Vector3 tTheta = (rPrime * squash) * d + (rEff * squash) * dPerp;
            Vector3 nRaw = Vector3.Cross(tTheta, Tk);
            if (nRaw.sqrMagnitude > 1e-8f)
            {
                normalSplat = nRaw.normalized;
            }
            else
            {
                normalSplat = d.normalized;
            }

            float perimeterStep = (2f * Mathf.PI * Mathf.Max(rEff, 0.001f)) / Mathf.Max(1, splatsPerRing);
            scaleU = Mathf.Max(perimeterStep, advanceSpacing) * 1.65f;
            scaleV = advanceSpacing * 1.65f;
        }
    }
}
