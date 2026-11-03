// 가드레일 §1: Android IL2CPP + ARM64 + ASTC 를 명시적으로 고정한다 (batchmode -executeMethod 용).
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class MoodPickerProjectSettings
{
    public static void Apply()
    {
        var android = NamedBuildTarget.Android;
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
        AssetDatabase.SaveAssets();
        Debug.Log("[MP] project settings: IL2CPP, ARM64, ASTC, target=" + EditorUserBuildSettings.activeBuildTarget);
    }
}
