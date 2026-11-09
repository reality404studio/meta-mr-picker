using System;
using System.Collections.Generic;

namespace MoodPicker.Splatter
{
    public static class MeshSplatSampler
    {
        public static SplatSample[] SampleMesh(
            float[] positionsXYZ,
            float[] normalsXYZ,
            int[] triangles,
            int surfaceCount,
            float letterSize,
            bool includeOutlineLayer,
            int seed = 1337)
        {
            if (positionsXYZ == null || triangles == null || triangles.Length < 3 || surfaceCount <= 0)
                return Array.Empty<SplatSample>();

            int triCount = triangles.Length / 3;
            var cdf = new double[triCount];
            double totalArea = 0.0;

            for (int t = 0; t < triCount; t++)
            {
                int i0 = triangles[t * 3] * 3;
                int i1 = triangles[t * 3 + 1] * 3;
                int i2 = triangles[t * 3 + 2] * 3;
                double ax = positionsXYZ[i1] - positionsXYZ[i0];
                double ay = positionsXYZ[i1 + 1] - positionsXYZ[i0 + 1];
                double az = positionsXYZ[i1 + 2] - positionsXYZ[i0 + 2];
                double bx = positionsXYZ[i2] - positionsXYZ[i0];
                double by = positionsXYZ[i2 + 1] - positionsXYZ[i0 + 1];
                double bz = positionsXYZ[i2 + 2] - positionsXYZ[i0 + 2];
                double cx = ay * bz - az * by;
                double cy = az * bx - ax * bz;
                double cz = ax * by - ay * bx;
                double area = 0.5 * Math.Sqrt(cx * cx + cy * cy + cz * cz);
                totalArea += area;
                cdf[t] = totalArea;
            }

            if (totalArea <= 1e-12)
                return Array.Empty<SplatSample>();

            float[] smoothNormals = includeOutlineLayer
                ? ComputeWeldedSmoothNormals(positionsXYZ, triangles, 1e-4f)
                : null;

            float baseSpacing = (float)Math.Sqrt(totalArea / surfaceCount) * 1.25f;
            float minSpacing = Math.Max(1e-4f, letterSize * 0.002f);
            float tangentScale = Math.Max(minSpacing, baseSpacing);
            float normalScale = tangentScale * LetterPoseMath.NormalThicknessRatio;
            float outlineOffset = LetterPoseMath.OutlinePushFactor * Math.Max(0.01f, letterSize);

            int totalOut = includeOutlineLayer ? surfaceCount * 2 : surfaceCount;
            var result = new SplatSample[totalOut];
            var rng = new Random(seed);

            for (int k = 0; k < surfaceCount; k++)
            {
                double target = ((k + rng.NextDouble()) / surfaceCount) * totalArea;
                int tri = LowerBound(cdf, target);
                int v0 = triangles[tri * 3];
                int v1 = triangles[tri * 3 + 1];
                int v2 = triangles[tri * 3 + 2];

                double r1 = Math.Sqrt(rng.NextDouble());
                double r2 = rng.NextDouble();
                float w0 = (float)(1.0 - r1);
                float w1 = (float)(r1 * (1.0 - r2));
                float w2 = (float)(r1 * r2);

                float px = w0 * positionsXYZ[v0 * 3] + w1 * positionsXYZ[v1 * 3] + w2 * positionsXYZ[v2 * 3];
                float py = w0 * positionsXYZ[v0 * 3 + 1] + w1 * positionsXYZ[v1 * 3 + 1] + w2 * positionsXYZ[v2 * 3 + 1];
                float pz = w0 * positionsXYZ[v0 * 3 + 2] + w1 * positionsXYZ[v1 * 3 + 2] + w2 * positionsXYZ[v2 * 3 + 2];

                float nx, ny, nz;
                if (normalsXYZ != null && normalsXYZ.Length >= positionsXYZ.Length)
                {
                    nx = w0 * normalsXYZ[v0 * 3] + w1 * normalsXYZ[v1 * 3] + w2 * normalsXYZ[v2 * 3];
                    ny = w0 * normalsXYZ[v0 * 3 + 1] + w1 * normalsXYZ[v1 * 3 + 1] + w2 * normalsXYZ[v2 * 3 + 1];
                    nz = w0 * normalsXYZ[v0 * 3 + 2] + w1 * normalsXYZ[v1 * 3 + 2] + w2 * normalsXYZ[v2 * 3 + 2];
                    Normalize(ref nx, ref ny, ref nz);
                }
                else
                {
                    FaceNormal(positionsXYZ, v0 * 3, v1 * 3, v2 * 3, out nx, out ny, out nz);
                }

                float qx, qy, qz, qw;
                NormalToQuaternion(nx, ny, nz, out qx, out qy, out qz, out qw);

                result[k] = new SplatSample
                {
                    CenterX = px,
                    CenterY = py,
                    CenterZ = pz,
                    NormalX = nx,
                    NormalY = ny,
                    NormalZ = nz,
                    ScaleTangentU = tangentScale,
                    ScaleTangentV = tangentScale,
                    ScaleNormal = normalScale,
                    RotX = qx,
                    RotY = qy,
                    RotZ = qz,
                    RotW = qw,
                    IsOutline = 0
                };

                if (includeOutlineLayer)
                {
                    float snx = w0 * smoothNormals[v0 * 3] + w1 * smoothNormals[v1 * 3] + w2 * smoothNormals[v2 * 3];
                    float sny = w0 * smoothNormals[v0 * 3 + 1] + w1 * smoothNormals[v1 * 3 + 1] + w2 * smoothNormals[v2 * 3 + 1];
                    float snz = w0 * smoothNormals[v0 * 3 + 2] + w1 * smoothNormals[v1 * 3 + 2] + w2 * smoothNormals[v2 * 3 + 2];
                    Normalize(ref snx, ref sny, ref snz);

                    float oqx, oqy, oqz, oqw;
                    NormalToQuaternion(snx, sny, snz, out oqx, out oqy, out oqz, out oqw);

                    result[surfaceCount + k] = new SplatSample
                    {
                        CenterX = px + snx * outlineOffset,
                        CenterY = py + sny * outlineOffset,
                        CenterZ = pz + snz * outlineOffset,
                        NormalX = snx,
                        NormalY = sny,
                        NormalZ = snz,
                        ScaleTangentU = tangentScale * 1.15f,
                        ScaleTangentV = tangentScale * 1.15f,
                        ScaleNormal = normalScale,
                        RotX = oqx,
                        RotY = oqy,
                        RotZ = oqz,
                        RotW = oqw,
                        IsOutline = 1
                    };
                }
            }

            return result;
        }

        public static float[] ComputeWeldedSmoothNormals(float[] positionsXYZ, int[] triangles, float tolerance = 1e-4f)
        {
            int vCount = positionsXYZ.Length / 3;
            float invTol = 1f / Math.Max(1e-6f, tolerance);
            var map = new Dictionary<(long, long, long), int>(vCount);
            var canonical = new int[vCount];
            var accum = new double[vCount * 3];

            for (int v = 0; v < vCount; v++)
            {
                long kx = (long)Math.Round(positionsXYZ[v * 3] * invTol);
                long ky = (long)Math.Round(positionsXYZ[v * 3 + 1] * invTol);
                long kz = (long)Math.Round(positionsXYZ[v * 3 + 2] * invTol);
                var key = (kx, ky, kz);
                if (!map.TryGetValue(key, out int rep))
                {
                    rep = v;
                    map[key] = rep;
                }
                canonical[v] = rep;
            }

            int triCount = triangles.Length / 3;
            for (int t = 0; t < triCount; t++)
            {
                int i0 = triangles[t * 3];
                int i1 = triangles[t * 3 + 1];
                int i2 = triangles[t * 3 + 2];
                int p0 = i0 * 3, p1 = i1 * 3, p2 = i2 * 3;
                double ax = positionsXYZ[p1] - positionsXYZ[p0];
                double ay = positionsXYZ[p1 + 1] - positionsXYZ[p0 + 1];
                double az = positionsXYZ[p1 + 2] - positionsXYZ[p0 + 2];
                double bx = positionsXYZ[p2] - positionsXYZ[p0];
                double by = positionsXYZ[p2 + 1] - positionsXYZ[p0 + 1];
                double bz = positionsXYZ[p2 + 2] - positionsXYZ[p0 + 2];
                double cx = ay * bz - az * by;
                double cy = az * bx - ax * bz;
                double cz = ax * by - ay * bx;

                int r0 = canonical[i0] * 3;
                int r1 = canonical[i1] * 3;
                int r2 = canonical[i2] * 3;
                accum[r0] += cx; accum[r0 + 1] += cy; accum[r0 + 2] += cz;
                accum[r1] += cx; accum[r1 + 1] += cy; accum[r1 + 2] += cz;
                accum[r2] += cx; accum[r2 + 1] += cy; accum[r2 + 2] += cz;
            }

            var outNormals = new float[vCount * 3];
            for (int v = 0; v < vCount; v++)
            {
                int r = canonical[v] * 3;
                float nx = (float)accum[r];
                float ny = (float)accum[r + 1];
                float nz = (float)accum[r + 2];
                Normalize(ref nx, ref ny, ref nz);
                outNormals[v * 3] = nx;
                outNormals[v * 3 + 1] = ny;
                outNormals[v * 3 + 2] = nz;
            }
            return outNormals;
        }

        public static void NormalToQuaternion(float nx, float ny, float nz, out float qx, out float qy, out float qz, out float qw)
        {
            Normalize(ref nx, ref ny, ref nz);
            if (nz < -0.9999f)
            {
                qx = 1f; qy = 0f; qz = 0f; qw = 0f;
                return;
            }
            float s = (float)Math.Sqrt((1.0 + nz) * 2.0);
            float invS = 1f / s;
            qx = -ny * invS;
            qy = nx * invS;
            qz = 0f;
            qw = 0.5f * s;
        }

        static int LowerBound(double[] cdf, double value)
        {
            int lo = 0, hi = cdf.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (cdf[mid] < value) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }

        static void FaceNormal(float[] pos, int p0, int p1, int p2, out float nx, out float ny, out float nz)
        {
            float ax = pos[p1] - pos[p0], ay = pos[p1 + 1] - pos[p0 + 1], az = pos[p1 + 2] - pos[p0 + 2];
            float bx = pos[p2] - pos[p0], by = pos[p2 + 1] - pos[p0 + 1], bz = pos[p2 + 2] - pos[p0 + 2];
            nx = ay * bz - az * by;
            ny = az * bx - ax * bz;
            nz = ax * by - ay * bx;
            Normalize(ref nx, ref ny, ref nz);
        }

        static void Normalize(ref float x, ref float y, ref float z)
        {
            double len = Math.Sqrt((double)x * x + (double)y * y + (double)z * z);
            if (len > 1e-8)
            {
                float inv = (float)(1.0 / len);
                x *= inv; y *= inv; z *= inv;
            }
            else
            {
                x = 0f; y = 0f; z = 1f;
            }
        }
    }
}
