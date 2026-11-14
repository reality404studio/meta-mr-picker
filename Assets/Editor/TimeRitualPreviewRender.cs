// 사용: Unity -batchmode -projectPath . -buildTarget Android -executeMethod TimeRitualPreviewRender.RenderBatch -ritualOut <폴더>   (가짜 손으로 뽑기·뭉치기·붓기·봉인을 돌려 장면별 PNG 와 sheet.png 를 남긴다)
using System;
using System.Collections.Generic;
using System.IO;
using MoodPicker.Hands;
using MoodPicker.Ritual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class TimeRitualPreviewRender
{
    const int Cell = 900;
    const float Dt = 1f / 72f;
    static double _start;
    static TimeRitual _ritual;
    static HandFrame _frame;
    static float _now;
    static string _dir;
    static Camera _cam;
    static RenderTexture _rt;
    static readonly List<Texture2D> Shots = new List<Texture2D>();
    static readonly Vector3[] Tip = new Vector3[2];
    static double _simMs;
    static int _simTicks;
    static double _worstMs;

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
            code = Run(Arg("-ritualOut", ".harness/state/ritual-preview")) ? 0 : 1;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            code = 1;
        }
        Debug.Log("[MP] ritual-preview " + (code == 0 ? "PASS" : "FAIL"));
        EditorApplication.Exit(code);
    }

    static string Arg(string name, string fallback)
    {
        string[] a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return fallback;
    }

    static HandPose Closed(Vector3 p)
    {
        Vector3 palm = p + new Vector3(0f, -0.02f, -0.08f);
        return new HandPose
        {
            tracked = true,
            thumbTip = p + new Vector3(-0.004f, 0f, 0f),
            indexTip = p + new Vector3(0.004f, 0f, 0f),
            middleTip = palm + new Vector3(0.01f, -0.012f, 0.03f),
            ringTip = palm + new Vector3(0f, -0.014f, 0.028f),
            littleTip = palm + new Vector3(-0.012f, -0.014f, 0.025f),
            palm = palm,
            palmNormal = Vector3.down
        };
    }

    static HandPose Open(Vector3 p)
    {
        Vector3 palm = p + new Vector3(0f, -0.02f, -0.08f);
        return new HandPose
        {
            tracked = true,
            thumbTip = p + new Vector3(-0.03f, 0f, -0.01f),
            indexTip = p + new Vector3(0.025f, 0f, 0f),
            middleTip = palm + new Vector3(0.012f, 0f, 0.095f),
            ringTip = palm + new Vector3(-0.008f, 0f, 0.09f),
            littleTip = palm + new Vector3(-0.028f, 0f, 0.08f),
            palm = palm,
            palmNormal = Vector3.down
        };
    }

    static void Hand(int h, Vector3 p, bool closed)
    {
        Tip[h] = p;
        HandPose pose = closed ? Closed(p) : Open(p);
        if (h == 0) _frame.left = pose;
        else _frame.right = pose;
    }

    static void Away(int h)
    {
        if (h == 0) _frame.left = default;
        else _frame.right = default;
    }

    static void Step(float seconds)
    {
        int steps = Mathf.Max(1, Mathf.RoundToInt(seconds / Dt));
        for (int i = 0; i < steps; i++)
        {
            _now += Dt;
            _frame.t = _now;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _ritual.Tick(_now, Dt, _frame);
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds;
            _simMs += ms;
            _simTicks++;
            if (ms > _worstMs) _worstMs = ms;
            if (ms > 4.0 && _simTicks > 5) Debug.Log($"[MP] ritual-preview slow tick {ms:0.0}ms t={_now:0.00} pieces={_ritual.Pieces.Count} pouring={_ritual.Jar.Pouring}");
        }
    }

    static void Move(int h, Vector3 to, float seconds, bool closed)
    {
        Vector3 from = Tip[h];
        int steps = Mathf.Max(1, Mathf.RoundToInt(seconds / Dt));
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Hand(h, Vector3.Lerp(from, to, t * t * (3f - 2f * t)), closed);
            Step(Dt);
        }
    }

    static void Look(Vector3 from, Vector3 at, float fov)
    {
        _cam.transform.position = from;
        _cam.transform.rotation = Quaternion.LookRotation(at - from, Vector3.up);
        _cam.fieldOfView = fov;
    }

    static void Shoot(string name)
    {
        var req = new UniversalRenderPipeline.SingleCameraRequest { destination = _rt };
        if (RenderPipeline.SupportsRenderRequest(_cam, req)) RenderPipeline.SubmitRenderRequest(_cam, req);
        else _cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = _rt;
        var img = new Texture2D(_rt.width, _rt.height, TextureFormat.RGBA32, false, false);
        img.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
        img.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(Path.Combine(_dir, $"{Shots.Count:00}-{name}.png"), img.EncodeToPNG());
        Shots.Add(img);
        Debug.Log($"[MP] ritual-preview shot {Shots.Count - 1:00} {name} t={_now:0.00} pieces={_ritual.Pieces.Count} jar={_ritual.Jar.TotalUnits}u grains={_ritual.Jar.TotalGrains} sealed={_ritual.Sealed}");
        if (Environment.GetEnvironmentVariable("RITUAL_DUMP_RENDERERS") == "1") DumpRenderers(name);
    }

    static void DumpRenderers(string shot)
    {
        var planes = GeometryUtility.CalculateFrustumPlanes(_cam);
        foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            Bounds b = r.bounds;
            if (!GeometryUtility.TestPlanesAABB(planes, b)) continue;
            string shader = r.sharedMaterial != null ? r.sharedMaterial.shader.name : "-";
            string parent = r.transform.parent != null ? r.transform.parent.name : "-";
            Debug.Log($"[MP] ritual-preview renderer {shot} {parent}/{r.name} {r.GetType().Name} shader={shader} center={b.center.x:0.000},{b.center.y:0.000},{b.center.z:0.000} size={b.size.x:0.000},{b.size.y:0.000},{b.size.z:0.000}");
        }
        if (Environment.GetEnvironmentVariable("RITUAL_ISOLATE_SHOT") != shot) return;
        int k = 0;
        foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            r.enabled = false;
            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = _rt };
            if (RenderPipeline.SupportsRenderRequest(_cam, req)) RenderPipeline.SubmitRenderRequest(_cam, req);
            else _cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var img = new Texture2D(_rt.width, _rt.height, TextureFormat.RGBA32, false, false);
            img.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
            RenderTexture.active = prev;
            string label = (r.transform.parent != null ? r.transform.parent.name + "_" : "") + r.name;
            File.WriteAllBytes(Path.Combine(_dir, $"isolate-{k++:00}-without-{label.Replace(' ', '_').Replace('/', '_')}.png"), img.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(img);
            r.enabled = true;
        }
        Renderer glass = _ritual.Jar.root.Find("Glass")?.GetComponent<Renderer>();
        if (glass == null) return;
        var keep = new MaterialPropertyBlock();
        glass.GetPropertyBlock(keep);
        var variants = new (string label, Action<MaterialPropertyBlock> set)[]
        {
            ("spec0", b => b.SetFloat("_Spec", 0f)),
            ("rim0", b => b.SetFloat("_Rim", 0f)),
            ("body0-inner0", b => { b.SetFloat("_Body", 0f); b.SetColor("_Inner", Color.clear); }),
            ("tint0-inner0", b => { b.SetColor("_Tint", Color.black); b.SetColor("_Inner", Color.clear); }),
            ("radius-tiny", b => b.SetFloat("_Radius", 0.0006f)),
        };
        foreach (var v in variants)
        {
            var block = new MaterialPropertyBlock();
            glass.GetPropertyBlock(block);
            v.set(block);
            glass.SetPropertyBlock(block);
            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = _rt };
            if (RenderPipeline.SupportsRenderRequest(_cam, req)) RenderPipeline.SubmitRenderRequest(_cam, req);
            else _cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var img = new Texture2D(_rt.width, _rt.height, TextureFormat.RGBA32, false, false);
            img.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(_dir, $"variant-glass-{v.label}.png"), img.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(img);
            glass.SetPropertyBlock(keep);
        }
    }

    static TimeHeap Heap(string id)
    {
        foreach (TimeHeap heap in UnityEngine.Object.FindObjectsByType<TimeHeap>(FindObjectsSortMode.None))
            if (heap.Category != null && heap.Category.id == id) return heap;
        throw new InvalidOperationException("heap missing: " + id);
    }

    static TimePieceView Pull(int h, string id, Vector3 dir, int units, float seconds, Action midway = null)
    {
        TimeHeap heap = Heap(id);
        dir.Normalize();
        Vector3 start = heap.transform.position + dir * (heap.Radius + 0.004f);
        Hand(h, start + dir * 0.03f, false);
        Step(0.1f);
        Move(h, start, 0.15f, false);
        Hand(h, start, true);
        Step(0.12f);
        float length = units * _ritual.Config.unitLength + 0.002f;
        if (midway != null)
        {
            Move(h, start + dir * (length * 0.62f), seconds * 0.62f, true);
            midway();
            Move(h, start + dir * length, seconds * 0.38f, true);
        }
        else Move(h, start + dir * length, seconds, true);
        Step(0.25f);
        TimePieceView piece = _ritual.Pieces[_ritual.Pieces.Count - 1];
        return piece;
    }

    static void Release(int h)
    {
        Hand(h, Tip[h], false);
        Step(0.3f);
        Move(h, Tip[h] + new Vector3(0f, 0.02f, -0.1f), 0.2f, false);
        Away(h);
    }

    static void Grab(int h, TimePieceView piece)
    {
        Vector3 c = piece.Center;
        Hand(h, c + new Vector3(0f, 0.02f, -0.06f), false);
        Step(0.05f);
        Move(h, c, 0.25f, false);
        Hand(h, c, true);
        Step(0.15f);
    }

    static void PourInto(int h, TimePieceView piece, Action midway = null)
    {
        Grab(h, piece);
        Vector3 above = _ritual.Jar.MouthCenter + Vector3.up * 0.16f;
        Move(h, above, 0.6f, true);
        Move(h, _ritual.Jar.MouthCenter + Vector3.up * 0.05f, 0.35f, true);
        Step(0.1f);
        Release(h);
        if (midway != null)
        {
            Step(0.55f);
            midway();
        }
        float guard = 0f;
        while (_ritual.Jar.Pouring && guard < 6f)
        {
            Step(0.1f);
            guard += 0.1f;
        }
    }

    static void CrumpleBetweenHands(TimePieceView piece)
    {
        piece.Segment(out Vector3 a, out Vector3 b);
        Hand(0, a + new Vector3(0f, 0.03f, -0.03f), false);
        Step(0.05f);
        Move(0, a, 0.25f, false);
        Hand(0, a, true);
        Step(0.15f);
        Hand(1, b + new Vector3(0f, 0.03f, -0.03f), false);
        Step(0.05f);
        Move(1, b, 0.25f, false);
        Hand(1, b, true);
        Step(0.15f);
        Vector3 mid = (a + b) * 0.5f;
        Vector3 axis = (b - a).normalized;
        Vector3 endA = mid - axis * 0.018f, endB = mid + axis * 0.018f;
        int steps = Mathf.RoundToInt(1.1f / Dt);
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            float e = t * t * (3f - 2f * t);
            Hand(0, Vector3.Lerp(a, endA, e), true);
            Hand(1, Vector3.Lerp(b, endB, e), true);
            Step(Dt);
            if (i == steps / 2) Shoot("crumple-mid");
        }
        Step(0.6f);
        Shoot("crumpled");
        Release(1);
        Release(0);
    }

    static void Knead(TimePieceView piece)
    {
        Vector3 c = piece.Center;
        float r = piece.Radius + 0.004f;
        Vector3 x = Vector3.right;
        Hand(0, c - x * r + new Vector3(0f, 0.02f, -0.03f), false);
        Step(0.05f);
        Move(0, c - x * r, 0.2f, false);
        Hand(0, c - x * r, true);
        Step(0.15f);
        Hand(1, c + x * r + new Vector3(0f, 0.02f, -0.03f), false);
        Step(0.05f);
        Move(1, c + x * r, 0.2f, false);
        Hand(1, c + x * r, true);
        Step(0.15f);
        for (int k = 0; k < 2; k++)
        {
            Vector3 mid = (Tip[0] + Tip[1]) * 0.5f;
            Vector3 a0 = Tip[0], b0 = Tip[1];
            int steps = Mathf.RoundToInt(0.45f / Dt);
            for (int i = 1; i <= steps; i++)
            {
                float e = i / (float)steps;
                Hand(0, Vector3.Lerp(a0, mid - x * (r * 0.45f), e), true);
                Hand(1, Vector3.Lerp(b0, mid + x * (r * 0.45f), e), true);
                Step(Dt);
            }
            if (k == 0) Shoot("knead-press");
            Vector3 a1 = Tip[0], b1 = Tip[1];
            for (int i = 1; i <= steps; i++)
            {
                float e = i / (float)steps;
                Hand(0, Vector3.Lerp(a1, mid - x * r, e), true);
                Hand(1, Vector3.Lerp(b1, mid + x * r, e), true);
                Step(Dt);
            }
        }
        Step(0.6f);
        Shoot("kneaded");
        Release(1);
        Release(0);
    }

    static void Refused(string id)
    {
        float keep = _ritual.Config.hoursWhenFull;
        _ritual.Config.hoursWhenFull = Mathf.Max(1f, _ritual.Jar.TotalUnits / 6f + 0.2f);
        int units = Mathf.Clamp(_ritual.Jar.Room + 2, 1, _ritual.Config.maxUnits);
        Debug.Log($"[MP] ritual-preview refuse test room={_ritual.Jar.Room}u piece={units}u");
        TimePieceView extra = Pull(1, id, new Vector3(0.5f, 0.6f, -0.6f), units, 0.8f);
        Release(1);
        Step(0.3f);
        Grab(1, extra);
        Move(1, _ritual.Jar.MouthCenter + Vector3.up * 0.16f, 0.6f, true);
        Move(1, _ritual.Jar.MouthCenter + Vector3.up * 0.05f, 0.35f, true);
        Step(0.15f);
        Shoot("refused");
        Step(0.5f);
        Shoot("refused-note");
        Release(1);
        _ritual.Config.hoursWhenFull = keep;
    }

    static void DripWax()
    {
        Vector3 stick = _ritual.Wax.stickRoot.position + Vector3.up * 0.003f;
        Hand(0, stick + new Vector3(0f, 0.05f, -0.03f), false);
        Step(0.1f);
        Move(0, stick, 0.3f, false);
        Hand(0, stick, true);
        Step(0.1f);
        Move(0, stick + new Vector3(0f, 0.06f, -0.01f), 0.45f, true);
        Step(0.1f);
        Shoot("wax-drawn");
        Vector3 above = _ritual.Jar.MouthCenter + Vector3.up * 0.07f;
        Move(0, above, 0.5f, true);
        Step(0.1f);
        Hand(0, above, false);
        Step(0.15f);
        Shoot("wax-falling");
        Step(0.7f);
        Shoot("wax-dripped");
        Move(0, above + new Vector3(0f, 0.05f, -0.08f), 0.3f, false);
        Away(0);
        Step(0.2f);
        Debug.Log($"[MP] ritual-preview wax dripped={_ritual.Wax.Dripped} ready={_ritual.Wax.Ready}");
    }

    public static bool Run(string dir)
    {
        _dir = Path.GetFullPath(dir);
        Directory.CreateDirectory(_dir);
        foreach (string old in Directory.GetFiles(_dir, "*.png")) File.Delete(old);
        Shots.Clear();
        _now = 0f;
        _frame = default;
        _simMs = 0;
        _simTicks = 0;
        _worstMs = 0;

        TimeRitualSceneSetup.Run();
        _ritual = UnityEngine.Object.FindFirstObjectByType<TimeRitual>();
        if (_ritual == null) throw new InvalidOperationException("TimeRitual missing");
        _ritual.Init();
        Vector3 root = new Vector3(0f, 1.0f, 0.42f);
        _ritual.PlaceAt(root, Quaternion.identity);
        Vector3 jar = _ritual.Jar.root.position;

        var desk = GameObject.CreatePrimitive(PrimitiveType.Quad);
        desk.name = "PreviewDesk";
        desk.transform.position = new Vector3(0f, jar.y - 0.0005f, 0.42f);
        desk.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        desk.transform.localScale = new Vector3(1.4f, 0.9f, 1f);
        var deskMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        deskMat.SetColor("_BaseColor", new Color(0.085f, 0.08f, 0.078f, 1f));
        desk.GetComponent<Renderer>().sharedMaterial = deskMat;

        var camGo = new GameObject("PreviewCam");
        _cam = camGo.AddComponent<Camera>();
        _cam.nearClipPlane = 0.02f;
        _cam.farClipPlane = 6f;
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0.045f, 0.047f, 0.06f, 1f);
        _cam.cullingMask = ~0;
        camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
        _rt = new RenderTexture(Cell, Cell, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        _rt.Create();
        _cam.targetTexture = _rt;
        _cam.aspect = 1f;

        Vector3 eye = new Vector3(0f, 1.17f, 0.02f);
        Vector3 bench = new Vector3(0.02f, 0.98f, 0.38f);
        Action wide = () => Look(eye, new Vector3(0.05f, 0.86f, 0.42f), 62f);
        Action close = () => Look(new Vector3(-0.02f, 1.08f, 0.14f), bench, 44f);
        Action jarView = () => Look(jar + new Vector3(-0.08f, 0.19f, -0.25f), jar + new Vector3(0f, 0.085f, 0f), 40f);
        Action jarNear = () => Look(jar + new Vector3(-0.03f, 0.1f, -0.16f), jar + new Vector3(0f, 0.06f, 0f), 44f);
        Action topView = () => Look(jar + new Vector3(0f, 0.36f, -0.11f), jar + new Vector3(0f, 0.16f, 0f), 30f);
        Action lowView = () => Look(jar + new Vector3(-0.16f, 0.17f, -0.4f), jar + new Vector3(0f, 0.05f, 0f), 46f);

        Step(0.4f);
        wide();
        Shoot("start");

        TimeHeap move = Heap("move");
        Vector3 mp = move.transform.position;
        Look(mp + new Vector3(-0.2f, 0.07f, -0.07f), mp + new Vector3(0f, 0.03f, -0.045f), 34f);
        TimePieceView moveA = Pull(1, "move", new Vector3(0.1f, 0.5f, -1f), 4, 0.9f, () => Shoot("pull-mid"));
        Shoot("pull-held-nodes");
        Release(1);
        Step(0.6f);
        Shoot("pull-released");

        TimePieceView moveB = Pull(1, "move", new Vector3(-0.9f, 0.3f, -0.6f), 2, 0.5f);
        Release(1);
        Step(0.4f);
        Vector3 ac = moveA.Center;
        Look(ac + new Vector3(-0.02f, 0.05f, -0.19f), ac + new Vector3(-0.02f, 0f, 0f), 36f);
        Shoot("two-pieces");
        Grab(1, moveB);
        Vector3 nearA = moveA.Center + new Vector3(-0.075f, 0.0f, 0f);
        Move(1, nearA, 0.5f, true);
        Step(0.1f);
        Shoot("merge-mid");
        Step(0.2f);
        Shoot("merge-flow");
        Step(1.0f);
        Shoot("merged");
        Move(1, new Vector3(-0.2f, 1.02f, 0.26f), 0.5f, true);
        Release(1);

        Vector3 wp = Heap("work").transform.position;
        Look(wp + new Vector3(0.24f, 0.09f, -0.09f), wp + new Vector3(0f, 0.045f, -0.06f), 38f);
        TimePieceView work = Pull(1, "work", new Vector3(0.0f, 0.55f, -1f), 6, 1.0f);
        Shoot("work-hour-nodes");
        Release(1);
        Step(0.7f);
        Shoot("work-hour-released");

        TimePieceView workB = Pull(1, "work", new Vector3(0.8f, 0.4f, -0.6f), 2, 0.5f);
        Release(1);
        TimePieceView workC = Pull(1, "work", new Vector3(-0.7f, 0.6f, -0.5f), 1, 0.4f);
        Release(1);
        Step(0.5f);
        wide();
        Shoot("scattered");
        Vector3 sweepFrom = Heap("work").transform.position + new Vector3(0.2f, 0.04f, -0.08f);
        Hand(0, sweepFrom, false);
        Step(0.1f);
        Move(0, sweepFrom + new Vector3(-0.3f, 0.02f, 0f), 0.55f, false);
        Move(0, sweepFrom + new Vector3(-0.05f, 0.03f, -0.02f), 0.5f, false);
        Step(0.4f);
        Away(0);
        Step(1.0f);
        Shoot("swept");

        TimePieceView drift = Pull(1, "drift", new Vector3(0.3f, 0.7f, -0.7f), 3, 0.6f);
        Release(1);
        TimePieceView play = Pull(1, "play", new Vector3(0.2f, 0.6f, -0.8f), 2, 0.5f);
        Release(1);
        TimePieceView sleep = Pull(0, "sleep", new Vector3(0.25f, 0.45f, -0.9f), 24, 1.6f);
        wide();
        Shoot("sleep-four-hours");
        Release(0);
        Step(0.8f);
        Vector3 sc = sleep.Center;
        Look(sc + new Vector3(-0.05f, 0.12f, -0.33f), sc, 48f);
        CrumpleBetweenHands(sleep);
        Debug.Log($"[MP] ritual-preview crumple shape={sleep.shape} units={sleep.Units} crumples={_ritual.Crumples}");
        Step(0.4f);
        Knead(sleep);
        Debug.Log($"[MP] ritual-preview knead radius={sleep.BlobRadiusNow / Mathf.Max(1e-5f, sleep.BlobRadiusFull):0.00} compactions={_ritual.Compactions}");
        wide();
        Shoot("all-pieces");

        jarView();
        TimePieceView firstMove = null;
        foreach (TimePieceView p in _ritual.Pieces) if (p.data.category == "move") firstMove = p;
        PourInto(1, firstMove, () => Shoot("pour-mid"));
        Step(0.3f);
        Shoot("poured-move");
        foreach (string id in new[] { "work", "drift", "play", "sleep" })
        {
            var batch = new List<TimePieceView>();
            foreach (TimePieceView p in _ritual.Pieces) if (p.data.category == id && p.state == PieceState.Floating) batch.Add(p);
            for (int i = 0; i < batch.Count; i++)
            {
                if (id == "sleep" && i == 0) PourInto(1, batch[i], () => Shoot("pour-sleep-mid"));
                else PourInto(1, batch[i]);
            }
        }
        Step(0.5f);
        Shoot("jar-layers");
        jarNear();
        Shoot("jar-near");
        topView();
        Shoot("jar-top");

        jarView();
        Refused("work");

        Look(jar + new Vector3(-0.09f, 0.27f, -0.3f), jar + new Vector3(0.03f, 0.1f, 0f), 44f);
        Step(2.6f);
        Shoot("wax-stick");
        DripWax();
        Look(jar + new Vector3(-0.09f, 0.27f, -0.3f), jar + new Vector3(0f, 0.13f, 0f), 40f);
        Vector3 drop = _ritual.Wax.DropCenter;
        Hand(0, drop + new Vector3(0f, 0.06f, 0f), false);
        Step(0.1f);
        Move(0, drop + new Vector3(0f, -0.005f, 0f), 0.3f, false);
        Step(0.5f);
        Shoot("press-half");
        Move(0, _ritual.Jar.MouthCenter + new Vector3(0f, 0.004f, 0f), 0.5f, false);
        Step(0.5f);
        Debug.Log($"[MP] ritual-preview hold={_ritual.HoldProgress:0.00} sealed={_ritual.Sealed}");
        Step(0.35f);
        Shoot("sealed-moment");
        Move(0, drop + new Vector3(0f, 0.1f, -0.1f), 0.4f, false);
        Away(0);
        Step(1.6f);
        Shoot("sealed-settled");
        topView();
        Shoot("seal-top");
        Step(3.5f);
        lowView();
        Shoot("desk-glow");
        wide();
        Shoot("end-wide");

        GlowStyle keptStyle = _ritual.Config.glowStyle;
        _ritual.Config.glowStyle = keptStyle == GlowStyle.Seep ? GlowStyle.Petals : GlowStyle.Seep;
        _ritual.RebloomGlow();
        Step(3.5f);
        lowView();
        Shoot("desk-glow-" + _ritual.Config.glowStyle.ToString().ToLowerInvariant());
        _ritual.Config.glowStyle = keptStyle;
        _ritual.RebloomGlow();

        int cols = 4;
        int rows = (Shots.Count + cols - 1) / cols;
        int small = 512;
        var sheet = new Texture2D(small * cols, small * rows, TextureFormat.RGBA32, false, false);
        var blank = new Color32[small * small * cols * rows];
        sheet.SetPixels32(blank);
        var scaled = new RenderTexture(small, small, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        for (int i = 0; i < Shots.Count; i++)
        {
            Graphics.Blit(Shots[i], scaled);
            var prev = RenderTexture.active;
            RenderTexture.active = scaled;
            var tile = new Texture2D(small, small, TextureFormat.RGBA32, false, false);
            tile.ReadPixels(new Rect(0, 0, small, small), 0, 0);
            tile.Apply();
            RenderTexture.active = prev;
            sheet.SetPixels(i % cols * small, (rows - 1 - i / cols) * small, small, small, tile.GetPixels());
            UnityEngine.Object.DestroyImmediate(tile);
        }
        sheet.Apply();
        File.WriteAllBytes(Path.Combine(_dir, "sheet.png"), sheet.EncodeToPNG());
        Debug.Log($"[MP] ritual-preview done shots={Shots.Count} sealed={_ritual.Sealed} jar={_ritual.Jar.TotalUnits}u layers={_ritual.Jar.Layers.Count} grains={_ritual.Jar.TotalGrains} " +
                  $"crumples={_ritual.Crumples} compactions={_ritual.Compactions} refusals={_ritual.Refusals} tickAvg={_simMs / Mathf.Max(1, _simTicks):0.000}ms tickWorst={_worstMs:0.00}ms out={_dir}");
        bool ok = _ritual.Sealed && _ritual.Jar.Layers.Count >= 4 && _ritual.Crumples >= 1 && _ritual.Compactions >= 1 && _ritual.Refusals >= 1;
        EditorSceneManager.OpenScene(TimeRitualSceneSetup.ScenePath, OpenSceneMode.Single);
        return ok;
    }
}
