namespace MoodPicker.Hands.Ovr
{
    public static class OvrPerfMetrics
    {
        public static bool GpuSupported => OVRPlugin.IsPerfMetricsSupported(OVRPlugin.PerfMetrics.App_GpuTime_Float);
        public static bool CpuSupported => OVRPlugin.IsPerfMetricsSupported(OVRPlugin.PerfMetrics.App_CpuTime_Float);

        public static float AppGpuRaw()
        {
            float? v = OVRPlugin.GetPerfMetricsFloat(OVRPlugin.PerfMetrics.App_GpuTime_Float);
            return v ?? float.NaN;
        }

        public static float AppCpuRaw()
        {
            float? v = OVRPlugin.GetPerfMetricsFloat(OVRPlugin.PerfMetrics.App_CpuTime_Float);
            return v ?? float.NaN;
        }
    }
}
