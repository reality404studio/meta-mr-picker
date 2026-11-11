using MoodPicker.Splatter.Sim;
using Pigment;
using UnityEngine;

namespace MoodPicker.Splatter
{
    public sealed class SplatSurfaceMaterial
    {
        public static readonly Vector4 WholeTexture = new Vector4(1f, 1f, 0f, 0f);

        public string label = "";
        public Texture texture;
        public FlipbookLayout layout = new FlipbookLayout { Frames = 1, Cols = 1, Rows = 1, Tile = 256, Fps = 1f };
        public TwoTonePalette palette;
        public bool mirrorTiled;
        public Vector4 crop = WholeTexture;
        public float gamma = 1f;
        public bool ownsTexture;
        public int mode = -1;
        public Texture calm;
        public Texture spark;
        public bool hasFeel;
        public SplatFeel feel;

        public int ResolveMode(int overrideMode)
        {
            if (overrideMode >= 0) return overrideMode;
            if (mode == (int)SplatShaderMode.Light && (calm == null || spark == null)) return 0;
            return mode >= 0 ? mode : 0;
        }

        public float FrameAt(float timeSeconds)
        {
            return mirrorTiled ? 0f : LetterPoseMath.SlotFrameContinuous(timeSeconds, layout.Fps, layout.Frames);
        }

        public static SplatSurfaceMaterial FromAsset(SplatPigmentSlotAsset asset)
        {
            if (asset == null || asset.atlasTexture == null) return null;
            return new SplatSurfaceMaterial
            {
                label = string.IsNullOrEmpty(asset.capturedAt) ? asset.sourceName : asset.capturedAt,
                texture = asset.atlasTexture,
                layout = asset.ToLayout(),
                palette = asset.ToPalette(),
                mode = asset.shaderMode,
                calm = asset.calmTexture,
                spark = asset.sparkTexture,
                hasFeel = asset.hasFeel,
                feel = asset.feel
            };
        }

        public static SplatSurfaceMaterial FromBuilt(BuiltPigmentSlot built, string label)
        {
            if (built == null) return null;
            var tex = new Texture2D(built.Layout.Width, built.Layout.Height, TextureFormat.RGBA32, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            tex.SetPixelData(built.AtlasRgba, 0);
            tex.Apply(false, false);
            return new SplatSurfaceMaterial { label = label, texture = tex, layout = built.Layout, palette = built.Palette, ownsTexture = true };
        }

        public static SplatSurfaceMaterial FromStill(Texture texture, string label, Vector4 crop, float gamma, bool ownsTexture)
        {
            return new SplatSurfaceMaterial
            {
                label = label,
                texture = texture,
                mirrorTiled = true,
                crop = crop,
                gamma = gamma,
                ownsTexture = ownsTexture
            };
        }

        public void Release()
        {
            if (!ownsTexture || texture == null) return;
            if (Application.isPlaying) Object.Destroy(texture);
            else Object.DestroyImmediate(texture);
            texture = null;
        }
    }
}
