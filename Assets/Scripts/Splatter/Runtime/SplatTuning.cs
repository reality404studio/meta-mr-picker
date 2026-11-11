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

        [DebugMember(Category = "5 Summon", Min = 0.2f, Max = 3f)] public float summonSpeed = 0.9f;
        [DebugMember(Category = "5 Summon", Min = 1f, Max = 20f)] public float summonEase = 6f;
        [DebugMember(Category = "5 Summon", Min = 0f, Max = 2f)] public float summonSwirl = 0.6f;
        [DebugMember(Category = "5 Summon", Min = 0.2f, Max = 4f)] public float summonSpread = 1.4f;
        [DebugMember(Category = "5 Summon", Min = 0.02f, Max = 0.6f)] public float appearSeconds = 0.12f;
        [DebugMember(Category = "5 Summon", Min = 0f, Max = 1f)] public float dissolveSag = 0.2f;
        [DebugMember(Category = "5 Summon", Min = 0.05f, Max = 2f)] public float dissolveSpread = 0.4f;

        [DebugMember(Category = "6 Look", Min = 0f, Max = 4f)] public int shaderMode = 0;
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
                fadeDistance = fadeDistance
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
