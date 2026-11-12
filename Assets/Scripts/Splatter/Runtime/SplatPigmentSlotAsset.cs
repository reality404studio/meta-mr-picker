using Pigment;
using UnityEngine;

namespace MoodPicker.Splatter
{
    public sealed class SplatPigmentSlotAsset : ScriptableObject
    {
        public string sourceName;
        public string capturedAt;
        public int shaderMode;
        public Texture2D calmTexture;
        public Texture2D sparkTexture;
        [Range(0f, 1f)] public float glintFromTexture;
        public Texture2D atlasTexture;
        public int frames;
        public int cols;
        public int rows;
        public int tile;
        public float fps;
        public int loopStart;
        public int loopEnd;
        public float loopScore;
        public float seamErrorBefore;
        public float seamErrorAfter;
        public Color lightTone;
        public Color shadowTone;
        public float threshold;

        public FlipbookLayout ToLayout()
        {
            return new FlipbookLayout
            {
                Frames = frames,
                Cols = cols,
                Rows = rows,
                Tile = tile,
                Fps = fps
            };
        }

        public TwoTonePalette ToPalette()
        {
            return new TwoTonePalette
            {
                LightR = lightTone.r,
                LightG = lightTone.g,
                LightB = lightTone.b,
                ShadowR = shadowTone.r,
                ShadowG = shadowTone.g,
                ShadowB = shadowTone.b,
                Threshold = threshold
            };
        }
    }
}
