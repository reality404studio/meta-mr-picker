using System;
using Pigment;
using UnityEngine;
using UnityEngine.Rendering;

namespace MoodPicker.Splatter
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SplatLetterRenderer : MonoBehaviour
    {
        public static readonly int PropAtlas = Shader.PropertyToID("_Atlas");
        public static readonly int PropLayout = Shader.PropertyToID("_Layout");
        public static readonly int PropFrame = Shader.PropertyToID("_Frame");
        public static readonly int PropHasSlot = Shader.PropertyToID("_HasSlot");
        public static readonly int PropMode = Shader.PropertyToID("_Mode");
        public static readonly int PropScale = Shader.PropertyToID("_Scale");
        public static readonly int PropIntensity = Shader.PropertyToID("_Intensity");
        public static readonly int PropLightTone = Shader.PropertyToID("_LightTone");
        public static readonly int PropShadowTone = Shader.PropertyToID("_ShadowTone");
        public static readonly int PropThreshold = Shader.PropertyToID("_Threshold");
        public static readonly int PropOutline = Shader.PropertyToID("_Outline");

        [SerializeField] Mesh sourceMesh;
        [SerializeField] Material splatMaterial;
        [SerializeField] int splatsPerLetter = 8000;
        [SerializeField] float letterSize = 0.085f;
        [SerializeField] bool includeOutline = true;
        [SerializeField] SplatShaderMode shaderMode = SplatShaderMode.TwoToneCel;
        [SerializeField] int assignedSlot = 0;

        Mesh _cloudMesh;
        MeshFilter _filter;
        MeshRenderer _renderer;
        MaterialPropertyBlock _block;
        int _splatCount;

        public int SplatCount => _splatCount;
        public SplatShaderMode Mode { get => shaderMode; set => shaderMode = value; }
        public int AssignedSlot { get => assignedSlot; set => assignedSlot = value; }

        public void RebuildFromMesh(Mesh source, Material material, int surfaceSplatCount, float sizeMeters, bool withOutline, int seed = 1337)
        {
            sourceMesh = source;
            splatMaterial = material;
            splatsPerLetter = Mathf.Max(64, surfaceSplatCount);
            letterSize = Mathf.Max(0.01f, sizeMeters);
            includeOutline = withOutline;
            Rebuild(seed);
        }

        public void Rebuild(int seed = 1337)
        {
            if (sourceMesh == null) return;
            EnsureComponents();

            Vector3[] v3 = sourceMesh.vertices;
            Vector3[] n3 = sourceMesh.normals;
            int[] tris = sourceMesh.triangles;

            var posXYZ = new float[v3.Length * 3];
            for (int i = 0; i < v3.Length; i++)
            {
                posXYZ[i * 3] = v3[i].x;
                posXYZ[i * 3 + 1] = v3[i].y;
                posXYZ[i * 3 + 2] = v3[i].z;
            }

            float[] nrmXYZ = null;
            if (n3 != null && n3.Length == v3.Length)
            {
                nrmXYZ = new float[n3.Length * 3];
                for (int i = 0; i < n3.Length; i++)
                {
                    nrmXYZ[i * 3] = n3[i].x;
                    nrmXYZ[i * 3 + 1] = n3[i].y;
                    nrmXYZ[i * 3 + 2] = n3[i].z;
                }
            }

            SplatSample[] samples = MeshSplatSampler.SampleMesh(posXYZ, nrmXYZ, tris, splatsPerLetter, letterSize, includeOutline, seed);
            SplatCloudMeshData data = SplatCloudMeshBuilder.Build(samples);
            _splatCount = data.SplatCount;

            if (_cloudMesh == null)
            {
                _cloudMesh = new Mesh { name = gameObject.name + "_SplatCloud", indexFormat = IndexFormat.UInt32 };
            }
            else
            {
                _cloudMesh.Clear();
                _cloudMesh.indexFormat = IndexFormat.UInt32;
            }

            int vCount = data.VerticesXYZ.Length / 3;
            var verts = new Vector3[vCount];
            var norms = new Vector3[vCount];
            var uvs = new Vector2[vCount];
            var centers = new Vector4[vCount];

            for (int i = 0; i < vCount; i++)
            {
                verts[i] = new Vector3(data.VerticesXYZ[i * 3], data.VerticesXYZ[i * 3 + 1], data.VerticesXYZ[i * 3 + 2]);
                norms[i] = new Vector3(data.NormalsXYZ[i * 3], data.NormalsXYZ[i * 3 + 1], data.NormalsXYZ[i * 3 + 2]);
                uvs[i] = new Vector2(data.QuadUV[i * 2], data.QuadUV[i * 2 + 1]);
                centers[i] = new Vector4(data.CenterAndFlagsXYZW[i * 4], data.CenterAndFlagsXYZW[i * 4 + 1], data.CenterAndFlagsXYZW[i * 4 + 2], data.CenterAndFlagsXYZW[i * 4 + 3]);
            }

            _cloudMesh.vertices = verts;
            _cloudMesh.normals = norms;
            _cloudMesh.uv = uvs;
            _cloudMesh.SetUVs(1, centers);
            _cloudMesh.SetTriangles(data.Indices, 0, true);

            _filter.sharedMesh = _cloudMesh;
            if (splatMaterial != null) _renderer.sharedMaterial = splatMaterial;
        }

        public void ApplyPigmentState(
            Texture2D atlas,
            FlipbookLayout layout,
            int currentFrame,
            bool hasSlot,
            TwoTonePalette palette,
            float scale,
            float intensity,
            bool outlineEnabled)
        {
            EnsureComponents();
            _block.Clear();
            if (atlas != null) _block.SetTexture(PropAtlas, atlas);
            float halfTexel = layout.Tile > 0 ? 0.5f / layout.Tile : 0.5f / 256f;
            _block.SetVector(PropLayout, new Vector4(Mathf.Max(1, layout.Cols), Mathf.Max(1, layout.Rows), halfTexel, Mathf.Max(1, layout.Frames)));
            _block.SetFloat(PropFrame, currentFrame);
            _block.SetFloat(PropHasSlot, hasSlot && atlas != null ? 1f : 0f);
            _block.SetFloat(PropMode, (float)shaderMode);
            _block.SetFloat(PropScale, scale);
            _block.SetFloat(PropIntensity, intensity);
            _block.SetColor(PropLightTone, SrgbToLinear(palette.LightR, palette.LightG, palette.LightB));
            _block.SetColor(PropShadowTone, SrgbToLinear(palette.ShadowR, palette.ShadowG, palette.ShadowB));
            _block.SetFloat(PropThreshold, palette.Threshold);
            _block.SetFloat(PropOutline, outlineEnabled ? 1f : 0f);
            _renderer.SetPropertyBlock(_block);
        }

        static Color SrgbToLinear(float r, float g, float b)
        {
            return new Color(Mathf.Pow(Mathf.Max(0f, r), 2.2f), Mathf.Pow(Mathf.Max(0f, g), 2.2f), Mathf.Pow(Mathf.Max(0f, b), 2.2f), 1f);
        }

        void EnsureComponents()
        {
            if (_filter == null) _filter = GetComponent<MeshFilter>();
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            if (_block == null) _block = new MaterialPropertyBlock();
        }

        void OnDestroy()
        {
            if (_cloudMesh != null)
            {
                if (Application.isPlaying) Destroy(_cloudMesh);
                else DestroyImmediate(_cloudMesh);
            }
        }
    }
}
