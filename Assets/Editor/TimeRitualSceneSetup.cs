// 사용: Unity -batchmode -projectPath . -buildTarget Android -executeMethod TimeRitualSceneSetup.RunBatch   (에셋을 기본값으로 되돌리려면 RITUAL_RESET_ASSETS=1)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MoodPicker.Ritual;
using MoodPicker.Splatter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TimeRitualSceneSetup
{
    public const string ScenePath = "Assets/Scenes/TimeRitual.unity";
    public const string AssetDir = "Assets/Ritual";
    public const string MatDir = "Assets/Materials/Ritual";
    public const string ConfigPath = AssetDir + "/TimeRitualConfig.asset";
    public static readonly string[] PlacedCategories = { "sleep", "move", "work", "play", "drift" };

    struct CategorySpec
    {
        public string id, ko, en, symbol, memory, slotAsset;
        public BeadShape shape;
        public PieceFeel feel;
        public float squeeze, nodeGlow, nodeGrain, twinkle, twinkleSpeed;
        public float glint, glintSpeed, sandIntensity, sparkle;
        public Color wax, glow;
    }

    static readonly CategorySpec[] Categories =
    {
        new CategorySpec { id = "sleep", ko = "여백", en = "Rested", symbol = "°", memory = "Quiet Rest", shape = BeadShape.Crescent, feel = PieceFeel.Water,
            slotAsset = "Slot4_IMG4565", squeeze = 0.7f, nodeGlow = 0.35f, nodeGrain = 1.15f, twinkle = 0f, twinkleSpeed = 2f,
            glint = 0.55f, glintSpeed = 0.4f, sandIntensity = 1.1f, sparkle = 0.6f, wax = new Color(0.38f, 0.48f, 0.82f), glow = new Color(0.55f, 0.76f, 1f) },
        new CategorySpec { id = "move", ko = "환기", en = "Moved", symbol = "•", memory = "Fresh Breath", shape = BeadShape.Droplet, feel = PieceFeel.Water,
            slotAsset = "Slot0_IMG4392", squeeze = 1f, nodeGlow = 0.9f, nodeGrain = 0.65f, twinkle = 0.7f, twinkleSpeed = 7f,
            glint = 1.4f, glintSpeed = 0.8f, sandIntensity = 1.2f, sparkle = 1.3f, wax = new Color(0.36f, 0.68f, 0.76f), glow = new Color(0.68f, 0.92f, 1f) },
        new CategorySpec { id = "work", ko = "몰입", en = "Focused", symbol = "*", memory = "Deep Dive", shape = BeadShape.Star, feel = PieceFeel.Jelly,
            slotAsset = "Slot1_IMG4398", squeeze = 1.1f, nodeGlow = 0.55f, nodeGrain = 0.85f, twinkle = 0.25f, twinkleSpeed = 2.5f,
            glint = 0.8f, glintSpeed = 0.5f, sandIntensity = 1.15f, sparkle = 0.9f, wax = new Color(0.82f, 0.52f, 0.28f), glow = new Color(1f, 0.76f, 0.52f) },
        new CategorySpec { id = "play", ko = "유희", en = "Enjoyed", symbol = "+", memory = "Warm Joy", shape = BeadShape.Heart, feel = PieceFeel.Jelly,
            slotAsset = "Slot3_IMG4566", squeeze = 0.9f, nodeGlow = 0.7f, nodeGrain = 0.8f, twinkle = 0.45f, twinkleSpeed = 5f,
            glint = 0.95f, glintSpeed = 0.7f, sandIntensity = 1.15f, sparkle = 1.05f, wax = new Color(0.84f, 0.24f, 0.22f), glow = new Color(1f, 0.48f, 0.30f) },
        new CategorySpec { id = "drift", ko = "탐색", en = "Explored", symbol = "·", memory = "Trial & Wonder", shape = BeadShape.Diamond, feel = PieceFeel.Jelly,
            slotAsset = "Slot2_IMG4543", squeeze = 0.9f, nodeGlow = 0.65f, nodeGrain = 0.85f, twinkle = 0.3f, twinkleSpeed = 3.5f,
            glint = 1f, glintSpeed = 0.6f, sandIntensity = 1.1f, sparkle = 1f, wax = new Color(0.86f, 0.64f, 0.18f), glow = new Color(1f, 0.84f, 0.36f) },
    };

    static bool ResetAssets => Environment.GetEnvironmentVariable("RITUAL_RESET_ASSETS") == "1";

    [MenuItem("Mood Picker/Setup Time Ritual Scene")]
    public static void Run()
    {
        MoodPickerUrpSetup.Ensure();
        var rig = MoodPickerSceneSetup.NewPassthroughScene(out Scene scene);
        OVRSkeleton leftSkel = MoodPickerSceneSetup.AddHand(rig.transform.Find("TrackingSpace/LeftHandAnchor"), OVRHand.Hand.HandLeft, OVRSkeleton.SkeletonType.XRHandLeft);
        OVRSkeleton rightSkel = MoodPickerSceneSetup.AddHand(rig.transform.Find("TrackingSpace/RightHandAnchor"), OVRHand.Hand.HandRight, OVRSkeleton.SkeletonType.XRHandRight);
        Material handMat = SplatterSceneSetup.EnsureFilmHandMaterial();
        AddHandMesh(leftSkel, OVRMesh.MeshType.XRHandLeft, handMat);
        AddHandMesh(rightSkel, OVRMesh.MeshType.XRHandRight, handMat);
        Transform centerEye = rig.transform.Find("TrackingSpace/CenterEyeAnchor");
        Transform trackingSpace = rig.transform.Find("TrackingSpace");

        Directory.CreateDirectory(AssetDir);
        TimeRitualConfig config = EnsureAsset<TimeRitualConfig>(ConfigPath, out bool _);
        if (ResetAssets)
        {
            var fresh = ScriptableObject.CreateInstance<TimeRitualConfig>();
            EditorUtility.CopySerialized(fresh, config);
            UnityEngine.Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(config);
        }

        var manager = rig.GetComponent<OVRManager>();
        var managerSo = new SerializedObject(manager);
        SetProperty(managerSo, "_enableDynamicResolution", p => p.boolValue = config.dynamicResolution);
        SetProperty(managerSo, "quest2MinDynamicResolutionScale", p => p.floatValue = config.minResolutionScale);
        SetProperty(managerSo, "quest2MaxDynamicResolutionScale", p => p.floatValue = config.maxResolutionScale);
        SetProperty(managerSo, "quest3MinDynamicResolutionScale", p => p.floatValue = config.minResolutionScale);
        SetProperty(managerSo, "quest3MaxDynamicResolutionScale", p => p.floatValue = config.maxResolutionScale);
        managerSo.ApplyModifiedPropertiesWithoutUndo();

        Material splatMat = SplatterSceneSetup.EnsureSplatMaterial();
        Material glassMat = EnsureMaterial("RitualGlass", "MoodPicker/RitualGlassSplat");
        Material waxMat = EnsureMaterial("RitualWax", "MoodPicker/RitualWaxSplat");
        Material glowMat = EnsureMaterial("RitualGlow", "MoodPicker/RitualGlowSplat");
        SplatPigmentSlotAsset[] presets = SplatterSceneSetup.EnsureRealVideoSlotAssets();
        TimeCategoryAsset[] categories = Categories.Select(c => EnsureCategory(c, presets)).ToArray();
        Mesh heapMesh = SplatterSceneSetup.EnsureShapeMesh("RitualHeap", SplatShapeMeshes.Sphere("RitualHeap", config.heapDiameter * 0.5f, 40, 30));
        Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Fraunces/Fraunces_72pt-Black.ttf")
            ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) throw new InvalidOperationException("Fraunces / LegacyRuntime.ttf 폰트 없음");

        var root = new GameObject("TimeRitual");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = new Vector3(0f, 1.0f, 0.42f);
        var tuning = root.AddComponent<SplatTuning>();

        var handInput = root.AddComponent<MoodPicker.Hands.Ovr.OvrHandInput>();
        var hso = new SerializedObject(handInput);
        hso.FindProperty("rig").objectReferenceValue = rig.GetComponent<OVRCameraRig>();
        hso.FindProperty("leftSkeleton").objectReferenceValue = leftSkel;
        hso.FindProperty("rightSkeleton").objectReferenceValue = rightSkel;
        hso.ApplyModifiedPropertiesWithoutUndo();

        var heapList = new List<TimeHeap>();
        for (int i = 0; i < PlacedCategories.Length; i++)
        {
            TimeCategoryAsset cat = categories.First(c => c.id == PlacedCategories[i]);
            var go = new GameObject("Heap " + cat.id);
            go.transform.SetParent(root.transform, false);
            float x = (i - (PlacedCategories.Length - 1) * 0.5f) * config.heapSpacing;
            go.transform.localPosition = new Vector3(x, 0f, -0.35f * x * x);
            go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.AddComponent<SplatLetterRenderer>();
            var heap = go.AddComponent<TimeHeap>();
            var so = new SerializedObject(heap);
            so.FindProperty("category").objectReferenceValue = cat;
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("tuning").objectReferenceValue = tuning;
            so.FindProperty("splatMaterial").objectReferenceValue = splatMat;
            so.FindProperty("heapMesh").objectReferenceValue = heapMesh;
            so.FindProperty("seed").intValue = 777 + i * 31;
            so.ApplyModifiedPropertiesWithoutUndo();
            heapList.Add(heap);
        }

        var pieces = new GameObject("Pieces");
        SceneManager.MoveGameObjectToScene(pieces, scene);

        var pull = root.AddComponent<TimePullController>();
        var pso = new SerializedObject(pull);
        var heapsProp = pso.FindProperty("heaps");
        heapsProp.arraySize = heapList.Count;
        for (int i = 0; i < heapList.Count; i++) heapsProp.GetArrayElementAtIndex(i).objectReferenceValue = heapList[i];
        pso.ApplyModifiedPropertiesWithoutUndo();

        var sound = root.AddComponent<RitualAudio>();
        AudioClip bgmClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Ritual/Audio/리추얼앱용힐링로파이음악.mp3");
        if (bgmClip == null)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Ritual/Audio" }))
            {
                bgmClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (bgmClip != null) break;
            }
        }
        if (bgmClip != null)
        {
            var sso = new SerializedObject(sound);
            sso.FindProperty("bgmClip").objectReferenceValue = bgmClip;
            sso.ApplyModifiedPropertiesWithoutUndo();
        }
        else Debug.LogWarning("[MP] bgmClip not found in Assets/Ritual/Audio");

        var ritual = root.AddComponent<TimeRitual>();
        var rso = new SerializedObject(ritual);
        rso.FindProperty("config").objectReferenceValue = config;
        rso.FindProperty("tuning").objectReferenceValue = tuning;
        rso.FindProperty("handInputSource").objectReferenceValue = handInput;
        rso.FindProperty("trackingSpace").objectReferenceValue = trackingSpace;
        rso.FindProperty("head").objectReferenceValue = centerEye;
        rso.FindProperty("pull").objectReferenceValue = pull;
        rso.FindProperty("sound").objectReferenceValue = sound;
        rso.FindProperty("splatMaterial").objectReferenceValue = splatMat;
        rso.FindProperty("glassMaterial").objectReferenceValue = glassMat;
        rso.FindProperty("waxMaterial").objectReferenceValue = waxMat;
        rso.FindProperty("glowMaterial").objectReferenceValue = glowMat;
        rso.FindProperty("labelFont").objectReferenceValue = font;
        rso.FindProperty("piecesRoot").objectReferenceValue = pieces.transform;
        var catsProp = rso.FindProperty("categories");
        catsProp.arraySize = categories.Length;
        for (int i = 0; i < categories.Length; i++) catsProp.GetArrayElementAtIndex(i).objectReferenceValue = categories[i];
        rso.ApplyModifiedPropertiesWithoutUndo();

        var bridge = root.AddComponent<RitualOvrBridge>();
        var bso = new SerializedObject(bridge);
        bso.FindProperty("ritual").objectReferenceValue = ritual;
        bso.FindProperty("passthroughLayer").objectReferenceValue = rig.GetComponent<OVRPassthroughLayer>();
        Camera[] rigCams = rig.GetComponentsInChildren<Camera>(true);
        var camsProp = bso.FindProperty("cameras");
        camsProp.arraySize = rigCams.Length;
        for (int i = 0; i < rigCams.Length; i++) camsProp.GetArrayElementAtIndex(i).objectReferenceValue = rigCams[i];
        bso.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[MP] time ritual scene saved {ScenePath} heaps={heapList.Count} dynamicResolution={config.dynamicResolution}");
    }

    static double _deadline;

    public static void RunBatch()
    {
        _deadline = EditorApplication.timeSinceStartup + 120;
        EditorApplication.update += WaitAndRun;
    }

    static void WaitAndRun()
    {
        if (OVRProjectConfig.CachedProjectConfig == null && EditorApplication.timeSinceStartup < _deadline) return;
        EditorApplication.update -= WaitAndRun;
        int code = 0;
        try { Run(); }
        catch (Exception e)
        {
            Debug.LogException(e);
            code = 1;
        }
        EditorApplication.Exit(code);
    }

    static void SetProperty(SerializedObject so, string name, Action<SerializedProperty> set)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) throw new InvalidOperationException("OVRManager 필드 없음: " + name);
        set(p);
    }

    static void AddHandMesh(OVRSkeleton skeleton, OVRMesh.MeshType meshType, Material mat)
    {
        GameObject go = skeleton.gameObject;
        go.layer = SplatFilmLook.HandLayer;
        var mesh = go.AddComponent<OVRMesh>();
        var meshSo = new SerializedObject(mesh);
        meshSo.FindProperty("_meshType").intValue = (int)meshType;
        meshSo.ApplyModifiedPropertiesWithoutUndo();
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMaterial = mat;
        smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        smr.receiveShadows = false;
        go.AddComponent<OVRMeshRenderer>();
    }

    static Material EnsureMaterial(string name, string shaderName)
    {
        Directory.CreateDirectory(MatDir);
        string path = MatDir + "/" + name + ".mat";
        var shader = Shader.Find(shaderName);
        if (shader == null) throw new InvalidOperationException(shaderName + " 셰이더 없음");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static T EnsureAsset<T>(string path, out bool created) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        created = asset == null;
        if (!created) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static TimeCategoryAsset EnsureCategory(CategorySpec spec, SplatPigmentSlotAsset[] presets)
    {
        TimeCategoryAsset asset = EnsureAsset<TimeCategoryAsset>($"{AssetDir}/Category_{spec.id}.asset", out bool created);
        if (created || asset.look == null || ResetAssets)
        {
            asset.id = spec.id;
            asset.displayKo = spec.ko;
            asset.look = presets.FirstOrDefault(p => p != null && p.name == spec.slotAsset);
            if (asset.look == null) throw new InvalidOperationException($"색 에셋 {spec.slotAsset} 없음");
            asset.nodeSqueeze = spec.squeeze;
            asset.nodeGlow = spec.nodeGlow;
            asset.nodeGrain = spec.nodeGrain;
            asset.nodeTwinkle = spec.twinkle;
            asset.nodeTwinkleSpeed = spec.twinkleSpeed;
            asset.sandGlint = spec.glint;
            asset.sandGlintSpeed = spec.glintSpeed;
            asset.sandIntensity = spec.sandIntensity;
            asset.sandSparkle = spec.sparkle;
            asset.glowColor = spec.glow;
        }
        asset.displayKo = spec.ko;
        asset.displayEn = spec.en;
        asset.symbol = spec.symbol;
        asset.memoryEn = spec.memory;
        asset.beadShape = spec.shape;
        asset.initialFeel = spec.feel;
        asset.waxColor = spec.wax;
        EditorUtility.SetDirty(asset);
        return asset;
    }
}
