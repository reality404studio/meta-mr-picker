// 배치:  Unity -batchmode -projectPath . -buildTarget Android -executeMethod MoodPickerSceneSetup.RunBatch
using System;
using Meta.XR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class MoodPickerSceneSetup
{
    const string ScenePath = "Assets/Scenes/FirstLight.unity";
    const string RigPrefab = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";

    [MenuItem("Mood Picker/Setup FirstLight Scene")]
    public static void Run()
    {
        ConfigureProject();
        BuildScene();
        OVRManifestPreprocessor.GenerateOrUpdateAndroidManifest(silentMode: true);
        AssetDatabase.SaveAssets();
        Debug.Log("[MP] scene setup done: " + ScenePath);
    }

    static double _deadline;
    public static void RunBatch()
    {
        _deadline = EditorApplication.timeSinceStartup + 120;
        EditorApplication.update += WaitAndRun;
    }

    static void WaitAndRun()
    {
        if (OVRProjectConfig.CachedProjectConfig == null)
        {
            if (EditorApplication.timeSinceStartup > _deadline)
            {
                Debug.LogError("[MP] OVRProjectConfig not ready (timeout)");
                EditorApplication.update -= WaitAndRun;
                EditorApplication.Exit(1);
            }
            return;
        }
        EditorApplication.update -= WaitAndRun;
        int code = 0;
        try { Run(); }
        catch (Exception e) { Debug.LogException(e); code = 1; }
        EditorApplication.Exit(code);
    }

    static void ConfigureProject()
    {
        MoodPickerUrpSetup.Ensure();
        var cfg = OVRProjectConfig.CachedProjectConfig;
        cfg.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Required;
        cfg.isPassthroughCameraAccessEnabled = true;
        cfg.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersAndHands;
        OVRProjectConfig.CommitProjectConfig(cfg);

        RenderSettings.skybox = null;
    }

    internal static GameObject NewPassthroughScene(out Scene scene)
    {
        scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.gray;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefab);
        if (prefab == null) throw new Exception("OVRCameraRig prefab not found: " + RigPrefab);
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        rig.name = "OVRCameraRig";

        var mgr = rig.GetComponent<OVRManager>();
        mgr.isInsightPassthroughEnabled = true;
        var so = new SerializedObject(mgr);
        SetBool(so, "requestPassthroughCameraAccessPermissionOnStartup", true);
        so.ApplyModifiedPropertiesWithoutUndo();

        if (rig.GetComponent<OVRPassthroughLayer>() == null) rig.AddComponent<OVRPassthroughLayer>();

        var cams = rig.GetComponentsInChildren<Camera>(true);
        foreach (var cam in cams)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            var urp = cam.GetUniversalAdditionalCameraData();
            if (urp != null) urp.renderPostProcessing = false;
        }
        return rig;
    }

    static void BuildScene()
    {
        var rig = NewPassthroughScene(out Scene scene);
        var mgr = rig.GetComponent<OVRManager>();
        var cams = rig.GetComponentsInChildren<Camera>(true);

        var pcaGo = new GameObject("PassthroughCameraAccess");
        SceneManager.MoveGameObjectToScene(pcaGo, scene);
        var pca = pcaGo.AddComponent<PassthroughCameraAccess>();
        pca.CameraPosition = PassthroughCameraAccess.CameraPositionType.Left;
        pca.RequestedResolution = new Vector2Int(1280, 960);

        BuildFirstLight(scene, rig, pca);

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        Debug.Log($"[MP] rig cameras={cams.Length} passthrough={mgr.isInsightPassthroughEnabled} " +
                  $"layer={rig.GetComponent<OVRPassthroughLayer>() != null} pca={pca.RequestedResolution}");
    }

    const string MatDir = "Assets/Materials/FirstLight";

    static void BuildFirstLight(Scene scene, GameObject rig, PassthroughCameraAccess pca)
    {
        var cubeMat = UnlitMaterial("FirstLight_Cube", new Color(0x7F / 255f, 0xA6 / 255f, 0xD9 / 255f, 1f));
        var quadMat = UnlitMaterial("FirstLight_CameraQuad", Color.white);
        var tipMat = UnlitMaterial("FirstLight_Tip", new Color(0xF0 / 255f, 0xEE / 255f, 0xE6 / 255f, 1f));

        var root = new GameObject("FirstLight");
        SceneManager.MoveGameObjectToScene(root, scene);

        var cube = Primitive(PrimitiveType.Cube, "Cube", root.transform, cubeMat, 0.08f);
        var quad = Primitive(PrimitiveType.Quad, "CameraQuad", root.transform, quadMat, 0.24f);
        var leftTip = Primitive(PrimitiveType.Sphere, "LeftIndexTip", root.transform, tipMat, 0.012f);
        var rightTip = Primitive(PrimitiveType.Sphere, "RightIndexTip", root.transform, tipMat, 0.012f);

        var cameraRig = rig.GetComponent<OVRCameraRig>();
        var leftSkel = AddHand(rig.transform.Find("TrackingSpace/LeftHandAnchor"), OVRHand.Hand.HandLeft, OVRSkeleton.SkeletonType.XRHandLeft);
        var rightSkel = AddHand(rig.transform.Find("TrackingSpace/RightHandAnchor"), OVRHand.Hand.HandRight, OVRSkeleton.SkeletonType.XRHandRight);

        var fl = root.AddComponent<FirstLight>();
        var so = new SerializedObject(fl);
        so.FindProperty("rig").objectReferenceValue = cameraRig;
        so.FindProperty("cameraAccess").objectReferenceValue = pca;
        so.FindProperty("cube").objectReferenceValue = cube.transform;
        so.FindProperty("cameraQuad").objectReferenceValue = quad.GetComponent<Renderer>();
        so.FindProperty("leftHand").objectReferenceValue = leftSkel;
        so.FindProperty("rightHand").objectReferenceValue = rightSkel;
        so.FindProperty("leftTip").objectReferenceValue = leftTip.transform;
        so.FindProperty("rightTip").objectReferenceValue = rightTip.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        var handInput = root.AddComponent<MoodPicker.Hands.Ovr.OvrHandInput>();
        var hi = new SerializedObject(handInput);
        hi.FindProperty("rig").objectReferenceValue = cameraRig;
        hi.FindProperty("leftSkeleton").objectReferenceValue = leftSkel;
        hi.FindProperty("rightSkeleton").objectReferenceValue = rightSkel;
        hi.ApplyModifiedPropertiesWithoutUndo();
        var rec = root.AddComponent<MoodPicker.Hands.Ovr.HandTrajectoryRecorder>();
        var rso = new SerializedObject(rec);
        rso.FindProperty("input").objectReferenceValue = handInput;
        rso.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static OVRSkeleton AddHand(Transform anchor, OVRHand.Hand hand, OVRSkeleton.SkeletonType type)
    {
        if (anchor == null) throw new Exception("hand anchor not found in OVRCameraRig");
        var go = new GameObject(hand == OVRHand.Hand.HandLeft ? "LeftHand" : "RightHand");
        go.transform.SetParent(anchor, false);
        var h = go.AddComponent<OVRHand>();
        var hso = new SerializedObject(h);
        hso.FindProperty("HandType").intValue = (int)hand;
        hso.ApplyModifiedPropertiesWithoutUndo();
        var s = go.AddComponent<OVRSkeleton>();
        var sso = new SerializedObject(s);
        sso.FindProperty("_skeletonType").intValue = (int)type;
        sso.ApplyModifiedPropertiesWithoutUndo();
        return s;
    }

    internal static GameObject Primitive(PrimitiveType t, string name, Transform parent, Material m, float size)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * size;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    internal static Material UnlitMaterial(string name, Color c)
    {
        System.IO.Directory.CreateDirectory(MatDir);
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) throw new Exception("URP Unlit shader not found");
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", c);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void SetBool(SerializedObject so, string name, bool v)
    {
        var p = so.FindProperty(name);
        if (p == null) throw new Exception("OVRManager field not found: " + name);
        p.boolValue = v;
    }
}
