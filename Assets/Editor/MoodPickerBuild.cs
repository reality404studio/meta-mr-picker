// Unity -batchmode -projectPath . -buildTarget Android -executeMethod MoodPickerBuild.BuildDevApkBatch   (-quit 없이)
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class MoodPickerBuild
{
    public const string ApkPath = "Builds/mood-picker-dev.apk";
    public const string DeskKeyboardApkPath = "Builds/desk-keyboard-dev.apk";
    public const string CatchApkPath = "Builds/catch-dev.apk";
    public const string SplatterApkPath = "Builds/splatter-dev.apk";
    public const string TimeRitualApkPath = "Builds/time-ritual-dev.apk";
    public const string TimeRitualPackage = "com.DefaultCompany.timeritual";
    public const string TimeRitualProductName = "Time Ritual";

    [MenuItem("Mood Picker/Build Dev APK")]
    public static bool BuildDevApk() => BuildDevApk(ApkPath);

    [MenuItem("Mood Picker/Build Splatter APK")]
    public static bool BuildSplatterApk()
    {
        SplatterSceneSetup.Run();
        SplatterPreviewRender.Run(".harness/state/splatter-preview.png");
        SplatterSceneSetup.Run();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SplatterSceneSetup.ScenePath, true) };
        return BuildDevApk(SplatterApkPath);
    }

    [MenuItem("Mood Picker/Build Time Ritual APK")]
    public static bool BuildTimeRitualApk()
    {
        TimeRitualSceneSetup.Run();
        string savedId = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
        string savedName = PlayerSettings.productName;
        var savedScenes = EditorBuildSettings.scenes;
        try
        {
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, TimeRitualPackage);
            PlayerSettings.productName = TimeRitualProductName;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(TimeRitualSceneSetup.ScenePath, true) };
            return BuildDevApk(TimeRitualApkPath);
        }
        finally
        {
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, savedId);
            PlayerSettings.productName = savedName;
            EditorBuildSettings.scenes = savedScenes;
            AssetDatabase.SaveAssets();
        }
    }

    public const string TimeRitualReleaseUnsignedPath = "Builds/time-ritual-release-unsigned.apk";
    public const string TimeRitualReleasePackage = "com.reality404.timeritual";
    public const string TimeRitualReleaseProductName = "Ourglass";
    public const string RitualIconPath = "Assets/Ritual/Icon/ourglass-icon.png";
    public const string RitualIconBackPath = "Assets/Ritual/Icon/ourglass-icon-adaptive-back.png";
    public const string RitualIconForePath = "Assets/Ritual/Icon/ourglass-icon-adaptive-fore.png";
    static readonly PlatformIconKind[] IconKinds = { AndroidPlatformIconKind.Legacy, AndroidPlatformIconKind.Round, AndroidPlatformIconKind.Adaptive };

    static Texture2D IconTexture(string path)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter imp && (imp.textureCompression != TextureImporterCompression.Uncompressed || imp.mipmapEnabled || !imp.isReadable))
        {
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.isReadable = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Texture2D[][][] SaveIcons(UnityEditor.Build.NamedBuildTarget target)
    {
        return IconKinds.Select(k => PlayerSettings.GetPlatformIcons(target, k).Select(i => i.GetTextures()).ToArray()).ToArray();
    }

    static void RestoreIcons(UnityEditor.Build.NamedBuildTarget target, Texture2D[][][] saved)
    {
        for (int k = 0; k < IconKinds.Length; k++)
        {
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(target, IconKinds[k]);
            for (int i = 0; i < icons.Length && i < saved[k].Length; i++) icons[i].SetTextures(saved[k][i]);
            PlayerSettings.SetPlatformIcons(target, IconKinds[k], icons);
        }
    }

    public static bool ApplyRitualIcon(UnityEditor.Build.NamedBuildTarget target)
    {
        Texture2D icon = IconTexture(RitualIconPath);
        Texture2D back = IconTexture(RitualIconBackPath);
        Texture2D fore = IconTexture(RitualIconForePath);
        if (icon == null || back == null || fore == null)
        {
            Debug.LogError($"[MP] ritual icon missing icon={icon != null} back={back != null} fore={fore != null}");
            return false;
        }
        foreach (PlatformIconKind kind in IconKinds)
        {
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(target, kind);
            foreach (PlatformIcon i in icons)
            {
                if (kind == AndroidPlatformIconKind.Adaptive) i.SetTextures(back, fore);
                else i.SetTextures(icon);
            }
            PlayerSettings.SetPlatformIcons(target, kind, icons);
        }
        Debug.Log($"[MP] ritual icon applied {icon.width}px adaptive {back.width}px");
        return true;
    }

    static string Arg(string name, string fallback)
    {
        string[] a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return fallback;
    }

    [MenuItem("Mood Picker/Build Time Ritual Release APK")]
    public static bool BuildTimeRitualReleaseApk()
    {
        string package = Arg("-ritualPackage", TimeRitualReleasePackage);
        int versionCode = int.TryParse(Arg("-ritualVersionCode", "1"), out int vc) && vc > 0 ? vc : 1;
        string versionName = Arg("-ritualVersionName", "1.0." + versionCode);
        string productName = Arg("-ritualProductName", TimeRitualReleaseProductName);
        TimeRitualSceneSetup.Run();
        var target = UnityEditor.Build.NamedBuildTarget.Android;
        string savedId = PlayerSettings.GetApplicationIdentifier(target);
        string savedName = PlayerSettings.productName;
        string savedVersion = PlayerSettings.bundleVersion;
        int savedCode = PlayerSettings.Android.bundleVersionCode;
        var savedScenes = EditorBuildSettings.scenes;
        bool savedDev = EditorUserBuildSettings.development;
        Texture2D[][][] savedIcons = SaveIcons(target);
        try
        {
            if (!ApplyRitualIcon(target)) return false;
            PlayerSettings.SetApplicationIdentifier(target, package);
            PlayerSettings.productName = productName;
            PlayerSettings.bundleVersion = versionName;
            PlayerSettings.Android.bundleVersionCode = versionCode;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(TimeRitualSceneSetup.ScenePath, true) };
            EditorUserBuildSettings.development = false;
            Directory.CreateDirectory(Path.GetDirectoryName(TimeRitualReleaseUnsignedPath));
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { TimeRitualSceneSetup.ScenePath },
                locationPathName = TimeRitualReleaseUnsignedPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[MP] release build result={s.result} errors={s.totalErrors} size={s.totalSize / (1024 * 1024)}MB time={s.totalTime.TotalSeconds:0}s package={package} label={productName} versionCode={versionCode} versionName={versionName} path={TimeRitualReleaseUnsignedPath}");
            return s.result == BuildResult.Succeeded;
        }
        finally
        {
            PlayerSettings.SetApplicationIdentifier(target, savedId);
            PlayerSettings.productName = savedName;
            PlayerSettings.bundleVersion = savedVersion;
            PlayerSettings.Android.bundleVersionCode = savedCode;
            EditorBuildSettings.scenes = savedScenes;
            EditorUserBuildSettings.development = savedDev;
            RestoreIcons(target, savedIcons);
            AssetDatabase.SaveAssets();
        }
    }

    public static void BuildTimeRitualReleaseApkBatch()
    {
        _deadline = EditorApplication.timeSinceStartup + 120;
        _job = BuildTimeRitualReleaseApk;
        EditorApplication.update += Wait;
    }

    public static void BuildTimeRitualApkBatch()
    {
        _deadline = EditorApplication.timeSinceStartup + 120;
        _job = BuildTimeRitualApk;
        EditorApplication.update += Wait;
    }

    [MenuItem("Mood Picker/Experiments/Build DeskKeyboard APK")]
    public static bool BuildDeskKeyboardApk()
    {
        var saved = EditorBuildSettings.scenes;
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(DeskKeyboardSceneSetup.ScenePath, true) };
        try { return BuildDevApk(DeskKeyboardApkPath); }
        finally { EditorBuildSettings.scenes = saved; }
    }

    [MenuItem("Mood Picker/Build Catch APK")]
    public static bool BuildCatchApk()
    {
        var saved = EditorBuildSettings.scenes;
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(CatchSceneSetup.ScenePath, true) };
        try { return BuildDevApk(CatchApkPath); }
        finally { EditorBuildSettings.scenes = saved; }
    }

    static bool BuildDevApk(string apkPath)
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(apkPath));
        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = apkPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.Development,
        };
        EditorUserBuildSettings.development = true;
        BuildReport report = BuildPipeline.BuildPlayer(opts);
        var s = report.summary;
        Debug.Log($"[MP] build result={s.result} errors={s.totalErrors} size={s.totalSize / (1024 * 1024)}MB time={s.totalTime.TotalSeconds:0}s path={apkPath}");
        return s.result == BuildResult.Succeeded;
    }

    static double _deadline;
    static Func<bool> _job;

    public static void BuildDevApkBatch()
    {
        _deadline = EditorApplication.timeSinceStartup + 120;
        _job = BuildDevApk;
        EditorApplication.update += Wait;
    }

    public static void BuildDeskKeyboardApkBatch()
    {
        _deadline = EditorApplication.timeSinceStartup + 120;
        _job = BuildDeskKeyboardApk;
        EditorApplication.update += Wait;
    }

    public static void BuildCatchApkBatch()
    {
        _deadline = EditorApplication.timeSinceStartup + 120;
        _job = BuildCatchApk;
        EditorApplication.update += Wait;
    }

    public static void BuildSplatterApkBatch()
    {
        _deadline = EditorApplication.timeSinceStartup + 120;
        _job = BuildSplatterApk;
        EditorApplication.update += Wait;
    }

    static void Wait()
    {
        if (OVRProjectConfig.CachedProjectConfig == null && EditorApplication.timeSinceStartup < _deadline) return;
        EditorApplication.update -= Wait;
        bool ok = false;
        try { ok = _job(); }
        catch (Exception e) { Debug.LogException(e); }
        EditorApplication.Exit(ok ? 0 : 1);
    }
}
