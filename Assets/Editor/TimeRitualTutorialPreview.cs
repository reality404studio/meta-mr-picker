// 사용: Unity -batchmode -projectPath . -buildTarget Android -executeMethod TimeRitualTutorialPreview.RenderBatch -tutorialOut <폴더>   (실제 씬 위에 튜토리얼 카드를 단계마다 띄워 사용자 눈 자리에서 찍는다)
using System;
using System.IO;
using MoodPicker.Hands;
using MoodPicker.Ritual;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class TimeRitualTutorialPreview
{
    const float Dt = 1f / 72f;
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
            code = Run(Arg("-tutorialOut", "evidence/build-7d6a/cards")) ? 0 : 1;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            code = 1;
        }
        Debug.Log("[MP] tutorial-preview " + (code == 0 ? "PASS" : "FAIL"));
        EditorApplication.Exit(code);
    }

    static string Arg(string name, string fallback)
    {
        string[] a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return fallback;
    }

    public static bool Run(string dir)
    {
        dir = Path.GetFullPath(dir);
        Directory.CreateDirectory(dir);
        foreach (string old in Directory.GetFiles(dir, "*.png")) File.Delete(old);

        TimeRitualSceneSetup.Run();
        var ritual = UnityEngine.Object.FindFirstObjectByType<TimeRitual>();
        if (ritual == null) throw new InvalidOperationException("TimeRitual missing");
        ritual.Init();
        ritual.PlaceAt(new Vector3(0f, 1.0f, 0.42f), Quaternion.identity);
        ritual.SetTutorial(false);
        float now = 0f;
        HandFrame frame = default;
        for (int i = 0; i < 60; i++) ritual.Tick(now += Dt, Dt, frame);

        Vector3 jar = ritual.Jar.root.position;
        var desk = GameObject.CreatePrimitive(PrimitiveType.Quad);
        desk.transform.position = new Vector3(0f, jar.y - 0.0005f, 0.42f);
        desk.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        desk.transform.localScale = new Vector3(1.4f, 0.9f, 1f);
        var deskMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        deskMat.SetColor("_BaseColor", new Color(0.085f, 0.08f, 0.078f, 1f));
        desk.GetComponent<Renderer>().sharedMaterial = deskMat;

        var camGo = new GameObject("TutorialPreviewCam");
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.02f;
        cam.farClipPlane = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.045f, 0.047f, 0.06f, 1f);
        camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
        var rt = new RenderTexture(1200, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        rt.Create();
        cam.targetTexture = rt;
        cam.aspect = 1200f / 900f;

        Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Fraunces/Fraunces_72pt-Black.ttf");
        Material frost = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ritual/RitualFrost.mat");
        TimeRitualConfig cfg = UnityEngine.Object.Instantiate(ritual.Config);
        cfg.tutorial = true;
        var tut = new RitualTutorial(ritual.transform, cfg, font, frost);
        Vector3 eye = new Vector3(0f, 1.17f, 0.02f);
        Vector3 anchor = ritual.transform.TransformPoint(cfg.tutorialOffset);
        var right = new RitualHand { tracked = true, thumb = new Vector3(0.12f, 0.95f, 0.3f), index = new Vector3(0.13f, 0.95f, 0.31f) };

        var s = new TutorialState();
        float idle = 0f;
        int shot = 0;
        void Advance(float seconds)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds / Dt));
            for (int i = 0; i < n; i++)
            {
                ritual.Tick(now += Dt, Dt, frame);
                tut.Tick(Dt, s, idle, anchor, ritual.Jar.BodyCenter, eye, right);
            }
        }
        void Shoot(string name, bool wide)
        {
            cam.transform.position = eye;
            Vector3 at = wide ? new Vector3(0.05f, 0.86f, 0.42f) : tut.Root.position;
            cam.transform.rotation = Quaternion.LookRotation(at - eye, Vector3.up);
            cam.fieldOfView = wide ? 62f : 24f;
            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var img = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, false);
            img.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            img.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(dir, $"{shot++:00}-{name}.png"), img.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(img);
            Debug.Log($"[MP] tutorial-preview shot {name} card={tut.Card} lines={tut.MainLines} h={tut.Height:0.000} widest={tut.WidestLine:0.000}/{tut.Width:0.000} text=\"{tut.MainText.Replace("\n", " / ")}\" foot=\"{tut.FootText.Replace("\n", " / ")}\"");
        }
        void Card(string name, bool wide = false)
        {
            Advance(1.2f);
            Shoot(name, false);
            if (wide) Shoot(name + "-wide", true);
        }

        s.clockShowing = true;
        Card("clock", true);
        s.clockShowing = false;
        s.springsReady = true;
        Card("pull");
        s.pulling = true;
        Card("pulling");
        s.pulling = false;
        s.pieces = 1;
        Card("pull-more");
        s.pieces = 2;
        s.pairOfKind = true;
        Card("merge");
        s.merged = true;
        s.pairOfKind = false;
        s.pieces = 1;
        idle = cfg.tutorialPourIdle * 2f + 0.1f;
        Card("pour", true);
        idle = 0f;
        s.poured = 1;
        s.pieces = 0;
        Card("pour-more");
        idle = 1.2f;
        Card("lid");
        idle = 0f;
        s.lidClosed = true;
        s.waxReady = true;
        Card("wax");
        idle = cfg.tutorialPulseIdle + 0.8f;
        Advance(0.8f);
        Shoot("wax-idle-pulse", false);
        idle = 0f;
        s.vesselSealed = true;
        s.sealAge = cfg.tutorialShelfDelay + 0.1f;
        Card("shelf");
        s.shelved = true;
        Card("look");
        s.endOrbReady = true;
        Card("end");
        tut.Dispose();
        UnityEngine.Object.DestroyImmediate(cfg);
        return shot == 15;
    }
}
