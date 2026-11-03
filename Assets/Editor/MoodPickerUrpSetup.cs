using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class MoodPickerUrpSetup
{
    const string Dir = "Assets/Settings";
    const string AssetPath = Dir + "/URP_Quest.asset";
    const string RendererPath = Dir + "/URP_Quest_Renderer.asset";

    [MenuItem("Mood Picker/Ensure URP Asset")]
    public static UniversalRenderPipelineAsset Ensure()
    {
        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
        if (asset == null)
        {
            System.IO.Directory.CreateDirectory(Dir);
            var data = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(data, RendererPath);
            ResourceReloader.ReloadAllNullIn(data, UniversalRenderPipelineAsset.packagePath);
            asset = UniversalRenderPipelineAsset.Create(data);
            AssetDatabase.CreateAsset(asset, AssetPath);
        }

        asset.msaaSampleCount = 4;
        asset.supportsHDR = false;
        asset.renderScale = 1f;
        asset.supportsCameraDepthTexture = false;
        asset.supportsCameraOpaqueTexture = false;
        EditorUtility.SetDirty(asset);

        GraphicsSettings.defaultRenderPipeline = asset;
        int current = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = asset;
        }
        QualitySettings.SetQualityLevel(current, false);

        AssetDatabase.SaveAssets();
        Debug.Log($"[MP] URP asset: {AssetPath} msaa={asset.msaaSampleCount} hdr={asset.supportsHDR}");
        return asset;
    }
}
