// 배치:  Unity -batchmode -projectPath . -buildTarget Android -executeMethod DeskKeyboardSceneSetup.RunBatch
using System;
using MoodPicker.DeskKeyboard;
using MoodPicker.Hands.Ovr;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DeskKeyboardSceneSetup
{
    public const string ScenePath = "Assets/Scenes/DeskKeyboard.unity";
    const string MatDir = "Assets/Materials/DeskKeyboard";

    [MenuItem("Mood Picker/Experiments/Setup DeskKeyboard Scene")]
    public static void Run()
    {
        var rig = MoodPickerSceneSetup.NewPassthroughScene(out Scene scene);
        var cameraRig = rig.GetComponent<OVRCameraRig>();

        var occluder = ShaderMaterial("DK_HandOccluder", "MoodPicker/HandOccluder");
        var glow = ShaderMaterial("DK_HandGlow", "MoodPicker/HandGlow");
        var leftSkel = HandWithMesh(rig, "TrackingSpace/LeftHandAnchor", OVRHand.Hand.HandLeft, OVRSkeleton.SkeletonType.XRHandLeft, OVRMesh.MeshType.XRHandLeft, occluder, glow);
        var rightSkel = HandWithMesh(rig, "TrackingSpace/RightHandAnchor", OVRHand.Hand.HandRight, OVRSkeleton.SkeletonType.XRHandRight, OVRMesh.MeshType.XRHandRight, occluder, glow);

        var root = new GameObject("DeskKeyboard");
        SceneManager.MoveGameObjectToScene(root, scene);
        var input = root.AddComponent<OvrHandInput>();
        var iso = new SerializedObject(input);
        iso.FindProperty("rig").objectReferenceValue = cameraRig;
        iso.FindProperty("leftSkeleton").objectReferenceValue = leftSkel;
        iso.FindProperty("rightSkeleton").objectReferenceValue = rightSkel;
        iso.ApplyModifiedPropertiesWithoutUndo();

        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;

        var exp = root.AddComponent<DeskKeyboardExperiment>();
        var eso = new SerializedObject(exp);
        eso.FindProperty("handInputSource").objectReferenceValue = input;
        eso.FindProperty("trackingSpace").objectReferenceValue = rig.transform.Find("TrackingSpace");
        eso.FindProperty("keyMaterial").objectReferenceValue = MoodPickerSceneSetup.UnlitMaterial("DK_Key", new Color(0xF0 / 255f, 0xEE / 255f, 0xE6 / 255f, 1f));
        eso.FindProperty("markerMaterial").objectReferenceValue = MoodPickerSceneSetup.UnlitMaterial("DK_Marker", new Color(0x7F / 255f, 0xA6 / 255f, 0xD9 / 255f, 1f));
        eso.FindProperty("clickSource").objectReferenceValue = audio;
        eso.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[MP] desk keyboard scene: " + ScenePath);
    }

    static OVRSkeleton HandWithMesh(GameObject rig, string anchorPath, OVRHand.Hand hand, OVRSkeleton.SkeletonType skel, OVRMesh.MeshType mesh, Material occluder, Material glow)
    {
        var s = MoodPickerSceneSetup.AddHand(rig.transform.Find(anchorPath), hand, skel);
        var go = s.gameObject;
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMaterials = new[] { occluder, glow };
        smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        smr.receiveShadows = false;
        var m = go.AddComponent<OVRMesh>();
        var mso = new SerializedObject(m);
        mso.FindProperty("_meshType").intValue = (int)mesh;
        mso.ApplyModifiedPropertiesWithoutUndo();
        var mr = go.AddComponent<OVRMeshRenderer>();
        var rso = new SerializedObject(mr);
        rso.FindProperty("_systemGestureBehavior").intValue = (int)OVRMeshRenderer.SystemGestureBehavior.None;
        rso.ApplyModifiedPropertiesWithoutUndo();
        return s;
    }

    static Material ShaderMaterial(string name, string shader)
    {
        System.IO.Directory.CreateDirectory(MatDir);
        string path = MatDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        var sh = Shader.Find(shader);
        if (sh == null) throw new Exception("shader not found: " + shader);
        mat = new Material(sh);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
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
