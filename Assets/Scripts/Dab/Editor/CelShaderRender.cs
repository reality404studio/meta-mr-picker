// 사용: Unity -batchmode -projectPath <proj> -buildTarget Android -executeMethod MoodPicker.Dab.CelShaderRender.RenderBatch -celPaint <paint.json> -celTex <png> -celOut <out.png>
using System;
using System.Collections.Generic;
using System.IO;
using MoodPicker.Catch;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MoodPicker.Dab
{
    public static class CelShaderRender
    {
        public const string ShaderPath = "Assets/Shaders/Cel/CelPaint.shader";
        const int W = 1024, H = 768;
        const int Patch = 10;

        static double _start;

        public static void RenderBatch()
        {
            _start = EditorApplication.timeSinceStartup;
            EditorApplication.update += Wait;
        }

        static void Wait()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.timeSinceStartup - _start < 3) return;
            EditorApplication.update -= Wait;
            int code;
            try { code = Run(Arg("-celPaint"), Arg("-celTex"), Arg("-celOut")) ? 0 : 1; }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            Debug.Log("[MP] cel-render " + (code == 0 ? "PASS" : "FAIL"));
            EditorApplication.Exit(code);
        }

        static string Arg(string name)
        {
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            throw new ArgumentException("missing " + name);
        }

        public static bool Run(string paintJson, string texPath, string outPng)
        {
            var fails = new List<string>();
            PaintRecord paint = PaintStore.FromJson(File.ReadAllText(paintJson));

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null) throw new IOException("shader missing " + ShaderPath);
            if (!shader.isSupported) fails.Add("shader not supported on " + SystemInfo.graphicsDeviceType);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var caught = new Texture2D(2, 2, TextureFormat.RGBA32, true, false) { hideFlags = HideFlags.HideAndDontSave };
            if (!caught.LoadImage(File.ReadAllBytes(texPath))) throw new IOException("bad png " + texPath);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;

            var mat = new Material(shader);
            CelPaint.Apply(mat, paint, caught);
            Vector3 l = CelPaint.LightDirOf(paint);
            Color lightTone = mat.GetColor(CelPaint.LightTone);
            Color shadowTone = mat.GetColor(CelPaint.ShadowTone);

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.position = Vector3.zero;
            var bottle = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            foreach (var go in new[] { sphere, bottle, box }) go.GetComponent<Renderer>().sharedMaterial = mat;

            Vector3 side = Vector3.Cross(l, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(l, Vector3.forward);
            Vector3 v = side.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, -v).normalized;
            bottle.transform.position = right * 1.4f;
            bottle.transform.localScale = new Vector3(0.6f, 0.7f, 0.6f);
            box.transform.position = -right * 1.4f;
            box.transform.rotation = Quaternion.Euler(25f, 35f, 10f);
            box.transform.localScale = Vector3.one * 0.8f;

            var camGo = new GameObject("CelRenderCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.transform.position = v * 4.2f;
            cam.transform.LookAt(Vector3.zero, Vector3.up);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 45f;
            cam.allowMSAA = false;
            cam.allowHDR = false;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = false;

            int lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length;
            if (lights != 0) fails.Add("lights in scene: " + lights);
            var rp = GraphicsSettings.currentRenderPipeline;
            if (!(rp is UniversalRenderPipelineAsset)) fails.Add("render pipeline is not URP: " + (rp == null ? "null" : rp.GetType().Name));

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            rt.Create();
            cam.targetTexture = rt;
            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var img = new Texture2D(W, H, TextureFormat.RGBA32, false, false);
            img.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            img.Apply();
            RenderTexture.active = prev;

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPng)));
            File.WriteAllBytes(outPng, img.EncodeToPNG());

            Color[] px = img.GetPixels();
            int covered = 0, magenta = 0, partial = 0;
            foreach (Color c in px)
            {
                if (c.a >= 0.99f) covered++;
                else if (c.a > 0.01f) partial++;
                if (c.r > 0.9f && c.g < 0.1f && c.b > 0.9f) magenta++;
            }
            float coverage = covered / (float)px.Length;
            Color corner = img.GetPixel(2, 2);

            Vector3 nLit = (v + l).normalized * 0.5f;
            Vector3 nShadow = (v - l).normalized * 0.5f;
            Stats lit = PatchStats(img, cam, nLit);
            Stats shadow = PatchStats(img, cam, nShadow);

            float litToLight = Dist(lit.mean, lightTone), litToShadow = Dist(lit.mean, shadowTone);
            float shToShadow = Dist(shadow.mean, shadowTone), shToLight = Dist(shadow.mean, lightTone);

            Debug.Log($"[MP] cel-render pipeline={(rp == null ? "null" : rp.name)} device={SystemInfo.graphicsDeviceType} lights={lights} lightDir={l} lightTone={CelExtract.Hex(lightTone)} shadowTone={CelExtract.Hex(shadowTone)}");
            Debug.Log($"[MP] cel-render coverage={coverage:F3} partialAlpha={partial} magenta={magenta} cornerAlpha={corner.a:F2}");
            Debug.Log($"[MP] cel-render lit={CelExtract.Hex(lit.mean)} lum={Lum(lit.mean):F3} std={lit.lumStd:F4} d(light)={litToLight:F3} d(shadow)={litToShadow:F3}");
            Debug.Log($"[MP] cel-render shadow={CelExtract.Hex(shadow.mean)} lum={Lum(shadow.mean):F3} std={shadow.lumStd:F4} d(shadow)={shToShadow:F3} d(light)={shToLight:F3}");
            Debug.Log("[MP] cel-render out=" + Path.GetFullPath(outPng));

            if (coverage < 0.05f) fails.Add($"object coverage with alpha 1 too small: {coverage:F3}");
            if (partial > 0) fails.Add("pixels with partial alpha: " + partial);
            if (corner.a > 0.01f) fails.Add("background alpha not 0");
            if (magenta > 0) fails.Add("magenta pixels: " + magenta);
            if (!lit.covered || !shadow.covered) fails.Add("lit/shadow patch not on object");
            if (!(litToLight < litToShadow)) fails.Add("lit side not closer to lightTone");
            if (!(shToShadow < shToLight)) fails.Add("shadow side not closer to shadowTone");
            if (Lum(lit.mean) - Lum(shadow.mean) < 0.25f * (Lum(lightTone) - Lum(shadowTone))) fails.Add("two tones not separated");
            if (lit.lumStd < 0.004f || shadow.lumStd < 0.004f) fails.Add("caught texture detail not visible (flat tone)");

            foreach (string f in fails) Debug.LogError("[MP] cel-render FAIL " + f);
            return fails.Count == 0;
        }

        struct Stats { public Color mean; public float lumStd; public bool covered; }

        static Stats PatchStats(Texture2D img, Camera cam, Vector3 world)
        {
            Vector3 s = cam.WorldToScreenPoint(world);
            int cx = Mathf.RoundToInt(s.x * img.width / cam.pixelWidth);
            int cy = Mathf.RoundToInt(s.y * img.height / cam.pixelHeight);
            var st = new Stats { covered = true };
            var lums = new List<float>();
            Color sum = Color.clear;
            for (int y = cy - Patch; y <= cy + Patch; y++)
                for (int x = cx - Patch; x <= cx + Patch; x++)
                {
                    Color c = img.GetPixel(Mathf.Clamp(x, 0, img.width - 1), Mathf.Clamp(y, 0, img.height - 1));
                    if (c.a < 0.99f) st.covered = false;
                    sum += c;
                    lums.Add(Lum(c));
                }
            st.mean = sum / lums.Count;
            float m = 0f; foreach (float f in lums) m += f; m /= lums.Count;
            float vs = 0f; foreach (float f in lums) vs += (f - m) * (f - m);
            st.lumStd = Mathf.Sqrt(vs / lums.Count);
            return st;
        }

        static float Lum(Color c) { return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b; }
        static float Dist(Color a, Color b) { return Mathf.Sqrt((a.r - b.r) * (a.r - b.r) + (a.g - b.g) * (a.g - b.g) + (a.b - b.b) * (a.b - b.b)); }
    }
}
