// 사용: Unity -batchmode -projectPath <proj> -buildTarget Android -executeMethod PunchCueRender.RenderBatch -cueOut <out.png>
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

public static class PunchCueRender
{
    const string LineMat = "Assets/Materials/FirstLight/Catch_Line.mat";
    const string DotMat = "Assets/Materials/FirstLight/Catch_Dot.mat";
    const int W = 960, H = 1080;
    const float Dt = 1f / 72f;
    static readonly Pose Head = new Pose(new Vector3(0f, 1.2f, 0f), Quaternion.identity);
    static readonly Color White = new Color(0xF0 / 255f, 0xEE / 255f, 0xE6 / 255f, 1f);
    static readonly Color Room = new Color(0.42f, 0.43f, 0.45f, 1f);
    static readonly Color Red = new Color(0xD2 / 255f, 0x3A / 255f, 0x2C / 255f, 1f);

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
        try { code = Run(Arg("-cueOut")) ? 0 : 1; }
        catch (Exception e) { Debug.LogException(e); code = 1; }
        Debug.Log("[MP] cue-render " + (code == 0 ? "PASS" : "FAIL"));
        EditorApplication.Exit(code);
    }

    static string Arg(string name)
    {
        string[] a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        throw new ArgumentException("missing " + name);
    }

    static Vector3 InFront(float x, float y, float z) => Head.position + Head.rotation * new Vector3(x, y, z);

    static HandPose Hand(Vector3 p, bool pinch)
    {
        float g = pinch ? 0.005f : 0.06f;
        return new HandPose
        {
            tracked = true,
            thumbTip = p + new Vector3(0f, -g * 0.5f, 0f),
            indexTip = p + new Vector3(0f, g * 0.5f, 0f),
            middleTip = p + new Vector3(0f, 0.03f, 0.02f),
            ringTip = p + new Vector3(0f, 0.03f, 0.01f),
            palm = p + new Vector3(0f, -0.02f, -0.05f),
            palmNormal = Vector3.down,
        };
    }

    struct Panel { public string name; public CatchPunch punch; public HandFrame last; }

    static Panel Simulate(string name, CatchSettings s, Func<float, (HandPose, HandPose)> hands, float seconds)
    {
        var punch = new CatchPunch(s);
        var f = new HandFrame();
        for (float t = 0f; t < seconds; t += Dt)
        {
            var (l, r) = hands(t);
            f = new HandFrame { t = t, left = l, right = r };
            punch.Update(f, Head);
        }
        return new Panel { name = name, punch = punch, last = f };
    }

    public static bool Run(string outPng)
    {
        var fails = new List<string>();
        var s = CatchSettings.Defaults();
        var line = AssetDatabase.LoadAssetAtPath<Material>(LineMat);
        var dot = AssetDatabase.LoadAssetAtPath<Material>(DotMat);
        if (line == null || dot == null) throw new IOException("material missing " + LineMat + " / " + DotMat);

        Vector3 L = InFront(-0.15f, -0.25f, 0.32f), R = InFront(0.15f, -0.25f, 0.32f);
        Vector3 drag = new Vector3(0.06f, 0.04f, 0f);
        float holdAt = 1.8f;
        var panels = new[]
        {
            Simulate("idle", s, _ => (Hand(L, false), Hand(R, false)), 0.5f),
            Simulate("drag", s, t => (Hand(L + drag * Mathf.Clamp01((t - 0.3f) / 0.5f), t > 0.2f), Hand(R, false)), 1.0f),
            Simulate("hold", s, t => (Hand(L, false), Hand(R, t > 0.3f)), 0.3f + 0.05f + holdAt + Dt),
        };

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        var camGo = new GameObject("CueCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.transform.SetPositionAndRotation(Head.position, Quaternion.Euler(22f, 0f, 0f));
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.02f;
        cam.allowMSAA = false;
        cam.allowHDR = false;
        camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;

        var sheet = new Texture2D(W * panels.Length, H, TextureFormat.RGBA32, false, false);
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
        rt.Create();
        cam.targetTexture = rt;

        for (int i = 0; i < panels.Length; i++)
        {
            Panel p = panels[i];
            var root = new GameObject("Cue" + p.name).transform;
            var view = new PunchCueView(root, line, dot, s);
            PunchCueState st = PunchCue.Compute(p.punch, p.last, Head, s);
            view.Draw(st, p.punch.Hole, Matrix4x4.identity, cam.transform.position);
            Debug.Log($"[MP] cue-render {p.name} leader={st.leader} lever={st.lever} blade={st.blade} progress={st.bladeProgress:0.000} angle={st.leverPose.angleDegrees:0.0} offset=({p.punch.Offset.x * 100f:0.0},{p.punch.Offset.y * 100f:0.0})cm white={view.WhiteSegments} red={view.RedSegments}");

            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var img = new Texture2D(W, H, TextureFormat.RGBA32, false, false);
            img.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            img.Apply();
            RenderTexture.active = prev;
            Color[] shown = img.GetPixels();
            for (int k = 0; k < shown.Length; k++) shown[k] = Color.Lerp(Room, new Color(shown[k].r, shown[k].g, shown[k].b, 1f), shown[k].a);
            sheet.SetPixels(i * W, 0, W, H, shown);

            int white = 0, navy = 0, red = 0, magenta = 0;
            foreach (Color c in img.GetPixels())
            {
                if (c.a < 0.99f) continue;
                if (Dist(c, White) < 0.04f) white++;
                else if (Dist(c, PunchCueView.Navy) < 0.04f) navy++;
                else if (Dist(c, Red) < 0.04f) red++;
                if (c.r > 0.9f && c.g < 0.1f && c.b > 0.9f) magenta++;
            }
            Debug.Log($"[MP] cue-render {p.name} px white={white} navy={navy} red={red} magenta={magenta} corner={img.GetPixel(1, 1).a:0.00}");
            if (white < 900) fails.Add($"{p.name}: white #F0EEE6 px {white}");
            if (navy < 400) fails.Add($"{p.name}: navy #15317C backing px {navy}");
            if (magenta > 0) fails.Add($"{p.name}: magenta {magenta}");
            if (img.GetPixel(1, 1).a > 0.01f) fails.Add($"{p.name}: background alpha not 0");
            bool hold = p.name == "hold";
            if (hold && red < 90) fails.Add($"hold: red fire progress px {red}");
            if (!hold && red > 0) fails.Add($"{p.name}: red outside fire progress {red}");
            if (p.name == "idle" && (st.leader != CueLine.Dotted || st.lever != CueLine.Dotted)) fails.Add("idle not dotted");
            if (p.name == "drag" && st.leader != CueLine.Solid) fails.Add("drag leader not solid");
            if (hold && (st.lever != CueLine.Solid || Mathf.Abs(st.bladeProgress - holdAt / s.holdSeconds) > 0.05f)) fails.Add($"hold progress {st.bladeProgress:0.000} != {holdAt / s.holdSeconds:0.000}");
            UnityEngine.Object.DestroyImmediate(img);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
        }

        sheet.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPng)));
        File.WriteAllBytes(outPng, sheet.EncodeToPNG());
        Debug.Log("[MP] cue-render out=" + Path.GetFullPath(outPng));
        foreach (string f in fails) Debug.LogError("[MP] cue-render FAIL " + f);
        return fails.Count == 0;
    }

    static float Dist(Color a, Color b) { return Mathf.Sqrt((a.r - b.r) * (a.r - b.r) + (a.g - b.g) * (a.g - b.g) + (a.b - b.b) * (a.b - b.b)); }
}
