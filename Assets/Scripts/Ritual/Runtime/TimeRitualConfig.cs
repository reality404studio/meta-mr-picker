using UnityEngine;

namespace MoodPicker.Ritual
{
    public enum MergedShape { Rod = 0, Blob = 1 }
    public enum GlowStyle { Seep = 0, Petals = 1 }
    public enum FormStyle { Gather = 0, Flow = 1 }
    public enum ClimaxStyle { Ring = 0, Shell = 1, Bloom = 2 }
    public enum ShelfStyle { Ledge = 0, Pedestals = 1 }
    public enum ClockCollapse { Crumble = 0, Burst = 1 }

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
        public bool sandSlosh = true;
        public float sloshBand = 0.015f;
        public float sloshRepose = 20f;
        public float sloshRange = 40f;
        public float sloshMaxShift = 0.009f;
        public float sloshLift = 0.8f;
        public float sloshFrequency = 1.6f;
        [Range(0.05f, 1f)] public float sloshDamping = 0.35f;
        public float sloshCoupling = 0.05f;
        public float sloshAccelMax = 30f;
        [Range(0f, 1f)] public float sandShiftVolume = 0.16f;
        public float sandShiftFullSpeed = 0.05f;
        public float sandShiftPitch = 0.8f;
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

        [Header("Clock")]
        public bool clockIntro = true;
        public float clockDistance = 0.55f;
        public float clockHeight = -0.04f;
        public float clockDigitHeight = 0.135f;
        public int clockGlyphsPerDigit = 135;
        public float clockGlyphSize = 0.0078f;
        public float clockDelay = 2.5f;
        public float clockTouchDepth = 0.035f;
        public float clockSilence = 0.8f;
        public ClockCollapse clockCollapse = ClockCollapse.Crumble;
        public float clockCollapseSeconds = 4.2f;
        public float clockSpread = 1.6f;

        [Header("Shelf")]
        public bool shelf = true;
        public bool shelfDemo = true;
        public int shelfDemoDays = 2;
        public string shelfFile = "ritual-days.json";
        public ShelfStyle shelfStyle = ShelfStyle.Pedestals;
        public int shelfSlots = 5;
        public int shelfTodaySlot = 2;
        public float shelfDistance = 0.58f;
        public float shelfLift = 0.16f;
        public float shelfSpacingDeg = 16f;
        public float shelfDepth = 0.13f;
        public float shelfSnapDistance = 0.12f;
        public float shelfReleaseDistance = 0.22f;
        [Range(0.2f, 1f)] public float shelfDepthTolerance = 0.5f;
        public float shelfLightBack = 3f;
        [Range(0.05f, 1f)] public float afterShelfLight = 0.34f;
        [Range(0f, 1f)] public float afterShelfWarmth = 1f;
        public Color warmTint = new Color(1f, 0.78f, 0.5f, 1f);
        [Range(0f, 1f)] public float warmSaturation = 0.45f;
        [Range(0.5f, 2f)] public float warmGamma = 1.05f;
        [Range(0f, 2f)] public float shelfLight = 1f;
        [Range(0f, 0.4f)] public float shelfFrost = 0.1f;
        [Range(0f, 1.5f)] public float shelfMemoryLight = 0.8f;
        public float shelfPokeRadius = 0.012f;
        public float shelfSwirl = 240f;
        public float shelfInfoSeconds = 3.2f;
        [Range(0.1f, 1f)] public float shelfMiniScale = 0.25f;
        public Vector3 shelfCorner = new Vector3(-0.38f, -0.285f, -0.04f);
        public float shelfApproachDelay = 1.5f;
        public float shelfApproachSeconds = 2.5f;
        [Range(0.25f, 1f)] public float shelfPastQuality = 0.7f;
        [Range(0.1f, 1f)] public float shelfMiniDetail = 0.25f;

        [Header("Ending")]
        public bool ending = true;
        public float endOrbDelay = 3f;
        public float endOrbRadius = 0.07f;
        public float endFadeSeconds = 3f;
        public bool endQuits = true;

        [Header("Seal Flow")]
        public bool waxStick = false;
        public bool sealOnRelease = true;
        public EmblemStyle emblemStyle = EmblemStyle.Whorl;
        public Vector3 lidOffset = new Vector3(-0.12f, 0.16f, -0.03f);
        public float lidGrabRadius = 0.05f;
        public float lidSnapDistance = 0.05f;
        public float lidCloseSeconds = 0.35f;
        public float meltSeconds = 3.4f;
        public float meltSpread = 0.8f;
        public float waxLift = 0.012f;
        [Range(0.2f, 1f)] public float pressGather = 0.55f;
        public ClimaxStyle climaxStyle = ClimaxStyle.Bloom;
        public float climaxSeconds = 1.8f;
        public float climaxReach = 2.0f;
        [Range(0f, 2f)] public float climaxIntensity = 0.9f;
        public float hushSeconds = 1.0f;
        public float bloomSeconds = 5f;
        public int bloomMotes = 640;
        public int bloomFog = 14;
        [Range(0f, 0.5f)] public float bloomFogIntensity = 0.12f;
        [Range(0f, 2f)] public float bloomJarGlow = 0.6f;
        public float coolSeconds = 4f;

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

        [Header("Hand Field")]
        public bool handField = true;
        public float fieldFinger = 0.013f;
        public float fieldPalm = 0.03f;
        public float fieldReach = 0.14f;
        [Range(0f, 1f)] public float partStrength = 0.5f;
        public float partRecover = 0.7f;
        public float scatterSpeed = 0.6f;
        public float pinchFormDistance = 0.045f;
        public float scatterGrace = 0.35f;
        [Range(0f, 1.5f)] public float scatterCarry = 0.7f;
        public float scatterBurst = 0.35f;
        public float scatterFlight = 0.35f;
        public float scatterDrag = 3.5f;
        public float returnSeconds = 2.2f;
        [Range(0f, 1.5f)] public float returnSwirl = 0.6f;

        [Header("Weight")]
        public float weightLag = 0.6f;
        public float weightUnits = 6f;
        public float weightFollow = 40f;
        [Range(0.4f, 1.5f)] public float weightDamping = 1f;
        public float weightMaxLag = 0.05f;
        [Range(0f, 1f)] public float releaseGlide = 0.3f;
        public float releaseGlideMax = 0.06f;

        [Header("Sticky Merge")]
        public bool stickyMerge = true;
        public float bondStart = 0.016f;
        public float bondBreak = 0.045f;
        public float bondSeconds = 0.7f;
        public float bondFollow = 0.12f;
        [Range(0f, 1f)] public float bondLean = 0.25f;
        [Range(0f, 1f)] public float bridgeShare = 0.4f;
        [Range(0f, 1f)] public float bridgeReach = 0.95f;
        public bool seam = false;
        public int seamSplats = 96;
        public float seamRadius = 0.0011f;
        [Range(1f, 1.15f)] public float seamLift = 1.03f;
        public float seamDelay = 0.5f;
        public float seamSeconds = 1.2f;
        [Range(0f, 2f)] public float seamGlow = 0.6f;

        [Header("Palm Pour And Vessel")]
        public bool palmPour = true;
        [Range(0f, 1f)] public float palmUp = 0.55f;
        public float palmOpen = 0.065f;
        public float palmCupReach = 0.07f;
        public float palmCupLift = 0.035f;
        public float palmCupSeconds = 0.2f;
        [Range(-0.5f, 0.9f)] public float palmPourTilt = 0.25f;
        public float palmJarReach = 0.09f;
        public float palmJarHeight = 0.3f;
        [Range(0f, 1f)] public float palmSettle = 0.9f;
        public float palmNormalSign = 1f;
        public float pourPitchLow = 0.85f;
        public float pourPitchHigh = 1.4f;
        public bool restSand = false;
        public float restSeconds = 2.6f;
        [Range(0.3f, 1f)] public float restGrain = 0.7f;

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
        public int cpuLevel = 2;
        public int gpuLevel = 2;
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
        public bool sampledSound = true;
        [Range(0f, 1f)] public float soundSpatial = 0.7f;
        [Range(0f, 1f)] public float clockTouchVolume = 0.55f;
        public bool clockCymbalShort = false;
        [Range(0f, 1f)] public float pullVolume = 0.26f;
        public float pullInterval = 0.09f;
        public float pullBaseSemitones = -5f;
        public float pullRiseSemitones = 12f;
        public float pullUnitsPerStep = 2f;
        [Range(0f, 1f)] public float formVolume = 0.4f;
        [Range(0f, 1f)] public float touchVolume = 0.3f;
        public float touchWarmPitch = 0.94f;
        public float touchFullSpeed = 0.6f;
        [Range(0f, 1f)] public float bondVolume = 0.3f;
        public int bondBeats = 3;
        [Range(0f, 1f)] public float mergeVolume = 0.45f;
        [Range(0f, 1f)] public float mergeCoolVolume = 0.45f;
        public float mergeCoolPitch = 1f;
        [Range(0f, 1f)] public float pourBedVolume = 0.3f;
        [Range(0f, 1f)] public float pourTailVolume = 0f;
        [Range(0f, 1f)] public float clockSandTailVolume = 0.8f;
        [Range(0f, 1f)] public float noteVolume = 0.42f;
        [Range(0f, 1f)] public float grainNoteVolume = 0.12f;
        public float grainNoteInterval = 0.24f;
        [Range(0f, 1f)] public float lidVolume = 0.45f;
        [Range(0f, 1f)] public float pressSparkleVolume = 0.5f;
        [Range(0f, 1f)] public float pressDuck = 0.6f;
        public int sealVoice = 1;
        [Range(0f, 1f)] public float afterglowVolume = 0.8f;
        [Range(0.2f, 1f)] public float afterglowLengthScale = 0.6f;
        [Range(0f, 0.9f)] public float afterglowHoldShare = 0.3f;
        public float afterglowLead = 1.2f;
        [Range(0f, 1f)] public float bloomVolume = 0.2f;
        [Range(0f, 1f)] public float shelfVolume = 0.6f;
        [Header("Tutorial")]
        public bool tutorial = true;
        public bool tutorialRemember = false;
        public Vector3 tutorialOffset = new Vector3(-0.08f, -0.235f, -0.10f);
        public float tutorialCardWidth = 0.2f;
        public float tutorialTextSize = 0.0019f;
        public float tutorialFootSize = 0.00135f;
        [Range(0.3f, 1f)] public float tutorialCardBrightness = 0.86f;
        public float tutorialFrameInset = 0.004f;
        public float tutorialFrameWidth = 0.0008f;
        [Range(0f, 1f)] public float tutorialFrameAlpha = 0.55f;
        public float tutorialFoldSeconds = 0.45f;
        public float tutorialPourIdle = 5f;
        public float tutorialLidIdle = 7f;
        public float tutorialShelfDelay = 4.5f;
        public float tutorialMergeSeconds = 10f;
        public float tutorialPulseIdle = 4.5f;
        public float tutorialOffHintIdle = 6f;
        public float tutorialOffHintWindow = 60f;

        [Header("Sound Space")]
        public bool reverb = true;
        public float reverbDecay = 3.6f;
        public int reverbRoom = -1100;
        public int reverbLevel = -500;

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
