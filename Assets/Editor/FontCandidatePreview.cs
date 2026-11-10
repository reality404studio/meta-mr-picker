// 사용: Unity -batchmode -projectPath <proj> -buildTarget Android -executeMethod FontCandidatePreview.RenderBatch -fontOut <dir>
using System;
using System.IO;
using MoodPicker.Splatter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class FontCandidatePreview
{
    const int W = 560;
    const int H = 640;
    const int SplatCount = 30000;
    const string Dir = "Assets/Models/FontCandidates";

    static readonly string[] Keys =
    {
        "Unbounded",
        "Fraunces",
        "DMSerifDisplay",
        "PlayfairDisplay",
        "InstrumentSerif",
        "BodoniModa",
        "CormorantGaramond"
    };

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
        try
        {
            code = Run(Arg("-fontOut")) ? 0 : 1;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            code = 1;
        }
        Debug.Log("[MP] font-preview " + (code == 0 ? "PASS" : "FAIL"));
        EditorApplication.Exit(code);
    }

    static string Arg(string name)
    {
        string[] a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return ".harness/state/font-preview";
    }

    public static bool Run(string outDir)
    {
        MoodPickerUrpSetup.Ensure();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Material splatMat = SplatterSceneSetup.EnsureSplatMaterial();
        SplatPigmentSlotAsset[] presets = SplatterSceneSetup.EnsureRealVideoSlotAssets();
        string fullDir = Path.GetFullPath(outDir);
        Directory.CreateDirectory(fullDir);

        var camGo = new GameObject("FontPreviewCam");
        var cam = camGo.AddComponent<Camera>();
        cam.transform.position = new Vector3(0f, 0f, -0.60f);
        cam.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        cam.fieldOfView = 28f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 5.0f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.09f, 0.13f, 1f);
        camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;

        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rt.antiAliasing = 4;
        rt.Create();
        cam.targetTexture = rt;
        cam.aspect = W / (float)H;

        bool ok = true;
        foreach (string key in Keys)
        {
            string glb = Dir + "/A_" + key + ".glb";
            GameObject word = SplatterSceneSetup.CreateSingleGlyphSplat(glb, "Font_" + key, splatMat, presets, SplatCount, SplatShaderMode.Raw, 0);
            word.transform.position = Vector3.zero;
            var ctrl = word.GetComponent<SplatWordController>();

            ctrl.StepAnimationAndMaterials(0.35f);
            ok &= Capture(cam, rt, Path.Combine(fullDir, key + "_rest.png"));

            ctrl.TriggerSampleSplash(
                new Vector3(0.025f, 0.015f, -0.004f),
                new Vector3(0.025f, 0.015f, -0.02f),
                new Vector3(0.55f, 0.35f, -0.20f),
                0.35f);
            ctrl.StepAnimationAndMaterials(0.35f);
            ok &= Capture(cam, rt, Path.Combine(fullDir, key + "_touch.png"));

            UnityEngine.Object.DestroyImmediate(word);
        }

        EditorSceneManager.OpenScene(SplatterSceneSetup.ScenePath, OpenSceneMode.Single);
        return ok;
    }

    static bool Capture(Camera cam, RenderTexture rt, string path)
    {
        var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
        if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
        else cam.Render();

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var img = new Texture2D(W, H, TextureFormat.RGBA32, false, false);
        img.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        img.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, img.EncodeToPNG());

        int nonBg = 0;
        Color bg = cam.backgroundColor;
        foreach (Color c in img.GetPixels())
        {
            if (Mathf.Abs(c.r - bg.r) + Mathf.Abs(c.g - bg.g) + Mathf.Abs(c.b - bg.b) > 0.06f) nonBg++;
        }
        UnityEngine.Object.DestroyImmediate(img);
        Debug.Log($"[MP] font-preview {Path.GetFileName(path)} nonBgPx={nonBg}");
        return nonBg > 2000;
    }
}
