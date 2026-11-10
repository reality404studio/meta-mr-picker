// 사용: Unity -batchmode -projectPath <proj> -buildTarget Android -executeMethod SplatterPreviewRender.RenderBatch -splatOut <out.png>   (시간 흐름 시트는 RenderSequenceBatch)
using System;
using System.IO;
using MoodPicker.Splatter;
using MoodPicker.Splatter.Sim;
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
            code = (_sequence ? RunSequence(Arg("-splatOut")) : Run(Arg("-splatOut"))) ? 0 : 1;
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

    static bool _sequence;

    public static void RenderSequenceBatch()
    {
        _sequence = true;
        RenderBatch();
    }

    static Texture2D Shoot(Camera cam, RenderTexture rt)
    {
        var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
        if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
        else cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var img = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, false);
        img.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        img.Apply();
        RenderTexture.active = prev;
        return img;
    }

    static void Advance(SplatWordController ctrl, float seconds, ref float clock)
    {
        const float dt = 1f / 72f;
        int steps = Mathf.Max(1, Mathf.RoundToInt(seconds / dt));
        for (int i = 0; i < steps; i++)
        {
            ctrl.SetColliders(null, 0);
            ctrl.StepSimulation(dt);
        }
        clock += seconds;
        ctrl.StepAnimationAndMaterials(clock);
    }

    public static bool RunSequence(string outPng)
    {
        const int cell = 420;
        const int cols = 6;
        const int rows = 3;
        MoodPickerUrpSetup.Ensure();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Material splatMat = SplatterSceneSetup.EnsureSplatMaterial();
        SplatPigmentSlotAsset[] presets = SplatterSceneSetup.EnsureRealVideoSlotAssets();
        GameObject word = SplatterSceneSetup.CreateLetterUpperASplat("Sequence_UpperA", splatMat, presets, 30000, SplatShaderMode.Raw, 0);
        word.transform.position = Vector3.zero;
        var ctrl = word.GetComponent<SplatWordController>();

        var camGo = new GameObject("SequenceCam");
        var cam = camGo.AddComponent<Camera>();
        cam.transform.position = new Vector3(0f, -0.04f, -0.62f);
        cam.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        cam.fieldOfView = 34f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.09f, 0.13f, 1f);
        camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
        var rt = new RenderTexture(cell, cell, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        rt.Create();
        cam.targetTexture = rt;
        cam.aspect = 1f;

        var sheet = new Texture2D(cell * cols, cell * rows, TextureFormat.RGBA32, false, false);
        float clock = 0f;
        int col = 0;
        int row = 0;
        void Put()
        {
            Texture2D img = Shoot(cam, rt);
            sheet.SetPixels(col * cell, (rows - 1 - row) * cell, cell, cell, img.GetPixels());
            UnityEngine.Object.DestroyImmediate(img);
            col++;
        }

        Vector3 from = new Vector3(-0.11f, -0.02f, -0.03f);
        Vector3 to = new Vector3(0.11f, 0.03f, 0.02f);
        Vector3 mid = Vector3.Lerp(from, to, 0.5f);
        PokeWithFinger(ctrl, from, mid, 0.14f);
        ctrl.StepAnimationAndMaterials(0.14f);
        Put();
        PokeWithFinger(ctrl, mid, to, 0.14f);
        ctrl.StepAnimationAndMaterials(0.28f);
        Put();
        foreach (float wait in new[] { 0.15f, 0.25f, 0.5f, 1.6f })
        {
            Advance(ctrl, wait, ref clock);
            Put();
        }

        row = 1;
        col = 0;
        ctrl.BeginSummon(new Vector3(-0.14f, -0.13f, -0.22f));
        foreach (float wait in new[] { 0.25f, 0.3f, 0.4f, 0.5f, 0.7f, 1.4f })
        {
            Advance(ctrl, wait, ref clock);
            Put();
        }

        row = 2;
        col = 0;
        ctrl.BeginDissolve();
        foreach (float wait in new[] { 0.15f, 0.25f, 0.25f, 0.3f, 0.4f, 0.6f })
        {
            Advance(ctrl, wait, ref clock);
            Put();
        }

        sheet.Apply();
        string fullOut = Path.GetFullPath(outPng);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOut));
        File.WriteAllBytes(fullOut, sheet.EncodeToPNG());
        EditorSceneManager.OpenScene(SplatterSceneSetup.ScenePath, OpenSceneMode.Single);
        Debug.Log("[MP] splatter-sequence out=" + fullOut);
        return true;
    }

    public static void PokeWithFinger(SplatWordController ctrl, Vector3 from, Vector3 to, float seconds)
    {
        const float dt = 1f / 72f;
        int steps = Mathf.Max(1, Mathf.RoundToInt(seconds / dt));
        Vector3 velocity = (to - from) / seconds;
        var colliders = new SplatCollider[1];
        for (int i = 0; i < steps; i++)
        {
            colliders[0] = new SplatCollider
            {
                position = Vector3.Lerp(from, to, (i + 1f) / steps),
                velocity = velocity,
                radius = 0.022f,
                strength = 1f
            };
            ctrl.SetColliders(colliders, 1);
            ctrl.StepSimulation(dt);
        }
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
        PokeWithFinger(rightCtrl, new Vector3(0.05f, -0.03f, -0.05f), new Vector3(0.21f, 0.03f, 0.03f), 0.45f);
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
