using System;

namespace MoodPicker.Splatter
{
    public sealed class SplatCloudMeshData
    {
        public float[] VerticesXYZ;
        public float[] NormalsXYZ;
        public float[] QuadUV;
        public float[] CenterAndFlagsXYZW;
        public int[] Indices;
        public int SplatCount;
    }

    public static class SplatCloudMeshBuilder
    {
        public static SplatCloudMeshData Build(SplatSample[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                return new SplatCloudMeshData
                {
                    VerticesXYZ = Array.Empty<float>(),
                    NormalsXYZ = Array.Empty<float>(),
                    QuadUV = Array.Empty<float>(),
                    CenterAndFlagsXYZW = Array.Empty<float>(),
                    Indices = Array.Empty<int>(),
                    SplatCount = 0
                };
            }

            int n = samples.Length;
            var verts = new float[n * 4 * 3];
            var norms = new float[n * 4 * 3];
            var uvs = new float[n * 4 * 2];
            var centers = new float[n * 4 * 4];
            var indices = new int[n * 6];

            float[] cornerU = { -1f, 1f, 1f, -1f };
            float[] cornerV = { -1f, -1f, 1f, 1f };

            for (int i = 0; i < n; i++)
            {
                SplatSample s = samples[i];
                RotateVector(s.ScaleTangentU, 0f, 0f, s.RotX, s.RotY, s.RotZ, s.RotW, out float ux, out float uy, out float uz);
                RotateVector(0f, s.ScaleTangentV, 0f, s.RotX, s.RotY, s.RotZ, s.RotW, out float vx, out float vy, out float vz);

                int vBase = i * 4;
                for (int c = 0; c < 4; c++)
                {
                    float cu = cornerU[c];
                    float cv = cornerV[c];
                    int vp = (vBase + c) * 3;
                    verts[vp] = s.CenterX + cu * ux + cv * vx;
                    verts[vp + 1] = s.CenterY + cu * uy + cv * vy;
                    verts[vp + 2] = s.CenterZ + cu * uz + cv * vz;

                    norms[vp] = s.NormalX;
                    norms[vp + 1] = s.NormalY;
                    norms[vp + 2] = s.NormalZ;

                    int up = (vBase + c) * 2;
                    uvs[up] = cu;
                    uvs[up + 1] = cv;

                    int cp = (vBase + c) * 4;
                    centers[cp] = s.CenterX;
                    centers[cp + 1] = s.CenterY;
                    centers[cp + 2] = s.CenterZ;
                    centers[cp + 3] = s.IsOutline != 0 ? 1f : 0f;
                }

                int iBase = i * 6;
                indices[iBase] = vBase;
                indices[iBase + 1] = vBase + 2;
                indices[iBase + 2] = vBase + 1;
                indices[iBase + 3] = vBase;
                indices[iBase + 4] = vBase + 3;
                indices[iBase + 5] = vBase + 2;
            }

            return new SplatCloudMeshData
            {
                VerticesXYZ = verts,
                NormalsXYZ = norms,
                QuadUV = uvs,
                CenterAndFlagsXYZW = centers,
                Indices = indices,
                SplatCount = n
            };
        }

        public static void RotateVector(
            float vx, float vy, float vz,
            float qx, float qy, float qz, float qw,
            out float rx, out float ry, out float rz)
        {
            float tx = 2f * (qy * vz - qz * vy);
            float ty = 2f * (qz * vx - qx * vz);
            float tz = 2f * (qx * vy - qy * vx);
            rx = vx + qw * tx + (qy * tz - qz * ty);
            ry = vy + qw * ty + (qz * tx - qx * tz);
            rz = vz + qw * tz + (qx * ty - qy * tx);
        }
    }
}
