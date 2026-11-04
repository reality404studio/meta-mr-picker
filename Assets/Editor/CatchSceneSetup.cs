// 배치:  Unity -batchmode -projectPath . -buildTarget Android -executeMethod CatchSceneSetup.RunBatch
using System;
using Meta.XR;
using MoodPicker.Catch;
using MoodPicker.Dab;
using MoodPicker.Hands.Ovr;
using MoodPicker.Portal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CatchSceneSetup
{
    public const string ScenePath = "Assets/Scenes/Catch.unity";
    public const string SettingsPath = "Assets/Settings/CatchSettings.asset";
    public const string PortalSettingsPath = "Assets/Settings/PortalSettings.asset";

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

        var portal = root.AddComponent<PortalController>();
        var pso = new SerializedObject(portal);
        pso.FindProperty("trackingSpace").objectReferenceValue = rig.transform.Find("TrackingSpace");
        pso.FindProperty("handInputSource").objectReferenceValue = input;
        pso.FindProperty("settings").objectReferenceValue = EnsureAsset<PortalSettings>(PortalSettingsPath);
        pso.FindProperty("lineMaterial").objectReferenceValue = MoodPickerSceneSetup.UnlitMaterial("Catch_Line", Line);
        pso.FindProperty("chipMaterial").objectReferenceValue = MoodPickerSceneSetup.UnlitMaterial("Catch_Chip", Color.white);
        pso.ApplyModifiedPropertiesWithoutUndo();

        var ctl = root.AddComponent<CatchController>();
        var cso = new SerializedObject(ctl);
        cso.FindProperty("rig").objectReferenceValue = cameraRig;
        cso.FindProperty("cameraAccess").objectReferenceValue = pca;
        cso.FindProperty("handInputSource").objectReferenceValue = input;
        cso.FindProperty("pinchGateSource").objectReferenceValue = portal;
        cso.FindProperty("settings").objectReferenceValue = EnsureAsset<CatchSettings>(SettingsPath);
        cso.FindProperty("lineMaterial").objectReferenceValue = MoodPickerSceneSetup.UnlitMaterial("Catch_Line", Line);
        cso.FindProperty("dotMaterial").objectReferenceValue = MoodPickerSceneSetup.UnlitMaterial("Catch_Dot", Dot);
        cso.FindProperty("chipMaterial").objectReferenceValue = MoodPickerSceneSetup.UnlitMaterial("Catch_Chip", Color.white);
        cso.ApplyModifiedPropertiesWithoutUndo();

        var dabRoot = new GameObject("Dab");
        SceneManager.MoveGameObjectToScene(dabRoot, scene);
        var bottle = DabBottleAssets.CreateBottle(null);
        SceneManager.MoveGameObjectToScene(bottle, scene);
        var cols = bottle.GetComponents<MeshCollider>();
        var dab = dabRoot.AddComponent<DabController>();
        var dso = new SerializedObject(dab);
        dso.FindProperty("trackingSpace").objectReferenceValue = rig.transform.Find("TrackingSpace");
        dso.FindProperty("head").objectReferenceValue = rig.transform.Find("TrackingSpace/CenterEyeAnchor");
        dso.FindProperty("handInputSource").objectReferenceValue = input;
        dso.FindProperty("portal").objectReferenceValue = portal;
        dso.FindProperty("settings").objectReferenceValue = DabBottleAssets.EnsureSettings();
        dso.FindProperty("target").objectReferenceValue = bottle.GetComponent<MeshRenderer>();
        dso.FindProperty("contactCollider").objectReferenceValue = cols[0];
        dso.FindProperty("uvCollider").objectReferenceValue = cols[1];
        dso.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[MP] catch scene: " + ScenePath);
    }

    public static T EnsureAsset<T>(string path) where T : ScriptableObject
    {
        var s = AssetDatabase.LoadAssetAtPath<T>(path);
        if (s != null) return s;
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        s = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(s, path);
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
