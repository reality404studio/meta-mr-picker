using MoodPicker.Hands.Ovr;
using MoodPicker.Splatter;
using UnityEngine;
using UnityEngine.XR;

namespace MoodPicker.Ritual
{
    public sealed class RitualOvrBridge : MonoBehaviour
    {
        const string Tag = "[MP]";

        [SerializeField] TimeRitual ritual;
        [SerializeField] OVRPassthroughLayer passthroughLayer;
        [SerializeField] Camera[] cameras;
        [SerializeField] OVRManager.FoveatedRenderingLevel foveation = OVRManager.FoveatedRenderingLevel.High;
        [SerializeField] bool dynamicFoveation = true;
        [SerializeField] bool handOcclusion = true;

        float _dim = float.NaN;
        bool _gpuMetric;
        bool _cpuMetric;
        float _probe = 5f;

        static OVRManager.ProcessorPerformanceLevel Level(int value)
        {
            switch (Mathf.Clamp(value, 0, 3))
            {
                case 0: return OVRManager.ProcessorPerformanceLevel.PowerSavings;
                case 1: return OVRManager.ProcessorPerformanceLevel.SustainedLow;
                case 3: return OVRManager.ProcessorPerformanceLevel.Boost;
                default: return OVRManager.ProcessorPerformanceLevel.SustainedHigh;
            }
        }

        void Start()
        {
            if (ritual == null) return;
            ritual.Init();
            TimeRitualConfig cfg = ritual.Config;
            OVRManager.foveatedRenderingLevel = foveation;
            OVRManager.useDynamicFoveatedRendering = dynamicFoveation;
            OVRManager.suggestedCpuPerfLevel = Level(cfg.cpuLevel);
            OVRManager.suggestedGpuPerfLevel = Level(cfg.gpuLevel);
            OVRManager manager = OVRManager.instance;
            if (manager != null && manager.enableDynamicResolution != cfg.dynamicResolution) manager.enableDynamicResolution = cfg.dynamicResolution;
            if (!Mathf.Approximately(cfg.devRenderScale, 1f)) XRSettings.eyeTextureResolutionScale = cfg.devRenderScale;
            _gpuMetric = OvrPerfMetrics.GpuSupported;
            _cpuMetric = OvrPerfMetrics.CpuSupported;
            Debug.Log($"{Tag} ritual perf metrics gpu={_gpuMetric} cpu={_cpuMetric}");
            if (cameras != null)
            {
                foreach (Camera cam in cameras)
                    if (cam != null) cam.cullingMask = SplatFilmLook.CullingMask(cam.cullingMask, handOcclusion);
            }
            Debug.Log($"{Tag} ritual device cpu={OVRManager.suggestedCpuPerfLevel} gpu={OVRManager.suggestedGpuPerfLevel} foveation={OVRManager.foveatedRenderingLevel} dynamicFoveation={OVRManager.useDynamicFoveatedRendering} " +
                      $"dynamicResolution={(manager != null && manager.enableDynamicResolution)} scale={(manager != null ? manager.minDynamicResolutionScale : 0f):0.00}-{(manager != null ? manager.maxDynamicResolutionScale : 0f):0.00} eyeScale={XRSettings.eyeTextureResolutionScale:0.00} handOcclusion={handOcclusion}");
        }

        void Update()
        {
            if (ritual == null || !ritual.Ready) return;
            float gpuRaw = _gpuMetric ? OvrPerfMetrics.AppGpuRaw() : float.NaN;
            float cpuRaw = _cpuMetric ? OvrPerfMetrics.AppCpuRaw() : float.NaN;
            float gpu = RitualGovernor.ToMilliseconds(gpuRaw);
            float cpu = RitualGovernor.ToMilliseconds(cpuRaw);
            ritual.ReportWork(gpu, cpu);
            if (_probe <= 0f) return;
            _probe -= Time.unscaledDeltaTime;
            if (_probe <= 0f) Debug.Log($"{Tag} ritual perf metrics raw gpu={gpuRaw} cpu={cpuRaw} -> gpu={gpu:0.00}ms cpu={cpu:0.00}ms");
        }

        void LateUpdate()
        {
            if (ritual == null || passthroughLayer == null || !ritual.Ready) return;
            float dim = Mathf.Clamp(ritual.PassthroughDim, -1f, 0f);
            if (!float.IsNaN(_dim) && Mathf.Abs(dim - _dim) < 0.004f) return;
            _dim = dim;
            passthroughLayer.SetBrightnessContrastSaturation(dim, 0f, 0f);
        }
    }
}
