// Unity -batchmode -projectPath . -buildTarget Android -executeMethod MoodPickerBuild.BuildDevApkBatch   (-quit 없이)
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class MoodPickerBuild
{
    public const string ApkPath = "Builds/mood-picker-dev.apk";
    public const string DeskKeyboardApkPath = "Builds/desk-keyboard-dev.apk";
    public const string CatchApkPath = "Builds/catch-dev.apk";

    [MenuItem("Mood Picker/Build Dev APK")]
    public static bool BuildDevApk() => BuildDevApk(ApkPath);

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
