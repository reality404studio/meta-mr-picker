using System;
using MoodPicker.Tokens;
using System.Collections.Generic;
using System.Linq;
using MoodPicker.MacAnchoring;
using MoodPicker.PullOut;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MoodPicker.Tests
{
    public class PullOutTests
    {
        static MacScreenRectangle Screen => new MacScreenRectangle
        {
            center = new Vector3(0.4f, 1.1f, 0.7f),
            normal = Quaternion.Euler(12f, 35f, 0f) * Vector3.back,
            up = Quaternion.Euler(12f, 35f, 0f) * Vector3.up,
            widthMeters = 0.32f,
            heightMeters = 0.21f
        };

        static Vector3 Tip(float depth, float x = 0f, float height = 0f) =>
            Screen.Plane.ToWorld(new Vector3(x, depth, height));

        static void Tap(Action<float, Vector3, bool, bool, bool> update, float start = 1f,
            float x = 0f, float height = 0f)
        {
            update(start, Tip(0.04f, x, height), true, true, true);
            update(start + 0.02f, Tip(0.02f, x, height), true, true, true);
            for (int i = 2; i <= 8; i++)
                update(start + 0.02f * i, Tip(0.001f, x, height), true, true, true);
        }

        [Test]
        public void InsideTap_StartsExactlyOnceInOrderAtPointOneFiveSecondIntervals()
        {
            var sequence = new PullOutSequence(Screen);
            var starts = new List<LetterStart>();
            int taps = 0;
            sequence.Tapped += _ => taps++;
            sequence.LetterStarted += starts.Add;
            Tap(sequence.UpdateTip);
            Assert.That(sequence.HasStarted, Is.True);
            Assert.That(starts.Select(e => e.Letter), Is.EqualTo(new[] { PullOutLetter.O1 }));
            for (int i = 1; i < 5; i++)
            {
                float start = sequence.TapTime + OrionTokens.MotionPulloutStaggerStaggerSeconds * i;
                sequence.Advance(start - 0.0001f);
                Assert.That(starts.Count, Is.EqualTo(i));
                sequence.Advance(start);
                Assert.That(starts.Count, Is.EqualTo(i + 1));
            }
            sequence.Advance(sequence.EndTime + 1f);
            Tap(sequence.UpdateTip, 3f);
            sequence.Advance(4f);
            Assert.That(taps, Is.EqualTo(1));
            Assert.That(starts.Select(e => e.Letter), Is.EqualTo(new[]
                { PullOutLetter.O1, PullOutLetter.R, PullOutLetter.I, PullOutLetter.O2, PullOutLetter.N }));
            for (int i = 0; i < 5; i++)
                Assert.That(starts[i].Time, Is.EqualTo(sequence.TapTime + OrionTokens.MotionPulloutStaggerStaggerSeconds * i).Within(0.00001f));
        }

        [TestCase(-0.17f, 0f)]
        [TestCase(0.17f, 0f)]
        [TestCase(0f, -0.115f)]
        [TestCase(0f, 0.115f)]
        public void TenOutsideTaps_DoNotFire(float x, float height)
        {
            var detector = new ScreenTapDetector(Screen);
            int taps = 0;
            detector.Tapped += _ => taps++;
            for (int i = 0; i < 10; i++) Tap(detector.Update, 1f + i, x, height);
            Assert.That(taps, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TenTremorsBelowThreshold_FireAtMostOnce(bool approachFromArmDepth)
        {
            var detector = new ScreenTapDetector(Screen);
            int taps = 0;
            detector.Tapped += _ => taps++;
            for (int i = 0; i < 10; i++)
            {
                float t = 1f + i * 0.2f;
                detector.Update(t, Tip(approachFromArmDepth ? 0.04f : 0.009f), true, true, true);
                detector.Update(t + 0.02f, Tip(0.002f), true, true, true);
                detector.Update(t + 0.04f, Tip(0.004f), true, true, true);
                detector.Update(t + 0.06f, Tip(0.009f), true, true, true);
                detector.Update(t + 0.08f, Tip(0.001f), true, true, true);
                detector.Update(t + 0.10f, Tip(0.008f), true, true, true);
            }
            Assert.That(taps, Is.LessThanOrEqualTo(1));
            Assert.That(taps, Is.Zero);
        }

        [Test]
        public void ValidTapRequiresContinuousHold_AndHeldFingerDoesNotRepeat()
        {
            var detector = new ScreenTapDetector(Screen);
            var taps = new List<float>();
            detector.Tapped += taps.Add;
            detector.Update(0f, Tip(0.04f), true, true, true);
            detector.Update(0.02f, Tip(0.001f), true, true, true);
            detector.Update(0.06f, Tip(0.001f), true, true, true);
            Assert.That(taps, Is.Empty);
            for (int i = 4; i < 50; i++) detector.Update(i * 0.02f, Tip(0.001f), true, true, true);
            Assert.That(taps.Count, Is.EqualTo(1));
            Assert.That(taps[0], Is.GreaterThanOrEqualTo(0.08f));
            Tap(detector.Update, 1f);
            Assert.That(taps.Count, Is.EqualTo(2));
        }

        [TestCase(false, true, true)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        public void TrackingPointingAndBareHand_AreRequired(bool tracked, bool pointing, bool bare)
        {
            var detector = new ScreenTapDetector(Screen);
            int taps = 0;
            detector.Tapped += _ => taps++;
            Tap((t, p, a, b, c) => detector.Update(t, p, tracked, pointing, bare));
            Assert.That(taps, Is.Zero);
            Tap(detector.Update, 2f);
            Assert.That(taps, Is.EqualTo(1));
        }

        [TestCase("tracking")]
        [TestCase("pointing")]
        [TestCase("bare")]
        [TestCase("outside")]
        [TestCase("gap")]
        [TestCase("backwards")]
        [TestCase("nan")]
        [TestCase("deep")]
        public void InterruptedContact_CannotCompleteUntilANewApproach(string interruption)
        {
            var detector = new ScreenTapDetector(Screen);
            int taps = 0;
            detector.Tapped += _ => taps++;
            detector.Update(0f, Tip(0.04f), true, true, true);
            detector.Update(0.02f, Tip(0.001f), true, true, true);
            float time = interruption == "gap" ? 0.3f : interruption == "backwards" ? 0.01f : 0.04f;
            Vector3 tip = interruption == "outside" ? Tip(0.001f, 0.2f) :
                interruption == "nan" ? new Vector3(float.NaN, 0f, 0f) :
                interruption == "deep" ? Tip(-0.03f) : Tip(0.001f);
            detector.Update(time, tip, interruption != "tracking", interruption != "pointing", interruption != "bare");
            for (int i = 1; i <= 10; i++) detector.Update(time + i * 0.02f, Tip(0.001f), true, true, true);
            Assert.That(taps, Is.Zero);
            Tap(detector.Update, 1f);
            Assert.That(taps, Is.EqualTo(1));
        }

        [Test]
        public void SlidingFromOutsideWhileTouching_AndApproachingFromBehind_DoNotFire()
        {
            var detector = new ScreenTapDetector(Screen);
            int taps = 0;
            detector.Tapped += _ => taps++;
            Tap(detector.Update, 0f, 0.2f);
            for (int i = 10; i < 20; i++) detector.Update(i * 0.02f, Tip(0.001f), true, true, true);
            for (int i = 20; i < 30; i++) detector.Update(i * 0.02f, Tip(-0.005f), true, true, true);
            Assert.That(taps, Is.Zero);
        }

        [Test]
        public void EndPoses_MatchConfiguredHoverWithinOneCentimeter_AndFullThickness()
        {
            var settings = new PullOutSettings
            {
                HoverNormalOffsetMeters = 0.27f,
                HoverLetterSpacingMeters = 0.095f,
                HoverHeightMeters = 0.18f,
                DurationSeconds = 0.6f
            };
            var sequence = new PullOutSequence(Screen, settings);
            Tap(sequence.UpdateTip);
            for (int i = 0; i < 5; i++)
            {
                var letter = (PullOutLetter)i;
                Vector3 expected = Screen.center + Screen.Plane.right * ((i - 2) * 0.095f)
                    + Screen.normal * 0.27f + Screen.up * 0.18f;
                var pose = sequence.Sample(letter, sequence.EndTime);
                Assert.That(Vector3.Distance(pose.Position, expected), Is.LessThanOrEqualTo(0.01f));
                Assert.That(pose.ThicknessScale, Is.EqualTo(OrionTokens.MotionPulloutStaggerThicknessTo).Within(0.00001f));
                Assert.That(Vector3.Distance(sequence.Sample(letter, sequence.EndTime + 2f).Position, expected),
                    Is.LessThanOrEqualTo(0.01f));
            }
        }

        [Test]
        public void AllLetters_StartAtTokenThickness_AndUseEaseOutBackForMotionAndDepth()
        {
            var settings = new PullOutSettings();
            var sequence = new PullOutSequence(Screen, settings);
            for (int i = 0; i < 5; i++)
            {
                var pose = sequence.Sample((PullOutLetter)i, 0f);
                Assert.That(Mathf.Abs(Screen.Plane.Height(pose.Position)), Is.LessThan(0.00001f));
                Assert.That(pose.ThicknessScale, Is.EqualTo(OrionTokens.MotionPulloutStaggerThicknessFrom));
            }
            Tap(sequence.UpdateTip);
            for (int i = 0; i < 5; i++)
            {
                var letter = (PullOutLetter)i;
                Assert.That(sequence.Sample(letter, sequence.StartTime(letter)).ThicknessScale, Is.EqualTo(OrionTokens.MotionPulloutStaggerThicknessFrom));
                var quarter = sequence.Sample(letter, sequence.StartTime(letter) + settings.DurationSeconds * 0.25f);
                Assert.That(quarter.ThicknessScale, Is.EqualTo(Mathf.LerpUnclamped(OrionTokens.MotionPulloutStaggerThicknessFrom, OrionTokens.MotionPulloutStaggerThicknessTo, 0.8174097f)).Within(0.00001f));
                var expected = Vector3.Lerp(sequence.ScreenPosition(letter), sequence.HoverPosition(letter), 0.8174097f);
                Assert.That(Vector3.Distance(quarter.Position, expected), Is.LessThan(0.00001f));
                var overshoot = sequence.Sample(letter, sequence.StartTime(letter) + settings.DurationSeconds * 0.6f);
                Assert.That(overshoot.ThicknessScale, Is.GreaterThan(OrionTokens.MotionPulloutStaggerThicknessTo));
                Assert.That(Vector3.Dot(overshoot.Position - sequence.HoverPosition(letter),
                    sequence.HoverPosition(letter) - sequence.ScreenPosition(letter)), Is.GreaterThan(0f));
            }
        }

        [Test]
        public void SkippedAnimationFrames_EmitAllDueLettersInOrderWithoutDuplicates()
        {
            var sequence = new PullOutSequence(Screen);
            var starts = new List<LetterStart>();
            sequence.LetterStarted += starts.Add;
            Tap(sequence.UpdateTip);
            sequence.Advance(sequence.EndTime);
            sequence.Advance(sequence.TapTime);
            sequence.Advance(sequence.EndTime + 1f);
            Assert.That(starts.Select(e => e.Letter), Is.EqualTo(Enum.GetValues(typeof(PullOutLetter))));
        }

        [Test]
        public void SettingsAreValidatedAndSnapshotted_ForTheWholeSequence()
        {
            Assert.Throws<ArgumentException>(() => new PullOutSequence(default));
            Assert.Throws<ArgumentException>(() => new PullOutSequence(Screen, new PullOutSettings { DurationSeconds = 0f }));
            Assert.Throws<ArgumentException>(() => new PullOutSequence(Screen, new PullOutSettings { HoldSeconds = float.NaN }));
            Assert.Throws<ArgumentException>(() => new PullOutSequence(Screen, new PullOutSettings { ArmDepthMeters = 0.001f }));
            var settings = new PullOutSettings();
            var sequence = new PullOutSequence(Screen, settings);
            Vector3 target = sequence.HoverPosition(PullOutLetter.O1);
            settings.HoverNormalOffsetMeters = 9f;
            settings.HoldSeconds = 9f;
            Tap(sequence.UpdateTip);
            Assert.That(sequence.HasStarted, Is.True);
            Assert.That(sequence.HoverPosition(PullOutLetter.O1), Is.EqualTo(target));
        }

        [Test]
        public void ImportedOrionRuntime_AppliesNamedPosesInAnchorSpaceAndScalesOnlyLocalDepth()
        {
            const string path = "Assets/Models/ORION.glb";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null);
            var host = new GameObject("PullOut test host");
            var space = new GameObject("Tracking space");
            try
            {
                space.transform.SetPositionAndRotation(new Vector3(2f, 0.2f, -1f), Quaternion.Euler(0f, 55f, 0f));
                var view = host.AddComponent<PullOutView>();
                var settings = new PullOutSettings();
                view.Initialize(Screen, prefab, space.transform, settings);
                var instance = space.transform.GetChild(0);
                for (int i = 0; i < 5; i++)
                {
                    var letter = instance.Find(((PullOutLetter)i).ToString());
                    Assert.That(letter.localScale.z, Is.EqualTo(prefab.transform.Find(((PullOutLetter)i).ToString()).localScale.z
                        * settings.ModelScale * OrionTokens.MotionPulloutStaggerThicknessFrom).Within(0.00001f));
                    Assert.That(Mathf.Abs(Screen.Plane.Height(letter.localPosition)), Is.LessThan(0.00001f));
                }
                Tap(view.SubmitTip);
                view.RenderAt(view.Sequence.EndTime);
                for (int i = 0; i < 5; i++)
                {
                    var id = (PullOutLetter)i;
                    var letter = instance.Find(id.ToString());
                    var original = prefab.transform.Find(id.ToString());
                    Vector3 expected = Screen.center + Screen.Plane.right * ((i - 2) * settings.HoverLetterSpacingMeters)
                        + Screen.normal * settings.HoverNormalOffsetMeters + Screen.up * settings.HoverHeightMeters;
                    Assert.That(Vector3.Distance(letter.position, space.transform.TransformPoint(expected)), Is.LessThan(0.01f));
                    Assert.That(Vector3.Distance(letter.localScale, original.localScale * settings.ModelScale), Is.LessThan(0.00001f));
                    Assert.That(Vector3.Dot(letter.forward, space.transform.TransformDirection(Screen.normal)), Is.GreaterThan(0.999f));
                    Assert.That(letter.GetComponent<MeshRenderer>().sharedMaterials,
                        Is.EqualTo(original.GetComponent<MeshRenderer>().sharedMaterials));
                }
            }
            finally
            {
                Object.DestroyImmediate(space);
                Object.DestroyImmediate(host);
            }
        }
    }
}
