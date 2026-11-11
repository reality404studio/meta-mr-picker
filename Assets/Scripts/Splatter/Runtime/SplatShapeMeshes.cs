using UnityEngine;

namespace MoodPicker.Splatter
{
    public static class SplatShapeMeshes
    {
        public static Mesh Sphere(string name, float radius, int segments, int rings)
        {
            var verts = new Vector3[(segments + 1) * (rings + 1)];
            var normals = new Vector3[verts.Length];
            int k = 0;
            for (int j = 0; j <= rings; j++)
            {
                float v = Mathf.PI * j / rings;
                for (int i = 0; i <= segments; i++)
                {
                    float u = 2f * Mathf.PI * i / segments;
                    var n = new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u));
                    normals[k] = n;
                    verts[k++] = n * radius;
                }
            }
            return Build(name, verts, normals, Grid(segments, rings));
        }

        public static Mesh Torus(string name, float majorRadius, float minorRadius, int segments, int rings)
        {
            var verts = new Vector3[(segments + 1) * (rings + 1)];
            var normals = new Vector3[verts.Length];
            int k = 0;
            for (int j = 0; j <= rings; j++)
            {
                float v = 2f * Mathf.PI * j / rings;
                for (int i = 0; i <= segments; i++)
                {
                    float u = 2f * Mathf.PI * i / segments;
                    var ringCenter = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0f) * majorRadius;
                    var n = new Vector3(Mathf.Cos(u) * Mathf.Cos(v), Mathf.Sin(u) * Mathf.Cos(v), Mathf.Sin(v));
                    normals[k] = n;
                    verts[k++] = ringCenter + n * minorRadius;
                }
            }
            return Build(name, verts, normals, Grid(segments, rings));
        }

        static int[] Grid(int segments, int rings)
        {
            var tris = new int[segments * rings * 6];
            int k = 0;
            for (int j = 0; j < rings; j++)
            {
                for (int i = 0; i < segments; i++)
                {
                    int a = j * (segments + 1) + i;
                    int b = a + segments + 1;
                    tris[k++] = a;
                    tris[k++] = a + 1;
                    tris[k++] = b;
                    tris[k++] = a + 1;
                    tris[k++] = b + 1;
                    tris[k++] = b;
                }
            }
            return tris;
        }

        static Mesh Build(string name, Vector3[] verts, Vector3[] normals, int[] tris)
        {
            var m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetNormals(normals);
            m.SetTriangles(tris, 0, true);
            return m;
        }
    }
}
