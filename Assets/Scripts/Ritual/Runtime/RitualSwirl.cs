using System.Collections.Generic;
using UnityEngine;

namespace MoodPicker.Ritual
{
    public sealed class RitualSwirl
    {
        readonly List<Transform> _layers = new List<Transform>();
        readonly List<Vector3> _restPos = new List<Vector3>();
        readonly List<Quaternion> _restRot = new List<Quaternion>();
        readonly List<float> _angle = new List<float>();
        readonly List<float> _speed = new List<float>();

        public int Count => _layers.Count;
        public float Energy { get; private set; }
        public float AngleOf(int i) => _angle[i];

        public void Clear()
        {
            for (int i = 0; i < _layers.Count; i++)
            {
                if (_layers[i] == null) continue;
                _layers[i].localRotation = _restRot[i];
                _layers[i].localPosition = _restPos[i];
            }
            _layers.Clear();
            _restPos.Clear();
            _restRot.Clear();
            _angle.Clear();
            _speed.Clear();
            Energy = 0f;
        }

        public void Add(Transform layer)
        {
            if (layer == null) return;
            _layers.Add(layer);
            _restPos.Add(layer.localPosition);
            _restRot.Add(layer.localRotation);
            _angle.Add(0f);
            _speed.Add(0f);
        }

        public void Kick(float degreesPerSecond)
        {
            int n = _layers.Count;
            for (int i = 0; i < n; i++)
            {
                float dir = i % 2 == 0 ? 1f : -1f;
                float k = 0.55f + 0.45f * (n <= 1 ? 1f : i / (float)(n - 1));
                _speed[i] += dir * k * degreesPerSecond;
            }
            Energy = Mathf.Max(Energy, Mathf.Abs(degreesPerSecond));
        }

        public void Tick(float dt)
        {
            float e = 0f;
            float decay = Mathf.Exp(-dt * 1.3f);
            for (int i = 0; i < _layers.Count; i++)
            {
                float sp = _speed[i];
                if (Mathf.Abs(sp) < 0.2f && Energy <= 0f) continue;
                _angle[i] += sp * dt;
                sp *= decay;
                if (Mathf.Abs(sp) < 0.2f) sp = 0f;
                _speed[i] = sp;
                e = Mathf.Max(e, Mathf.Abs(sp));
                Transform t = _layers[i];
                if (t == null) continue;
                float lift = 0.0025f * Mathf.Clamp01(Mathf.Abs(sp) / 200f);
                t.localRotation = _restRot[i] * Quaternion.AngleAxis(_angle[i], Vector3.up);
                t.localPosition = _restPos[i] + Vector3.up * lift;
            }
            Energy = e;
        }
    }
}
