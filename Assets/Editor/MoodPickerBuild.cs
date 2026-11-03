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

    [MenuItem("Mood Picker/Build Dev APK")]
    public static bool BuildDevApk()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = ApkPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.Development,
        };
        EditorUserBuildSettings.development = true;
        BuildReport report = BuildPipeline.BuildPlayer(opts);
        var s = report.summary;
        Debug.Log($"[MP] build result={s.result} errors={s.totalErrors} size={s.totalSize / (1024 * 1024)}MB time={s.totalTime.TotalSeconds:0}s path={ApkPath}");
        return s.result == BuildResult.Succeeded;
    }

    static double _deadline;
    public static void BuildDevApkBatch()
    {
        _deadline = EditorApplication.timeSinceStartup + 120;
        EditorApplication.update += Wait;
    }

    static void Wait()
    {
        if (OVRProjectConfig.CachedProjectConfig == null && EditorApplication.timeSinceStartup < _deadline) return;
        EditorApplication.update -= Wait;
        bool ok = false;
        try { ok = BuildDevApk(); }
        catch (Exception e) { Debug.LogException(e); }
        EditorApplication.Exit(ok ? 0 : 1);
    }
}
