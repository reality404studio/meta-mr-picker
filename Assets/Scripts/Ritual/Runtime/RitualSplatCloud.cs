using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MoodPicker.Ritual
{
    public static class RitualUtil
    {
        public static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }

        public static void Quiet(Renderer r)
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            r.allowOcclusionWhenDynamic = false;
        }

        public static float Hash(int n) => Strata.Hash01(n);

        public static float Smooth(float t)
        {
            t = math.saturate(t);
            return t * t * (3f - 2f * t);
        }

        public static void Basis(float3 n, out float3 t, out float3 b)
        {
            float3 up = math.abs(n.y) < 0.99f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
            t = math.normalize(math.cross(up, n));
            b = math.cross(n, t);
        }

        public static float SegmentDistance(float3 p1, float3 q1, float3 p2, float3 q2, out float3 c1, out float3 c2)
        {
            float3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = math.dot(d1, d1), e = math.dot(d2, d2), f = math.dot(d2, r);
            float s, t;
            if (a <= 1e-10f && e <= 1e-10f)
            {
                s = t = 0f;
            }
            else if (a <= 1e-10f)
            {
                s = 0f;
                t = math.saturate(f / e);
            }
            else
            {
                float c = math.dot(d1, r);
                if (e <= 1e-10f)
                {
                    t = 0f;
                    s = math.saturate(-c / a);
                }
                else
                {
                    float b = math.dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom > 1e-10f ? math.saturate((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = math.saturate(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = math.saturate((b - c) / a);
                    }
                }
            }
            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
            return math.distance(c1, c2);
        }
    }

    public sealed class RitualSplatCloud : IDisposable
    {
        public const int TextureWidth = 256;
        const MeshUpdateFlags Quick = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers;

        [StructLayout(LayoutKind.Sequential)]
        struct Vertex
        {
            public Vector3 position;
            public Vector3 normal;
            public Vector2 quad;
            public Vector4 center;
            public Vector2 texel;
        }

        static readonly VertexAttributeDescriptor[] Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 0),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4, 0),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 2, 0)
        };

        static readonly float[] CornerU = { -1f, 1f, 1f, -1f };
        static readonly float[] CornerV = { -1f, -1f, 1f, 1f };

        public readonly GameObject gameObject;
        public readonly Transform transform;
        public readonly MeshRenderer renderer;
        public readonly int capacity;
        public readonly float3[] home;
        public readonly float3[] normal;
        public readonly float[] halfSize;
        public readonly float3[] disp;
        public readonly float3[] vel;
        public readonly float[] scale;
        public readonly float[] glow;

        Mesh _mesh;
        Texture2D _sim;
        NativeArray<Vertex> _verts;
        NativeArray<half4> _upload;
        readonly Bounds _bounds;
        int _count;
        int _draw = -1;
        int _drawApplied = -1;
        int _dirtyMin = int.MaxValue;
        int _dirtyMax = -1;
        bool _simDirty;
        bool _disposed;

        public int Count => _count;
        public Texture SimTexture => _sim;
        public bool Disposed => _disposed;
        public int DrawCount => _draw < 0 ? _count : Mathf.Min(_draw, _count);

        public RitualSplatCloud(string name, Transform parent, Material material, int capacity, float boundsRadius)
        {
            this.capacity = Mathf.Max(1, capacity);
            gameObject = new GameObject(name);
            transform = gameObject.transform;
            transform.SetParent(parent, false);
            var filter = gameObject.AddComponent<MeshFilter>();
            renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            RitualUtil.Quiet(renderer);

            home = new float3[this.capacity];
            normal = new float3[this.capacity];
            halfSize = new float[this.capacity];
            disp = new float3[this.capacity];
            vel = new float3[this.capacity];
            scale = new float[this.capacity];
            glow = new float[this.capacity];

            int vertexCount = this.capacity * 4;
            _mesh = new Mesh { name = name + "_Splats", hideFlags = HideFlags.DontSave };
            _mesh.SetVertexBufferParams(vertexCount, Layout);
            bool wide = vertexCount > 65535;
            _mesh.SetIndexBufferParams(this.capacity * 6, wide ? IndexFormat.UInt32 : IndexFormat.UInt16);
            if (wide)
            {
                var idx = new NativeArray<uint>(this.capacity * 6, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < this.capacity; i++)
                {
                    uint v = (uint)(i * 4);
                    int o = i * 6;
                    idx[o] = v;
                    idx[o + 1] = v + 2;
                    idx[o + 2] = v + 1;
                    idx[o + 3] = v;
                    idx[o + 4] = v + 3;
                    idx[o + 5] = v + 2;
                }
                _mesh.SetIndexBufferData(idx, 0, 0, idx.Length, Quick);
                idx.Dispose();
            }
            else
            {
                var idx = new NativeArray<ushort>(this.capacity * 6, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < this.capacity; i++)
                {
                    int v = i * 4;
                    int o = i * 6;
                    idx[o] = (ushort)v;
                    idx[o + 1] = (ushort)(v + 2);
                    idx[o + 2] = (ushort)(v + 1);
                    idx[o + 3] = (ushort)v;
                    idx[o + 4] = (ushort)(v + 3);
                    idx[o + 5] = (ushort)(v + 2);
                }
                _mesh.SetIndexBufferData(idx, 0, 0, idx.Length, Quick);
                idx.Dispose();
            }
            _bounds = new Bounds(Vector3.zero, Vector3.one * (boundsRadius * 2f));
            _mesh.subMeshCount = 1;
            _mesh.SetSubMesh(0, new SubMeshDescriptor(0, 0) { bounds = _bounds }, Quick);
            _mesh.bounds = _bounds;
            filter.sharedMesh = _mesh;

            _verts = new NativeArray<Vertex>(vertexCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            int height = Mathf.Max(1, (this.capacity * 2 + TextureWidth - 1) / TextureWidth);
            _upload = new NativeArray<half4>(TextureWidth * height, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _sim = new Texture2D(TextureWidth, height, TextureFormat.RGBAHalf, false, true)
            {
                name = name + "_Sim",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            _sim.SetPixelData(_upload, 0);
            _sim.Apply(false, false);
        }

        public int Add(float3 homeLocal, float3 normalLocal, float half)
        {
            if (_count >= capacity) return -1;
            int i = _count++;
            disp[i] = float3.zero;
            vel[i] = float3.zero;
            scale[i] = 1f;
            glow[i] = 0f;
            SetHome(i, homeLocal, normalLocal, half);
            _simDirty = true;
            return i;
        }

        public void SetHome(int i, float3 homeLocal, float3 normalLocal, float half)
        {
            home[i] = homeLocal;
            normal[i] = normalLocal;
            halfSize[i] = half;
            RitualUtil.Basis(normalLocal, out float3 t, out float3 b);
            Vector2 texel = new Vector2((i * 2) % TextureWidth, (i * 2) / TextureWidth);
            Vector4 center = new Vector4(homeLocal.x, homeLocal.y, homeLocal.z, 0f);
            Vector3 n = normalLocal;
            int v = i * 4;
            for (int c = 0; c < 4; c++)
            {
                float3 p = homeLocal + (t * CornerU[c] + b * CornerV[c]) * half;
                _verts[v + c] = new Vertex
                {
                    position = p,
                    normal = n,
                    quad = new Vector2(CornerU[c], CornerV[c]),
                    center = center,
                    texel = texel
                };
            }
            if (i < _dirtyMin) _dirtyMin = i;
            if (i > _dirtyMax) _dirtyMax = i;
        }

        public void Truncate(int count)
        {
            _count = Mathf.Clamp(count, 0, _count);
            _simDirty = true;
        }

        public void SetDrawCount(int count)
        {
            _draw = count;
        }

        public void MarkSim()
        {
            _simDirty = true;
        }

        public Vector3 WorldOf(int i)
        {
            return transform.TransformPoint(home[i]) + (Vector3)disp[i];
        }

        public void Flush()
        {
            if (_disposed) return;
            if (_dirtyMax >= _dirtyMin)
            {
                int start = _dirtyMin * 4;
                int n = (_dirtyMax - _dirtyMin + 1) * 4;
                _mesh.SetVertexBufferData(_verts, start, start, n, 0, Quick);
                _dirtyMin = int.MaxValue;
                _dirtyMax = -1;
            }
            int draw = DrawCount;
            if (draw != _drawApplied)
            {
                _mesh.SetSubMesh(0, new SubMeshDescriptor(0, draw * 6) { bounds = _bounds }, Quick);
                _mesh.bounds = _bounds;
                _drawApplied = draw;
            }
            if (_simDirty)
            {
                for (int i = 0; i < _count; i++)
                {
                    float3 d = disp[i];
                    float3 v = vel[i];
                    _upload[i * 2] = new half4(new float4(d.x, d.y, d.z, scale[i]));
                    _upload[i * 2 + 1] = new half4(new float4(v.x, v.y, v.z, glow[i]));
                }
                _sim.SetPixelData(_upload, 0);
                _sim.Apply(false, false);
                _simDirty = false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_verts.IsCreated) _verts.Dispose();
            if (_upload.IsCreated) _upload.Dispose();
            RitualUtil.Kill(_mesh);
            RitualUtil.Kill(_sim);
            if (gameObject != null) RitualUtil.Kill(gameObject);
        }
    }

    public static class RitualStaticSplats
    {
        public struct Builder
        {
            public List<Vector3> positions;
            public List<Vector3> normals;
            public List<Vector2> quad;
            public List<Vector4> a;
            public List<Vector4> b;
            public List<Vector4> c;
            public List<int> indices;

            public static Builder Create(int capacity)
            {
                return new Builder
                {
                    positions = new List<Vector3>(capacity * 4),
                    normals = new List<Vector3>(capacity * 4),
                    quad = new List<Vector2>(capacity * 4),
                    a = new List<Vector4>(capacity * 4),
                    b = new List<Vector4>(capacity * 4),
                    c = new List<Vector4>(capacity * 4),
                    indices = new List<int>(capacity * 6)
                };
            }

            public int Count => positions.Count / 4;

            public void Add(Vector3 center, Vector3 normal, Vector4 dataA, Vector4 dataB, Vector4 dataC)
            {
                int v = positions.Count;
                for (int k = 0; k < 4; k++)
                {
                    positions.Add(center);
                    normals.Add(normal);
                    quad.Add(new Vector2(k == 1 || k == 2 ? 1f : -1f, k >= 2 ? 1f : -1f));
                    a.Add(dataA);
                    b.Add(dataB);
                    c.Add(dataC);
                }
                indices.Add(v);
                indices.Add(v + 2);
                indices.Add(v + 1);
                indices.Add(v);
                indices.Add(v + 3);
                indices.Add(v + 2);
            }

            public Mesh ToMesh(string name, float boundsRadius)
            {
                var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
                if (positions.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(positions);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, quad);
                mesh.SetUVs(1, a);
                mesh.SetUVs(2, b);
                mesh.SetUVs(3, c);
                mesh.SetTriangles(indices, 0, false);
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (boundsRadius * 2f));
                mesh.UploadMeshData(true);
                return mesh;
            }
        }

        public static MeshRenderer Spawn(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            RitualUtil.Quiet(r);
            return r;
        }
    }
}
