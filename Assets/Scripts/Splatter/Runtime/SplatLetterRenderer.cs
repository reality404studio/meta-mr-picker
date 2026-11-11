using System.Threading.Tasks;
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
        public float overlay;
        public float glintAmount;
        public float glintSpeed;
        public float glintScale;
        public float glintRough;
        public float glintSharpness;
        public float glintSize;
        public float glintMaterial;

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
            sparkle = 0.6f,
            overlay = 1f,
            glintAmount = 0f,
            glintSpeed = 1.2f,
            glintScale = 120f,
            glintRough = 0.45f,
            glintSharpness = 160f,
            glintSize = 0.15f,
            glintMaterial = 0.6f
        };
    }

    public struct SplatSurfaceState
    {
        public SplatSurfaceMaterial current;
        public float currentFrame;
        public SplatSurfaceMaterial previous;
        public float previousFrame;
        public Vector4 apply;
        public Vector4 applyShape;
        public float time;
        public float triplanarScale;
        public float intensity;
        public bool outline;

        public static Vector4 NoApply => new Vector4(0f, 0f, 0f, 1000f);
    }

    public sealed class SplatBodyBuild
    {
        public Mesh source;
        public int splatCount;
        public Vector3[] vertices;
        public Vector3[] normals;
        public Vector2[] quadUV;
        public Vector4[] centers;
        public Vector2[] simUV;
        public int[] indices;
        public Vector3[] homes;
        public SplatSimPlan plan;
    }

    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SplatLetterRenderer : MonoBehaviour
    {
        public static readonly int PropAtlas = Shader.PropertyToID("_Atlas");
        public static readonly int PropLayout = Shader.PropertyToID("_Layout");
        public static readonly int PropFrame = Shader.PropertyToID("_Frame");
        public static readonly int PropHasSlot = Shader.PropertyToID("_HasSlot");
        public static readonly int PropAtlas2 = Shader.PropertyToID("_Atlas2");
        public static readonly int PropLayout2 = Shader.PropertyToID("_Layout2");
        public static readonly int PropSlot2 = Shader.PropertyToID("_Slot2");
        public static readonly int PropCrop2 = Shader.PropertyToID("_AtlasCrop2");
        public static readonly int PropCalm = Shader.PropertyToID("_Calm");
        public static readonly int PropSpark = Shader.PropertyToID("_Spark");
        public static readonly int PropCalm2 = Shader.PropertyToID("_Calm2");
        public static readonly int PropSpark2 = Shader.PropertyToID("_Spark2");
        public static readonly int PropSlot2b = Shader.PropertyToID("_Slot2b");
        public static readonly int PropApply = Shader.PropertyToID("_Apply");
        public static readonly int PropApplyShape = Shader.PropertyToID("_ApplyShape");
        public static readonly int PropSplatTime = Shader.PropertyToID("_SplatTime");
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
        public static readonly int PropGlintParams = Shader.PropertyToID("_GlintParams");
        public static readonly int PropGlintShape = Shader.PropertyToID("_GlintShape");

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

        public int SplatCount => _splatCount;
        public SplatShaderMode Mode { get => shaderMode; set => shaderMode = value; }
        public int AssignedSlot { get => assignedSlot; set => assignedSlot = value; }
        public SplatSimRunner Sim => _sim;
        public float LetterSize => letterSize;
        public Mesh SourceMesh => sourceMesh;

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
            ReadMesh(sourceMesh, out float[] pos, out float[] nrm, out int[] tris);
            SplatBodyBuild build = Compute(sourceMesh, pos, nrm, tris, splatsPerLetter, letterSize, includeOutline, seed, 0);
            ApplyBuild(build, false);
        }

        static void ReadMesh(Mesh mesh, out float[] positions, out float[] normals, out int[] triangles)
        {
            Vector3[] v3 = mesh.vertices;
            Vector3[] n3 = mesh.normals;
            triangles = mesh.triangles;
            positions = new float[v3.Length * 3];
            for (int i = 0; i < v3.Length; i++)
            {
                positions[i * 3] = v3[i].x;
                positions[i * 3 + 1] = v3[i].y;
                positions[i * 3 + 2] = v3[i].z;
            }
            normals = null;
            if (n3 != null && n3.Length == v3.Length)
            {
                normals = new float[n3.Length * 3];
                for (int i = 0; i < n3.Length; i++)
                {
                    normals[i * 3] = n3[i].x;
                    normals[i * 3 + 1] = n3[i].y;
                    normals[i * 3 + 2] = n3[i].z;
                }
            }
        }

        public static Task<SplatBodyBuild> BuildAsync(Mesh source, double splatsPerSquareMeter, int minCount, int maxCount, float sizeMeters, int seed, int particlesPerCluster)
        {
            ReadMesh(source, out float[] pos, out float[] nrm, out int[] tris);
            return Task.Run(() =>
            {
                int count = SplatDensity.CountFor(SplatDensity.MeshArea(pos, tris), splatsPerSquareMeter, minCount, maxCount);
                return Compute(source, pos, nrm, tris, count, sizeMeters, false, seed, particlesPerCluster);
            });
        }

        static SplatBodyBuild Compute(Mesh source, float[] pos, float[] nrm, int[] tris, int count, float size, bool outline, int seed, int particlesPerCluster)
        {
            SplatSample[] samples = MeshSplatSampler.SampleMesh(pos, nrm, tris, count, size, outline, seed);
            SplatCloudMeshData data = SplatCloudMeshBuilder.Build(samples);
            int vCount = data.VerticesXYZ.Length / 3;
            var b = new SplatBodyBuild
            {
                source = source,
                splatCount = data.SplatCount,
                vertices = new Vector3[vCount],
                normals = new Vector3[vCount],
                quadUV = new Vector2[vCount],
                centers = new Vector4[vCount],
                simUV = new Vector2[vCount],
                indices = data.Indices,
                homes = new Vector3[samples.Length]
            };
            for (int i = 0; i < vCount; i++)
            {
                b.vertices[i] = new Vector3(data.VerticesXYZ[i * 3], data.VerticesXYZ[i * 3 + 1], data.VerticesXYZ[i * 3 + 2]);
                b.normals[i] = new Vector3(data.NormalsXYZ[i * 3], data.NormalsXYZ[i * 3 + 1], data.NormalsXYZ[i * 3 + 2]);
                b.quadUV[i] = new Vector2(data.QuadUV[i * 2], data.QuadUV[i * 2 + 1]);
                b.centers[i] = new Vector4(data.CenterAndFlagsXYZW[i * 4], data.CenterAndFlagsXYZW[i * 4 + 1], data.CenterAndFlagsXYZW[i * 4 + 2], data.CenterAndFlagsXYZW[i * 4 + 3]);
            }
            for (int i = 0; i < samples.Length; i++)
            {
                b.homes[i] = new Vector3(samples[i].CenterX, samples[i].CenterY, samples[i].CenterZ);
                Vector2 texel = SplatSimRunner.TexelFor(i);
                for (int c = 0; c < 4; c++) b.simUV[i * 4 + c] = texel;
            }
            if (particlesPerCluster > 0) b.plan = SplatSimRunner.Plan(b.homes, particlesPerCluster);
            return b;
        }

        public void ApplyBuild(SplatBodyBuild b, bool startHidden)
        {
            EnsureComponents();
            sourceMesh = b.source;
            _splatCount = b.splatCount;
            if (_cloudMesh == null)
                _cloudMesh = new Mesh { name = gameObject.name + "_SplatCloud", indexFormat = IndexFormat.UInt32 };
            else
            {
                _cloudMesh.Clear();
                _cloudMesh.indexFormat = IndexFormat.UInt32;
            }
            _cloudMesh.SetVertices(b.vertices);
            _cloudMesh.SetNormals(b.normals);
            _cloudMesh.SetUVs(0, b.quadUV);
            _cloudMesh.SetUVs(1, b.centers);
            _cloudMesh.SetUVs(2, b.simUV);
            _cloudMesh.SetTriangles(b.indices, 0, true);
            _cloudMesh.bounds = new Bounds(_cloudMesh.bounds.center, _cloudMesh.bounds.size + Vector3.one * 4f);
            _homes = b.homes;
            _sim?.Dispose();
            _sim = null;
            if (b.plan != null)
            {
                _sim = new SplatSimRunner(b.plan);
                _simClusterSize = b.plan.perCluster;
                if (startHidden) _sim.Hide();
            }
            _filter.sharedMesh = _cloudMesh;
            if (splatMaterial != null) _renderer.sharedMaterial = splatMaterial;
        }

        static Vector4 LayoutVector(FlipbookLayout layout)
        {
            float halfTexel = layout.Tile > 0 ? 0.5f / layout.Tile : 0.5f / 256f;
            return new Vector4(Mathf.Max(1, layout.Cols), Mathf.Max(1, layout.Rows), halfTexel, Mathf.Max(1, layout.Frames));
        }

        public void ApplyState(in SplatSurfaceState s)
        {
            EnsureComponents();
            if (_cloudMesh == null && sourceMesh != null) Rebuild();
            _block.Clear();
            SplatSurfaceMaterial cur = s.current;
            bool hasCur = cur != null && cur.texture != null;
            if (hasCur)
            {
                _block.SetTexture(PropAtlas, cur.texture);
                _block.SetVector(PropLayout, LayoutVector(cur.layout));
                _block.SetVector(PropAtlasCrop, cur.crop);
                _block.SetColor(PropLightTone, new Color(cur.palette.LightR, cur.palette.LightG, cur.palette.LightB, 1f));
                _block.SetColor(PropShadowTone, new Color(cur.palette.ShadowR, cur.palette.ShadowG, cur.palette.ShadowB, 1f));
                _block.SetFloat(PropThreshold, cur.palette.Threshold);
                if (cur.calm != null) _block.SetTexture(PropCalm, cur.calm);
                if (cur.spark != null) _block.SetTexture(PropSpark, cur.spark);
            }
            _block.SetFloat(PropFrame, s.currentFrame);
            _block.SetFloat(PropHasSlot, hasCur ? 1f : 0f);
            _block.SetFloat(PropCropOn, hasCur && cur.mirrorTiled ? 1f : 0f);

            SplatSurfaceMaterial prev = s.previous;
            bool hasPrev = prev != null && prev.texture != null;
            if (hasPrev)
            {
                _block.SetTexture(PropAtlas2, prev.texture);
                _block.SetVector(PropLayout2, LayoutVector(prev.layout));
                _block.SetVector(PropCrop2, prev.crop);
                if (prev.calm != null) _block.SetTexture(PropCalm2, prev.calm);
                if (prev.spark != null) _block.SetTexture(PropSpark2, prev.spark);
            }
            _block.SetVector(PropSlot2b, new Vector4(hasPrev ? prev.ResolveMode(-1) : 0f, 0f, 0f, 0f));
            _block.SetVector(PropSlot2, new Vector4(s.previousFrame, hasPrev ? 1f : 0f, hasPrev && prev.mirrorTiled ? 1f : 0f, hasPrev ? prev.gamma : 1f));
            _block.SetVector(PropApply, hasPrev ? s.apply : SplatSurfaceState.NoApply);
            _block.SetVector(PropApplyShape, s.applyShape);
            _block.SetFloat(PropSplatTime, s.time);

            _block.SetFloat(PropMode, (float)shaderMode);
            _block.SetFloat(PropScale, s.triplanarScale / Mathf.Max(0.01f, letterSize));
            _block.SetFloat(PropIntensity, s.intensity);
            _block.SetFloat(PropOutline, s.outline ? 1f : 0f);
            bool simOn = _sim != null && _sim.DrivesShader;
            if (simOn) _block.SetTexture(PropSimTex, _sim.Texture);
            _block.SetFloat(PropSimOn, simOn ? 1f : 0f);
            _block.SetVector(PropFlowParams, new Vector4(_look.flowAmplitude, _look.flowSpeed, _look.contrast, hasCur ? cur.gamma : 1f));
            _block.SetVector(PropDropParams, new Vector4(Mathf.Max(1e-4f, _look.detachDistance), _look.stretchSeconds, _look.stretchMax, _look.grain));
            _block.SetVector(PropGlowParams, new Vector4(_look.glowGain, _look.sparkle, _look.overlay, 0f));
            _block.SetVector(PropGlintParams, new Vector4(_look.glintAmount, _look.glintSpeed, _look.glintScale, _look.glintRough));
            _block.SetVector(PropGlintShape, new Vector4(Mathf.Max(1f, _look.glintSharpness), _look.glintSize, _look.glintMaterial, 0f));
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
