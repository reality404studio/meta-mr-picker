// 배치: Unity -batchmode -projectPath . -buildTarget Android -executeMethod VideoSplatPipeline.BuildSlotFromEnvBatch
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MoodPicker.Splatter;
using Pigment;
using UnityEditor;
using UnityEngine;

public static class VideoSplatPipeline
{
    public const string DefaultSlotDir = "Assets/Textures/Splatter";
    public const string DefaultMatPath = "Assets/Materials/Splatter/LetterSplat.mat";

    public static void BuildSlotFromEnvBatch()
    {
        string slotName = Environment.GetEnvironmentVariable("VIDEO_SPLAT_NAME");
        string framesDir = Environment.GetEnvironmentVariable("VIDEO_SPLAT_FRAMES_DIR");
        int tileSize = 256;
        float fps = 30f;

        if (int.TryParse(Environment.GetEnvironmentVariable("VIDEO_SPLAT_TILE"), out int parsedTile) && parsedTile >= 32)
            tileSize = parsedTile;
        if (float.TryParse(Environment.GetEnvironmentVariable("VIDEO_SPLAT_FPS"), out float parsedFps) && parsedFps >= 1f)
            fps = parsedFps;

        if (string.IsNullOrWhiteSpace(slotName) || string.IsNullOrWhiteSpace(framesDir))
        {
            Debug.LogError("[VideoSplat] VIDEO_SPLAT_NAME and VIDEO_SPLAT_FRAMES_DIR must be set");
            EditorApplication.Exit(1);
            return;
        }

        int code = 0;
        try
        {
            SplatPigmentSlotAsset asset = BuildSlotAssetFromFrames(slotName, framesDir, tileSize, fps, forceRebuild: true);
            if (asset == null) code = 1;
            AssetDatabase.SaveAssets();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            code = 1;
        }
        EditorApplication.Exit(code);
    }

    public static void BuildLightLayers(SplatPigmentSlotAsset slot, string outputDir = DefaultSlotDir)
    {
        string atlasPath = AssetDatabase.GetAssetPath(slot.atlasTexture);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        ImageConversion.LoadImage(tex, File.ReadAllBytes(atlasPath), false);
        Color32[] px = tex.GetPixels32();
        var rgba = new byte[px.Length * 4];
        for (int i = 0; i < px.Length; i++)
        {
            rgba[i * 4] = px[i].r;
            rgba[i * 4 + 1] = px[i].g;
            rgba[i * 4 + 2] = px[i].b;
            rgba[i * 4 + 3] = 255;
        }
        FlipbookLayout layout = slot.ToLayout();
        LightLayersResult result = LightLayers.Build(rgba, layout);
        UnityEngine.Object.DestroyImmediate(tex);

        string calmPath = $"{outputDir}/{slot.sourceName}_Calm.png";
        string sparkPath = $"{outputDir}/{slot.sourceName}_Spark.png";
        WritePng(calmPath, result.CalmRgba, layout.Tile, layout.Tile, TextureWrapMode.Repeat);
        WritePng(sparkPath, result.SparkRgba, layout.Width, layout.Height, TextureWrapMode.Clamp);
        slot.calmTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(calmPath);
        slot.sparkTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(sparkPath);
        EditorUtility.SetDirty(slot);
        Debug.Log($"[VideoSplat] {slot.sourceName}: light layers calm={layout.Tile}px spark={layout.Width}x{layout.Height} cover={result.SparkCover:P2}");
    }

    static void WritePng(string path, byte[] rgba, int w, int h, TextureWrapMode wrap)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        t.SetPixelData(rgba, 0);
        t.Apply(false, false);
        File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }

    public static SplatPigmentSlotAsset BuildSlotAssetFromFrames(
        string assetBaseName,
        string framesDir,
        int tileSize = 256,
        float fps = 30f,
        bool forceRebuild = false,
        string outputDir = DefaultSlotDir)
    {
        Directory.CreateDirectory(outputDir);
        string assetPath = $"{outputDir}/{assetBaseName}.asset";
        string pngPath = $"{outputDir}/{assetBaseName}_Atlas.png";

        if (!forceRebuild)
        {
            var existing = AssetDatabase.LoadAssetAtPath<SplatPigmentSlotAsset>(assetPath);
            if (existing != null && existing.atlasTexture != null && existing.frames > 0)
            {
                return existing;
            }
        }

        if (!Directory.Exists(framesDir))
        {
            return AssetDatabase.LoadAssetAtPath<SplatPigmentSlotAsset>(assetPath);
        }

        string[] files = Directory.GetFiles(framesDir, "f_*.png").OrderBy(f => f).ToArray();
        if (files.Length < 6)
        {
            return AssetDatabase.LoadAssetAtPath<SplatPigmentSlotAsset>(assetPath);
        }

        var rgbaList = new List<byte[]>(files.Length);
        var grayList = new List<float[]>(files.Length);
        var tmpTex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        try
        {
            foreach (string file in files)
            {
                byte[] pngBytes = File.ReadAllBytes(file);
                ImageConversion.LoadImage(tmpTex, pngBytes, false);
                Color32[] c32 = tmpTex.GetPixels32();
                var rgba = new byte[tileSize * tileSize * 4];
                int count = Math.Min(c32.Length, tileSize * tileSize);
                for (int i = 0; i < count; i++)
                {
                    int p = i * 4;
                    rgba[p] = c32[i].r;
                    rgba[p + 1] = c32[i].g;
                    rgba[p + 2] = c32[i].b;
                    rgba[p + 3] = 255;
                }
                rgbaList.Add(rgba);
                grayList.Add(PigmentAtlasBuilder.DownsampleGray64(rgba, tileSize));
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(tmpTex);
        }

        BuiltPigmentSlot built = PigmentAtlasBuilder.BuildFromFrames(
            rgbaList,
            grayList,
            tileSize,
            fps,
            1.0f,
            3.0f,
            8,
            48);

        var atlasTex = new Texture2D(built.Layout.Width, built.Layout.Height, TextureFormat.RGBA32, false, true);
        atlasTex.SetPixelData(built.Layout.Width > 0 ? built.AtlasRgba : Array.Empty<byte>(), 0);
        atlasTex.Apply(false, false);
        File.WriteAllBytes(pngPath, atlasTex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(atlasTex);

        AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
        if (importer != null)
        {
            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        var importedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        var slotAsset = AssetDatabase.LoadAssetAtPath<SplatPigmentSlotAsset>(assetPath);
        if (slotAsset == null)
        {
            slotAsset = ScriptableObject.CreateInstance<SplatPigmentSlotAsset>();
            AssetDatabase.CreateAsset(slotAsset, assetPath);
        }

        slotAsset.sourceName = assetBaseName;
        slotAsset.atlasTexture = importedTex;
        slotAsset.frames = built.Layout.Frames;
        slotAsset.cols = built.Layout.Cols;
        slotAsset.rows = built.Layout.Rows;
        slotAsset.tile = built.Layout.Tile;
        slotAsset.fps = built.Layout.Fps;
        slotAsset.loopStart = built.Loop.Start;
        slotAsset.loopEnd = built.Loop.End;
        slotAsset.loopScore = built.Loop.Score;
        slotAsset.seamErrorBefore = built.SeamErrorBefore;
        slotAsset.seamErrorAfter = built.SeamErrorAfter;
        slotAsset.lightTone = new Color(built.Palette.LightR, built.Palette.LightG, built.Palette.LightB, 1f);
        slotAsset.shadowTone = new Color(built.Palette.ShadowR, built.Palette.ShadowG, built.Palette.ShadowB, 1f);
        slotAsset.threshold = built.Palette.Threshold;
        EditorUtility.SetDirty(slotAsset);

        Debug.Log($"[VideoSplat] {assetBaseName}: srcFrames={files.Length} loop=[{built.Loop.Start}..{built.Loop.End}] len={built.Loop.Length} ({built.Loop.Length / fps:F2}s) score={built.Loop.Score:F2} atlas={built.Layout.Cols}x{built.Layout.Rows} ({built.Layout.Frames}f @ {built.Layout.Fps:F1}fps) seam={built.SeamErrorBefore:F2}->{built.SeamErrorAfter:F2} th={built.Palette.Threshold:F3}");
        return slotAsset;
    }
}
