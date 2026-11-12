using UnityEngine;

namespace MoodPicker.Splatter
{
    public sealed class SplatFoveation : MonoBehaviour
    {
        const string Tag = "[MP]";

        [SerializeField] OVRManager.FoveatedRenderingLevel level = OVRManager.FoveatedRenderingLevel.High;
        [SerializeField] bool dynamic = true;

        void Start()
        {
            OVRManager.foveatedRenderingLevel = level;
            OVRManager.useDynamicFoveatedRendering = dynamic;
            Debug.Log($"{Tag} foveation level={OVRManager.foveatedRenderingLevel} dynamic={OVRManager.useDynamicFoveatedRendering}");
        }
    }
}
