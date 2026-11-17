using System;
using MoodPicker.Splatter;
using MoodPicker.Splatter.Sim;
using UnityEngine;

namespace MoodPicker.Ritual
{
    public static class TimeRitualLook
    {
        public const float RestLift = 0.0002f;
        public const string LeanKeyword = "SPLAT_RITUAL";

        public static SplatLook Piece(SplatTuning tuning, PieceFeel feel = PieceFeel.Jelly)
        {
            SplatLook look = tuning != null ? tuning.ToLook() : SplatLook.Default;
            switch (feel)
            {
                case PieceFeel.Water:
                {
                    SplatFeelLook water = SplatFeelMap.Look(SplatFeel.Water);
                    look.grain = water.grain;
                    look.stretchMax = water.stretchMax;
                    look.stretchSeconds = water.stretchSeconds;
                    look.fluff = 0f;
                    look.comb = 0f;
                    look.detachDistance = water.detach;
                    look.flowAmplitude = 0.0008f;
                    look.flowSpeed = 1.6f;
                    look.overlay = 1f;
                    look.sparkle = 0.6f;
                    look.glowGain = 0.35f;
                    break;
                }
                default:
                {
                    SplatFeelLook jelly = SplatFeelMap.Look(SplatFeel.Jelly);
                    look.grain = jelly.grain;
                    look.stretchMax = jelly.stretchMax;
                    look.stretchSeconds = jelly.stretchSeconds;
                    look.fluff = 0f;
                    look.comb = 0f;
                    look.detachDistance = MathF.Max(jelly.detach, 0.45f);
                    look.flowAmplitude = 0.0020f;
                    look.flowSpeed = 2.2f;
                    look.overlay = 0.16f;
                    look.sparkle = 0.55f;
                    look.glowGain = 0.55f;
                    break;
                }
            }
            return look;
        }

        public static string FeelName(PieceFeel feel)
        {
            switch (feel)
            {
                case PieceFeel.Water: return "Droplet";
                default: return "Jelly";
            }
        }

        public static SplatLook Sand(SplatTuning tuning, TimeCategoryAsset category, bool detach, float grainScale)
        {
            SplatLook look = tuning != null ? tuning.ToLook() : SplatLook.Default;
            SplatFeelLook sand = SplatFeelMap.Look(SplatFeel.Sand);
            look.grain = Mathf.Max(0.2f, grainScale);
            look.stretchMax = 1.6f;
            look.stretchSeconds = 0.004f;
            look.fluff = 0f;
            look.flowAmplitude = 0f;
            look.detachDistance = detach ? 1e-4f : sand.detach + 1f;
            look.sparkle = category != null ? category.sandSparkle : 0.9f;
            look.glintAmount = category != null ? category.sandGlint : 0.9f;
            look.glintSpeed = category != null ? category.sandGlintSpeed : 0.6f;
            look.glintScale = 900f;
            look.glintRough = 0.5f;
            look.glintSharpness = 90f;
            look.glintSize = 0.35f;
            look.glintMaterial = 0.75f;
            return look;
        }
    }

    public sealed class RitualSurface
    {
        static readonly int AspectMaxId = Shader.PropertyToID("_AspectMax");
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        public SplatSurfaceMaterial material;
        public SplatSurfaceMaterial previous;
        public SplatLook look = SplatLook.Default;
        public float sizeMeters = 0.18f;
        public float triplanarScale = 1.15f;
        public float intensity = 1.15f;
        public float aspectMax = 99f;
        public Vector4 apply = SplatSurfaceState.NoApply;
        public Vector4 applyShape = new Vector4(0.012f, 0f, 0.25f, 0f);
        bool _dirty = true;
        Texture _simBound;

        public SplatShaderMode Mode => (SplatShaderMode)(material != null ? material.ResolveMode(-1) : 0);

        public void MarkDirty()
        {
            _dirty = true;
        }

        public void Apply(Renderer renderer, Texture simTex, float time, float intensityScale)
        {
            if (renderer == null) return;
            float frame = material != null ? material.FrameAt(time) : 0f;
            if (_dirty || previous != null || simTex != _simBound)
            {
                var state = new SplatSurfaceState
                {
                    current = material,
                    currentFrame = frame,
                    previous = previous,
                    previousFrame = previous != null ? previous.FrameAt(time) : 0f,
                    apply = previous != null ? apply : SplatSurfaceState.NoApply,
                    applyShape = applyShape,
                    time = time,
                    triplanarScale = triplanarScale,
                    intensity = intensity * intensityScale,
                    outline = false
                };
                SplatLetterRenderer.WriteSurface(_block, state, look, Mode, sizeMeters, simTex);
                _block.SetFloat(AspectMaxId, aspectMax);
                _simBound = simTex;
                _dirty = false;
            }
            else
            {
                _block.SetFloat(SplatLetterRenderer.PropFrame, frame);
                _block.SetFloat(SplatLetterRenderer.PropSplatTime, time);
                _block.SetFloat(SplatLetterRenderer.PropIntensity, intensity * intensityScale);
            }
            renderer.SetPropertyBlock(_block);
        }
    }
}
