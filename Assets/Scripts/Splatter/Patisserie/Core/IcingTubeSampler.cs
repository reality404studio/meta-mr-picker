using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoodPicker.Splatter.Patisserie
{
    public struct ParallelFrame
    {
        public Vector3 Position;
        public Vector3 Tangent;
        public Vector3 Normal;
        public Vector3 Binormal;
        public float ArcLength;

        public static ParallelFrame Initialize(Vector3 p0, Vector3 p1, Vector3 cakeNormalWS)
        {
            Vector3 T = (p1 - p0).normalized;
            if (T.sqrMagnitude < 1e-6f) T = Vector3.forward;

            Vector3 N = cakeNormalWS - Vector3.Dot(cakeNormalWS, T) * T;
            if (N.sqrMagnitude < 1e-6f)
            {
                Vector3 upRef = Mathf.Abs(T.y) < 0.98f ? Vector3.up : Vector3.right;
                N = Vector3.Cross(T, upRef);
            }
            N.Normalize();
            Vector3 B = Vector3.Cross(T, N).normalized;

            return new ParallelFrame
            {
                Position = p0,
                Tangent = T,
                Normal = N,
                Binormal = B,
                ArcLength = 0f
            };
        }

        public ParallelFrame Advance(Vector3 nextPos)
        {
            Vector3 delta = nextPos - Position;
            float dist = delta.magnitude;
            if (dist < 1e-6f) return this;

            Vector3 nextT = delta / dist;

            Vector3 nextN = Normal - Vector3.Dot(Normal, nextT) * nextT;
            if (nextN.sqrMagnitude < 1e-6f)
            {
                Vector3 upRef = Mathf.Abs(nextT.y) < 0.98f ? Vector3.up : Vector3.right;
                nextN = Vector3.Cross(nextT, upRef);
            }
            nextN.Normalize();
            Vector3 nextB = Vector3.Cross(nextT, nextN).normalized;

            return new ParallelFrame
            {
                Position = nextPos,
                Tangent = nextT,
                Normal = nextN,
                Binormal = nextB,
                ArcLength = ArcLength + dist
            };
        }
    }

    public static class IcingTubeSampler
    {
        public static SplatSample[] SampleRing(
            ParallelFrame frame,
            IcingNozzleProfile profile,
            float squeeze,
            Vector3 cakeNormalWS,
            float twistRate = 0f,
            int ringIndex = 0)
        {
            int M = Mathf.Max(6, profile.splatsPerRing);
            var result = new SplatSample[M];
            float twistAngle = twistRate * frame.ArcLength;

            for (int j = 0; j < M; j++)
            {
                float theta = (2f * Mathf.PI * j / M) + twistAngle;

                profile.EvaluateRingPoint(
                    theta,
                    squeeze,
                    frame.Tangent,
                    frame.Normal,
                    frame.Binormal,
                    cakeNormalWS,
                    out Vector3 localOffset,
                    out Vector3 nSplat,
                    out float scaleU,
                    out float scaleV);

                Vector3 pos = frame.Position + localOffset;

                MeshSplatSampler.NormalToQuaternion(nSplat.x, nSplat.y, nSplat.z, out float qx, out float qy, out float qz, out float qw);

                result[j] = new SplatSample
                {
                    CenterX = pos.x,
                    CenterY = pos.y,
                    CenterZ = pos.z,
                    NormalX = nSplat.x,
                    NormalY = nSplat.y,
                    NormalZ = nSplat.z,
                    ScaleTangentU = scaleU,
                    ScaleTangentV = scaleV,
                    ScaleNormal = scaleV * LetterPoseMath.NormalThicknessRatio,
                    RotX = qx,
                    RotY = qy,
                    RotZ = qz,
                    RotW = qw,
                    IsOutline = 0
                };
            }

            return result;
        }

        public static SplatSample[] SampleStrokePath(
            IList<Vector3> pathPoints,
            IList<float> squeezeValues,
            IcingNozzleProfile profile,
            Vector3 cakeNormalWS,
            float twistRate = 0f)
        {
            if (pathPoints == null || pathPoints.Count < 2)
                return Array.Empty<SplatSample>();

            var samples = new List<SplatSample>();
            ParallelFrame frame = ParallelFrame.Initialize(pathPoints[0], pathPoints[1], cakeNormalWS);

            float stepSpacing = Mathf.Max(0.001f, profile.advanceSpacing);

            int pCount = pathPoints.Count;
            int ringIdx = 0;

            for (int i = 0; i < pCount - 1; i++)
            {
                Vector3 pA = pathPoints[i];
                Vector3 pB = pathPoints[i + 1];
                float sqA = squeezeValues != null && i < squeezeValues.Count ? squeezeValues[i] : 1f;
                float sqB = squeezeValues != null && (i + 1) < squeezeValues.Count ? squeezeValues[i + 1] : sqA;

                float segmentLen = Vector3.Distance(pA, pB);
                if (segmentLen < 1e-5f) continue;

                float t = 0f;
                while (t <= segmentLen)
                {
                    float factor = segmentLen > 1e-6f ? t / segmentLen : 0f;
                    Vector3 currentPos = Vector3.Lerp(pA, pB, factor);
                    float currentSqueeze = Mathf.Lerp(sqA, sqB, factor);

                    frame = frame.Advance(currentPos);
                    SplatSample[] ring = SampleRing(frame, profile, currentSqueeze, cakeNormalWS, twistRate, ringIdx++);
                    samples.AddRange(ring);

                    t += stepSpacing;
                }
            }

            return samples.ToArray();
        }
    }
}
