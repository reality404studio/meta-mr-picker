// 배치:  Unity -batchmode -projectPath . -buildTarget Android -executeMethod CatchSceneSetup.RunBatch
using System;
using Meta.XR;
using MoodPicker.Catch;
using MoodPicker.Hands.Ovr;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CatchSceneSetup
{
    public const string ScenePath = "Assets/Scenes/Catch.unity";
    public const string SettingsPath = "Assets/Settings/CatchSettings.asset";

    public static readonly Color Line = new Color(0xF0 / 255f, 0xEE / 255f, 0xE6 / 255f, 1f);
    public static readonly Color Dot = new Color(0xD2 / 255f, 0x3A / 255f, 0x2C / 255f, 1f);

    [MenuItem("Mood Picker/Setup Catch Scene")]
    public static void Run()
    {
        var rig = MoodPickerSceneSetup.NewPassthroughScene(out Scene scene);
        var cameraRig = rig.GetComponent<OVRCameraRig>();

        var pcaGo = new GameObject("PassthroughCameraAccess");
        SceneManager.MoveGameObjectToScene(pcaGo, scene);
        var pca = pcaGo.AddComponent<PassthroughCameraAccess>();
        pca.CameraPosition = PassthroughCameraAccess.CameraPositionType.Left;
        pca.RequestedResolution = new Vector2Int(1280, 960);

        var leftSkel = MoodPickerSceneSetup.AddHand(rig.transform.Find("TrackingSpace/LeftHandAnchor"), OVRHand.Hand.HandLeft, OVRSkeleton.SkeletonType.XRHandLeft);
        var rightSkel = MoodPickerSceneSetup.AddHand(rig.transform.Find("TrackingSpace/RightHandAnchor"), OVRHand.Hand.HandRight, OVRSkeleton.SkeletonType.XRHandRight);

        var root = new GameObject("Catch");
        SceneManager.MoveGameObjectToScene(root, scene);
        var input = root.AddComponent<OvrHandInput>();
        var iso = new SerializedObject(input);
        iso.FindProperty("rig").objectReferenceValue = cameraRig;
        iso.FindProperty("leftSkeleton").objectReferenceValue = leftSkel;
        iso.FindProperty("rightSkeleton").objectReferenceValue = rightSkel;
        iso.ApplyModifiedPropertiesWithoutUndo();
        var rec = root.AddComponent<HandTrajectoryRecorder>();
        var rso = new SerializedObject(rec);
        rso.FindProperty("input").objectReferenceValue = input;
        rso.ApplyModifiedPropertiesWithoutUndo();

        var ctl = root.AddComponent<CatchController>();
        var cso = new SerializedObject(ctl);
        cso.FindProperty("rig").objectReferenceValue = cameraRig;
        cso.FindProperty("cameraAccess").objectReferenceValue = pca;
        cso.FindProperty("handInputSource").objectReferenceValue = input;
        cso.FindProperty("settings").objectReferenceValue = EnsureSettings();
        cso.FindProperty("lineMaterial").objectReferenceValue = MoodPickerSceneSetup.UnlitMaterial("Catch_Line", Line);
        cso.FindProperty("dotMaterial").objectReferenceValue = MoodPickerSceneSetup.UnlitMaterial("Catch_Dot", Dot);
        cso.FindProperty("chipMaterial").objectReferenceValue = MoodPickerSceneSetup.UnlitMaterial("Catch_Chip", Color.white);
        cso.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[MP] catch scene: " + ScenePath);
    }

    public static CatchSettings EnsureSettings()
    {
        var s = AssetDatabase.LoadAssetAtPath<CatchSettings>(SettingsPath);
        if (s != null) return s;
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SettingsPath));
        s = ScriptableObject.CreateInstance<CatchSettings>();
        AssetDatabase.CreateAsset(s, SettingsPath);
        return s;
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
