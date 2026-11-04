using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MoodPicker.Dab
{
    public static class DabBottleAssets
    {
        public const string MeshPath = "Assets/Models/Dab/PetBottle.asset";
        public const string BottleMatPath = "Assets/Materials/Dab/Dab_Bottle.mat";
        public const string OutlineMatPath = "Assets/Materials/Dab/Dab_Outline.mat";
        public const string SettingsPath = "Assets/Settings/DabSettings.asset";
        public const int TriangleBudget = 1500;
        public const int Segments = 32;

        static readonly Vector2[] Profile =
        {
            new Vector2(0.000f, 0.000f), new Vector2(0.026f, 0.000f), new Vector2(0.032f, 0.006f), new Vector2(0.033f, 0.030f),
            new Vector2(0.030f, 0.050f), new Vector2(0.033f, 0.070f), new Vector2(0.033f, 0.140f), new Vector2(0.030f, 0.165f),
            new Vector2(0.020f, 0.190f), new Vector2(0.013f, 0.205f), new Vector2(0.013f, 0.218f), new Vector2(0.015f, 0.220f),
            new Vector2(0.015f, 0.232f), new Vector2(0.000f, 0.232f),
        };

        public static Mesh BuildMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float total = 0f;
            var along = new float[Profile.Length];
            for (int i = 1; i < Profile.Length; i++) { total += Vector2.Distance(Profile[i], Profile[i - 1]); along[i] = total; }
            for (int i = 0; i < Profile.Length; i++)
                for (int s = 0; s <= Segments; s++)
                {
                    float a = s * Mathf.PI * 2f / Segments;
                    verts.Add(new Vector3(Mathf.Cos(a) * Profile[i].x, Profile[i].y, Mathf.Sin(a) * Profile[i].x));
                    uvs.Add(new Vector2((float)s / Segments, along[i] / total));
                }
            int row = Segments + 1;
            for (int i = 0; i < Profile.Length - 1; i++)
                for (int s = 0; s < Segments; s++)
                {
                    int a = i * row + s, b = a + 1, c = a + row, d = c + 1;
                    if (Profile[i].x > 0f) { tris.Add(a); tris.Add(c); tris.Add(b); }
                    if (Profile[i + 1].x > 0f) { tris.Add(b); tris.Add(c); tris.Add(d); }
                }
            var m = new Mesh { name = "PetBottle" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public static Mesh EnsureMesh()
        {
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (m != null) return m;
            Directory.CreateDirectory(Path.GetDirectoryName(MeshPath));
            m = BuildMesh();
            AssetDatabase.CreateAsset(m, MeshPath);
            return m;
        }

        public static Material EnsureMaterial(string path, string shaderPath)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var sh = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (sh == null) throw new IOException("shader missing " + shaderPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            mat = new Material(sh);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        public static Material EnsureOutlineMaterial()
        {
            var m = EnsureMaterial(OutlineMatPath, CelShaderRender.OutlineShaderPath);
            m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Front);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static DabSettings EnsureSettings()
        {
            var s = AssetDatabase.LoadAssetAtPath<DabSettings>(SettingsPath);
            if (s != null) return s;
            s = ScriptableObject.CreateInstance<DabSettings>();
            AssetDatabase.CreateAsset(s, SettingsPath);
            return s;
        }

        public static GameObject CreateBottle(Transform parent)
        {
            var go = new GameObject("PetBottle");
            go.transform.SetParent(parent, false);
            var mesh = EnsureMesh();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { EnsureMaterial(BottleMatPath, CelShaderRender.ShaderPath), EnsureOutlineMaterial() };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var convex = go.AddComponent<MeshCollider>();
            convex.sharedMesh = mesh;
            convex.convex = true;
            var exact = go.AddComponent<MeshCollider>();
            exact.sharedMesh = mesh;
            exact.convex = false;
            return go;
        }
    }
}
