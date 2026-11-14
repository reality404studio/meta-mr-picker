using UnityEngine;

namespace MoodPicker.Ritual
{
    public sealed class TimeRitualPlacer : MonoBehaviour
    {
        const string Tag = "[MP]";

        [SerializeField] TimeRitualConfig config;
        [SerializeField] Transform head;
        [SerializeField] int settleFrames = 30;

        bool _placed;
        int _frames;

        void LateUpdate()
        {
            if (_placed || head == null) return;
            if (head.position.sqrMagnitude < 1e-6f) return;
            if (++_frames < settleFrames) return;
            Place();
        }

        public void Place()
        {
            if (head == null) return;
            Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            float distance = config != null ? config.placeDistance : 0.4f;
            float below = config != null ? config.placeBelowEye : 0.18f;
            transform.SetPositionAndRotation(head.position + fwd * distance + Vector3.down * below, Quaternion.LookRotation(fwd, Vector3.up));
            _placed = true;
            Debug.Log($"{Tag} ritual placed at {transform.position}");
        }
    }
}
