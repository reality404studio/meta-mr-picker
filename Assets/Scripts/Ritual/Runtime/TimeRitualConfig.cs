using UnityEngine;

namespace MoodPicker.Ritual
{
    public enum MergedShape { Rod = 0, Blob = 1 }
    public enum GlowStyle { Seep = 0, Petals = 1 }
    public enum FormStyle { Gather = 0, Flow = 1 }

    [CreateAssetMenu(menuName = "Mood Picker/Time Ritual Config")]
    public sealed class TimeRitualConfig : ScriptableObject
    {
        [Header("Units")]
        public float unitLength = 0.020f;
        public int maxUnits = 24;
        [Range(0f, 0.45f)] public float hysteresis = 0.15f;

        [Header("Heaps")]
        public float heapDiameter = 0.06f;
        public float heapSpacing = 0.12f;
        public int heapSplats = 1800;
        public float heapTextureSize = 0.18f;
        public float grabRadius = 0.042f;
        public float heapPulse = 0.35f;
        public float heapHover = 0.18f;
        public float heapHoverRange = 0.06f;
        public float heapSpin = 1f;
        public float heapSpinDrag = 2.5f;

        [Header("Pinch")]
        public float pinchStart = 0.018f;
        public float pinchEnd = 0.028f;

        [Header("Piece")]
        public float barRadius = 0.014f;
        public int splatsPerRing = 32;
        public int ringsPerUnit = 12;
        public int capSplats = 12;
        [Range(0.5f, 1.6f)] public float splatOverlap = 1.18f;
        public float pieceTextureSize = 0.055f;
        [Range(0f, 0.8f)] public float node10 = 0.2f;
        [Range(0f, 0.8f)] public float node30 = 0.36f;
        [Range(0f, 0.8f)] public float node60 = 0.5f;
        [Range(1f, 6f)] public float nodeFalloff = 3f;
        public float nodeMeltSeconds = 0.5f;
        public float nodeRiseSeconds = 0.12f;
        public bool nodesAlwaysVisible;
        public float unitFlash = 0.9f;
        public float springStiffness = 170f;
        public float springDamping = 15f;
        [Range(0f, 0.8f)] public float stiffnessJitter = 0.35f;
        [Range(0f, 1f)] public float carry = 0.78f;
        [Range(0f, 1f)] public float stretchShare = 0.6f;
        public float glowDecay = 2.2f;
        public float unitGrowRate = 22f;
        public float hoverBob = 0.002f;
        public float hoverBobSpeed = 0.7f;
        public float pieceIntensity = 1.15f;
        public float releaseDrift = 0f;
        public float threadBend = 1f;
        public float threadFollow = 5f;
        [Range(0f, 0.6f)] public float threadBendMax = 0.35f;
        public float threadRelax = 3f;

        [Header("Hold")]
        public float holdReach = 0.052f;
        public float holdFollow = 32f;
        [Range(0f, 1f)] public float holdRotate = 0.90f;
        public float rollGlow = 0.35f;
        public float rollGlowMax = 1f;

        [Header("Crumple")]
        public bool crumpleEnabled = true;
        public float crumpleDistance = 0.05f;
        public float crumpleMinSpan = 0.07f;
        [Range(0f, 1f)] public float squeezeShorten = 0.7f;
        public float squeezeWrinkle = 1.1f;
        [Range(0f, 0.5f)] public float crumpleLump = 0.24f;
        public float compactMinSpan = 0.025f;
        public float compactTravel = 0.015f;
        [Range(0.1f, 0.8f)] public float compactShare = 0.3f;
        [Range(0.5f, 0.98f)] public float compactStep = 0.85f;
        [Range(0.3f, 1f)] public float compactMin = 0.7f;
        public float compactLumpStep = 0.05f;
        [Range(0f, 0.6f)] public float compactLumpMax = 0.4f;
        [Range(0f, 0.8f)] public float blobSquash = 0.4f;

        [Header("Merge")]
        public float mergeDistance = 0.055f;
        public float mergeMagnetRange = 0.06f;
        public float mergeMagnetSpeed = 0.32f;
        public float mergeTouchDistance = 0.012f;
        [Range(0f, 1f)] public float mergeMagnetGlow = 0.35f;
        public float mergeSeconds = 0.5f;
        public MergedShape mergedShape = MergedShape.Blob;
        public float mergeDelaySpread = 0.26f;
        public float mergeStiffness = 110f;
        public float mergeDamping = 13f;
        public float repelDistance = 0.012f;
        public float repelPush = 0.9f;
        public float pieceDrag = 4f;
        public float chimeInterval = 0.055f;

        [Header("Sweep")]
        public bool sweepEnabled = false;
        public float sweepSpeed = 0.3f;
        public float sweepRadius = 0.15f;
        public float sweepPull = 1.6f;
        public float sweepOpenFinger = 0.075f;

        [Header("Jar")]
        public JarShape jar = JarShape.Default;
        public float hoursWhenFull = 24f;
        public Vector3 jarOffset = new Vector3(0.17f, -0.3f, -0.06f);
        public float jarGrabMargin = 0.03f;
        public int glassSplats = 5600;
        public float glassSplatRadius = 0.0018f;
        public float glassRim = 0.42f;
        public float glassSpec = 0.58f;
        public float glassBody = 0f;

        [Header("Sand")]
        public float grainSpacing = 0.00177f;
        public float grainRadius = 0.00124f;
        public int maxGrains = 20000;
        public int capGrains = 2200;
        public float sandTextureSize = 0.106f;
        public bool sandDetach = true;
        public float layerBump = 0.0025f;
        public float moundHeight = 0.004f;
        public float landGlow = 0.85f;
        public float landGlowDecay = 1.8f;

        [Header("Pour")]
        public float pourZoneHeight = 0.095f;
        public float pourZoneMargin = 0.025f;
        public float pourSecondsMin = 0.9f;
        public float pourSecondsMax = 1.8f;
        public float pourSecondsPerUnit = 0.04f;
        public float grainFlight = 0.5f;
        public float spoutHeight = 0.035f;
        public float streamRadius = 0.007f;
        public float rejectBounce = 0.45f;
        public float roomNoteSeconds = 2.5f;

        [Header("Wax")]
        public float waxAppearDelay = 1.2f;
        public float waxHover = 0.0015f;
        public float waxRadius = 0.015f;
        public int waxSplats = 6400;
        public float sealRadius = 0.0245f;
        public float sealThickness = 0.0035f;
        public float pressDepth = 0.008f;
        public float pressRadius = 0.038f;
        public float holdSeconds = 1.0f;
        public float recoverSeconds = 0.6f;
        public float stampDepth = 0.00125f;
        public float stampSeconds = 0.35f;
        public int coverSplats = 4200;
        public int twineSplats = 420;
        public float waxGlow = 0.95f;
        public Vector3 waxStickOffset = new Vector3(0.085f, 0f, -0.045f);
        public float waxStickYaw = -35f;
        public float waxStickLength = 0.075f;
        public float waxStickRadius = 0.0055f;
        public int waxStickSplats = 1100;
        public float waxDrawLength = 0.035f;
        public float waxDropRadius = 0.009f;
        public float waxDripHeight = 0.14f;

        [Header("Close")]
        public float breathRise = 0.65f;
        public float breathSeconds = 2.6f;
        public float sealedIntensity = 0.96f;
        public float idleBreath = 0.06f;
        public float idleBreathSeconds = 6.5f;
        public float heapFadeSeconds = 1.4f;
        public float glowBloomSeconds = 2.5f;
        public float glowRadius = 0.23f;
        public GlowStyle glowStyle = GlowStyle.Seep;
        public int glowSeepSplats = 96;
        [Range(0f, 1f)] public float glowSeepGain = 0.28f;
        public int glowSeepMotes = 20;
        public int glowPetals = 84;
        public int glowMotes = 56;
        public float glowIntensity = 0.58f;
        public bool deskPool = true;
        [Range(0f, 2f)] public float deskPoolGain = 1f;
        public float deskPoolFadeHeight = 0.08f;
        [Range(0f, 1f)] public float deskPoolOpenMin = 0.3f;
        [Range(0f, 1f)] public float deskPoolOpenMax = 0.75f;
        public float deskPoolSwell = 0.8f;
        public bool guideTrails = false;
        [Range(0.1f, 2f)] public float guideIntensity = 0.85f;
        public float baseDim = -0.08f;
        public float sealDim = -0.28f;

        [Header("Room Light")]
        public bool roomDarkFromStart = true;
        [Range(0.05f, 1f)] public float roomLight = 0.38f;
        [Range(0.05f, 1f)] public float roomLightSealed = 0.32f;
        public float roomLightRate = 0.22f;

        [Header("Pull Out")]
        [Range(0f, 0.3f)] public float threadSag = 0.06f;
        public FormStyle formStyle = FormStyle.Gather;
        public float formFlowSeconds = 0.6f;
        public float formMinRadius = 0.022f;
        public bool floatAway = false;
        public float floatSpeed = 0.07f;
        public float floatDistance = 0.46f;
        public float floatSpread = 55f;
        public float floatLow = -0.05f;
        public float floatHigh = 0.10f;
        public bool springSwarm = true;
        public int springSplats = 520;
        public float springSpin = 0.9f;
        public float springLoose = 1.35f;
        public float springGrain = 0.0024f;
        [Range(0f, 1f)] public float springMotion = 1f;
        public bool pullHint = true;
        public float hintIdleSeconds = 4.5f;
        public float hintLean = 0.02f;

        [Header("Scatter")]
        [Range(1f, 3.5f)] public float scatterAspectMax = 1.6f;

        [Header("Halo")]
        public bool halos = true;
        [Range(0f, 2f)] public float haloIntensity = 0.55f;
        public float haloCore = 1.8f;
        public float haloWide = 3.4f;
        [Range(0f, 1.5f)] public float haloBehind = 0.6f;
        [Range(0f, 1f)] public float haloJar = 0.45f;
        public bool handLight = true;
        public float handLightRange = 0.12f;
        [Range(0f, 2f)] public float handLightIntensity = 0.7f;
        public float handLightSize = 0.011f;

        [Header("Labels")]
        public bool timeLabels = false;
        public float labelTagScale = 0.42f;
        public float labelTagHeight = 0.158f;
        public float labelTagSide = 0.022f;
        public float labelTagTilt = 7f;
        public float labelSwitchSeconds = 0.45f;

        [Header("Performance")]
        public int cpuLevel = 1;
        public int gpuLevel = 1;
        public bool dynamicResolution = true;
        public float minResolutionScale = 0.8f;
        public float maxResolutionScale = 1f;
        public float devRenderScale = 1f;
        public bool governor = true;
        public float frameBudgetMs = 13.9f;
        [Range(0.3f, 1f)] public float minQuality = 0.5f;
        public float governorWarmup = 3f;

        [Header("Sound")]
        [Range(0f, 1f)] public float volume = 0.7f;

        [Header("Dev")]
        public bool showMinutesLabel = true;
        public bool showTotals = true;
        public int resetCounter;

        [Header("Placement")]
        public float placeDistance = 0.42f;
        public float placeBelowEye = 0.14f;

        public int SplatsPerUnit => Mathf.Max(1, ringsPerUnit) * Mathf.Max(6, splatsPerRing);

        public float NodeDepth(int boundary)
        {
            switch (TimeUnits.NodeStrength(boundary))
            {
                case 2: return node60;
                case 1: return node30;
                default: return node10;
            }
        }
    }
}
