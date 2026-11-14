using MoodPicker.Ritual;
using NUnit.Framework;

namespace MoodPicker.Tests
{
    public class TimeUnitsTests
    {
        const float Unit = 0.015f;
        const float H = 0.15f;

        [Test]
        public void PullRoundsToTenMinuteUnitsFromZero()
        {
            Assert.That(TimeUnits.Step(0f, 0, Unit, H, 24), Is.EqualTo(0));
            Assert.That(TimeUnits.Step(Unit * 0.6f, 0, Unit, H, 24), Is.EqualTo(0), "경계(0.5)를 15% 넘기 전에는 0");
            Assert.That(TimeUnits.Step(Unit * 0.66f, 0, Unit, H, 24), Is.EqualTo(1));
            Assert.That(TimeUnits.Step(Unit * 4f, 0, Unit, H, 24), Is.EqualTo(4));
        }

        [Test]
        public void UnitsDoNotFlickerAtTheBoundary()
        {
            int u = TimeUnits.Step(Unit * 3.7f, 3, Unit, H, 24);
            Assert.That(u, Is.EqualTo(4));
            Assert.That(TimeUnits.Step(Unit * 3.45f, u, Unit, H, 24), Is.EqualTo(4), "경계 바로 아래로 조금 돌아가도 4 유지");
            Assert.That(TimeUnits.Step(Unit * 3.3f, u, Unit, H, 24), Is.EqualTo(3), "15% 넘게 돌아가면 3");
        }

        [Test]
        public void OnePullCapsAtFourHours()
        {
            Assert.That(TimeUnits.Step(Unit * 40f, 0, Unit, H, 24), Is.EqualTo(24));
            Assert.That(TimeUnits.Minutes(24), Is.EqualTo(240));
        }

        [Test]
        public void HalfHourAndHourNodesAreStronger()
        {
            Assert.That(TimeUnits.NodeStrength(1), Is.EqualTo(0));
            Assert.That(TimeUnits.NodeStrength(3), Is.EqualTo(1));
            Assert.That(TimeUnits.NodeStrength(6), Is.EqualTo(2));
            Assert.That(TimeUnits.NodeStrength(9), Is.EqualTo(1));
            Assert.That(TimeUnits.NodeStrength(12), Is.EqualTo(2));
        }

        [Test]
        public void DevLabelReadsMinutesAndHours()
        {
            Assert.That(TimeUnits.Label(4), Is.EqualTo("40분"));
            Assert.That(TimeUnits.Label(6), Is.EqualTo("1시간"));
            Assert.That(TimeUnits.Label(9), Is.EqualTo("1시간 30분"));
        }

        [Test]
        public void EnglishLabelsFormatMinutesAndHours()
        {
            Assert.That(TimeUnits.LabelEn(0), Is.EqualTo("00m"));
            Assert.That(TimeUnits.LabelEn(4), Is.EqualTo("40m"));
            Assert.That(TimeUnits.LabelEn(6), Is.EqualTo("01h 00m"));
            Assert.That(TimeUnits.LabelEn(9), Is.EqualTo("01h 30m"));
            Assert.That(TimeUnits.ShortEn(0), Is.EqualTo("0m"));
            Assert.That(TimeUnits.ShortEn(4), Is.EqualTo("40m"));
            Assert.That(TimeUnits.ShortEn(6), Is.EqualTo("1h"));
            Assert.That(TimeUnits.ShortEn(9), Is.EqualTo("1h 30m"));
        }
    }
}
