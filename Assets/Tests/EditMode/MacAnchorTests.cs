using System;
using System.Collections.Generic;
using System.IO;
using MoodPicker.Hands;
using MoodPicker.MacAnchoring;
using NUnit.Framework;
using UnityEngine;

namespace MoodPicker.Tests
{
    public class MacAnchorTests
    {
        [Serializable]
        sealed class SavedFixture
        {
            public int version = MacAnchorSettings.FormatVersion;
            public MacScreenRectangle rectangle;
        }

        string _directory;
        string _path;
        MacAnchorSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "orion-mac-anchor-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _path = Path.Combine(_directory, "nested", MacAnchorSettings.FileName);
            _settings = new MacAnchorSettings();
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_directory, true);

        static MacScreenRectangle Screen(float tilt = 15f) => new MacScreenRectangle
        {
            center = new Vector3(0.12f, 1.05f, 0.55f),
            normal = Quaternion.Euler(tilt, 23f, 4f) * Vector3.back,
            up = Quaternion.Euler(tilt, 23f, 4f) * Vector3.up,
            widthMeters = 0.32f,
            heightMeters = 0.21f
        };

        static HandFrame FourPoints(MacScreenRectangle screen, float time, float jitter = 0f)
        {
            var plane = screen.Plane;
            float w = screen.widthMeters * 0.5f, h = screen.heightMeters * 0.5f;
            Vector3 P(float x, float z) => plane.ToWorld(new Vector3(x, 0f, z));
            Vector3 noise = new Vector3(jitter, -jitter, jitter);
            return new HandFrame
            {
                t = time,
                left = new HandPose
                {
                    tracked = true, indexTip = P(-w, h) + noise, thumbTip = P(-w, -h) - noise
                },
                right = new HandPose
                {
                    tracked = true, thumbTip = P(w, -h) + noise, indexTip = P(w, h) - noise
                }
            };
        }

        static IEnumerable<HandFrame> Trajectory(MacScreenRectangle screen, float start = 0f)
        {
            for (int k = 0; k < 200; k++)
                yield return FourPoints(screen, start + k / 72f, Mathf.Sin(k) * 0.001f);
        }

        void WriteFixture(MacScreenRectangle screen, int version = MacAnchorSettings.FormatVersion)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, JsonUtility.ToJson(new SavedFixture { version = version, rectangle = screen }));
        }

        [TestCase(-25f)]
        [TestCase(0f)]
        [TestCase(25f)]
        public void FourPointHandTrajectory_PreservesScreenPlaneAndSizeWithinOneCentimeter(float tilt)
        {
            var expected = Screen(tilt);
            var calibrator = new MacAnchorCalibrator(_settings);
            foreach (var frame in Trajectory(expected)) calibrator.UpdateCorners(frame);
            Assert.That(calibrator.Done, Is.True);
            var actual = calibrator.Rectangle;
            Assert.That(Vector3.Distance(actual.center, expected.center), Is.LessThanOrEqualTo(0.01f));
            Assert.That(actual.widthMeters, Is.EqualTo(expected.widthMeters).Within(0.01f));
            Assert.That(actual.heightMeters, Is.EqualTo(expected.heightMeters).Within(0.01f));
            Assert.That(Vector3.Angle(actual.normal, expected.normal), Is.LessThan(1f));
            Assert.That(Vector3.Angle(actual.up, expected.up), Is.LessThan(1f));
            Assert.That(Vector3.Distance(actual.center + actual.normal * actual.widthMeters,
                expected.center + expected.normal * expected.widthMeters), Is.LessThanOrEqualTo(0.01f));
            Assert.That(calibrator.Progress, Is.EqualTo(1f));
        }

        [Test]
        public void TwoBottomPinchesAndHeight_ProduceSameRectangle()
        {
            var expected = Screen();
            var calibrator = new MacAnchorCalibrator(_settings);
            foreach (var frame in Trajectory(expected))
            {
                var pinches = frame;
                pinches.left.indexTip = pinches.left.thumbTip;
                pinches.right.indexTip = pinches.right.thumbTip;
                calibrator.UpdatePinches(pinches, expected.heightMeters, expected.up);
            }
            Assert.That(calibrator.Done, Is.True);
            Assert.That(Vector3.Distance(calibrator.Rectangle.center, expected.center), Is.LessThan(0.01f));
            Assert.That(calibrator.Rectangle.widthMeters, Is.EqualTo(expected.widthMeters).Within(0.01f));
            Assert.That(calibrator.Rectangle.heightMeters, Is.EqualTo(expected.heightMeters).Within(0.01f));
            Assert.That(Vector3.Angle(calibrator.Rectangle.normal, expected.normal), Is.LessThan(1f));
        }

        [Test]
        public void SaveThenReload_PreservesAllValuesAndOverwritesPreviousAnchor()
        {
            var anchor = new MacAnchor(_path, _settings);
            Assert.That(anchor.IsAvailable, Is.False);
            Assert.That(anchor.Save(Screen()), Is.True);
            var replacement = Screen(-20f);
            replacement.center += Vector3.right;
            Assert.That(anchor.Save(replacement), Is.True);
            var reloaded = new MacAnchor(_path, _settings);
            Assert.That(reloaded.IsAvailable, Is.True);
            Assert.That(reloaded.Rectangle, Is.EqualTo(replacement));
            Assert.That(File.Exists(_path + ".tmp"), Is.False);
        }

        [Test]
        public void CalibratedButUnsaved_AndMissingAnchor_AreUnavailable()
        {
            var calibrator = new MacAnchorCalibrator(_settings);
            foreach (var frame in Trajectory(Screen())) calibrator.UpdateCorners(frame);
            Assert.That(calibrator.Done, Is.True);
            var anchor = new MacAnchor(_path, _settings);
            Assert.That(anchor.IsAvailable, Is.False);
            Assert.That(anchor.Save(calibrator.Rectangle), Is.True);
            Assert.That(anchor.IsAvailable, Is.True);
            File.Delete(_path);
            Assert.That(anchor.Load(), Is.False);
            Assert.That(anchor.IsAvailable, Is.False);
            Assert.That(anchor.Rectangle, Is.EqualTo(default(MacScreenRectangle)));
        }

        [TestCase(0.049f, 0.21f)]
        [TestCase(0.601f, 0.21f)]
        [TestCase(0.32f, 0.049f)]
        [TestCase(0.32f, 0.601f)]
        public void OutOfRangeWidthOrHeight_IsUnavailable(float width, float height)
        {
            var screen = Screen();
            screen.widthMeters = width;
            screen.heightMeters = height;
            WriteFixture(screen);
            var anchor = new MacAnchor(_path, _settings);
            Assert.That(anchor.IsAvailable, Is.False);
            Assert.That(anchor.Save(screen), Is.False);
        }

        [TestCase(0.05f)]
        [TestCase(0.60f)]
        public void SizeLimits_AreInclusive(float size)
        {
            var screen = Screen();
            screen.widthMeters = screen.heightMeters = size;
            WriteFixture(screen);
            Assert.That(new MacAnchor(_path, _settings).IsAvailable, Is.True);
        }

        [TestCase(-90f, false)]
        [TestCase(-30.1f, false)]
        [TestCase(-30f, true)]
        [TestCase(30f, true)]
        [TestCase(30.1f, false)]
        [TestCase(90f, false)]
        public void VerticalTiltLimit_IsAppliedToSavedPlane(float tilt, bool available)
        {
            var screen = Screen();
            screen.normal = Quaternion.Euler(tilt, 0f, 0f) * Vector3.back;
            screen.up = Quaternion.Euler(tilt, 0f, 0f) * Vector3.up;
            WriteFixture(screen);
            Assert.That(new MacAnchor(_path, _settings).IsAvailable, Is.EqualTo(available));
        }

        [TestCase("")]
        [TestCase("{")]
        [TestCase("{}")]
        [TestCase("null")]
        public void CorruptOrIncompleteJson_IsUnavailable(string json)
        {
            File.WriteAllText(_path = Path.Combine(_directory, "bad.json"), json);
            Assert.That(new MacAnchor(_path, _settings).IsAvailable, Is.False);
        }

        [Test]
        public void UnknownVersion_AndInvalidBasis_AreUnavailable()
        {
            WriteFixture(Screen(), MacAnchorSettings.FormatVersion + 1);
            Assert.That(new MacAnchor(_path, _settings).IsAvailable, Is.False);
            var screen = Screen();
            screen.up = screen.normal;
            WriteFixture(screen);
            Assert.That(new MacAnchor(_path, _settings).IsAvailable, Is.False);
            screen = Screen();
            screen.normal = Vector3.zero;
            WriteFixture(screen);
            Assert.That(new MacAnchor(_path, _settings).IsAvailable, Is.False);
        }

        [Test]
        public void NonFiniteValues_AreRejected()
        {
            var screen = Screen();
            screen.center.x = float.NaN;
            Assert.That(screen.IsValid(_settings), Is.False);
            screen = Screen();
            screen.widthMeters = float.PositiveInfinity;
            Assert.That(screen.IsValid(_settings), Is.False);
        }

        [Test]
        public void Settings_ControlValidationAndCalibration()
        {
            _settings.MaxSizeMeters = 0.30f;
            Assert.That(new MacAnchor(_path, _settings).Save(Screen()), Is.False);
            var calibrator = new MacAnchorCalibrator(_settings);
            foreach (var frame in Trajectory(Screen())) calibrator.UpdateCorners(frame);
            Assert.That(calibrator.Done, Is.False);
        }

        [Test]
        public void LostTracking_ResetsHold_AndNewTrajectoryCanComplete()
        {
            var calibrator = new MacAnchorCalibrator(_settings);
            for (int k = 0; k < 120; k++) calibrator.UpdateCorners(FourPoints(Screen(), k / 72f));
            Assert.That(calibrator.Done, Is.False);
            var lost = FourPoints(Screen(), 120f / 72f);
            lost.right.tracked = false;
            calibrator.UpdateCorners(lost);
            Assert.That(calibrator.Progress, Is.Zero);
            foreach (var frame in Trajectory(Screen(), 2f)) calibrator.UpdateCorners(frame);
            Assert.That(calibrator.Done, Is.True);
            calibrator.Reset();
            Assert.That(calibrator.Done, Is.False);
            foreach (var frame in Trajectory(Screen(-20f), 0f)) calibrator.UpdateCorners(frame);
            Assert.That(calibrator.Done, Is.True);
        }

        [Test]
        public void MovingHands_AndNonPlanarCorners_DoNotCalibrate()
        {
            var calibrator = new MacAnchorCalibrator(_settings);
            for (int k = 0; k < 250; k++)
            {
                var screen = Screen();
                screen.center.x += k / 72f;
                calibrator.UpdateCorners(FourPoints(screen, k / 72f));
            }
            Assert.That(calibrator.Done, Is.False);
            calibrator.Reset();
            foreach (var frame in Trajectory(Screen()))
            {
                var warped = frame;
                warped.left.indexTip += Screen().normal * 0.1f;
                calibrator.UpdateCorners(warped);
            }
            Assert.That(calibrator.Done, Is.False);
        }

        [Test]
        public void ChangingScreenOrientationBeforeHold_CanSettleAtNewOrientation()
        {
            var calibrator = new MacAnchorCalibrator(_settings);
            calibrator.UpdateCorners(FourPoints(Screen(-25f), 0f));
            foreach (var frame in Trajectory(Screen(25f), 2f)) calibrator.UpdateCorners(frame);
            Assert.That(calibrator.Done, Is.True);
            Assert.That(Vector3.Angle(calibrator.Rectangle.normal, Screen(25f).normal), Is.LessThan(1f));
        }

        [Test]
        public void SaveIoFailure_DoesNotMakeUnsavedAnchorAvailable()
        {
            string blocker = Path.Combine(_directory, "file-not-directory");
            File.WriteAllText(blocker, "block");
            var anchor = new MacAnchor(Path.Combine(blocker, "anchor.json"), _settings);
            Assert.That(anchor.Save(Screen()), Is.False);
            Assert.That(anchor.IsAvailable, Is.False);
        }
    }
}
