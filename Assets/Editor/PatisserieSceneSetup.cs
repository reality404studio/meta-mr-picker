// 배치: Unity -batchmode -projectPath . -buildTarget Android -executeMethod PatisserieSceneSetup.BuildApkBatch
using System.IO;
using Meta.XR;
using MoodPicker.Splatter;
using MoodPicker.Splatter.Patisserie;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PatisserieSceneSetup
{
    public const string ScenePath = "Assets/Scenes/PatisserieLab.unity";
    public const string MatPath = "Assets/Materials/Splatter/LetterSplat.mat";
    public const string TexDir = "Assets/Textures/Splatter";
    public const string ApkPath = "Builds/patisserie-dev.apk";

    [MenuItem("Mood Picker/Patisserie/Setup Lab Scene")]
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
        MoodPickerSceneSetup.AddHand(rig.transform.Find("TrackingSpace/LeftHandAnchor"), OVRHand.Hand.HandLeft, OVRSkeleton.SkeletonType.XRHandLeft);
        MoodPickerSceneSetup.AddHand(rig.transform.Find("TrackingSpace/RightHandAnchor"), OVRHand.Hand.HandRight, OVRSkeleton.SkeletonType.XRHandRight);

        Material splatMat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        SplatPigmentSlotAsset[] slots = LoadAvailableSlots();

        GameObject cakeObj = new GameObject("PatisserieCakeExperience");
        SceneManager.MoveGameObjectToScene(cakeObj, scene);
        cakeObj.transform.position = new Vector3(0f, 1.05f, 0.42f);

        var canvas = cakeObj.AddComponent<IcingCanvasController>();
        var canvasSo = new SerializedObject(canvas);
        canvasSo.FindProperty("splatMaterial").objectReferenceValue = splatMat;
        if (slots != null && slots.Length > 0)
        {
            canvasSo.FindProperty("defaultCakeSlot").objectReferenceValue = slots[0];
        }
        canvasSo.ApplyModifiedPropertiesWithoutUndo();

        var interactor = cakeObj.AddComponent<PipingBagInteractor>();
        var interSo = new SerializedObject(interactor);
        interSo.FindProperty("canvas").objectReferenceValue = canvas;
        if (slots != null && slots.Length > 0)
        {
            var paletteProp = interSo.FindProperty("paletteSlots");
            paletteProp.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++)
            {
                paletteProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            }
        }
        interSo.ApplyModifiedPropertiesWithoutUndo();

        EnsureDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
    }

    public static void RunBatch()
    {
        Run();
        EditorApplication.Exit(0);
    }

    [MenuItem("Mood Picker/Patisserie/Build APK")]
    public static bool BuildApk()
    {
        Run();
        EnsureDirectory(Path.GetDirectoryName(ApkPath));
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = ApkPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.Development
        };
        EditorUserBuildSettings.development = true;
        BuildReport report = BuildPipeline.BuildPlayer(opts);
        var s = report.summary;
        Debug.Log($"[Patisserie] build result={s.result} errors={s.totalErrors} time={s.totalTime.TotalSeconds:0}s path={ApkPath}");
        return s.result == BuildResult.Succeeded;
    }

    static double _deadline;
    public static void BuildApkBatch()
    {
        _deadline = EditorApplication.timeSinceStartup + 120;
        EditorApplication.update += WaitAndBuild;
    }

    static void WaitAndBuild()
    {
        if (OVRProjectConfig.CachedProjectConfig == null && EditorApplication.timeSinceStartup < _deadline) return;
        EditorApplication.update -= WaitAndBuild;
        bool ok = BuildApk();
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static SplatPigmentSlotAsset[] LoadAvailableSlots()
    {
        string[] guids = AssetDatabase.FindAssets("t:SplatPigmentSlotAsset", new[] { TexDir });
        var list = new System.Collections.Generic.List<SplatPigmentSlotAsset>();
        for (int i = 0; i < guids.Length; i++)
        {
            string p = AssetDatabase.GUIDToAssetPath(guids[i]);
            var slot = AssetDatabase.LoadAssetAtPath<SplatPigmentSlotAsset>(p);
            if (slot != null) list.Add(slot);
        }
        return list.ToArray();
    }

    static void EnsureDirectory(string dir)
    {
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }
}
