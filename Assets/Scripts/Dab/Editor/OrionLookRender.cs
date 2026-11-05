using System;
using System.Collections.Generic;
using System.IO;
using MoodPicker.Catch;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MoodPicker.Dab
{
    public static class OrionLookRender
    {
        public const string ClayPath = "Assets/Materials/Orion/Clay.mat";
        public const int Width = 1200, Height = 400;
        const float Spacing = 1.3f;
        const float MinTextureStd = 0.01f;
        static double started;

        public static void RenderBatch()
        {
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Wait;
        }

        static void Wait()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup - started < 3) return;
            EditorApplication.update -= Wait;
            int code;
            try { code = Run(Arg("-orionSamples"), Arg("-orionOut")) ? 0 : 1; }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            Debug.Log("[MP] orion-look " + (code == 0 ? "PASS" : "FAIL"));
            EditorApplication.Exit(code);
        }

        static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            throw new ArgumentException("missing " + name);
        }

        public static Material NewPaintMaterial()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(CelShaderRender.ShaderPath);
            if (shader == null || !shader.isSupported || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("CelPaint unavailable or shader compilation failed");
            return new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        static Texture2D LoadTile(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            if (texture.LoadImage(File.ReadAllBytes(path))) return texture;
            Object.DestroyImmediate(texture);
            throw new IOException("invalid tile: " + path);
        }

        public static bool Run(string samples, string output)
        {
            Directory.CreateDirectory(output);
            var owned = new List<Object>();
            var failures = new List<string>();
            try
            {
                var yunseul = LoadTile(Path.Combine(samples, "yunseul/tile.png")); owned.Add(yunseul);
                var gradient = LoadTile(Path.Combine(samples, "gradient_sample/tile.png")); owned.Add(gradient);
                var water = NewPaintMaterial(); owned.Add(water);
                var colors = NewPaintMaterial(); owned.Add(colors);
                CelPaint.Apply(water, new PaintRecord(), yunseul, 0, false);
                CelPaint.Apply(colors, new PaintRecord(), gradient, 0, false);
                var clay = AssetDatabase.LoadAssetAtPath<Material>(ClayPath);
                if (clay == null || clay.shader != water.shader) throw new InvalidOperationException("Clay material shader link missing");
                Texture2D actual = RenderMaterials(new[] { water, colors, clay }); owned.Add(actual);
                File.WriteAllBytes(Path.Combine(output, "orion-look.png"), actual.EncodeToPNG());
                water.SetTexture(CelPaint.BaseMap, Texture2D.whiteTexture);
                colors.SetTexture(CelPaint.BaseMap, Texture2D.whiteTexture);
                Texture2D reference = RenderMaterials(new[] { water, colors, clay }); owned.Add(reference);
                File.WriteAllBytes(Path.Combine(output, "orion-look-white-reference.png"), reference.EncodeToPNG());
                Color32[] pixels = actual.GetPixels32();
                Color32[] baseline = reference.GetPixels32();
                int magenta = 0, partial = 0, backgroundBad = 0, interiorBad = 0;
                foreach (Color32 p in pixels)
                {
                    if (p.r > 230 && p.g < 26 && p.b > 230) magenta++;
                    if (p.a != 0 && p.a != 255) partial++;
                }
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                    {
                        float nearest = float.MaxValue;
                        for (int i = 0; i < 3; i++)
                        {
                            float cx = Width * 0.5f + (i - 1) * Spacing * Height / 1.5f;
                            float dx = x + 0.5f - cx, dy = y + 0.5f - Height * 0.5f;
                            nearest = Mathf.Min(nearest, Mathf.Sqrt(dx * dx + dy * dy));
                        }
                        byte alpha = pixels[y * Width + x].a;
                        float radius = Height / 3f;
                        if (nearest > radius + 2 && alpha != 0) backgroundBad++;
                        if (nearest < radius * 0.9f && alpha != 255) interiorBad++;
                    }
                Check(magenta == 0, "magenta=" + magenta, failures);
                Check(partial == 0 && backgroundBad == 0 && interiorBad == 0,
                    $"alpha partial={partial} backgroundBad={backgroundBad} objectBad={interiorBad}", failures);
                for (int i = 0; i < 3; i++)
                {
                    Stats stats = Measure(pixels, baseline, i);
                    Debug.Log($"[MP] orion-look sphere={i} pixels={stats.count} tones={stats.tones} meanRGB={stats.mean} textureStd={stats.textureStd:F5}");
                    Check(stats.count > 30000, "sphere coverage " + i, failures);
                    if (i < 2)
                    {
                        Check(stats.tones > 2, "more than two tones " + i, failures);
                        Check(stats.textureStd > MinTextureStd, "texture retained " + i, failures);
                    }
                    else
                    {
                        Check(Mathf.Abs(stats.mean.x - 231) <= 8 && Mathf.Abs(stats.mean.y - 231) <= 8 && Mathf.Abs(stats.mean.z - 231) <= 8,
                            "clay RGB #E7E7E7 +/-8", failures);
                    }
                }
                Check(!ShaderUtil.ShaderHasError(water.shader), "shader compiler errors=0", failures);
                foreach (string failure in failures) Debug.LogError("[MP] orion-look FAIL " + failure);
                return failures.Count == 0;
            }
            finally { foreach (Object item in owned) Object.DestroyImmediate(item); }
        }

        static void Check(bool condition, string message, List<string> failures)
        {
            if (!condition) failures.Add(message);
            else Debug.Log("[MP] orion-look PASS " + message);
        }

        struct Stats
        {
            public int count, tones;
            public Vector3 mean;
            public double textureStd;
        }

        static Stats Measure(Color32[] pixels, Color32[] reference, int panel)
        {
            var stats = new Stats();
            var tones = new HashSet<int>();
            double sum = 0, squares = 0;
            for (int y = 0; y < Height; y++)
                for (int x = panel * Width / 3; x < (panel + 1) * Width / 3; x++)
                {
                    int k = y * Width + x;
                    Color32 p = pixels[k];
                    if (p.a != 255) continue;
                    stats.count++;
                    stats.mean += new Vector3(p.r, p.g, p.b);
                    tones.Add((p.r / 8 << 10) | (p.g / 8 << 5) | p.b / 8);
                    double residual = Luminance(p) - Luminance(reference[k]);
                    sum += residual;
                    squares += residual * residual;
                }
            if (stats.count == 0) return stats;
            stats.mean /= stats.count;
            stats.tones = tones.Count;
            stats.textureStd = Math.Sqrt(Math.Max(0, squares / stats.count - Math.Pow(sum / stats.count, 2)));
            return stats;
        }

        static double Luminance(Color32 p) => (0.2126 * p.r + 0.7152 * p.g + 0.0722 * p.b) / 255;

        public static Texture2D RenderMaterials(Material[] materials)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("real graphics device required");
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) throw new InvalidOperationException("URP required");
            foreach (Material material in materials) material.hideFlags |= HideFlags.DontSave;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            RenderTexture previousTarget = RenderTexture.active;
            Texture2D image = null;
            try
            {
                RenderSettings.skybox = null;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = Color.black;
                RenderSettings.ambientIntensity = 0;
                RenderSettings.reflectionIntensity = 0;
                RenderSettings.fog = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphere.transform.position = Vector3.right * ((i - (materials.Length - 1) * 0.5f) * Spacing);
                    sphere.GetComponent<Renderer>().sharedMaterial = materials[i];
                }
                var camera = new GameObject("OrionLookCamera").AddComponent<Camera>();
                camera.scene = scene;
                camera.transform.position = new Vector3(0, 0, -4);
                camera.transform.rotation = Quaternion.identity;
                camera.orthographic = true;
                camera.orthographicSize = 0.75f;
                camera.aspect = Width / (float)Height;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.GetComponentsInChildren<Light>(true).Length != 0) throw new InvalidOperationException("lights must be zero");
                rt.Create();
                camera.targetTexture = rt;
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new InvalidOperationException("URP camera render request unsupported");
                RenderPipeline.SubmitRenderRequest(camera, request);
                camera.targetTexture = null;
                RenderTexture.active = rt;
                image = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false) { hideFlags = HideFlags.DontSave };
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                Debug.Log($"[MP] orion-look device={SystemInfo.graphicsDeviceType} colorSpace={QualitySettings.activeColorSpace} lights=0 ambient=black skybox=none");
                return image;
            }
            catch { if (image != null) Object.DestroyImmediate(image); throw; }
            finally
            {
                RenderTexture.active = previousTarget;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }
    }
}
