using System;
using MoodPicker.DeskKeyboard;
using MoodPicker.Hands;
using MoodPicker.MacAnchoring;
using UnityEngine;

namespace MoodPicker.PullOut
{
    public sealed class ScreenTapDetector
    {
        readonly MacScreenRectangle _screen;
        readonly PullOutSettings _settings;
        readonly TouchDetector _touch;
        bool _armed;
        float? _contactTime;
        float? _lastTime;

        public event Action<float> Tapped;

        public ScreenTapDetector(MacScreenRectangle screen, PullOutSettings settings = null)
        {
            if (!screen.IsValid(new MacAnchorSettings())) throw new ArgumentException("Invalid screen rectangle.");
            _screen = screen;
            _settings = (settings ?? new PullOutSettings()).Snapshot();
            _touch = new TouchDetector(screen.Plane)
            {
                enterHeight = _settings.EnterDepthMeters,
                exitHeight = _settings.ExitDepthMeters,
                armHeight = _settings.ArmDepthMeters,
                minDownSpeed = _settings.MinDownSpeed,
                minContactSpeed = _settings.MinContactSpeed,
                predictLead = _settings.PredictLeadSeconds,
                velocitySmoothing = _settings.VelocitySmoothing
            };
            _touch.Contact += OnContact;
        }

        public void Reset()
        {
            _armed = false;
            _contactTime = null;
            _lastTime = null;
            _touch.Update(default);
        }

        public void Update(float time, Vector3 indexTip, bool tracked, bool isPointing, bool isBareHand)
        {
            if (!PullOutSettings.Finite(time) || (_lastTime.HasValue && time <= _lastTime.Value))
            {
                Reset();
                return;
            }
            if (_lastTime.HasValue && time - _lastTime.Value > _settings.MaxSampleGapSeconds) Reset();
            _lastTime = time;
            if (!tracked || !isPointing || !isBareHand || !Finite(indexTip))
            {
                Reset();
                return;
            }
            Vector3 local = _screen.Plane.ToLocal(indexTip);
            if (Mathf.Abs(local.x) > _screen.widthMeters * 0.5f ||
                Mathf.Abs(local.z) > _screen.heightMeters * 0.5f || local.y < -_settings.MaxPenetrationMeters)
            {
                Reset();
                return;
            }
            if (local.y >= _settings.ArmDepthMeters) _armed = true;
            if (_contactTime.HasValue && local.y >= _settings.EnterDepthMeters)
            {
                _contactTime = null;
                _armed = local.y >= _settings.ArmDepthMeters;
            }
            _touch.Update(new HandFrame
            {
                t = time,
                right = new HandPose { tracked = true, indexTip = indexTip }
            });
            if (!_contactTime.HasValue || time - _contactTime.Value < _settings.HoldSeconds) return;
            _contactTime = null;
            _armed = false;
            Tapped?.Invoke(time);
        }

        void OnContact(TouchEvent contact)
        {
            if (contact.finger == Finger.RightIndex && _armed)
            {
                _armed = false;
                _contactTime = contact.t;
            }
        }

        static bool Finite(Vector3 value) => PullOutSettings.Finite(value.x) &&
            PullOutSettings.Finite(value.y) && PullOutSettings.Finite(value.z);
    }
}
