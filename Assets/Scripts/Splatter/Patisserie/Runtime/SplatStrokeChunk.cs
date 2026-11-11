using System;
using System.Runtime.InteropServices;
using MoodPicker.Splatter.Sim;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MoodPicker.Splatter.Patisserie
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SplatVertex
    {
        public Vector3 positionOS;
        public Vector3 normalOS;
        public Vector2 quadUV;
        public Vector4 centerAndFlags;
        public Vector2 simTexel;
    }

    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SplatStrokeChunk : MonoBehaviour, IDisposable
    {
        public const int Capacity = 2048;
        public const int VertexCapacity = Capacity * 4;
        public const int IndexCapacity = Capacity * 6;
        public const int SimTextureWidth = 256;
        public const int SimTextureHeight = 16;

        static readonly VertexAttributeDescriptor[] VertexLayout = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 0),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4, 0),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 2, 0)
        };

        static readonly float[] CornerU = { -1f, 1f, 1f, -1f };
        static readonly float[] CornerV = { -1f, -1f, 1f, 1f };

        Mesh _mesh;
        MeshFilter _filter;
        MeshRenderer _renderer;
        MaterialPropertyBlock _block;

        NativeArray<SplatVertex> _vertices;
        NativeArray<ushort> _indices;
        NativeArray<float4> _texels;

        Texture2D _simTex;
        int _activeSplatCount;
        bool _isDirty;

        float3[] _homeBodies;
        float3[] _displacements;
        float3[] _velocities;
        float[] _scales;
        float[] _glows;

        public int ActiveSplatCount => _activeSplatCount;
        public int RemainingCapacity => Capacity - _activeSplatCount;
        public bool IsFull => _activeSplatCount >= Capacity;
        public MeshRenderer Renderer => _renderer;

        void Awake()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (_mesh != null) return;

            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            _block = new MaterialPropertyBlock();

            _mesh = new Mesh { name = "PatisserieStrokeChunk" };
            _mesh.SetVertexBufferParams(VertexCapacity, VertexLayout);
            _mesh.SetIndexBufferParams(IndexCapacity, IndexFormat.UInt16);

            _vertices = new NativeArray<SplatVertex>(VertexCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _indices = new NativeArray<ushort>(IndexCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _texels = new NativeArray<float4>(Capacity * 2, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            _homeBodies = new float3[Capacity];
            _displacements = new float3[Capacity];
            _velocities = new float3[Capacity];
            _scales = new float[Capacity];
            _glows = new float[Capacity];

            for (int i = 0; i < Capacity; i++)
            {
                int vBase = i * 4;
                int iBase = i * 6;
                _indices[iBase] = (ushort)vBase;
                _indices[iBase + 1] = (ushort)(vBase + 2);
                _indices[iBase + 2] = (ushort)(vBase + 1);
                _indices[iBase + 3] = (ushort)vBase;
                _indices[iBase + 4] = (ushort)(vBase + 3);
                _indices[iBase + 5] = (ushort)(vBase + 2);
            }

            _mesh.SetIndexBufferData(_indices, 0, 0, IndexCapacity, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
            _mesh.subMeshCount = 1;
            _mesh.SetSubMesh(0, new SubMeshDescriptor(0, 0), MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2.0f);

            _filter.sharedMesh = _mesh;

            _simTex = new Texture2D(SimTextureWidth, SimTextureHeight, TextureFormat.RGBAFloat, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "PatisserieSimTex"
            };
        }

        public bool AppendRing(
            SplatSample[] ringSamples,
            Vector3[] homeBodies,
            Vector3 nozzleTipWS,
            Vector3 jetDirWS,
            float jetSpeed)
        {
            if (ringSamples == null || ringSamples.Length == 0) return false;
            int count = ringSamples.Length;
            if (_activeSplatCount + count > Capacity) return false;

            EnsureInitialized();

            Matrix4x4 bodyToWorld = transform.localToWorldMatrix;
            int startIdx = _activeSplatCount;

            for (int k = 0; k < count; k++)
            {
                int splatIdx = startIdx + k;
                SplatSample s = ringSamples[k];
                Vector3 home = homeBodies != null && k < homeBodies.Length ? homeBodies[k] : new Vector3(s.CenterX, s.CenterY, s.CenterZ);

                SplatCloudMeshBuilder.RotateVector(s.ScaleTangentU, 0f, 0f, s.RotX, s.RotY, s.RotZ, s.RotW, out float ux, out float uy, out float uz);
                SplatCloudMeshBuilder.RotateVector(0f, s.ScaleTangentV, 0f, s.RotX, s.RotY, s.RotZ, s.RotW, out float vx, out float vy, out float vz);

                Vector2 texel = SplatSimRunner.TexelFor(splatIdx);

                int vBase = splatIdx * 4;
                for (int c = 0; c < 4; c++)
                {
                    float cu = CornerU[c];
                    float cv = CornerV[c];

                    var v = new SplatVertex
                    {
                        positionOS = new Vector3(home.x + cu * ux + cv * vx, home.y + cu * uy + cv * vy, home.z + cu * uz + cv * vz),
                        normalOS = new Vector3(s.NormalX, s.NormalY, s.NormalZ),
                        quadUV = new Vector2(cu, cv),
                        centerAndFlags = new Vector4(home.x, home.y, home.z, 0f),
                        simTexel = texel
                    };
                    _vertices[vBase + c] = v;
                }

                _homeBodies[splatIdx] = home;

                Vector3 homeWorld = bodyToWorld.MultiplyPoint3x4(home);
                Vector3 initialDispWS = nozzleTipWS - homeWorld;

                _displacements[splatIdx] = initialDispWS;
                _velocities[splatIdx] = jetDirWS * jetSpeed;
                _scales[splatIdx] = 1.0f;
                _glows[splatIdx] = 0.65f;

                _texels[splatIdx * 2] = new float4(initialDispWS.x, initialDispWS.y, initialDispWS.z, 1.0f);
                _texels[splatIdx * 2 + 1] = new float4(jetDirWS.x * jetSpeed, jetDirWS.y * jetSpeed, jetDirWS.z * jetSpeed, 0.65f);
            }

            _activeSplatCount += count;
            _isDirty = true;
            return true;
        }

        public void StepPhysics(float dt, float homeStiffness = 140f, float homeDamping = 14f, float glowDecay = 2.5f)
        {
            if (_activeSplatCount == 0) return;

            float dtClamped = Mathf.Min(dt, 0.033f);

            for (int i = 0; i < _activeSplatCount; i++)
            {
                float3 disp = _displacements[i];
                float3 vel = _velocities[i];
                float glow = _glows[i];

                float3 springForce = -homeStiffness * disp;
                float3 dampingForce = -homeDamping * vel;
                float3 accel = springForce + dampingForce;

                vel += accel * dtClamped;
                disp += vel * dtClamped;
                glow = Mathf.Max(0f, glow - glowDecay * dtClamped);

                _displacements[i] = disp;
                _velocities[i] = vel;
                _glows[i] = glow;

                _texels[i * 2] = new float4(disp.x, disp.y, disp.z, _scales[i]);
                _texels[i * 2 + 1] = new float4(vel.x, vel.y, vel.z, glow);
            }

            _simTex.SetPixelData(_texels, 0);
            _simTex.Apply(false, false);
        }

        static Vector4 LayoutVector(Pigment.FlipbookLayout layout)
        {
            float halfTexel = layout.Tile > 0 ? 0.5f / layout.Tile : 0.5f / 256f;
            return new Vector4(Mathf.Max(1, layout.Cols), Mathf.Max(1, layout.Rows), halfTexel, Mathf.Max(1, layout.Frames));
        }

        public void FlushMeshUpdates()
        {
            if (!_isDirty || _activeSplatCount == 0) return;

            int vertexCount = _activeSplatCount * 4;
            _mesh.SetVertexBufferData(_vertices, 0, 0, vertexCount, 0, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
            _mesh.SetSubMesh(0, new SubMeshDescriptor(0, _activeSplatCount * 6), MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);

            _simTex.SetPixelData(_texels, 0);
            _simTex.Apply(false, false);

            _isDirty = false;
        }

        public void BindMaterialProperties(
            SplatPigmentSlotAsset slotAsset,
            SplatShaderMode mode,
            float frame,
            float intensity = 1.0f,
            float scale = 1.2f)
        {
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            _renderer.GetPropertyBlock(_block);

            if (slotAsset != null && slotAsset.atlasTexture != null)
            {
                _block.SetTexture(SplatLetterRenderer.PropAtlas, slotAsset.atlasTexture);
                _block.SetVector(SplatLetterRenderer.PropLayout, LayoutVector(slotAsset.ToLayout()));
                _block.SetFloat(SplatLetterRenderer.PropHasSlot, 1f);
                _block.SetColor(SplatLetterRenderer.PropLightTone, slotAsset.lightTone);
                _block.SetColor(SplatLetterRenderer.PropShadowTone, slotAsset.shadowTone);
                _block.SetFloat(SplatLetterRenderer.PropThreshold, slotAsset.threshold);
            }
            else
            {
                _block.SetFloat(SplatLetterRenderer.PropHasSlot, 0f);
            }

            _block.SetFloat(SplatLetterRenderer.PropFrame, frame);
            _block.SetFloat(SplatLetterRenderer.PropMode, (float)mode);
            _block.SetFloat(SplatLetterRenderer.PropScale, scale);
            _block.SetFloat(SplatLetterRenderer.PropIntensity, intensity);
            _block.SetFloat(SplatLetterRenderer.PropOutline, 0f);
            _block.SetFloat(SplatLetterRenderer.PropCropOn, 0f);

            _block.SetTexture(SplatLetterRenderer.PropSimTex, _simTex);
            _block.SetFloat(SplatLetterRenderer.PropSimOn, 1f);

            _block.SetVector(SplatLetterRenderer.PropFlowParams, new Vector4(0.0006f, 1.4f, 1.05f, 1.0f));
            _block.SetVector(SplatLetterRenderer.PropDropParams, new Vector4(0.012f, 0.010f, 2.2f, 0.90f));
            _block.SetVector(SplatLetterRenderer.PropGlowParams, new Vector4(0.35f, 0.6f, 1f, 0f));
            _block.SetVector(SplatLetterRenderer.PropGlintParams, new Vector4(0f, 1.2f, 120f, 0.45f));
            _block.SetVector(SplatLetterRenderer.PropGlintShape, new Vector4(160f, 0.15f, 0.6f, 0f));

            _renderer.SetPropertyBlock(_block);
        }

        public void Clear()
        {
            _activeSplatCount = 0;
            _isDirty = false;
            if (_mesh != null)
            {
                _mesh.SetSubMesh(0, new SubMeshDescriptor(0, 0), MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
            }
        }

        public void Dispose()
        {
            if (_vertices.IsCreated) _vertices.Dispose();
            if (_indices.IsCreated) _indices.Dispose();
            if (_texels.IsCreated) _texels.Dispose();

            if (_mesh != null)
            {
                DestroyImmediate(_mesh);
                _mesh = null;
            }
            if (_simTex != null)
            {
                DestroyImmediate(_simTex);
                _simTex = null;
            }
        }

        void OnDestroy()
        {
            Dispose();
        }
    }
}
