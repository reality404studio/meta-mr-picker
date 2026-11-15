using MoodPicker.Ritual;
using NUnit.Framework;

namespace MoodPicker.Tests
{
    public class RitualCoreTests
    {
        const float Hours = 24f;
        const int Day = 144;

        [Test]
        public void ASystemFrameCapWithAnIdleGpuNeverThinsTheSand()
        {
            Assert.That(RitualGovernor.Judge(27.8f, 4.5f, 13.9f), Is.EqualTo(GovernorVerdict.Calm), "36fps 상한이어도 GPU 가 한가하면 입자를 줄이지 않는다 (11-14 실기)");
            Assert.That(RitualGovernor.Judge(27.8f, float.NaN, 13.9f), Is.EqualTo(GovernorVerdict.Hold), "작업 시간을 모르면 줄이지 않는다");
            Assert.That(RitualGovernor.Judge(18.3f, float.NaN, 13.9f), Is.Not.EqualTo(GovernorVerdict.Over), "로딩 한 번 버벅임도 마찬가지");
        }

        [Test]
        public void OnlyRealWorkOverBudgetThinsTheSand()
        {
            Assert.That(RitualGovernor.Judge(27.8f, 14.5f, 13.9f), Is.EqualTo(GovernorVerdict.Over));
            Assert.That(RitualGovernor.Judge(13.9f, 13f, 13.9f), Is.EqualTo(GovernorVerdict.Hold), "빠듯하면 되살리지 않는다");
            Assert.That(RitualGovernor.Judge(13.9f, 6f, 13.9f), Is.EqualTo(GovernorVerdict.Calm));
            Assert.That(RitualGovernor.Work(4f, 9f), Is.EqualTo(9f));
            Assert.That(RitualGovernor.Work(float.NaN, 9f), Is.EqualTo(9f));
            Assert.That(RitualGovernor.Work(4f, float.NaN), Is.EqualTo(4f));
            Assert.That(float.IsNaN(RitualGovernor.Work(float.NaN, float.NaN)), Is.True);
        }

        [TestCase(0.0045f, 4.5f)]
        [TestCase(4.5f, 4.5f)]
        public void PerfMetricsReadAsMillisecondsWhicheverUnitTheyCome(float raw, float ms)
        {
            Assert.That(RitualGovernor.ToMilliseconds(raw), Is.EqualTo(ms).Within(1e-4f));
        }

        [Test]
        public void MissingPerfMetricsAreUnknownNotZero()
        {
            Assert.That(float.IsNaN(RitualGovernor.ToMilliseconds(0f)), Is.True);
            Assert.That(float.IsNaN(RitualGovernor.ToMilliseconds(float.NaN)), Is.True);
        }

        [Test]
        public void JarIsTheSizeTheGuideAsksFor()
        {
            JarShape jar = JarShape.Default;
            Assert.That(jar.height, Is.EqualTo(0.2f).Within(1e-5f), "높이 20cm (11-14 사람: 24시간을 담게 좀 더 크게)");
            Assert.That(jar.OuterRadius(0.06f) * 2f, Is.EqualTo(0.104f).Within(1e-4f), "몸통 지름 10.4cm");
            Assert.That(jar.InnerRadius(jar.height) * 2f, Is.EqualTo(0.08f).Within(1e-4f), "입구 지름 8cm");
            Assert.That(jar.OuterRadius(0.175f), Is.LessThan(jar.OuterRadius(0.06f)), "목이 몸통보다 좁다");
        }

        [Test]
        public void ADayOfTwentyFourHoursFillsTheBodyExactly()
        {
            JarShape jar = JarShape.Default;
            float full = RitualFill.HeightAfter(jar, Hours, Day);
            Assert.That(full, Is.EqualTo(jar.bodyTop).Within(0.0006f));
            float oneHour = RitualFill.HeightAfter(jar, Hours, 6) - jar.floor;
            Assert.That(oneHour, Is.EqualTo((jar.bodyTop - jar.floor) / Hours).Within(0.0004f), "시간에 비례");
            float tenMinutes = RitualFill.HeightAfter(jar, Hours, 1) - jar.floor;
            Assert.That(tenMinutes, Is.GreaterThan(0.001f), "10분 층이 1mm 보다 두껍다");
        }

        [Test]
        public void MoreThanADayRisesIntoTheNeckAndStopsBelowTheLip()
        {
            JarShape jar = JarShape.Default;
            int capacity = RitualFill.Capacity(jar, Hours);
            Assert.That(capacity, Is.GreaterThan(Day), "24시간이 다 들어가고도 목까지 여유가 있다");
            float top = RitualFill.HeightAfter(jar, Hours, capacity);
            Assert.That(top, Is.GreaterThan(jar.bodyTop));
            Assert.That(top, Is.LessThanOrEqualTo(jar.FillCeiling + 1e-4f));
        }

        [Test]
        public void LayerBoundariesAreBumpyButNeverCross()
        {
            float[] below = Strata.Flat(0.005f);
            float[] above = Strata.Flat(0f);
            float rise = 0.0012f;
            for (int layer = 0; layer < 12; layer++)
            {
                Strata.Raise(below, above, rise, 0.0025f, 40 + layer);
                float min = float.MaxValue, max = float.MinValue;
                for (int i = 0; i < Strata.Samples; i++)
                {
                    Assert.That(above[i], Is.GreaterThan(below[i]), "층이 뒤집히지 않는다");
                    min = System.Math.Min(min, above[i] - below[i]);
                    max = System.Math.Max(max, above[i] - below[i]);
                }
                Assert.That(max - min, Is.GreaterThan(rise * 0.1f), "경계가 울퉁불퉁하다");
                Assert.That(Strata.Mean(above) - Strata.Mean(below), Is.EqualTo(rise).Within(rise * 0.05f), "평균 두께는 시간에 비례");
                float[] swap = below;
                below = above;
                above = swap;
            }
        }

        static SealPress Hold(float depth, float seconds, SealPress press = default)
        {
            const float dt = 1f / 72f;
            for (float t = 0f; t < seconds; t += dt) press.Step(depth, dt, 0.008f, 1.2f, 0.6f);
            return press;
        }

        [Test]
        public void LiftingBeforeTheHoldTimeDoesNotSeal()
        {
            SealPress press = Hold(0.012f, 1.0f);
            Assert.That(press.sealedShut, Is.False);
            Assert.That(press.progress, Is.GreaterThan(0.7f));
            press = Hold(0f, 0.8f, press);
            Assert.That(press.sealedShut, Is.False);
            Assert.That(press.progress, Is.EqualTo(0f), "떼면 밀랍이 원래대로 돌아간다");
        }

        [Test]
        public void HoldingLongEnoughSealsForGood()
        {
            SealPress press = Hold(0.012f, 1.25f);
            Assert.That(press.sealedShut, Is.True);
            press = Hold(0f, 3f, press);
            Assert.That(press.sealedShut, Is.True, "한 번 찍으면 다시 열리지 않는다");
            Assert.That(press.progress, Is.EqualTo(1f));
        }

        [Test]
        public void AShallowTouchIsNotAPress()
        {
            SealPress press = Hold(0.005f, 3f);
            Assert.That(press.sealedShut, Is.False);
            Assert.That(press.progress, Is.EqualTo(0f), "0.8cm 보다 얕으면 누름이 아니다");
        }

        [Test]
        public void WaxTakesTheColourOfTheLongestTime()
        {
            Assert.That(RitualTally.Dominant(new[] { 42, 6, 9, 0, 6 }), Is.EqualTo(0));
            Assert.That(RitualTally.Dominant(new[] { 0, 6, 30, 0, 6 }), Is.EqualTo(2));
            Assert.That(RitualTally.Dominant(new[] { 0, 0, 0 }), Is.EqualTo(-1));
            Assert.That(RitualTally.Total(new[] { 42, 6, 9 }), Is.EqualTo(57));
        }

        [Test]
        public void SealEmblemIsARaisedMotifInARecessedFieldInsideARaisedRim()
        {
            Assert.That(SealEmblem.Height(0f, 0.3f), Is.EqualTo(1f).Within(1e-3f), "가운데 점은 솟아 있다");
            Assert.That(SealEmblem.Height(0f, 0.52f), Is.EqualTo(0f).Within(1e-3f), "무늬 사이 바탕은 눌려 있다");
            Assert.That(SealEmblem.Height(0.7f, 0f), Is.EqualTo(1f).Within(1e-3f), "테두리 고리는 솟아 있다");
            Assert.That(SealEmblem.Height(0.97f, 0f), Is.EqualTo(1f).Within(1e-3f), "도장 밖 밀랍은 눌리지 않는다");
        }


        [Test]
        public void WithSealOnReleaseTheSealComesOnlyWhenTheThumbLiftsAfterAFullPress()
        {
            var press = new SealPress();
            for (int i = 0; i < 30; i++) press.Step(0.01f, 0.05f, 0.008f, 1.0f, 0.6f, true);
            Assert.That(press.full, Is.True, "1초 넘게 누르면 가장자리까지 번진다");
            Assert.That(press.sealedShut, Is.False, "누르고 있는 동안은 아직 닫히지 않는다");
            press.Step(0f, 0.05f, 0.008f, 1.0f, 0.6f, true);
            Assert.That(press.sealedShut, Is.True, "엄지를 떼는 순간 닫힌다");

            var early = new SealPress();
            for (int i = 0; i < 8; i++) early.Step(0.01f, 0.05f, 0.008f, 1.0f, 0.6f, true);
            for (int i = 0; i < 40; i++) early.Step(0f, 0.05f, 0.008f, 1.0f, 0.6f, true);
            Assert.That(early.sealedShut, Is.False, "도중에 떼면 닫히지 않는다");
            Assert.That(early.progress, Is.EqualTo(0f), "밀랍이 다시 오므라든다");

            var old = new SealPress();
            for (int i = 0; i < 30; i++) old.Step(0.01f, 0.05f, 0.008f, 1.0f, 0.6f);
            Assert.That(old.sealedShut, Is.True, "플래그를 끄면 이전처럼 누르는 중에 닫힌다");
        }

        [Test]
        public void TheWhorlSealHasFingerprintRidgesARaisedCoreAndARim()
        {
            int transitions = 0;
            bool up = SealEmblem.HeightOf(EmblemStyle.Whorl, 0.12f, 0f) > 0.5f;
            for (int i = 1; i <= 120; i++)
            {
                float x = 0.12f + 0.58f * i / 120f;
                bool now = SealEmblem.HeightOf(EmblemStyle.Whorl, x, 0f) > 0.5f;
                if (now != up) transitions++;
                up = now;
            }
            Assert.That(transitions, Is.GreaterThanOrEqualTo(12), "지문처럼 촘촘한 골이 여러 겹");
            Assert.That(SealEmblem.HeightOf(EmblemStyle.Whorl, 0f, 0f), Is.GreaterThan(0.9f), "가운데 심이 솟아 있다");
            Assert.That(SealEmblem.HeightOf(EmblemStyle.Whorl, 0.79f, 0f), Is.GreaterThan(0.9f), "테두리");
            Assert.That(SealEmblem.HeightOf(EmblemStyle.Whorl, 0.97f, 0f), Is.EqualTo(1f).Within(1e-3f), "도장 밖은 눌리지 않는다");
            Assert.That(SealEmblem.HeightOf(EmblemStyle.Sun, 0f, 0.3f), Is.EqualTo(SealEmblem.Height(0f, 0.3f)), "이전 문양은 그대로 고를 수 있다");
        }
    }
}
