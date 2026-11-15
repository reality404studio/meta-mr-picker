using MoodPicker.Splatter;
using UnityEngine;

namespace MoodPicker.Ritual
{
    public enum BeadShape { Droplet = 0, Heart = 1, Star = 2, Diamond = 3, Crescent = 4, Feather = 7 }
    public enum PieceFeel { Jelly = 0, Water = 1, Fur = 2 }
    public enum SwarmMotion { Orbit = 0, Settle = 1, Ripple = 2, Wander = 3, Bounce = 4 }

    [CreateAssetMenu(menuName = "Mood Picker/Time Category")]
    public sealed class TimeCategoryAsset : ScriptableObject
    {
        public string id = "work";
        public string displayKo = "몰입";
        public string displayEn = "Focused";
        public string symbol = "*";
        public string memoryEn = "Deep Dive";
        public BeadShape beadShape = BeadShape.Star;
        public PieceFeel initialFeel = PieceFeel.Jelly;
        public SwarmMotion swarmMotion = SwarmMotion.Orbit;
        public SplatPigmentSlotAsset look;

        [Header("Node")]
        [Range(0f, 2f)] public float nodeSqueeze = 1f;
        [Range(0f, 2f)] public float nodeGlow = 0.55f;
        [Range(0.3f, 1.6f)] public float nodeGrain = 0.75f;
        [Range(0f, 1f)] public float nodeTwinkle = 0f;
        [Range(0.2f, 20f)] public float nodeTwinkleSpeed = 6f;

        [Header("Sand")]
        [Range(0f, 3f)] public float sandGlint = 0.9f;
        [Range(0f, 4f)] public float sandGlintSpeed = 0.6f;
        [Range(0.3f, 2.5f)] public float sandIntensity = 1.1f;
        [Range(0f, 3f)] public float sandSparkle = 0.9f;

        [Header("Seal")]
        public Color waxColor = new Color(0.55f, 0.16f, 0.12f, 1f);
        public Color glowColor = new Color(1f, 0.82f, 0.55f, 1f);

        public BeadShape EffectiveShape
        {
            get
            {
                if (!string.IsNullOrEmpty(id))
                {
                    if (id == "sleep" && beadShape == BeadShape.Star) return BeadShape.Crescent;
                    if (id == "move" && beadShape == BeadShape.Star) return BeadShape.Droplet;
                    if (id == "play" && beadShape == BeadShape.Star) return BeadShape.Heart;
                    if (id == "drift" && beadShape == BeadShape.Star) return BeadShape.Diamond;
                }
                return beadShape;
            }
        }

        public PieceFeel EffectiveFeel
        {
            get
            {
                if (!string.IsNullOrEmpty(id) && initialFeel == PieceFeel.Jelly)
                {
                    if (id == "sleep" || id == "drift") return PieceFeel.Fur;
                    if (id == "move") return PieceFeel.Water;
                }
                return initialFeel;
            }
        }

        public bool UsesWindChime
        {
            get
            {
                BeadShape s = EffectiveShape;
                return id == "play" || id == "drift" || s == BeadShape.Heart || s == BeadShape.Diamond;
            }
        }

        public string EffectiveTitleEn
        {
            get
            {
                if (!string.IsNullOrEmpty(displayEn)
                    && displayEn != "Sleep" && displayEn != "Move" && displayEn != "Work" && displayEn != "Play" && displayEn != "Drift"
                    && (displayEn != "Focused" || id == "work"))
                    return displayEn;
                switch (id)
                {
                    case "sleep": return "Rested";
                    case "move": return "Moved";
                    case "play": return "Enjoyed";
                    case "drift": return "Explored";
                    default: return "Focused";
                }
            }
        }

        public string EffectiveSymbol
        {
            get
            {
                if (!string.IsNullOrEmpty(symbol) && (symbol != "*" || id == "work")) return symbol;
                switch (id)
                {
                    case "sleep": return "°";
                    case "move": return "•";
                    case "play": return "+";
                    case "drift": return "·";
                    default: return "*";
                }
            }
        }

        public string EffectiveMemoryEn
        {
            get
            {
                if (!string.IsNullOrEmpty(memoryEn)
                    && memoryEn != "Deep Rest" && memoryEn != "Vital Flow" && memoryEn != "Sunlit Walk"
                    && memoryEn != "Deep Focus" && memoryEn != "Bright Joy" && memoryEn != "Autumn Joy"
                    && memoryEn != "Quiet Wonder" && memoryEn != "Quiet Reverie"
                    && (memoryEn != "Deep Dive" || id == "work"))
                    return memoryEn;
                switch (id)
                {
                    case "sleep": return "Quiet Rest";
                    case "move": return "Fresh Breath";
                    case "play": return "Warm Joy";
                    case "drift": return "Trial & Wonder";
                    default: return "Deep Dive";
                }
            }
        }
    }
}
