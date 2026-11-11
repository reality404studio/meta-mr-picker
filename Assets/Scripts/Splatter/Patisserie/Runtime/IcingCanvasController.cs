using System;
using System.Collections.Generic;
using MoodPicker.Splatter;
using MoodPicker.Splatter.Sim;
using UnityEngine;

namespace MoodPicker.Splatter.Patisserie
{
    public sealed class IcingCanvasController : MonoBehaviour
    {
        [SerializeField] Material splatMaterial;
        [SerializeField] SplatPigmentSlotAsset defaultCakeSlot;
        [SerializeField] SplatShaderMode cakeShaderMode = SplatShaderMode.Raw;
        [SerializeField] int cakeBaseSplatCount = 12000;
        [SerializeField] int maxChunks = 12;

        Transform _cakeRoot;
        MeshFilter _cakeFilter;
        MeshRenderer _cakeRenderer;
        MaterialPropertyBlock _cakeBlock;

        List<SplatStrokeChunk> _chunks = new List<SplatStrokeChunk>();
        int _activeChunkIndex = 0;

        SplatPigmentSlotAsset _currentSlot;
        SplatPigmentSlotAsset _previousSlot;
        bool _glazeActive;
        float _glazeProgress;
        float _glazeDuration = 1.5f;
        Vector3 _glazeOriginOS;
        float _cakeRadius = 0.12f;

        public Transform CakeRoot => _cakeRoot;
        public SplatPigmentSlotAsset CurrentSlot => _currentSlot;
        public IReadOnlyList<SplatStrokeChunk> Chunks => _chunks;

        void Awake()
        {
            InitializeCake();
        }

        public void InitializeCake()
        {
            if (_cakeRoot != null) return;

            GameObject rootGo = new GameObject("CakeRoot");
            rootGo.transform.SetParent(transform, false);
            _cakeRoot = rootGo.transform;

            GameObject baseGo = new GameObject("CakeBaseMesh");
            baseGo.transform.SetParent(_cakeRoot, false);
            _cakeFilter = baseGo.AddComponent<MeshFilter>();
            _cakeRenderer = baseGo.AddComponent<MeshRenderer>();
            _cakeRenderer.sharedMaterial = splatMaterial;
            _cakeBlock = new MaterialPropertyBlock();

            Mesh cylinderMesh = GenerateTwoTierCakeMesh(0.12f, 0.04f, 0.08f, 0.035f);
            BuildCakeSplatBase(cylinderMesh);

            for (int i = 0; i < maxChunks; i++)
            {
                GameObject chunkGo = new GameObject($"IcingChunk_{i:D2}");
                chunkGo.transform.SetParent(_cakeRoot, false);
                var chunk = chunkGo.AddComponent<SplatStrokeChunk>();
                chunk.Renderer.sharedMaterial = splatMaterial;
                chunk.EnsureInitialized();
                _chunks.Add(chunk);
            }

            _currentSlot = defaultCakeSlot;
        }

        Mesh GenerateTwoTierCakeMesh(float rBottom, float hBottom, float rTop, float hTop)
        {
            Mesh mesh = new Mesh { name = "ProceduralTwoTierCake" };
            int seg = 32;
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<int> tris = new List<int>();

            AddCylinderSection(verts, norms, tris, 0f, hBottom, rBottom, seg);
            AddCylinderSection(verts, norms, tris, hBottom, hBottom + hTop, rTop, seg);

            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        void AddCylinderSection(List<Vector3> verts, List<Vector3> norms, List<int> tris, float y0, float y1, float radius, int seg)
        {
            int centerTopIdx = verts.Count;
            verts.Add(new Vector3(0, y1, 0));
            norms.Add(Vector3.up);

            int rimTopStart = verts.Count;
            for (int i = 0; i < seg; i++)
            {
                float a = (2f * Mathf.PI * i) / seg;
                float x = Mathf.Cos(a) * radius;
                float z = Mathf.Sin(a) * radius;
                verts.Add(new Vector3(x, y1, z));
                norms.Add(Vector3.up);
            }

            for (int i = 0; i < seg; i++)
            {
                int next = (i + 1) % seg;
                tris.Add(centerTopIdx);
                tris.Add(rimTopStart + i);
                tris.Add(rimTopStart + next);
            }

            int sideStart = verts.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = (2f * Mathf.PI * (i % seg)) / seg;
                float x = Mathf.Cos(a) * radius;
                float z = Mathf.Sin(a) * radius;
                Vector3 n = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));

                verts.Add(new Vector3(x, y1, z));
                norms.Add(n);
                verts.Add(new Vector3(x, y0, z));
                norms.Add(n);
            }

            for (int i = 0; i < seg; i++)
            {
                int i0 = sideStart + i * 2;
                int i1 = sideStart + i * 2 + 1;
                int i2 = sideStart + (i + 1) * 2;
                int i3 = sideStart + (i + 1) * 2 + 1;

                tris.Add(i0);
                tris.Add(i2);
                tris.Add(i1);

                tris.Add(i1);
                tris.Add(i2);
                tris.Add(i3);
            }
        }

        void BuildCakeSplatBase(Mesh source)
        {
            Vector3[] v3 = source.vertices;
            Vector3[] n3 = source.normals;
            int[] tris = source.triangles;

            float[] pos = new float[v3.Length * 3];
            float[] nrm = new float[n3.Length * 3];
            for (int i = 0; i < v3.Length; i++)
            {
                pos[i * 3] = v3[i].x;
                pos[i * 3 + 1] = v3[i].y;
                pos[i * 3 + 2] = v3[i].z;

                nrm[i * 3] = n3[i].x;
                nrm[i * 3 + 1] = n3[i].y;
                nrm[i * 3 + 2] = n3[i].z;
            }

            SplatSample[] samples = MeshSplatSampler.SampleMesh(pos, nrm, tris, cakeBaseSplatCount, _cakeRadius * 2f, false);
            SplatCloudMeshData cloudData = SplatCloudMeshBuilder.Build(samples);

            Mesh splatMesh = new Mesh { name = "CakeBaseSplatMesh" };
            int vCount = cloudData.VerticesXYZ.Length / 3;
            Vector3[] vertices = new Vector3[vCount];
            Vector3[] normals = new Vector3[vCount];
            Vector2[] uvs = new Vector2[vCount];
            Vector4[] centers = new Vector4[vCount];

            for (int i = 0; i < vCount; i++)
            {
                vertices[i] = new Vector3(cloudData.VerticesXYZ[i * 3], cloudData.VerticesXYZ[i * 3 + 1], cloudData.VerticesXYZ[i * 3 + 2]);
                normals[i] = new Vector3(cloudData.NormalsXYZ[i * 3], cloudData.NormalsXYZ[i * 3 + 1], cloudData.NormalsXYZ[i * 3 + 2]);
                uvs[i] = new Vector2(cloudData.QuadUV[i * 2], cloudData.QuadUV[i * 2 + 1]);
                centers[i] = new Vector4(cloudData.CenterAndFlagsXYZW[i * 4], cloudData.CenterAndFlagsXYZW[i * 4 + 1], cloudData.CenterAndFlagsXYZW[i * 4 + 2], 0f);
            }

            splatMesh.SetVertices(vertices);
            splatMesh.SetNormals(normals);
            splatMesh.SetUVs(0, uvs);
            splatMesh.SetUVs(1, centers);
            splatMesh.SetTriangles(cloudData.Indices, 0);
            splatMesh.RecalculateBounds();

            _cakeFilter.sharedMesh = splatMesh;
        }

        public SplatStrokeChunk GetOrCreateActiveChunk(int neededSplats)
        {
            if (_chunks.Count == 0) return null;

            SplatStrokeChunk current = _chunks[_activeChunkIndex];
            if (current.RemainingCapacity >= neededSplats)
            {
                return current;
            }

            _activeChunkIndex = (_activeChunkIndex + 1) % _chunks.Count;
            SplatStrokeChunk next = _chunks[_activeChunkIndex];
            next.Clear();
            return next;
        }

        public void PourGlaze(SplatPigmentSlotAsset newSlot, Vector3 hitPointOS, float duration = 1.5f)
        {
            if (newSlot == null) return;
            _previousSlot = _currentSlot;
            _currentSlot = newSlot;
            _glazeOriginOS = hitPointOS;
            _glazeProgress = 0f;
            _glazeDuration = Mathf.Max(0.5f, duration);
            _glazeActive = true;
        }

        public void RotateTurntable(float angleDegrees)
        {
            if (_cakeRoot != null)
            {
                _cakeRoot.Rotate(Vector3.up, angleDegrees, Space.World);
            }
        }

        static Vector4 LayoutVector(Pigment.FlipbookLayout layout)
        {
            float halfTexel = layout.Tile > 0 ? 0.5f / layout.Tile : 0.5f / 256f;
            return new Vector4(Mathf.Max(1, layout.Cols), Mathf.Max(1, layout.Rows), halfTexel, Mathf.Max(1, layout.Frames));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float time = Time.time;

            if (_glazeActive)
            {
                _glazeProgress += dt / _glazeDuration;
                if (_glazeProgress >= 1f)
                {
                    _glazeProgress = 1f;
                    _glazeActive = false;
                }
            }

            UpdateBaseMaterial(time);

            float frame = 0f;
            if (_currentSlot != null)
            {
                frame = LetterPoseMath.SlotFrameContinuous(time, _currentSlot.fps, _currentSlot.frames);
            }

            for (int i = 0; i < _chunks.Count; i++)
            {
                SplatStrokeChunk chunk = _chunks[i];
                if (chunk.ActiveSplatCount > 0)
                {
                    chunk.StepPhysics(dt);
                    chunk.FlushMeshUpdates();
                    chunk.BindMaterialProperties(_currentSlot, cakeShaderMode, frame);
                }
            }
        }

        void UpdateBaseMaterial(float time)
        {
            if (_cakeRenderer == null) return;
            _cakeRenderer.GetPropertyBlock(_cakeBlock);

            float frame = 0f;
            if (_currentSlot != null && _currentSlot.atlasTexture != null)
            {
                frame = LetterPoseMath.SlotFrameContinuous(time, _currentSlot.fps, _currentSlot.frames);
                _cakeBlock.SetTexture(SplatLetterRenderer.PropAtlas, _currentSlot.atlasTexture);
                _cakeBlock.SetVector(SplatLetterRenderer.PropLayout, LayoutVector(_currentSlot.ToLayout()));
                _cakeBlock.SetFloat(SplatLetterRenderer.PropHasSlot, 1f);
                _cakeBlock.SetColor(SplatLetterRenderer.PropLightTone, _currentSlot.lightTone);
                _cakeBlock.SetColor(SplatLetterRenderer.PropShadowTone, _currentSlot.shadowTone);
                _cakeBlock.SetFloat(SplatLetterRenderer.PropThreshold, _currentSlot.threshold);
            }
            else
            {
                _cakeBlock.SetFloat(SplatLetterRenderer.PropHasSlot, 0f);
            }

            _cakeBlock.SetFloat(SplatLetterRenderer.PropFrame, frame);
            _cakeBlock.SetFloat(SplatLetterRenderer.PropMode, (float)cakeShaderMode);
            _cakeBlock.SetFloat(SplatLetterRenderer.PropScale, 1.2f);
            _cakeBlock.SetFloat(SplatLetterRenderer.PropIntensity, 1f);
            _cakeBlock.SetFloat(SplatLetterRenderer.PropSimOn, 0f);

            if (_glazeActive && _previousSlot != null && _previousSlot.atlasTexture != null)
            {
                float prevFrame = LetterPoseMath.SlotFrameContinuous(time, _previousSlot.fps, _previousSlot.frames);
                _cakeBlock.SetTexture(SplatLetterRenderer.PropAtlas2, _previousSlot.atlasTexture);
                _cakeBlock.SetVector(SplatLetterRenderer.PropLayout2, LayoutVector(_previousSlot.ToLayout()));
                _cakeBlock.SetVector(SplatLetterRenderer.PropSlot2, new Vector4(prevFrame, 1f, 0f, 1f));

                float t = _glazeProgress;
                float easeOut = 1f - Mathf.Pow(1f - t, 3f);
                float radius = easeOut * (_cakeRadius * 2.2f);

                _cakeBlock.SetVector(SplatLetterRenderer.PropApply, new Vector4(_glazeOriginOS.x, _glazeOriginOS.y, _glazeOriginOS.z, radius));
                _cakeBlock.SetVector(SplatLetterRenderer.PropApplyShape, new Vector4(0.045f, 0.005f, 0.35f, 0f));
            }
            else
            {
                _cakeBlock.SetVector(SplatLetterRenderer.PropSlot2, Vector4.zero);
                _cakeBlock.SetVector(SplatLetterRenderer.PropApply, SplatSurfaceState.NoApply);
            }

            _cakeRenderer.SetPropertyBlock(_cakeBlock);
        }

        public void ClearAllIcing()
        {
            for (int i = 0; i < _chunks.Count; i++)
            {
                _chunks[i].Clear();
            }
            _activeChunkIndex = 0;
        }

        void OnDestroy()
        {
            if (_cakeFilter != null && _cakeFilter.sharedMesh != null)
            {
                DestroyImmediate(_cakeFilter.sharedMesh);
            }
        }
    }
}
