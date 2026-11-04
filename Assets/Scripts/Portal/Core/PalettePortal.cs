using System;
using MoodPicker.Hands;
using UnityEngine;

namespace MoodPicker.Portal
{
    public struct PortalPose
    {
        public Vector3 center;
        public Vector3 normal;
        public Vector3 right;
        public Vector3 forward;

        public Vector3 ToLocal(Vector3 p)
        {
            Vector3 d = p - center;
            return new Vector3(Vector3.Dot(d, right), Vector3.Dot(d, forward), Vector3.Dot(d, normal));
        }

        public Vector3 ToWorld(Vector2 xy) => center + right * xy.x + forward * xy.y;
    }

    public struct DipEvent
    {
        public int chip;
        public float t;
        public Vector3 tip;
    }

    public class PalettePortal : IPinchGate
    {
        readonly PortalSettings _s;

        public bool IsOpen { get; private set; }
        public PortalPose Pose { get; private set; }
        public int ChipCount { get; set; }

        public event Action<float> Opened;
        public event Action<float> Closed;
        public event Action<DipEvent> Dipped;

        float _upSince = -1f, _downSince = -1f, _lostSince = -1f, _closedAt = float.NegativeInfinity, _lastT = float.NaN;
        bool _gateHot;
        Vector3 _lastPalm, _smoothNormal;
        bool _hasPalm;
        bool _wasAbove, _inside;
        float _enteredAt;
        int _entryChip;

        public PalettePortal(PortalSettings settings) { _s = settings; }

        public bool SuppressesPinch(float t) => IsOpen || _gateHot || t - _closedAt < _s.pinchCooldownSeconds;

        public Vector3 PalmNormal(HandPose p) => _s.invertPalmNormal ? -p.palmNormal : p.palmNormal;

        public void Update(HandFrame f)
        {
            float dt = float.IsNaN(_lastT) ? 0f : Mathf.Max(0f, f.t - _lastT);
            _lastT = f.t;
            HandPose l = f.left;

            if (!l.tracked)
            {
                _upSince = -1f;
                _hasPalm = false;
                _gateHot = false;
                if (IsOpen)
                {
                    if (_lostSince < 0f) _lostSince = f.t;
                    if (f.t - _lostSince >= _s.lostCloseSeconds) Close(f.t);
                }
                return;
            }
            _lostSince = -1f;

            Vector3 n = PalmNormal(l).normalized;
            float up = Vector3.Dot(n, Vector3.up);
            float speed = _hasPalm && dt > 0f ? (l.palm - _lastPalm).magnitude / dt : 0f;
            _lastPalm = l.palm;
            _hasPalm = true;
            _gateHot = up > _s.closeUpDot;

            if (!IsOpen)
            {
                if (up >= _s.openUpDot && speed <= _s.maxPalmSpeed)
                {
                    if (_upSince < 0f) _upSince = f.t;
                    if (f.t - _upSince >= _s.openHoldSeconds) Open(f.t, n);
                }
                else _upSince = -1f;
            }
            else
            {
                if (up < _s.closeUpDot)
                {
                    if (_downSince < 0f) _downSince = f.t;
                    if (f.t - _downSince >= _s.closeHoldSeconds) { Close(f.t); return; }
                }
                else _downSince = -1f;
            }

            if (!IsOpen) return;
            float k = 1f - Mathf.Exp(-_s.normalSmoothing * dt);
            _smoothNormal = Vector3.Slerp(_smoothNormal, n, k).normalized;
            Pose = MakePose(l, _smoothNormal);
            UpdateDip(f);
        }

        PortalPose MakePose(HandPose l, Vector3 n)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(l.middleTip - l.palm, n);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(Vector3.forward, n);
            fwd.Normalize();
            return new PortalPose
            {
                center = l.palm + n * _s.heightAbovePalm,
                normal = n,
                forward = fwd,
                right = Vector3.Cross(n, fwd).normalized,
            };
        }

        void Open(float t, Vector3 n)
        {
            IsOpen = true;
            _upSince = -1f;
            _downSince = -1f;
            _smoothNormal = n;
            _wasAbove = false;
            _inside = false;
            Opened?.Invoke(t);
        }

        void Close(float t)
        {
            IsOpen = false;
            _downSince = -1f;
            _inside = false;
            _closedAt = t;
            Closed?.Invoke(t);
        }

        void UpdateDip(HandFrame f)
        {
            HandPose r = f.right;
            if (!r.tracked) { _inside = false; _wasAbove = false; return; }
            Vector3 p = Pose.ToLocal(r.indexTip);
            bool overDisk = new Vector2(p.x, p.y).magnitude <= _s.radius;

            if (!_inside)
            {
                if (overDisk && p.z > 0f) _wasAbove = true;
                else if (!overDisk) _wasAbove = false;
                if (_wasAbove && overDisk && p.z <= -_s.dipDepth)
                {
                    _inside = true;
                    _enteredAt = f.t;
                    _entryChip = ChipAt(new Vector2(p.x, p.y));
                }
                return;
            }

            if (f.t - _enteredAt > _s.dipMaxSeconds || (!overDisk && p.z < 0f)) { _inside = false; _wasAbove = false; return; }
            if (p.z >= _s.exitHeight)
            {
                _inside = false;
                _wasAbove = true;
                if (_entryChip >= 0) Dipped?.Invoke(new DipEvent { chip = _entryChip, t = f.t, tip = r.indexTip });
            }
        }

        public Vector2 ChipSlot(int i)
        {
            int cols = Mathf.Max(1, _s.chipCols);
            int rows = Mathf.Max(1, (ChipCount + cols - 1) / cols);
            float x = (i % cols - (cols - 1) * 0.5f) * _s.chipPitch;
            float y = ((rows - 1) * 0.5f - i / cols) * _s.chipPitch;
            return new Vector2(x, y);
        }

        public int ChipAt(Vector2 xy)
        {
            int best = -1;
            float bd = float.MaxValue;
            for (int i = 0; i < ChipCount; i++)
            {
                float d = (ChipSlot(i) - xy).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }
    }
}
