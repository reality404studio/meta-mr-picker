// 사용: Unity -batchmode -projectPath <proj> -buildTarget Android -executeMethod PunchLeverRender.RenderBatch -leverOut <out.png>
using System;
using System.Collections.Generic;
using System.IO;
using MoodPicker.Catch;
using MoodPicker.Hands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class PunchLeverRender
{
    const int W = 1200, H = 520;
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
        try { code = Run(Arg("-leverOut")) ? 0 : 1; }
        catch (Exception e) { Debug.LogException(e); code = 1; }
        Debug.Log("[MP] lever-render " + (code == 0 ? "PASS" : "FAIL"));
        EditorApplication.Exit(code);
    }

    static string Arg(string name)
    {
        string[] a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        throw new ArgumentException("missing " + name);
    }

    static HandPose Hand(Vector3 palm, float scale) => new HandPose
    {
        tracked = true,
        palm = palm,
        palmNormal = Vector3.down,
        indexTip = palm + new Vector3(-0.02f, 0.03f, 0.10f) * scale,
        thumbTip = palm + new Vector3(-0.04f, -0.01f, 0.055f) * scale,
        middleTip = palm + new Vector3(0f, 0.02f, 0.105f) * scale,
        ringTip = palm + new Vector3(0.015f, 0.015f, 0.095f) * scale,
    };

    public static bool Run(string outPng)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var line = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/FirstLight/Catch_Line.mat");
        var dot = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/FirstLight/Catch_Dot.mat");
        if (line == null || dot == null) throw new IOException("Catch_Line / Catch_Dot material missing");
        var s = ScriptableObject.CreateInstance<CatchSettings>();

        float[] scales = { 0.8f, 1f, 1.25f };
        float worst = 1f;
        for (int k = 0; k < scales.Length; k++)
        {
            var palm = new Vector3((k - 1) * 0.16f, 0f, 0f);
            HandPose h = Hand(palm, scales[k]);
            LeverPose lp = PunchCue.Lever(h, new Pose(palm + Vector3.up * 0.45f, Quaternion.LookRotation(Vector3.down, Vector3.forward)), 0.3f, s);
            worst = Mathf.Min(worst, PunchCue.Coverage(lp, h.thumbTip, h.indexTip));
            var segs = new List<(Vector3, Vector3)>();
            PunchCue.Polygon(lp.plate, CueLine.Solid, s, segs);
            PunchCue.Segments(lp.hinge, lp.armTip, CueLine.Solid, s, segs);
            foreach (var (a, b) in segs) Seg(a, b, line, 0.0025f);
            Seg(h.palm, h.indexTip, dot, 0.004f);
            Seg(h.palm, h.thumbTip, dot, 0.004f);
            Dot(h.thumbTip, dot);
            Dot(h.indexTip, dot);
        }

        var camGo = new GameObject("LeverCam");
        var cam = camGo.AddComponent<Camera>();
        cam.transform.position = new Vector3(0f, 0.6f, 0.06f);
        cam.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        cam.orthographic = true;
        cam.orthographicSize = 0.11f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.09f, 0.12f, 1f);
        camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
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
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPng)));
        File.WriteAllBytes(outPng, img.EncodeToPNG());
        int lit = 0;
        foreach (Color c in img.GetPixels()) if (c.r > 0.8f && c.g > 0.8f) lit++;
        Debug.Log($"[MP] lever-render coverageMin={worst:P0} linePx={lit} out={Path.GetFullPath(outPng)}");
        return worst >= 0.9f && lit > 500;
    }

    static void Seg(Vector3 a, Vector3 b, Material m, float w)
    {
        var lr = new GameObject("seg").AddComponent<LineRenderer>();
        lr.sharedMaterial = m;
        lr.useWorldSpace = true;
        lr.widthMultiplier = w;
        lr.positionCount = 2;
        lr.SetPositions(new[] { a, b });
        lr.alignment = LineAlignment.View;
    }

    static void Dot(Vector3 p, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.transform.position = p;
        go.transform.localScale = Vector3.one * 0.012f;
        go.GetComponent<Renderer>().sharedMaterial = m;
    }
}
