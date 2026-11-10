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

        var bridge = upperAWord.AddComponent<SplatOvrHandBridge>();
        var bso = new SerializedObject(bridge);
        bso.FindProperty("wordController").objectReferenceValue = upperAWord.GetComponent<SplatWordController>();
        bso.FindProperty("leftSkeleton").objectReferenceValue = leftSkel;
        bso.FindProperty("rightSkeleton").objectReferenceValue = rightSkel;
        bso.FindProperty("leftHand").objectReferenceValue = leftSkel != null ? leftSkel.GetComponent<OVRHand>() : null;
        bso.FindProperty("rightHand").objectReferenceValue = rightSkel != null ? rightSkel.GetComponent<OVRHand>() : null;
        bso.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        OVRManifestPreprocessor.GenerateOrUpdateAndroidManifest(silentMode: true);
        AssetDatabase.SaveAssets();
        Debug.Log("[MP] splatter scene setup done (uppercase A letter + 3s mystical summon): " + ScenePath);
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

        return list.ToArray();
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
