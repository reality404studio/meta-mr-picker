using MoodPicker.Splatter;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MoodPicker.Tests
{
    public class SplatFilmTests
    {
        [Test]
        public void FilmOffKeepsThePassthroughRoomAndHidesTheHandCutOut()
        {
            int everything = ~0;
            Assert.That(SplatFilmLook.ClearAlpha(false), Is.EqualTo(0f));
            Assert.That(SplatFilmLook.CullingMask(everything, false) & (1 << SplatFilmLook.HandLayer), Is.Zero);
            Assert.That(SplatFilmLook.PassthroughBrightness(false, -0.7f), Is.EqualTo(-0.7f));
        }

        [Test]
        public void FilmOnCoversTheRoomButCutsOutRealHandsAtNaturalBrightness()
        {
            Assert.That(SplatFilmLook.ClearAlpha(true), Is.EqualTo(1f));
            Assert.That(SplatFilmLook.ClearGrey(true, 0.08f), Is.EqualTo(0.08f).Within(1e-6f));
            Assert.That(SplatFilmLook.CullingMask(0, true) & (1 << SplatFilmLook.HandLayer), Is.Not.Zero);
            Assert.That(SplatFilmLook.PassthroughBrightness(true, -1f), Is.EqualTo(0f));
            Assert.That(SplatFilmLook.PassthroughContrast(true, 0.5f), Is.EqualTo(0f));
            Assert.That(SplatFilmLook.PassthroughSaturation(true, -0.5f), Is.EqualTo(0f));
        }

        [Test]
        public void FilmToggleIsTheFirstDialRowAndSwitchesTuning()
        {
            var go = new GameObject("film-dial-test");
            try
            {
                var tuning = go.AddComponent<SplatTuning>();
                var dial = go.AddComponent<SplatFeelDial>();
                var so = new SerializedObject(dial);
                so.FindProperty("tuning").objectReferenceValue = tuning;
                so.ApplyModifiedPropertiesWithoutUndo();
                dial.BuildRows();

                Assert.That(dial.Rows[0].name, Is.EqualTo(SplatFilmLook.ToggleRow));
                Assert.That(dial.Rows[1].name, Is.EqualTo(SplatFilmLook.BackgroundRow));
                Assert.That(dial.Selected, Is.EqualTo(0));
                tuning.filmVr = false;
                dial.Rows[0].set(dial.Rows[0].Stepped(dial.Rows[0].get(), 1));
                Assert.That(tuning.filmVr, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
