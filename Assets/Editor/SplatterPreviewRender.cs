// 사용: Unity -batchmode -projectPath <proj> -buildTarget Android -executeMethod SplatterPreviewRender.RenderBatch -splatOut <out.png>
using System;
using System.IO;
using MoodPicker.Splatter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class SplatterPreviewRender
{
    const int W = 1280;
    const int H = 720;
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
            SplatterSceneSetup.Run();
            code = Run(Arg("-splatOut")) ? 0 : 1;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            code = 1;
        }
        Debug.Log("[MP] splatter-preview " + (code == 0 ? "PASS" : "FAIL"));
        EditorApplication.Exit(code);
    }

    static string Arg(string name)
    {
        string[] a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return ".harness/state/splatter-preview.png";
    }

    public static bool Run(string outPng)
    {
        MoodPickerUrpSetup.Ensure();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Material splatMat = SplatterSceneSetup.EnsureSplatMaterial();
        SplatPigmentSlotAsset[] presets = SplatterSceneSetup.EnsureRealVideoSlotAssets();

        GameObject leftA = SplatterSceneSetup.CreateSingleLetterASplat(
            "Preview_LetterA_Rest",
            splatMat,
            presets,
            28000,
            SplatShaderMode.Raw,
            0);
        leftA.transform.position = new Vector3(-0.13f, 0f, 0f);
        leftA.GetComponent<SplatWordController>().StepAnimationAndMaterials(0.35f);

        GameObject rightA = SplatterSceneSetup.CreateSingleLetterASplat(
            "Preview_LetterA_Splash",
            splatMat,
            presets,
            28000,
            SplatShaderMode.Raw,
            0);
        rightA.transform.position = new Vector3(0.13f, 0f, 0f);
        var rightCtrl = rightA.GetComponent<SplatWordController>();
        rightCtrl.TriggerSampleSplash(
            new Vector3(0.02f, 0.01f, -0.01f),
            new Vector3(0.15f, 0.01f, -0.02f),
            new Vector3(0.55f, 0.35f, -0.20f),
            0.35f);
        rightCtrl.StepAnimationAndMaterials(0.35f);

        var camGo = new GameObject("PreviewCam");
        var cam = camGo.AddComponent<Camera>();
        cam.transform.position = new Vector3(0f, 0f, -0.58f);
        cam.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        cam.orthographic = false;
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

        var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
        if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
        else cam.Render();

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var img = new Texture2D(W, H, TextureFormat.RGBA32, false, false);
        img.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        img.Apply();
        RenderTexture.active = prev;

        string fullOut = Path.GetFullPath(outPng);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOut));
        File.WriteAllBytes(fullOut, img.EncodeToPNG());

        int nonBg = 0;
        Color bg = cam.backgroundColor;
        foreach (Color c in img.GetPixels())
        {
            if (Mathf.Abs(c.r - bg.r) + Mathf.Abs(c.g - bg.g) + Mathf.Abs(c.b - bg.b) > 0.06f)
                nonBg++;
        }

        EditorSceneManager.OpenScene(SplatterSceneSetup.ScenePath, OpenSceneMode.Single);
        Debug.Log($"[MP] splatter-preview nonBgPx={nonBg} out={fullOut}");
        return nonBg > 5000;
    }
}
