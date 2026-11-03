// setup-scene: 가드레일 §3 (패스스루 + 카메라 접근) 을 코드로 고정한다. 다시 돌려도 같은 결과.
// 메뉴: Mood Picker > Setup FirstLight Scene
// 배치:  Unity -batchmode -projectPath . -buildTarget Android -executeMethod MoodPickerSceneSetup.RunBatch
//        (-quit 없이. Meta 에디터 초기화(EditorReady)를 기다렸다가 스스로 종료한다)
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

    // ---- batch: Meta 쪽 InitializeOnLoad 가 끝나야 CachedProjectConfig 가 null 이 아니다
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

    // ---- 프로젝트 설정 (OculusProjectConfig.asset → 매니페스트)
    static void ConfigureProject()
    {
        var cfg = OVRProjectConfig.CachedProjectConfig;
        cfg.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Required;
        cfg.isPassthroughCameraAccessEnabled = true;
        // 앱은 핸즈 퍼스트. 디버그 중 셸 창 대응으로 컨트롤러도 허용 (가드레일 §5). 최종 테스트는 컨트롤러 끄고.
        cfg.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersAndHands;
        OVRProjectConfig.CommitProjectConfig(cfg);

        RenderSettings.skybox = null; // 씬마다 저장되지만 새 씬 기본값도 비워 둔다
    }

    // ---- 씬
    static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Lighting: Skybox 없음, 환경광은 단색 (MR 씬엔 Directional Light 없음 → Unlit 기본, §4)
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.gray;

        // Camera Rig 하나 (기본 Main Camera 는 EmptyScene 이라 없음)
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefab);
        if (prefab == null) throw new Exception("OVRCameraRig prefab not found: " + RigPrefab);
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        rig.name = "OVRCameraRig";

        // OVRManager: 패스스루 켜기 + 카메라 권한은 여기 한 곳에서만 요청 (§3)
        var mgr = rig.GetComponent<OVRManager>();
        mgr.isInsightPassthroughEnabled = true;
        var so = new SerializedObject(mgr);
        SetBool(so, "requestPassthroughCameraAccessPermissionOnStartup", true);
        so.ApplyModifiedPropertiesWithoutUndo();

        // OVRPassthroughLayer 하나 (기본 Underlay)
        if (rig.GetComponent<OVRPassthroughLayer>() == null) rig.AddComponent<OVRPassthroughLayer>();

        // CenterEye: SolidColor + 알파 0, 포스트 프로세싱 끔
        var cams = rig.GetComponentsInChildren<Camera>(true);
        foreach (var cam in cams)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            var urp = cam.GetUniversalAdditionalCameraData();
            if (urp != null) urp.renderPostProcessing = false;
        }

        // 카메라 접근: MRUK PassthroughCameraAccess, 지원 목록 값 1280×960 요청 (실제 값은 CurrentResolution)
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

    // ---- First Light 오브젝트 (setup-firstlight): Unlit 큐브 + 카메라 쿼드 + 손끝 점, OpenXR 손 스켈레톤
    const string MatDir = "Assets/Materials/FirstLight";

    static void BuildFirstLight(Scene scene, GameObject rig, PassthroughCameraAccess pca)
    {
        var cubeMat = UnlitMaterial("FirstLight_Cube", new Color(0x7F / 255f, 0xA6 / 255f, 0xD9 / 255f, 1f)); // 보조 하늘 #7FA6D9 (IL-01 B안)
        var quadMat = UnlitMaterial("FirstLight_CameraQuad", Color.white);
        var tipMat = UnlitMaterial("FirstLight_Tip", new Color(0xF0 / 255f, 0xEE / 255f, 0xE6 / 255f, 1f)); // 선 #F0EEE6 (IL-01 B안)

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
    }

    static OVRSkeleton AddHand(Transform anchor, OVRHand.Hand hand, OVRSkeleton.SkeletonType type)
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

    static GameObject Primitive(PrimitiveType t, string name, Transform parent, Material m, float size)
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

    // 에디터 전용이라 Shader.Find 를 써도 된다 (런타임 금지는 게이트가 막음). 결과는 .mat 에셋으로 남는다
    static Material UnlitMaterial(string name, Color c)
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
        m.SetColor("_BaseColor", c); // 알파 1 (§4: 알파 0 이면 패스스루 위에서 사라짐)
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
