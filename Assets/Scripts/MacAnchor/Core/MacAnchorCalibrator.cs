using System;
using MoodPicker.DeskKeyboard;
using MoodPicker.Hands;
using UnityEngine;

namespace MoodPicker.MacAnchoring
{
    public sealed class MacAnchorCalibrator
    {
        readonly MacAnchorSettings _settings;
        RectCalibrator _calibrator;
        DeskPlane? _reference;
        float? _lastTime;

        public bool Done { get; private set; }
        public MacScreenRectangle Rectangle { get; private set; }
        public float Progress => _calibrator.Progress;

        public MacAnchorCalibrator(MacAnchorSettings settings = null)
        {
            _settings = settings ?? new MacAnchorSettings();
            Reset();
        }

        public void Reset()
        {
            if (!_settings.IsValid) throw new ArgumentException("Invalid MacAnchor settings.");
            _calibrator = new RectCalibrator
            {
                holdSeconds = _settings.HoldSeconds,
                ignoreFirstSeconds = _settings.IgnoreFirstSeconds,
                stillRadius = _settings.StillRadiusMeters,
                maxHeightSpread = _settings.RectangleToleranceMeters,
                minWidth = _settings.MinSizeMeters,
                maxWidth = _settings.MaxSizeMeters
            };
            _reference = null;
            _lastTime = null;
            Done = false;
            Rectangle = default;
        }

        public void UpdatePinches(HandFrame pinchesAtBottomEdge, float heightMeters, Vector3 screenUp)
        {
            if (!MacScreenRectangle.Finite(screenUp) || screenUp.sqrMagnitude == 0f ||
                !MacAnchorSettings.Finite(heightMeters) || heightMeters <= 0f)
            {
                if (!Done) Reset();
                return;
            }
            Vector3 left = pinchesAtBottomEdge.left.PinchPoint;
            Vector3 right = pinchesAtBottomEdge.right.PinchPoint;
            Vector3 height = screenUp.normalized * heightMeters;
            var corners = pinchesAtBottomEdge;
            corners.left.thumbTip = left;
            corners.left.indexTip = left + height;
            corners.right.thumbTip = right;
            corners.right.indexTip = right + height;
            UpdateCorners(corners);
        }

        public void UpdateCorners(HandFrame rectangleCorners)
        {
            if (Done) return;
            var f = rectangleCorners;
            var c = RectCalibrator.CornersOf(f);
            if (!f.left.tracked || !f.right.tracked || !MacAnchorSettings.Finite(f.t) ||
                (_lastTime.HasValue && f.t <= _lastTime.Value) ||
                !MacScreenRectangle.TryFromCorners(c[0], c[1], c[2], c[3], _settings, out var candidate))
            {
                Reset();
                return;
            }
            _lastTime = f.t;
            if (!_reference.HasValue) _reference = candidate.Plane;
            var basis = _reference.Value;
            f.left.indexTip = basis.ToLocal(c[0]);
            f.left.thumbTip = basis.ToLocal(c[1]);
            f.right.thumbTip = basis.ToLocal(c[2]);
            f.right.indexTip = basis.ToLocal(c[3]);
            _calibrator.Update(f);
            if (_calibrator.State == RectReject.Tilted)
            {
                Reset();
                return;
            }
            if (!_calibrator.Done) return;
            var mean = _calibrator.Corners;
            if (!MacScreenRectangle.TryFromCorners(basis.ToWorld(mean[0]), basis.ToWorld(mean[1]),
                basis.ToWorld(mean[2]), basis.ToWorld(mean[3]), _settings, out var result))
            {
                Reset();
                return;
            }
            Rectangle = result;
            Done = true;
        }
    }
}
