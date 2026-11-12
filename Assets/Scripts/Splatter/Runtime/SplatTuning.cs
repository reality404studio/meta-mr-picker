using System;
using System.IO;
using Meta.XR.ImmersiveDebugger;
using MoodPicker.Splatter.Sim;
using UnityEngine;

namespace MoodPicker.Splatter
{
    public sealed class SplatTuning : MonoBehaviour
    {
        public const string LiveFile = "tuning.json";
        public const string SavedFile = "tuning-saved.json";
        const string Tag = "[MP]";

        [DebugMember(Category = "0 Feel", Min = 0f, Max = 1f)] public float feelWeight = 0.2f;
        [DebugMember(Category = "0 Feel", Min = 0f, Max = 1f)] public float feelCohesion = 0.2f;
        [DebugMember(Category = "0 Feel", Min = 0f, Max = 1f)] public float feelViscosity = 0.1f;
        [DebugMember(Category = "0 Feel", Min = 0f, Max = 1f)] public float feelBounce = 0.2f;
        [DebugMember(Category = "0 Feel", Min = 0f, Max = 1f)] public float feelGrain = 1f;
        [DebugMember(Category = "0 Feel", Min = 0f, Max = 1f)] public float feelFluff = 0f;
        [DebugMember(Category = "0 Feel", Min = 0f, Max = 1f)] public float feelSquish = 0f;
        [DebugMember(Category = "0 Feel", Min = 0f, Max = 1f)] public float feelPlastic = 0f;
        [DebugMember(Category = "0 Feel", Min = 0f, Max = 1f)] public float feelTack = 0f;
        [DebugMember(Category = "0 Feel", Min = 0f, Max = 1f)] public float feelThicken = 0f;

        [DebugMember(Category = "1 Spring", Min = 20f, Max = 600f)] public float homeStiffness = 180f;
        [DebugMember(Category = "1 Spring", Min = 0f, Max = 30f)] public float homeDamping = 5f;
        [DebugMember(Category = "1 Spring", Min = 0f, Max = 0.9f)] public float stiffnessJitter = 0.35f;
        [DebugMember(Category = "1 Spring", Min = 2f, Max = 80f)] public float maxSpringAccel = 25f;
        [DebugMember(Category = "1 Spring", Min = 0f, Max = 1f)] public float carry = 0.85f;

        [DebugMember(Category = "2 Cohesion", Min = 0f, Max = 1f)] public float cohesion = 0.7f;
        [DebugMember(Category = "2 Cohesion", Min = 10f, Max = 400f)] public float clusterStiffness = 90f;
        [DebugMember(Category = "2 Cohesion", Min = 0f, Max = 20f)] public float clusterDamping = 3f;
        [DebugMember(Category = "2 Cohesion", Min = 0.1f, Max = 1f)] public float clusterGather = 0.55f;
        [DebugMember(Category = "2 Cohesion", Min = 0.005f, Max = 0.1f)] public float gatherDistance = 0.03f;
        [DebugMember(Category = "2 Cohesion", Min = 0f, Max = 1.5f)] public float clusterReach = 0.6f;
        [DebugMember(Category = "2 Cohesion", Min = 0f, Max = 80f)] public float clusterViscosity = 25f;

        [DebugMember(Category = "3 Hand", Min = 0.008f, Max = 0.05f)] public float tipRadius = 0.022f;
        [DebugMember(Category = "3 Hand", Min = 0.02f, Max = 0.09f)] public float palmRadius = 0.045f;
        [DebugMember(Category = "3 Hand", Min = 0f, Max = 200f)] public float handPush = 60f;
        [DebugMember(Category = "3 Hand", Min = 0f, Max = 60f)] public float handDrag = 18f;
        [DebugMember(Category = "3 Hand", Min = 0f, Max = 10f)] public float handAttract = 1.2f;
        [DebugMember(Category = "3 Hand", Min = 1f, Max = 4f)] public float attractShell = 2.2f;
        [DebugMember(Category = "3 Hand", Min = 0f, Max = 0.6f)] public float stickFraction = 0.12f;
        [DebugMember(Category = "3 Hand", Min = 0f, Max = 30f)] public float stickGain = 8f;
        [DebugMember(Category = "3 Hand", Min = 0.1f, Max = 6f)] public float stickDecay = 1.2f;
        [DebugMember(Category = "3 Hand", Min = 0f, Max = 600f)] public float stickPull = 220f;
        [DebugMember(Category = "3 Hand", Min = 0.01f, Max = 0.2f)] public float stickReach = 0.06f;

        [DebugMember(Category = "3 Hand", Min = 0.008f, Max = 0.03f)] public float pinchStart = 0.016f;
        [DebugMember(Category = "3 Hand", Min = 0.02f, Max = 0.06f)] public float pinchEnd = 0.034f;

        [DebugMember(Category = "4 Weight", Min = 0f, Max = 9.8f)] public float weight = 0f;
        [DebugMember(Category = "4 Weight", Min = 0.5f, Max = 9.8f)] public float fallGravity = 2.4f;
        [DebugMember(Category = "4 Weight", Min = 0f, Max = 5f)] public float fallDamping = 0.4f;
        [DebugMember(Category = "4 Weight", Min = 0f, Max = 1f)] public float bounce = 0.3f;
        [DebugMember(Category = "4 Weight", Min = 0.1f, Max = 1.5f)] public float floorBelow = 0.45f;
        [DebugMember(Category = "4 Weight", Min = 0.1f, Max = 1.5f)] public float fadeDistance = 0.5f;
        [DebugMember(Category = "4 Weight", Min = 0f, Max = 10f)] public float burstDamping = 2.2f;
        [DebugMember(Category = "4 Weight", Min = 0f, Max = 9.8f)] public float burstGravity = 0.4f;
        [DebugMember(Category = "4 Weight", Min = 0.05f, Max = 1.5f)] public float gatherRamp = 0.35f;
        [DebugMember(Category = "4 Weight", Min = 0f, Max = 1f)] public float field = 0f;
        [DebugMember(Category = "4 Weight", Min = 20f, Max = 2000f)] public float fieldStiffness = 400f;
        [DebugMember(Category = "4 Weight", Min = 0f, Max = 100f)] public float fieldAnchor = 10f;
        [DebugMember(Category = "4 Weight", Min = 0f, Max = 6000f)] public float fieldPin = 3000f;
        [DebugMember(Category = "4 Weight", Min = 0f, Max = 20f)] public float fieldDamping = 2f;
        [DebugMember(Category = "4 Weight", Min = 0f, Max = 1f)] public float fieldTack = 0f;
        [DebugMember(Category = "4 Weight", Min = 0.01f, Max = 0.2f)] public float fieldTackBreak = 0.035f;
        [DebugMember(Category = "4 Weight", Min = 0f, Max = 1f)] public float fieldReach = 1f;
        [DebugMember(Category = "4 Weight", Min = 0f, Max = 10f)] public float fieldTouchGain = 6f;
        [DebugMember(Category = "4 Weight", Min = 0.002f, Max = 0.06f)] public float fieldRestMax = 0.015f;
        [DebugMember(Category = "4 Weight", Min = 0f, Max = 2f)] public float fieldHeal = 0.15f;
        [DebugMember(Category = "4 Weight", Min = 0.2f, Max = 20f)] public float fieldStickRate = 2.5f;
        [DebugMember(Category = "4 Weight", Min = 0f, Max = 2f)] public float fieldTackPull = 0.5f;
        [DebugMember(Category = "4 Weight", Min = 0f, Max = 1f)] public float crack = 0f;
        [DebugMember(Category = "4 Weight", Min = 0.02f, Max = 2f)] public float crackSpeed = 0.25f;
        [DebugMember(Category = "4 Weight", Min = 0f, Max = 0.98f)] public float crackSpread = 0.8f;
        [DebugMember(Category = "4 Weight", Min = 0.5f, Max = 60f)] public float crackRate = 10f;
        [DebugMember(Category = "4 Weight", Min = 0f, Max = 3f)] public float crackHeal = 0.3f;
        [DebugMember(Category = "4 Weight", Min = 0f, Max = 1f)] public float comb = 0f;
        [DebugMember(Category = "4 Weight", Min = 0f, Max = 4f)] public float combMax = 1.6f;
        [DebugMember(Category = "4 Weight", Min = 0.5f, Max = 40f)] public float combRate = 15f;
        [DebugMember(Category = "4 Weight", Min = 0f, Max = 2f)] public float combHeal = 0.05f;
        [DebugMember(Category = "4 Weight", Min = 0f, Max = 0.04f)] public float combReach = 0.012f;
        [DebugMember(Category = "4 Weight", Min = 0f, Max = 20f)] public float plastic = 0f;
        [DebugMember(Category = "4 Weight", Min = 0.001f, Max = 0.05f)] public float plasticYield = 0.02f;
        [DebugMember(Category = "4 Weight", Min = 0.01f, Max = 0.2f)] public float plasticMax = 0.08f;
        [DebugMember(Category = "4 Weight", Min = 0f, Max = 1f)] public float thicken = 0f;
        [DebugMember(Category = "4 Weight", Min = 0.05f, Max = 2f)] public float thickenSpeed = 0.35f;

        [DebugMember(Category = "5 Summon", Min = 0.2f, Max = 3f)] public float summonSpeed = 0.9f;
        [DebugMember(Category = "5 Summon", Min = 1f, Max = 20f)] public float summonEase = 6f;
        [DebugMember(Category = "5 Summon", Min = 0f, Max = 2f)] public float summonSwirl = 0.6f;
        [DebugMember(Category = "5 Summon", Min = 0.2f, Max = 4f)] public float summonSpread = 1.4f;
        [DebugMember(Category = "5 Summon", Min = 0.02f, Max = 0.6f)] public float appearSeconds = 0.12f;
        [DebugMember(Category = "5 Summon", Min = 0f, Max = 1f)] public float dissolveSag = 0.2f;
        [DebugMember(Category = "5 Summon", Min = 0.05f, Max = 2f)] public float dissolveSpread = 0.4f;

        [DebugMember(Category = "5 Summon", Min = 0.05f, Max = 1.5f)] public float applySpeed = 0.32f;
        [DebugMember(Category = "5 Summon", Min = 0.005f, Max = 0.1f)] public float applyBand = 0.03f;
        [DebugMember(Category = "5 Summon", Min = 0f, Max = 0.015f)] public float applyBulge = 0.004f;
        [DebugMember(Category = "5 Summon", Min = 0f, Max = 1f)] public float applyLift = 0.15f;
        [DebugMember(Category = "5 Summon", Min = 0.01f, Max = 0.08f)] public float grabReach = 0.035f;
        [DebugMember(Category = "5 Summon", Min = 0.2f, Max = 1.2f)] public float contactFraction = 0.8f;
        [DebugMember(Category = "5 Summon", Min = 0.1f, Max = 3f)] public float regrowSeconds = 0.9f;
        [DebugMember(Category = "5 Summon", Min = 0.1f, Max = 1f)] public float pinchTapSeconds = 0.45f;

        [DebugMember(Category = "6 Look", Min = -1f, Max = 5f)] public int shaderMode = -1;
        [DebugMember(Category = "6 Look", Min = 0.2f, Max = 4f)] public float triplanarScale = 1.15f;
        [DebugMember(Category = "6 Look", Min = 0f, Max = 2.5f)] public float intensity = 1.15f;
        [DebugMember(Category = "6 Look", Min = 0.5f, Max = 2.5f)] public float contrast = 1f;
        [DebugMember(Category = "6 Look", Min = 0f, Max = 0.004f)] public float flowAmplitude = 0.0008f;
        [DebugMember(Category = "6 Look", Min = 0f, Max = 6f)] public float flowSpeed = 1.6f;
        [DebugMember(Category = "6 Look", Min = 0.003f, Max = 0.08f)] public float detachDistance = 0.012f;
        [DebugMember(Category = "6 Look", Min = 0f, Max = 0.03f)] public float stretchSeconds = 0.008f;
        [DebugMember(Category = "6 Look", Min = 0f, Max = 6f)] public float stretchMax = 2.5f;
        [DebugMember(Category = "6 Look", Min = 0.3f, Max = 2f)] public float grain = 0.6f;
        [DebugMember(Category = "6 Look", Min = 0f, Max = 2f)] public float glowGain = 0.35f;
        [DebugMember(Category = "6 Look", Min = 0f, Max = 3f)] public float sparkle = 0.6f;
        [DebugMember(Category = "6 Look", Min = 0f, Max = 1f)] public float overlay = 1f;
        [DebugMember(Category = "6 Look", Min = 0f, Max = 1f)] public float fluff = 0f;
        [DebugMember(Category = "6 Look", Min = 0.5f, Max = 8f)] public float fluffLength = 3f;
        [DebugMember(Category = "6 Look", Min = 0f, Max = 4f)] public float glintAmount = 0f;
        [DebugMember(Category = "6 Look", Min = 0f, Max = 4f)] public float glintSpeed = 1.2f;
        [DebugMember(Category = "6 Look", Min = 20f, Max = 400f)] public float glintScale = 120f;
        [DebugMember(Category = "6 Look", Min = 0.05f, Max = 1.5f)] public float glintRough = 0.45f;
        [DebugMember(Category = "6 Look", Min = 50f, Max = 3000f)] public float glintSharpness = 160f;
        [DebugMember(Category = "6 Look", Min = -0.6f, Max = 1f)] public float glintSize = 0.15f;
        [DebugMember(Category = "6 Look", Min = 0f, Max = 1f)] public float glintMaterial = 0.6f;
        [DebugMember(Category = "6 Look", Min = -1f, Max = 0f)] public float passthroughBrightness = 0f;
        [DebugMember(Category = "6 Look", Min = -1f, Max = 1f)] public float passthroughContrast = 0f;
        [DebugMember(Category = "6 Look", Min = -1f, Max = 1f)] public float passthroughSaturation = 0f;

        [DebugMember(Category = "8 Catch")] public bool liveCamera;
        [DebugMember(Category = "8 Catch", Min = 0.1f, Max = 1f)] public float cameraCrop = 0.45f;
        [DebugMember(Category = "8 Catch", Min = 0.4f, Max = 2.4f)] public float cameraGamma = 1f;

        [DebugMember(Category = "7 Run", Min = 1f, Max = 3f)] public int stepEveryFrames = 1;
        [DebugMember(Category = "7 Run", Min = 40f, Max = 600f)] public int particlesPerCluster = 300;
        [DebugMember(Category = "7 Run")] public bool frozen;
        [DebugMember(Category = "7 Run", Tweakable = false)] public float activity;
        [DebugMember(Category = "7 Run", Tweakable = false)] public int activeColliders;
        [DebugMember(Category = "7 Run", Tweakable = false)] public bool simAsleep = true;
        [DebugMember(Category = "7 Run", Tweakable = false)] public float simMilliseconds;
        [DebugMember(Category = "7 Run", Tweakable = false)] public float awakeFraction;

        float _nextPoll;
        DateTime _liveStamp;

        public event Action RecenterRequested;
        public event Action ToggleRequested;
        public event Action CatchRequested;
        public event Action ReleaseCatchRequested;
        public event Action ShapeRequested;
        public event Action FreezeRequested;
        public event Action StillRequested;

        public SplatFeel CurrentFeel => new SplatFeel { weight = feelWeight, cohesion = feelCohesion, viscosity = feelViscosity, bounce = feelBounce, grain = feelGrain, fluff = feelFluff, squish = feelSquish, plastic = feelPlastic, tack = feelTack, thicken = feelThicken };

        public void SetFeel(SplatFeel feel)
        {
            SplatFeel f = feel.Clamped;
            feelWeight = f.weight;
            feelCohesion = f.cohesion;
            feelViscosity = f.viscosity;
            feelBounce = f.bounce;
            feelGrain = f.grain;
            feelFluff = f.fluff;
            feelSquish = f.squish;
            feelPlastic = f.plastic;
            feelTack = f.tack;
            feelThicken = f.thicken;
            DeriveFromFeel();
        }

        public void DeriveFromFeel()
        {
            SplatSimParams p = ToSimParams(0f);
            SplatFeelMap.Apply(CurrentFeel, ref p);
            homeStiffness = p.homeStiffness;
            homeDamping = p.homeDamping;
            maxSpringAccel = p.maxSpringAccel;
            cohesion = p.cohesion;
            clusterStiffness = p.clusterStiffness;
            clusterDamping = p.clusterDamping;
            clusterViscosity = p.clusterViscosity;
            clusterGather = p.clusterGather;
            handPush = p.handPush;
            handDrag = p.handDrag;
            stickFraction = p.stickFraction;
            stickDecay = p.stickDecay;
            stickReach = p.stickReach;
            field = p.field;
            fieldStiffness = p.fieldStiffness;
            fieldAnchor = p.fieldAnchor;
            fieldPin = p.fieldPin;
            fieldDamping = p.fieldDamping;
            fieldTack = p.fieldTack;
            fieldTackBreak = p.fieldTackBreak;
            fieldReach = p.fieldReach;
            fieldTouchGain = p.fieldTouchGain;
            fieldRestMax = p.fieldRestMax;
            fieldHeal = p.fieldHeal;
            fieldStickRate = p.fieldStickRate;
            fieldTackPull = p.fieldTackPull;
            crack = p.crack;
            crackSpeed = p.crackSpeed;
            crackSpread = p.crackSpread;
            crackRate = p.crackRate;
            crackHeal = p.crackHeal;
            comb = p.comb;
            combMax = p.combMax;
            combRate = p.combRate;
            combHeal = p.combHeal;
            combReach = p.combReach;
            plastic = p.plastic;
            plasticYield = p.plasticYield;
            plasticMax = p.plasticMax;
            thicken = p.thicken;
            thickenSpeed = p.thickenSpeed;
            weight = p.weight;
            fallGravity = p.fallGravity;
            fallDamping = p.fallDamping;
            bounce = p.bounce;
            burstDamping = p.burstDamping;
            burstGravity = p.burstGravity;
            gatherRamp = p.gatherRamp;
            SplatFeelLook look = SplatFeelMap.Look(CurrentFeel);
            grain = look.grain;
            stretchMax = look.stretchMax;
            stretchSeconds = look.stretchSeconds;
            fluff = look.fluff;
            detachDistance = look.detach;
        }

        public SplatLook ToLook()
        {
            return new SplatLook
            {
                flowAmplitude = flowAmplitude,
                flowSpeed = flowSpeed,
                contrast = contrast,
                detachDistance = detachDistance,
                stretchSeconds = stretchSeconds,
                stretchMax = stretchMax,
                grain = grain,
                glowGain = glowGain,
                sparkle = sparkle,
                overlay = overlay,
                fluff = fluff,
                fluffLength = fluffLength,
                comb = comb,
                glintAmount = glintAmount,
                glintSpeed = glintSpeed,
                glintScale = glintScale,
                glintRough = glintRough,
                glintSharpness = glintSharpness,
                glintSize = glintSize,
                glintMaterial = glintMaterial
            };
        }

        public Vector4 ApplyShape => new Vector4(applyBand, applyBulge, applyLift, 0f);

        public SplatSimParams ToSimParams(float floorWorldY)
        {
            return new SplatSimParams
            {
                homeStiffness = homeStiffness,
                homeDamping = homeDamping,
                stiffnessJitter = stiffnessJitter,
                maxSpringAccel = maxSpringAccel,
                cohesion = cohesion,
                clusterStiffness = clusterStiffness,
                clusterDamping = clusterDamping,
                clusterGather = clusterGather,
                gatherDistance = gatherDistance,
                clusterReach = clusterReach,
                clusterViscosity = clusterViscosity,
                handPush = handPush,
                handDrag = handDrag,
                handAttract = handAttract,
                attractShell = attractShell,
                stickFraction = stickFraction,
                stickGain = stickGain,
                stickDecay = stickDecay,
                stickPull = stickPull,
                stickReach = stickReach,
                glowDecay = 5f,
                weight = weight,
                fallGravity = fallGravity,
                fallDamping = fallDamping,
                bounce = bounce,
                floorY = floorWorldY,
                maxSpeed = 4f,
                carry = carry,
                summonSpeed = summonSpeed,
                summonEase = summonEase,
                summonResponse = 30f,
                summonSwirl = summonSwirl,
                appearSeconds = appearSeconds,
                fadeDistance = fadeDistance,
                burstDamping = burstDamping,
                burstGravity = burstGravity,
                gatherRamp = gatherRamp,
                field = field,
                fieldStiffness = fieldStiffness,
                fieldAnchor = fieldAnchor,
                fieldPin = fieldPin,
                fieldDamping = fieldDamping,
                fieldTack = fieldTack,
                fieldTackBreak = fieldTackBreak,
                fieldReach = fieldReach,
                fieldTouchGain = fieldTouchGain,
                fieldRestMax = fieldRestMax,
                fieldHeal = fieldHeal,
                fieldStickRate = fieldStickRate,
                fieldTackPull = fieldTackPull,
                crack = crack,
                crackSpeed = crackSpeed,
                crackSpread = crackSpread,
                crackRate = crackRate,
                crackHeal = crackHeal,
                comb = comb,
                combMax = combMax,
                combRate = combRate,
                combHeal = combHeal,
                combReach = combReach,
                combMinSpeed = 0.02f,
                plastic = plastic,
                plasticYield = plasticYield,
                plasticMax = plasticMax,
                thicken = thicken,
                thickenSpeed = thickenSpeed
            };
        }

        [DebugMember(Category = "7 Run")]
        public void Recenter()
        {
            RecenterRequested?.Invoke();
        }

        [DebugMember(Category = "7 Run")]
        public void ToggleSummon()
        {
            ToggleRequested?.Invoke();
        }

        [DebugMember(Category = "7 Run")]
        public void NextShape()
        {
            ShapeRequested?.Invoke();
        }

        [DebugMember(Category = "7 Run")]
        public void Freeze()
        {
            FreezeRequested?.Invoke();
        }

        [DebugMember(Category = "7 Run")]
        public void SaveStill()
        {
            StillRequested?.Invoke();
        }

        [DebugMember(Category = "8 Catch")]
        public void Catch()
        {
            CatchRequested?.Invoke();
        }

        [DebugMember(Category = "8 Catch")]
        public void ReleaseCatch()
        {
            ReleaseCatchRequested?.Invoke();
        }

        [DebugMember(Category = "7 Run")]
        public void SaveTuning()
        {
            string path = Path.Combine(Application.persistentDataPath, SavedFile);
            File.WriteAllText(path, JsonUtility.ToJson(this, true));
            Debug.Log($"{Tag} tuning saved {path}");
        }

        void Update()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.5f;
            string path = Path.Combine(Application.persistentDataPath, LiveFile);
            if (!File.Exists(path)) return;
            DateTime stamp = File.GetLastWriteTimeUtc(path);
            if (stamp == _liveStamp) return;
            _liveStamp = stamp;
            try
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(path), this);
                Debug.Log($"{Tag} tuning loaded {path}");
            }
            catch (Exception e)
            {
                Debug.Log($"{Tag} tuning load failed {e.Message}");
            }
        }
    }
}
