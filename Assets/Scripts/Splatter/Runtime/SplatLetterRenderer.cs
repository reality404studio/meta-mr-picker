using System;
using MoodPicker.Splatter.Sim;
using Pigment;
using UnityEngine;
using UnityEngine.Rendering;

namespace MoodPicker.Splatter
{
    public struct SplatLook
    {
        public float flowAmplitude;
        public float flowSpeed;
        public float contrast;
        public float detachDistance;
        public float stretchSeconds;
        public float stretchMax;
        public float grain;
        public float glowGain;
        public float sparkle;

        public static SplatLook Default => new SplatLook
        {
            flowAmplitude = 0.0008f,
            flowSpeed = 1.6f,
            contrast = 1f,
            detachDistance = 0.012f,
            stretchSeconds = 0.008f,
            stretchMax = 2.5f,
            grain = 0.6f,
            glowGain = 0.35f,
            sparkle = 0.6f
        };
    }

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
        public static readonly int PropAtlasCrop = Shader.PropertyToID("_AtlasCrop");
        public static readonly int PropCropOn = Shader.PropertyToID("_CropOn");
        public static readonly int PropSimTex = Shader.PropertyToID("_SimTex");
        public static readonly int PropSimOn = Shader.PropertyToID("_SimOn");
        public static readonly int PropFlowParams = Shader.PropertyToID("_FlowParams");
        public static readonly int PropDropParams = Shader.PropertyToID("_DropParams");
        public static readonly int PropGlowParams = Shader.PropertyToID("_GlowParams");

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
        Vector3[] _homes;
        SplatSimRunner _sim;
        int _simClusterSize;
        SplatLook _look = SplatLook.Default;
        Vector4 _crop = new Vector4(1f, 1f, 0f, 0f);
        bool _cropOn;
        float _cropGamma = 1f;

        public int SplatCount => _splatCount;
        public SplatShaderMode Mode { get => shaderMode; set => shaderMode = value; }
        public int AssignedSlot { get => assignedSlot; set => assignedSlot = value; }
        public SplatSimRunner Sim => _sim;
        public float LetterSize => letterSize;

        public SplatSimRunner EnsureSim(int particlesPerCluster)
        {
            if (_cloudMesh == null && sourceMesh != null) Rebuild();
            if (_homes == null || _homes.Length == 0) return null;
            if (_sim != null && _simClusterSize == particlesPerCluster) return _sim;
            SplatSimMode mode = _sim != null ? _sim.Mode : SplatSimMode.Formed;
            _sim?.Dispose();
            _sim = new SplatSimRunner(_homes, particlesPerCluster);
            _simClusterSize = particlesPerCluster;
            if (mode == SplatSimMode.Hidden) _sim.Hide();
            return _sim;
        }

        public void SetLook(SplatLook look)
        {
            _look = look;
        }

        public void SetCrop(bool on, Vector4 crop, float gamma)
        {
            _cropOn = on;
            _crop = crop;
            _cropGamma = gamma;
        }

        void Awake()
        {
            if (_cloudMesh == null && sourceMesh != null) Rebuild();
        }

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

            _homes = new Vector3[samples.Length];
            var simUV = new Vector2[vCount];
            for (int i = 0; i < samples.Length; i++)
            {
                _homes[i] = new Vector3(samples[i].CenterX, samples[i].CenterY, samples[i].CenterZ);
                Vector2 texel = SplatSimRunner.TexelFor(i);
                for (int c = 0; c < 4; c++) simUV[i * 4 + c] = texel;
            }
            _cloudMesh.SetUVs(2, simUV);
            _sim?.Dispose();
            _sim = null;
            _cloudMesh.SetTriangles(data.Indices, 0, true);
            _cloudMesh.bounds = new Bounds(_cloudMesh.bounds.center, _cloudMesh.bounds.size + Vector3.one * 4f);

            _filter.sharedMesh = _cloudMesh;
            if (splatMaterial != null) _renderer.sharedMaterial = splatMaterial;
        }

        public void ApplyPigmentState(
            Texture atlas,
            FlipbookLayout layout,
            float currentFrame,
            bool hasSlot,
            TwoTonePalette palette,
            float scale,
            float intensity,
            bool outlineEnabled)
        {
            EnsureComponents();
            if (_cloudMesh == null && sourceMesh != null) Rebuild();
            _block.Clear();
            if (atlas != null) _block.SetTexture(PropAtlas, atlas);
            float halfTexel = layout.Tile > 0 ? 0.5f / layout.Tile : 0.5f / 256f;
            float spatialScale = scale / Mathf.Max(0.01f, letterSize);
            _block.SetVector(PropLayout, new Vector4(Mathf.Max(1, layout.Cols), Mathf.Max(1, layout.Rows), halfTexel, Mathf.Max(1, layout.Frames)));
            _block.SetFloat(PropFrame, currentFrame);
            _block.SetFloat(PropHasSlot, hasSlot && atlas != null ? 1f : 0f);
            _block.SetFloat(PropMode, (float)shaderMode);
            _block.SetFloat(PropScale, spatialScale);
            _block.SetFloat(PropIntensity, intensity);
            _block.SetColor(PropLightTone, new Color(palette.LightR, palette.LightG, palette.LightB, 1f));
            _block.SetColor(PropShadowTone, new Color(palette.ShadowR, palette.ShadowG, palette.ShadowB, 1f));
            _block.SetFloat(PropThreshold, palette.Threshold);
            _block.SetFloat(PropOutline, outlineEnabled ? 1f : 0f);
            bool simOn = _sim != null && _sim.DrivesShader;
            if (simOn) _block.SetTexture(PropSimTex, _sim.Texture);
            _block.SetFloat(PropSimOn, simOn ? 1f : 0f);
            _block.SetVector(PropFlowParams, new Vector4(_look.flowAmplitude, _look.flowSpeed, _look.contrast, _cropGamma));
            _block.SetVector(PropAtlasCrop, _crop);
            _block.SetFloat(PropCropOn, _cropOn ? 1f : 0f);
            _block.SetVector(PropDropParams, new Vector4(Mathf.Max(1e-4f, _look.detachDistance), _look.stretchSeconds, _look.stretchMax, _look.grain));
            _block.SetVector(PropGlowParams, new Vector4(_look.glowGain, _look.sparkle, 0f, 0f));
            _renderer.SetPropertyBlock(_block);
            bool hidden = _sim != null && _sim.Mode == SplatSimMode.Hidden;
            if (_renderer.enabled == hidden) _renderer.enabled = !hidden;
        }

        void EnsureComponents()
        {
            if (_filter == null) _filter = GetComponent<MeshFilter>();
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            if (_block == null) _block = new MaterialPropertyBlock();
        }

        void OnDestroy()
        {
            _sim?.Dispose();
            _sim = null;
            if (_cloudMesh != null)
            {
                if (Application.isPlaying) Destroy(_cloudMesh);
                else DestroyImmediate(_cloudMesh);
            }
        }
    }
}
