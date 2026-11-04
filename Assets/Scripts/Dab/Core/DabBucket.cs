using System;
using UnityEngine;

namespace MoodPicker.Dab
{
    public class DabBucket<TPaint> where TPaint : class
    {
        readonly DabSettings _s;
        bool _armed = true, _touching;
        float _fillStart = float.NegativeInfinity;

        public TPaint Paint { get; private set; }
        public float FillStartedAt => _fillStart;
        public bool Touching => _touching;
        public event Action<TPaint, float> Filled;

        public DabBucket(DabSettings settings) { _s = settings; }

        public float Fill(float t) => Paint == null ? 0f : Mathf.Clamp01((t - _fillStart) / Mathf.Max(1e-3f, _s.fillSeconds));

        public void Update(float t, float signedDistance, bool tracked, TPaint fingertip)
        {
            if (!tracked) { _touching = false; return; }
            if (signedDistance > _s.releaseHeight) { _armed = true; _touching = false; return; }
            if (signedDistance > -_s.contactDepth) return;
            _touching = true;
            if (!_armed) return;
            _armed = false;
            if (fingertip == null) return;
            Paint = fingertip;
            _fillStart = t;
            Filled?.Invoke(fingertip, t);
        }
    }
}
