// 배치: Unity -batchmode -projectPath . -buildTarget Android -executeMethod SplatterSceneSetup.RunBatch
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Meta.XR;
using MoodPicker.Splatter;
using Pigment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SplatterSceneSetup
{
    public const string ScenePath = "Assets/Scenes/Splatter.unity";
    public const string MatDir = "Assets/Materials/Splatter";
    public const string MatPath = "Assets/Materials/Splatter/LetterSplat.mat";
    public const string TexDir = "Assets/Textures/Splatter";
    public const string GlbPath = "Assets/Models/ORION.glb";
    public const string LetterAGlbPath = "Assets/Models/LETTER_A.glb";
    public const string LetterUpperAGlbPath = "Assets/Models/LETTER_UPPER_A.glb";
    public const string ThreadsGlbPath = "Assets/Models/THREADS.glb";
    public const string GlyphDir = "Assets/Models/Glyphs";
    public const string ShapeDir = "Assets/Models/Shapes";
    public const int ShapeLayer = 25;

    [MenuItem("Mood Picker/Setup Splatter Scene")]
    public static void Run()
    {
        MoodPickerUrpSetup.Ensure();
        var cfg = OVRProjectConfig.CachedProjectConfig;
        if (cfg != null)
        {
            cfg.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Required;
            cfg.isPassthroughCameraAccessEnabled = true;
            cfg.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersAndHands;
            OVRProjectConfig.CommitProjectConfig(cfg);
        }

        var rig = MoodPickerSceneSetup.NewPassthroughScene(out Scene scene);
        OVRSkeleton leftSkel = MoodPickerSceneSetup.AddHand(rig.transform.Find("TrackingSpace/LeftHandAnchor"), OVRHand.Hand.HandLeft, OVRSkeleton.SkeletonType.XRHandLeft);
        OVRSkeleton rightSkel = MoodPickerSceneSetup.AddHand(rig.transform.Find("TrackingSpace/RightHandAnchor"), OVRHand.Hand.HandRight, OVRSkeleton.SkeletonType.XRHandRight);

        Material splatMat = EnsureSplatMaterial();
        SplatPigmentSlotAsset[] presets = EnsureRealVideoSlotAssets();
        Transform centerEye = rig.transform.Find("TrackingSpace/CenterEyeAnchor");

        GameObject upperAWord = CreateLetterUpperASplat(
            "SplatterLetter_UpperA",
            splatMat,
            presets,
            30000,
            SplatShaderMode.Raw,
            0,
            centerEye,
            0.35f,
            -0.06f);
        SceneManager.MoveGameObjectToScene(upperAWord, scene);
        upperAWord.transform.position = new Vector3(0f, 1.22f, 0.35f);

        var tuning = upperAWord.AddComponent<SplatTuning>();
        var wordSo = new SerializedObject(upperAWord.GetComponent<SplatWordController>());
        wordSo.FindProperty("tuning").objectReferenceValue = tuning;
        wordSo.ApplyModifiedPropertiesWithoutUndo();

        var bridge = upperAWord.AddComponent<SplatOvrHandBridge>();
        var bso = new SerializedObject(bridge);
        bso.FindProperty("wordController").objectReferenceValue = upperAWord.GetComponent<SplatWordController>();
        bso.FindProperty("tuning").objectReferenceValue = tuning;
        bso.FindProperty("passthroughLayer").objectReferenceValue = rig.GetComponent<OVRPassthroughLayer>();
        var handInput = upperAWord.AddComponent<MoodPicker.Hands.Ovr.OvrHandInput>();
        var hso = new SerializedObject(handInput);
        hso.FindProperty("rig").objectReferenceValue = rig.GetComponent<OVRCameraRig>();
        hso.FindProperty("leftSkeleton").objectReferenceValue = leftSkel;
        hso.FindProperty("rightSkeleton").objectReferenceValue = rightSkel;
        hso.ApplyModifiedPropertiesWithoutUndo();
        bso.FindProperty("handInputSource").objectReferenceValue = handInput;
        bso.FindProperty("trackingSpace").objectReferenceValue = rig.transform.Find("TrackingSpace");
        bso.ApplyModifiedPropertiesWithoutUndo();

        var pcaGo = new GameObject("PassthroughCameraAccess");
        SceneManager.MoveGameObjectToScene(pcaGo, scene);
        var pca = pcaGo.AddComponent<PassthroughCameraAccess>();
        pca.CameraPosition = PassthroughCameraAccess.CameraPositionType.Left;
        pca.RequestedResolution = new Vector2Int(1280, 960);
        pca.enabled = false;

        var catcher = upperAWord.AddComponent<SplatCameraCatch>();
        var catchSo = new SerializedObject(catcher);
        catchSo.FindProperty("wordController").objectReferenceValue = upperAWord.GetComponent<SplatWordController>();
        catchSo.FindProperty("tuning").objectReferenceValue = tuning;
        catchSo.FindProperty("cameraAccess").objectReferenceValue = pca;
        catchSo.ApplyModifiedPropertiesWithoutUndo();

        var micro = upperAWord.AddComponent<MoodPicker.Hands.Ovr.OvrMicrogestureInput>();
        var mso = new SerializedObject(micro);
        mso.FindProperty("leftHand").objectReferenceValue = leftSkel.GetComponent<OVRHand>();
        mso.FindProperty("rightHand").objectReferenceValue = rightSkel.GetComponent<OVRHand>();
        mso.ApplyModifiedPropertiesWithoutUndo();

        var dialFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (dialFont == null) throw new InvalidOperationException("LegacyRuntime.ttf 내장 폰트 없음");
        TextMesh dialText = MakeText(scene, "FeelDial", dialFont, 0.0016f, TextAnchor.MiddleRight, "dial");
        dialText.richText = true;

        var dial = upperAWord.AddComponent<SplatFeelDial>();
        var dso = new SerializedObject(dial);
        dso.FindProperty("tuning").objectReferenceValue = tuning;
        dso.FindProperty("microgestureSource").objectReferenceValue = micro;
        dso.FindProperty("label").objectReferenceValue = dialText;
        dso.FindProperty("anchor").objectReferenceValue = upperAWord.transform;
        dso.FindProperty("head").objectReferenceValue = centerEye;
        dso.ApplyModifiedPropertiesWithoutUndo();

        var controller = upperAWord.GetComponent<SplatWordController>();
        Mesh[] shapeMeshes = EnsureShapeCatalog(out string[] shapeNames);
        Mesh blobMesh = EnsureShapeMesh("Blob", SplatShapeMeshes.Sphere("Blob", 0.026f, 40, 30));
        Transform shapeBody = upperAWord.transform.Find("A");
        shapeBody.gameObject.layer = ShapeLayer;
        shapeBody.localRotation = Quaternion.Euler(0f, 180f, 0f);

        var shelfGo = new GameObject("Shelf");
        SceneManager.MoveGameObjectToScene(shelfGo, scene);
        var shelf = shelfGo.AddComponent<SplatShelf>();
        var shelfSo = new SerializedObject(shelf);
        shelfSo.FindProperty("shape").objectReferenceValue = controller;
        shelfSo.FindProperty("tuning").objectReferenceValue = tuning;
        shelfSo.FindProperty("head").objectReferenceValue = centerEye;
        shelfSo.FindProperty("splatMaterial").objectReferenceValue = splatMat;
        shelfSo.FindProperty("blobMesh").objectReferenceValue = blobMesh;
        shelfSo.FindProperty("labelFont").objectReferenceValue = dialFont;
        var presetsProp = shelfSo.FindProperty("presets");
        presetsProp.arraySize = presets.Length;
        for (int i = 0; i < presets.Length; i++) presetsProp.GetArrayElementAtIndex(i).objectReferenceValue = presets[i];
        shelfSo.ApplyModifiedPropertiesWithoutUndo();

        TextMesh toast = MakeText(scene, "StillToast", dialFont, 0.0012f, TextAnchor.UpperCenter, "saved");
        toast.gameObject.SetActive(false);
        var still = upperAWord.AddComponent<SplatStillCapture>();
        var stillSo = new SerializedObject(still);
        stillSo.FindProperty("toast").objectReferenceValue = toast;
        stillSo.FindProperty("layer").intValue = ShapeLayer;
        stillSo.ApplyModifiedPropertiesWithoutUndo();

        var ctrlSo = new SerializedObject(controller);
        ctrlSo.FindProperty("shelf").objectReferenceValue = shelf;
        ctrlSo.FindProperty("stillCapture").objectReferenceValue = still;
        var shapesProp = ctrlSo.FindProperty("shapes");
        var namesProp = ctrlSo.FindProperty("shapeNames");
        shapesProp.arraySize = shapeMeshes.Length;
        namesProp.arraySize = shapeNames.Length;
        for (int i = 0; i < shapeMeshes.Length; i++)
        {
            shapesProp.GetArrayElementAtIndex(i).objectReferenceValue = shapeMeshes[i];
            namesProp.GetArrayElementAtIndex(i).stringValue = shapeNames[i];
        }
        ctrlSo.FindProperty("startShape").intValue = 0;
        ctrlSo.ApplyModifiedPropertiesWithoutUndo();

        bso.Update();
        bso.FindProperty("shelf").objectReferenceValue = shelf;
        bso.FindProperty("microgestureSource").objectReferenceValue = micro;
        bso.ApplyModifiedPropertiesWithoutUndo();
        catchSo.Update();
        catchSo.FindProperty("shelf").objectReferenceValue = shelf;
        catchSo.ApplyModifiedPropertiesWithoutUndo();

        OVROverlayCanvasSettings overlaySettings = OVROverlayCanvasSettings.Instance;
        if (overlaySettings.GetShader(OVROverlayCanvas.DrawMode.Transparent) == null || overlaySettings.GetShader(OVROverlayCanvas.DrawMode.Opaque) == null)
            throw new InvalidOperationException("OVROverlayCanvasSettings 에 조율 패널용 셰이더가 없음");
        EditorUtility.SetDirty(overlaySettings);

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        OVRManifestPreprocessor.GenerateOrUpdateAndroidManifest(silentMode: true);
        AssetDatabase.SaveAssets();
        Debug.Log($"[MP] splatter scene setup done (shapes={shapeMeshes.Length}, shelf presets={presets.Length}, still layer={ShapeLayer}): " + ScenePath);
    }

    static TextMesh MakeText(Scene scene, string name, Font font, float characterSize, TextAnchor anchor, string text)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        var tm = go.AddComponent<TextMesh>();
        tm.font = font;
        tm.fontSize = 60;
        tm.characterSize = characterSize;
        tm.anchor = anchor;
        tm.alignment = TextAlignment.Left;
        tm.color = new Color(0.94f, 0.93f, 0.90f, 1f);
        tm.text = text;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = font.material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return tm;
    }

    static Mesh LoadGlbMesh(string path)
    {
        if (!File.Exists(path)) return null;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault();
    }

    public static Mesh EnsureShapeMesh(string name, Mesh fresh)
    {
        Directory.CreateDirectory(ShapeDir);
        string path = ShapeDir + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(fresh, path);
            return fresh;
        }
        EditorUtility.CopySerialized(fresh, existing);
        UnityEngine.Object.DestroyImmediate(fresh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    public static Mesh[] EnsureShapeCatalog(out string[] names)
    {
        var meshes = new List<Mesh>();
        var labels = new List<string>();
        Mesh a = LoadGlbMesh(LetterUpperAGlbPath);
        if (a == null) throw new FileNotFoundException("LETTER_UPPER_A.glb mesh 없음");
        meshes.Add(a);
        labels.Add("A");
        for (char c = 'B'; c <= 'Z'; c++)
        {
            Mesh m = LoadGlbMesh(GlyphDir + "/GLYPH_" + c + ".glb");
            if (m == null) continue;
            meshes.Add(m);
            labels.Add(c.ToString());
        }
        meshes.Add(EnsureShapeMesh("Sphere", SplatShapeMeshes.Sphere("Sphere", 0.085f, 64, 48)));
        labels.Add("Sphere");
        meshes.Add(EnsureShapeMesh("Torus", SplatShapeMeshes.Torus("Torus", 0.068f, 0.032f, 96, 48)));
        labels.Add("Donut");
        names = labels.ToArray();
        return meshes.ToArray();
    }

    public static SplatPigmentSlotAsset[] EnsureRealVideoSlotAssets()
    {
        Directory.CreateDirectory(TexDir);
        var list = new List<SplatPigmentSlotAsset>();

        SplatPigmentSlotAsset slot0 = BuildOrLoadVideoSlot(
            "Slot0_IMG4392",
            "/tmp/vidref_frames/4392",
            256,
            30f);
        if (slot0 != null) list.Add(slot0);

        SplatPigmentSlotAsset slot1 = BuildOrLoadVideoSlot(
            "Slot1_IMG4398",
            "/tmp/vidref_frames/4398",
            256,
            30f);
        if (slot1 != null) list.Add(slot1);

        SplatPigmentSlotAsset slot2 = BuildOrLoadVideoSlot(
            "Slot2_IMG4543",
            GinkgoFramesDir,
            256,
            30f);
        if (slot2 != null)
        {
            if (slot2.calmTexture == null || slot2.sparkTexture == null) VideoSplatPipeline.BuildLightLayers(slot2, TexDir);
            list.Add(slot2);
        }

        DefaultMaterial(slot0, "11.03  15:21", SplatShaderMode.Raw, MoodPicker.Splatter.Sim.SplatFeel.Water);
        DefaultMaterial(slot1, "IMG 4398", SplatShaderMode.Raw, MoodPicker.Splatter.Sim.SplatFeel.Water);
        DefaultMaterial(slot2, "11.10  13:30", SplatShaderMode.Light, MoodPicker.Splatter.Sim.SplatFeel.Leaf);
        return list.ToArray();
    }

    public static string GinkgoFramesDir => Environment.GetEnvironmentVariable("GINKGO_FRAMES_DIR") ?? "/tmp/vidref_frames/4543";

    static void DefaultMaterial(SplatPigmentSlotAsset slot, string capturedAt, SplatShaderMode mode, MoodPicker.Splatter.Sim.SplatFeel feel)
    {
        if (slot == null || slot.hasFeel) return;
        if (string.IsNullOrEmpty(slot.capturedAt)) slot.capturedAt = capturedAt;
        slot.shaderMode = (int)mode;
        slot.feel = feel;
        slot.hasFeel = true;
        EditorUtility.SetDirty(slot);
    }

    static SplatPigmentSlotAsset BuildOrLoadVideoSlot(string assetBaseName, string framesDir, int tileSize, float fps)
    {
        return VideoSplatPipeline.BuildSlotAssetFromFrames(assetBaseName, framesDir, tileSize, fps, forceRebuild: false, outputDir: TexDir);
    }

    public static Material EnsureSplatMaterial()
    {
        Directory.CreateDirectory(MatDir);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        var shader = Shader.Find("MoodPicker/LetterSplatURP");
        if (shader == null) throw new InvalidOperationException("MoodPicker/LetterSplatURP shader not found");
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        else
        {
            mat.shader = shader;
        }
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    public static GameObject CreateLetterUpperASplat(
        string rootName,
        Material splatMat,
        SplatPigmentSlotAsset[] presets,
        int splatsPerLetter,
        SplatShaderMode mode,
        int slot,
        Transform headAnchor = null,
        float placeDistance = 0.35f,
        float placeOffsetY = -0.06f)
    {
        return CreateSingleGlyphSplat(LetterUpperAGlbPath, rootName, splatMat, presets, splatsPerLetter, mode, slot, headAnchor, placeDistance, placeOffsetY);
    }

    public static GameObject CreateSingleGlyphSplat(
        string glbPath,
        string rootName,
        Material splatMat,
        SplatPigmentSlotAsset[] presets,
        int splatsPerLetter,
        SplatShaderMode mode,
        int slot,
        Transform headAnchor = null,
        float placeDistance = 0.35f,
        float placeOffsetY = -0.06f)
    {
        AssetDatabase.ImportAsset(glbPath, ImportAssetOptions.ForceSynchronousImport);
        var glbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
        if (glbPrefab == null) throw new FileNotFoundException("Glyph GLB not found at " + glbPath);

        var root = new GameObject(rootName);
        var tempInstance = UnityEngine.Object.Instantiate(glbPrefab);
        try
        {
            MeshFilter mfSrc = tempInstance.GetComponentInChildren<MeshFilter>(true);
            if (mfSrc == null || mfSrc.sharedMesh == null)
                throw new InvalidOperationException("MeshFilter not found in " + glbPath);

            Mesh srcMesh = mfSrc.sharedMesh;
            var letterGo = new GameObject("A");
            letterGo.transform.SetParent(root.transform, false);
            letterGo.transform.localPosition = Vector3.zero;
            letterGo.transform.localRotation = Quaternion.identity;
            letterGo.transform.localScale = Vector3.one;

            letterGo.AddComponent<MeshFilter>();
            var mr = letterGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = splatMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var slr = letterGo.AddComponent<SplatLetterRenderer>();
            bool useOutline = mode == SplatShaderMode.TwoToneCel;

            var lso = new SerializedObject(slr);
            lso.FindProperty("sourceMesh").objectReferenceValue = srcMesh;
            lso.FindProperty("splatMaterial").objectReferenceValue = splatMat;
            lso.FindProperty("splatsPerLetter").intValue = splatsPerLetter;
            lso.FindProperty("letterSize").floatValue = 0.18f;
            lso.FindProperty("includeOutline").boolValue = useOutline;
            lso.FindProperty("shaderMode").intValue = (int)mode;
            lso.FindProperty("assignedSlot").intValue = slot;
            lso.ApplyModifiedPropertiesWithoutUndo();

            slr.Mode = mode;
            slr.AssignedSlot = slot;
            slr.RebuildFromMesh(srcMesh, splatMat, splatsPerLetter, 0.18f, useOutline, 1337);

            var ctrl = root.AddComponent<SplatWordController>();
            var cso = new SerializedObject(ctrl);
            var lettersProp = cso.FindProperty("letters");
            lettersProp.arraySize = 1;
            lettersProp.GetArrayElementAtIndex(0).objectReferenceValue = slr;
            if (presets != null)
            {
                var presetsProp = cso.FindProperty("presetSlots");
                presetsProp.arraySize = presets.Length;
                for (int s = 0; s < presets.Length; s++)
                    presetsProp.GetArrayElementAtIndex(s).objectReferenceValue = presets[s];
            }
            cso.FindProperty("head").objectReferenceValue = headAnchor;
            cso.FindProperty("placeDistance").floatValue = placeDistance;
            cso.FindProperty("placeOffsetY").floatValue = placeOffsetY;
            cso.FindProperty("letterSize").floatValue = 0.18f;
            cso.FindProperty("triplanarScale").floatValue = 1.15f;
            cso.FindProperty("intensity").floatValue = 1.15f;
            cso.FindProperty("outlineEnabled").boolValue = useOutline;
            cso.FindProperty("autoFillSampleSlot0").boolValue = true;
            cso.ApplyModifiedPropertiesWithoutUndo();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(tempInstance);
        }

        return root;
    }

    public static GameObject CreateSingleLetterASplat(
        string rootName,
        Material splatMat,
        SplatPigmentSlotAsset[] presets,
        int splatsPerLetter,
        SplatShaderMode mode,
        int slot,
        Transform headAnchor = null,
        float placeDistance = 0.30f,
        float placeOffsetY = -0.07f)
    {
        AssetDatabase.ImportAsset(LetterAGlbPath, ImportAssetOptions.ForceSynchronousImport);
        var glbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LetterAGlbPath);
        if (glbPrefab == null) throw new FileNotFoundException("LETTER_A.glb not found at " + LetterAGlbPath);

        var root = new GameObject(rootName);
        var tempInstance = UnityEngine.Object.Instantiate(glbPrefab);
        try
        {
            MeshFilter mfSrc = tempInstance.GetComponentInChildren<MeshFilter>(true);
            if (mfSrc == null || mfSrc.sharedMesh == null)
                throw new InvalidOperationException("MeshFilter not found in LETTER_A.glb");

            Mesh srcMesh = mfSrc.sharedMesh;
            var letterGo = new GameObject("a");
            letterGo.transform.SetParent(root.transform, false);
            letterGo.transform.localPosition = Vector3.zero;
            letterGo.transform.localRotation = Quaternion.identity;
            letterGo.transform.localScale = Vector3.one;

            letterGo.AddComponent<MeshFilter>();
            var mr = letterGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = splatMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var slr = letterGo.AddComponent<SplatLetterRenderer>();
            bool useOutline = mode == SplatShaderMode.TwoToneCel;

            var lso = new SerializedObject(slr);
            lso.FindProperty("sourceMesh").objectReferenceValue = srcMesh;
            lso.FindProperty("splatMaterial").objectReferenceValue = splatMat;
            lso.FindProperty("splatsPerLetter").intValue = splatsPerLetter;
            lso.FindProperty("letterSize").floatValue = 0.16f;
            lso.FindProperty("includeOutline").boolValue = useOutline;
            lso.FindProperty("shaderMode").intValue = (int)mode;
            lso.FindProperty("assignedSlot").intValue = slot;
            lso.ApplyModifiedPropertiesWithoutUndo();

            slr.Mode = mode;
            slr.AssignedSlot = slot;
            slr.RebuildFromMesh(srcMesh, splatMat, splatsPerLetter, 0.16f, useOutline, 1337);

            var ctrl = root.AddComponent<SplatWordController>();
            var cso = new SerializedObject(ctrl);
            var lettersProp = cso.FindProperty("letters");
            lettersProp.arraySize = 1;
            lettersProp.GetArrayElementAtIndex(0).objectReferenceValue = slr;
            if (presets != null)
            {
                var presetsProp = cso.FindProperty("presetSlots");
                presetsProp.arraySize = presets.Length;
                for (int s = 0; s < presets.Length; s++)
                    presetsProp.GetArrayElementAtIndex(s).objectReferenceValue = presets[s];
            }
            cso.FindProperty("head").objectReferenceValue = headAnchor;
            cso.FindProperty("placeDistance").floatValue = placeDistance;
            cso.FindProperty("placeOffsetY").floatValue = placeOffsetY;
            cso.FindProperty("letterSize").floatValue = 0.16f;
            cso.FindProperty("triplanarScale").floatValue = 1.15f;
            cso.FindProperty("intensity").floatValue = 1.15f;
            cso.FindProperty("outlineEnabled").boolValue = useOutline;
            cso.FindProperty("autoFillSampleSlot0").boolValue = true;
            cso.ApplyModifiedPropertiesWithoutUndo();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(tempInstance);
        }

        return root;
    }

    public static GameObject CreateThreadsSplatWord(
        string rootName,
        Material splatMat,
        SplatPigmentSlotAsset[] presets,
        int splatsPerLetter,
        SplatShaderMode mode,
        int slot,
        Transform headAnchor = null,
        float placeDistance = 0.38f,
        float placeOffsetY = -0.06f)
    {
        AssetDatabase.ImportAsset(ThreadsGlbPath, ImportAssetOptions.ForceSynchronousImport);
        var glbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThreadsGlbPath);
        if (glbPrefab == null) throw new FileNotFoundException("THREADS.glb not found at " + ThreadsGlbPath);

        var root = new GameObject(rootName);
        var tempInstance = UnityEngine.Object.Instantiate(glbPrefab);
        try
        {
            string[] order = { "t", "h", "r", "e", "a", "d", "s" };
            var meshFilters = tempInstance.GetComponentsInChildren<MeshFilter>(true)
                .OrderBy(mf =>
                {
                    int idx = Array.IndexOf(order, mf.gameObject.name);
                    return idx >= 0 ? idx : 99;
                })
                .ToArray();

            bool flipX = meshFilters.Length >= 2 && meshFilters[0].transform.localPosition.x > meshFilters[meshFilters.Length - 1].transform.localPosition.x;

            var letterRenderers = new SplatLetterRenderer[meshFilters.Length];
            for (int i = 0; i < meshFilters.Length; i++)
            {
                Mesh srcMesh = meshFilters[i].sharedMesh;
                var letterGo = new GameObject(meshFilters[i].gameObject.name);
                letterGo.transform.SetParent(root.transform, false);
                Vector3 lp = meshFilters[i].transform.localPosition;
                if (flipX) lp.x = -lp.x;
                letterGo.transform.localPosition = lp;
                letterGo.transform.localRotation = flipX ? Quaternion.Euler(0f, 180f, 0f) : meshFilters[i].transform.localRotation;
                letterGo.transform.localScale = meshFilters[i].transform.localScale;

                letterGo.AddComponent<MeshFilter>();
                var mr = letterGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = splatMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                var slr = letterGo.AddComponent<SplatLetterRenderer>();
                bool useOutline = mode == SplatShaderMode.TwoToneCel;

                var lso = new SerializedObject(slr);
                lso.FindProperty("sourceMesh").objectReferenceValue = srcMesh;
                lso.FindProperty("splatMaterial").objectReferenceValue = splatMat;
                lso.FindProperty("splatsPerLetter").intValue = splatsPerLetter;
                lso.FindProperty("letterSize").floatValue = 0.12f;
                lso.FindProperty("includeOutline").boolValue = useOutline;
                lso.FindProperty("shaderMode").intValue = (int)mode;
                lso.FindProperty("assignedSlot").intValue = slot;
                lso.ApplyModifiedPropertiesWithoutUndo();

                slr.Mode = mode;
                slr.AssignedSlot = slot;
                slr.RebuildFromMesh(srcMesh, splatMat, splatsPerLetter, 0.12f, useOutline, 1337 + i * 101);
                letterRenderers[i] = slr;
            }

            var ctrl = root.AddComponent<SplatWordController>();
            var cso = new SerializedObject(ctrl);
            var lettersProp = cso.FindProperty("letters");
            lettersProp.arraySize = letterRenderers.Length;
            for (int i = 0; i < letterRenderers.Length; i++)
                lettersProp.GetArrayElementAtIndex(i).objectReferenceValue = letterRenderers[i];
            if (presets != null)
            {
                var presetsProp = cso.FindProperty("presetSlots");
                presetsProp.arraySize = presets.Length;
                for (int s = 0; s < presets.Length; s++)
                    presetsProp.GetArrayElementAtIndex(s).objectReferenceValue = presets[s];
            }
            cso.FindProperty("head").objectReferenceValue = headAnchor;
            cso.FindProperty("placeDistance").floatValue = placeDistance;
            cso.FindProperty("placeOffsetY").floatValue = placeOffsetY;
            cso.FindProperty("letterSize").floatValue = 0.12f;
            cso.FindProperty("triplanarScale").floatValue = 1.15f;
            cso.FindProperty("intensity").floatValue = 1.15f;
            cso.FindProperty("outlineEnabled").boolValue = false;
            cso.FindProperty("autoFillSampleSlot0").boolValue = true;
            cso.ApplyModifiedPropertiesWithoutUndo();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(tempInstance);
        }

        return root;
    }

    public static GameObject CreateOrionSplatWord(
        string rootName,
        Material splatMat,
        SplatPigmentSlotAsset[] presets,
        int splatsPerLetter,
        SplatShaderMode[] modes,
        int[] slots,
        Transform headAnchor = null,
        float placeDistance = 0.68f,
        float placeOffsetY = 0f)
    {
        AssetDatabase.ImportAsset(GlbPath, ImportAssetOptions.ForceSynchronousImport);
        var glbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GlbPath);
        if (glbPrefab == null) throw new FileNotFoundException("ORION.glb not found at " + GlbPath);

        var root = new GameObject(rootName);
        var tempInstance = UnityEngine.Object.Instantiate(glbPrefab);
        try
        {
            string[] order = { "O1", "R", "I", "O2", "N" };
            var meshFilters = tempInstance.GetComponentsInChildren<MeshFilter>(true)
                .OrderBy(mf =>
                {
                    int idx = Array.IndexOf(order, mf.gameObject.name);
                    return idx >= 0 ? idx : 99;
                })
                .ToArray();

            bool flipX = meshFilters.Length >= 2 && meshFilters[0].transform.localPosition.x > meshFilters[meshFilters.Length - 1].transform.localPosition.x;

            var letterRenderers = new SplatLetterRenderer[meshFilters.Length];
            for (int i = 0; i < meshFilters.Length; i++)
            {
                Mesh srcMesh = meshFilters[i].sharedMesh;
                var letterGo = new GameObject(meshFilters[i].gameObject.name);
                letterGo.transform.SetParent(root.transform, false);
                Vector3 lp = meshFilters[i].transform.localPosition;
                if (flipX) lp.x = -lp.x;
                letterGo.transform.localPosition = lp;
                letterGo.transform.localRotation = flipX ? Quaternion.Euler(0f, 180f, 0f) : meshFilters[i].transform.localRotation;
                letterGo.transform.localScale = meshFilters[i].transform.localScale;

                letterGo.AddComponent<MeshFilter>();
                var mr = letterGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = splatMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                var slr = letterGo.AddComponent<SplatLetterRenderer>();
                SplatShaderMode mode = modes != null && i < modes.Length ? modes[i] : SplatShaderMode.TwoToneCel;
                int slot = slots != null && i < slots.Length ? slots[i] : 0;
                bool useOutline = mode == SplatShaderMode.TwoToneCel;

                var lso = new SerializedObject(slr);
                lso.FindProperty("sourceMesh").objectReferenceValue = srcMesh;
                lso.FindProperty("splatMaterial").objectReferenceValue = splatMat;
                lso.FindProperty("splatsPerLetter").intValue = splatsPerLetter;
                lso.FindProperty("letterSize").floatValue = 0.12f;
                lso.FindProperty("includeOutline").boolValue = useOutline;
                lso.FindProperty("shaderMode").intValue = (int)mode;
                lso.FindProperty("assignedSlot").intValue = slot;
                lso.ApplyModifiedPropertiesWithoutUndo();

                slr.Mode = mode;
                slr.AssignedSlot = slot;
                slr.RebuildFromMesh(srcMesh, splatMat, splatsPerLetter, 0.12f, useOutline, 1337 + i * 101);
                letterRenderers[i] = slr;
            }

            var ctrl = root.AddComponent<SplatWordController>();
            var cso = new SerializedObject(ctrl);
            var lettersProp = cso.FindProperty("letters");
            lettersProp.arraySize = letterRenderers.Length;
            for (int i = 0; i < letterRenderers.Length; i++)
                lettersProp.GetArrayElementAtIndex(i).objectReferenceValue = letterRenderers[i];
            if (presets != null)
            {
                var presetsProp = cso.FindProperty("presetSlots");
                presetsProp.arraySize = presets.Length;
                for (int s = 0; s < presets.Length; s++)
                    presetsProp.GetArrayElementAtIndex(s).objectReferenceValue = presets[s];
            }
            cso.FindProperty("head").objectReferenceValue = headAnchor;
            cso.FindProperty("placeDistance").floatValue = placeDistance;
            cso.FindProperty("placeOffsetY").floatValue = placeOffsetY;
            cso.FindProperty("letterSize").floatValue = 0.12f;
            cso.FindProperty("triplanarScale").floatValue = 1.2f;
            cso.FindProperty("intensity").floatValue = 1.05f;
            cso.FindProperty("outlineEnabled").boolValue = true;
            cso.FindProperty("autoFillSampleSlot0").boolValue = true;
            cso.ApplyModifiedPropertiesWithoutUndo();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(tempInstance);
        }

        return root;
    }

    static double _deadline;
    public static void RunBatch()
    {
        _deadline = EditorApplication.timeSinceStartup + 120;
        EditorApplication.update += Wait;
    }

    static void Wait()
    {
        if (OVRProjectConfig.CachedProjectConfig == null && EditorApplication.timeSinceStartup < _deadline) return;
        EditorApplication.update -= Wait;
        int code = 0;
        try { Run(); }
        catch (Exception e) { Debug.LogException(e); code = 1; }
        EditorApplication.Exit(code);
    }
}
