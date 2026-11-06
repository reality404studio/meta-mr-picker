using System;
using MoodPicker.Portal;
using MoodPicker.Tokens;
using NUnit.Framework;
using UnityEngine;

namespace MoodPicker.Tests
{
    public class OrionTokensUnityTests
    {
        [Test]
        public void PortalDefaultsUsePalmTokens()
        {
            var settings = PortalSettings.Defaults();
            try
            {
                Assert.That(settings.openUpDot, Is.EqualTo(OrionTokens.HysteresisPalmUpEnterDot));
                Assert.That(settings.closeUpDot, Is.EqualTo(OrionTokens.HysteresisPalmUpExitDot));
                Assert.That(settings.openHoldSeconds, Is.EqualTo(OrionTokens.HysteresisPalmUpHoldSeconds));
                Assert.That(settings.closeHoldSeconds, Is.EqualTo(OrionTokens.HysteresisPalmUpReleaseSeconds));
                Assert.That(settings.heightAbovePalm, Is.EqualTo(OrionTokens.ZoneHandFolioAboveMeters));
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        [TestCase(OrionTokens.ColorPaperFaceHex, OrionTokens.ColorPaperFaceR, OrionTokens.ColorPaperFaceG, OrionTokens.ColorPaperFaceB)]
        [TestCase(OrionTokens.ColorPrintCobaltHex, OrionTokens.ColorPrintCobaltR, OrionTokens.ColorPrintCobaltG, OrionTokens.ColorPrintCobaltB)]
        public void HexHelperPreservesSrgbChannelsAndOpaqueAlpha(string hex, float r, float g, float b)
        {
            Assert.That(OrionTokens.ToColor(hex), Is.EqualTo(new Color(r, g, b, 1f)));
        }

        [TestCase(null)]
        [TestCase("F0EEE6")]
        [TestCase("#GG0000")]
        [TestCase("#12345678")]
        public void HexHelperRejectsMalformedColors(string hex)
        {
            Assert.Throws<ArgumentException>(() => OrionTokens.ToColor(hex));
        }
    }
}
