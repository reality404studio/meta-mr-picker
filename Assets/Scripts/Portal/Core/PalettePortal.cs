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
        public int ChipCount
        {
            get => _chipCount;
            set { _chipCount = Mathf.Max(0, value); FrontIndex = 0; WheelDrag = 0f; Sweeping = false; _hasSweep = false; }
        }
        public int SlotCount => Mathf.Max(_chipCount, Mathf.Max(1, _s.wheelMinSlots));
        public float SlotStepDegrees => 360f / SlotCount;
        public int FrontIndex { get; private set; }
        public float WheelDrag { get; private set; }
        public bool Sweeping { get; private set; }
        public int FrontChip => FrontIndex < _chipCount ? FrontIndex : -1;

        public event Action<float> Opened;
        public event Action<float> Closed;
        public event Action<DipEvent> Dipped;
        public event Action<int, float> Rotated;
        public event Action<float> Washed;
        public const int WellTarget = -2;

        float _upSince = -1f, _downSince = -1f, _lostSince = -1f, _closedAt = float.NegativeInfinity, _lastT = float.NaN;
        bool _gateHot;
        Vector3 _lastPalm, _smoothNormal;
        bool _hasPalm;
        bool _wasAbove, _inside;
        float _enteredAt;
        int _entryChip;
        int _chipCount;
        bool _hasSweep;
        float _sweepLast, _sweepAccum, _sweepZ, _sweepT;
        float _leftPinchReleased = float.NegativeInfinity;
        bool _leftPinching;

        public bool LeftPinchBlocks(float t) => _leftPinching || t - _leftPinchReleased < _s.leftPinchReleaseSeconds;

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

            bool pinching = l.PinchDistance < _s.leftPinchDistance;
            if (_leftPinching && !pinching) _leftPinchReleased = f.t;
            _leftPinching = pinching;
            bool blocked = LeftPinchBlocks(f.t);

            Vector3 n = PalmNormal(l).normalized;
            float up = Vector3.Dot(n, Vector3.up);
            float speed = _hasPalm && dt > 0f ? (l.palm - _lastPalm).magnitude / dt : 0f;
            _lastPalm = l.palm;
            _hasPalm = true;
            _gateHot = up > _s.closeUpDot && !blocked;

            if (!IsOpen)
            {
                if (!blocked && up >= _s.openUpDot && speed <= _s.maxPalmSpeed)
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
            EndSweep(t, false);
            Opened?.Invoke(t);
        }

        void Close(float t)
        {
            IsOpen = false;
            _downSince = -1f;
            _inside = false;
            EndSweep(t, false);
            _closedAt = t;
            Closed?.Invoke(t);
        }

        void UpdateDip(HandFrame f)
        {
            HandPose r = f.right;
            if (!r.tracked) { _inside = false; _wasAbove = false; EndSweep(f.t, true); return; }
            Vector3 p = Pose.ToLocal(r.indexTip);
            bool overDisk = new Vector2(p.x, p.y).magnitude <= _s.radius;
            UpdateSweep(p, f.t);

            if (!_inside)
            {
                if (overDisk && p.z > 0f) _wasAbove = true;
                else if (!overDisk) _wasAbove = false;
                if (_wasAbove && overDisk && p.z <= -_s.dipDepth)
                {
                    EndSweep(f.t, false);
                    _inside = true;
                    _enteredAt = f.t;
                    _entryChip = DipTarget(new Vector2(p.x, p.y));
                }
                return;
            }

            if (f.t - _enteredAt > _s.dipMaxSeconds || (!overDisk && p.z < 0f)) { _inside = false; _wasAbove = false; return; }
            if (p.z >= _s.exitHeight)
            {
                _inside = false;
                _wasAbove = true;
                if (_entryChip == WellTarget) Washed?.Invoke(f.t);
                else if (_entryChip >= 0) Dipped?.Invoke(new DipEvent { chip = _entryChip, t = f.t, tip = r.indexTip });
            }
        }

        void UpdateSweep(Vector3 p, float t)
        {
            float r = new Vector2(p.x, p.y).magnitude;
            bool inBand = !_inside && r >= _s.rimInner && r <= _s.rimOuter && p.z >= -_s.rimBelow && p.z <= _s.rimAbove;
            if (!inBand) { EndSweep(t, p.z >= -_s.rimBelow); return; }
            float a = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
            if (!_hasSweep) { _hasSweep = true; _sweepLast = a; _sweepAccum = 0f; _sweepZ = p.z; _sweepT = t; return; }
            float vz = t > _sweepT ? Mathf.Abs(p.z - _sweepZ) / (t - _sweepT) : 0f;
            _sweepZ = p.z;
            _sweepT = t;
            if (!Sweeping && vz > _s.sweepMaxVerticalSpeed) { _sweepAccum = 0f; _sweepLast = a; return; }
            _sweepAccum += Mathf.DeltaAngle(_sweepLast, a);
            _sweepLast = a;
            if (!Sweeping && Mathf.Abs(_sweepAccum) >= _s.sweepEngageDegrees && _chipCount > 0) Sweeping = true;
            if (Sweeping) WheelDrag = _sweepAccum;
        }

        void EndSweep(float t, bool snap)
        {
            if (snap && Sweeping)
            {
                int k = Mathf.RoundToInt(WheelDrag / SlotStepDegrees);
                if (k != 0)
                {
                    int n = SlotCount;
                    FrontIndex = ((FrontIndex - k) % n + n) % n;
                    Rotated?.Invoke(FrontIndex, t);
                }
            }
            Sweeping = false;
            WheelDrag = 0f;
            _hasSweep = false;
            _sweepAccum = 0f;
        }

        public float SlotAngle(int i) => _s.frontAngleDegrees + (i - FrontIndex) * SlotStepDegrees + WheelDrag;

        public Vector2 ChipSlot(int i)
        {
            float a = SlotAngle(i) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * _s.wheelRadius;
        }

        public Vector2 FrontSlot
        {
            get { float a = _s.frontAngleDegrees * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * _s.wheelRadius; }
        }

        public float ChipSize(float max)
        {
            float arc = 2f * Mathf.PI * _s.wheelRadius / SlotCount;
            return Mathf.Min(max, arc * 0.8f);
        }

        public int DipTarget(Vector2 xy)
        {
            if (xy.magnitude <= _s.wellRadius) return WellTarget;
            if (FrontChip >= 0 && (xy - FrontSlot).magnitude <= _s.slotHitRadius) return FrontChip;
            return -1;
        }
    }
}
