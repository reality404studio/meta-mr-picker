using System;
using MoodPicker.Catch;
using MoodPicker.Folio;
using MoodPicker.Tokens;
using NUnit.Framework;

namespace MoodPicker.Tests
{
    public class FolioTests
    {
        static PaintRecord Paint(int number) => new PaintRecord { number=number, id="paint_"+number, lightTone=OrionTokens.ColorPaperFaceHex };
        static float Enter => OrionTokens.HysteresisTouchDabEnterMeters;
        static float Exit => OrionTokens.HysteresisTouchDabExitMeters;

        [TestCase(4), TestCase(5)]
        public void FillsFirstEmptyInOrderThenReplacesOldestWithoutUsingClear(int count)
        {
            var slots=new FolioSlots(count);
            for (int i=0;i<count-1;i++)
            {
                Assert.That(slots.FirstEmpty,Is.EqualTo(i));
                Assert.That(slots.Insert(Paint(i+1)),Is.EqualTo(i));
            }
            Assert.That(slots.FirstEmpty,Is.EqualTo(-1));
            Assert.That(slots.Insert(Paint(99)),Is.EqualTo(0));
            Assert.That(slots.Insert(Paint(100)),Is.EqualTo(1));
            Assert.That(slots[0].number,Is.EqualTo(99));
            Assert.That(slots[count-1],Is.Null);
            slots.Remove(0);
            Assert.That(slots.Insert(Paint(101)),Is.EqualTo(0));
            Assert.That(slots.NextSlot,Is.EqualTo(2));
        }
        [TestCase(3),TestCase(6)]
        public void RejectsSlotCountsOutsideTokens(int count)
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>new FolioLayout(count));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new FolioSlots(count));
        }
        [Test]
        public void CannotStoreNullOrRemoveClear()
        {
            var slots=new FolioSlots();
            Assert.Throws<ArgumentNullException>(()=>slots.Insert(null));
            Assert.Throws<ArgumentOutOfRangeException>(()=>slots.Remove(slots.ClearIndex));
        }
        [TestCase(4),TestCase(5)]
        public void FoldAnglesAlternateAndAdjacentHingesHaveTwoMillimeterGap(int count)
        {
            var layout=new FolioLayout(count);
            for (int i=0;i<count;i++)
            {
                Assert.That(layout[i].YawDegrees,Is.EqualTo((i%2==0 ? 1 : -1)*OrionTokens.GeometryFolioFoldAngleDeg));
                if (i==0) continue;
                float previous=layout[i-1].YawDegrees*(float)Math.PI/180;
                float next=layout[i].YawDegrees*(float)Math.PI/180;
                var rightEdge=layout[i-1].Position+new FolioVector((float)Math.Cos(previous),0,-(float)Math.Sin(previous))*(ChipMeshBuilder.Width/2f);
                var leftEdge=layout[i].Position-new FolioVector((float)Math.Cos(next),0,-(float)Math.Sin(next))*(ChipMeshBuilder.Width/2f);
                Assert.That((rightEdge-leftEdge).Length,Is.EqualTo(OrionTokens.GeometryFolioSlotGapMeters).Within(0.000001));
            }
            Assert.That(layout.ClearIndex,Is.EqualTo(count-1));
        }
        [Test]
        public void SummonTwentyBoundaryOscillationsNeverFlicker()
        {
            var summon=new FolioSummon(); int changes=0;
            for (int i=0;i<20;i++)
            {
                if (summon.Step(true,OrionTokens.HysteresisPalmUpEnterDot+0.01f,OrionTokens.HysteresisPalmUpHoldSeconds/2)) changes++;
                if (summon.Step(true,OrionTokens.HysteresisPalmUpEnterDot-0.01f,OrionTokens.HysteresisPalmUpHoldSeconds/2)) changes++;
            }
            Assert.That(changes,Is.Zero); Assert.That(summon.IsSummoned,Is.False);
            Assert.That(summon.Step(true,OrionTokens.HysteresisPalmUpEnterDot,OrionTokens.HysteresisPalmUpHoldSeconds),Is.True);
            for (int i=0;i<20;i++)
            {
                if (summon.Step(true,OrionTokens.HysteresisPalmUpExitDot-0.01f,OrionTokens.HysteresisPalmUpReleaseSeconds/2)) changes++;
                if (summon.Step(true,OrionTokens.HysteresisPalmUpExitDot+0.01f,OrionTokens.HysteresisPalmUpReleaseSeconds/2)) changes++;
            }
            Assert.That(changes,Is.Zero); Assert.That(summon.IsSummoned,Is.True);
            Assert.That(summon.Step(true,OrionTokens.HysteresisPalmUpExitDot,OrionTokens.HysteresisPalmUpReleaseSeconds),Is.False);
            Assert.That(summon.Step(true,OrionTokens.HysteresisPalmUpExitDot-0.01f,OrionTokens.HysteresisPalmUpReleaseSeconds),Is.True);
            Assert.That(summon.IsSummoned,Is.False);
        }
        [Test]
        public void SummonRequiresFullDwellAndTrackingLossReturnsAfterReleaseDwell()
        {
            var summon=new FolioSummon();
            Assert.That(summon.Step(true,1,OrionTokens.HysteresisPalmUpHoldSeconds-0.01f),Is.False);
            Assert.That(summon.Step(false,1,0.01f),Is.False);
            Assert.That(summon.Step(true,1,OrionTokens.HysteresisPalmUpHoldSeconds),Is.True);
            Assert.That(summon.Step(false,0,OrionTokens.HysteresisPalmUpReleaseSeconds-0.01f),Is.False);
            Assert.That(summon.Step(false,0,0.011f),Is.True);
        }
        [Test]
        public void DockAndSummonedPlacementUseTokensAndNormalizedPalmNormal()
        {
            var dock=FolioPlacement.DockOffset;
            Assert.That(dock.X,Is.EqualTo(OrionTokens.ZoneHeroFolioX));
            Assert.That(dock.Y,Is.EqualTo(OrionTokens.ZoneHeroY));
            Assert.That(dock.Z,Is.EqualTo(OrionTokens.ZoneHeroZ));
            var palm=new FolioVector(1,2,3); var normal=new FolioVector(0,3,4);
            var offset=FolioPlacement.AbovePalm(palm,normal)-palm;
            Assert.That(offset.Length,Is.EqualTo(OrionTokens.ZoneHandFolioAboveMeters).Within(0.000001));
            Assert.That(FolioVector.Dot(offset.Normalized,normal.Normalized),Is.EqualTo(1).Within(0.00001));
            Assert.Throws<ArgumentException>(()=>FolioPlacement.AbovePalm(palm,default));
        }
        [Test]
        public void FlightsReserveFirstEmptyAndArriveAtTheirReservedSlot()
        {
            var slots=new FolioSlots(); slots.Insert(Paint(1));
            var layout=new FolioLayout();
            var first=new FlyToFolio(slots,Paint(2),new FolioVector(0,0,1));
            var second=new FlyToFolio(slots,Paint(3),new FolioVector(0,0,1));
            Assert.That(first.Slot,Is.EqualTo(1)); Assert.That(second.Slot,Is.EqualTo(2));
            Assert.That(first.HasArrived(FlyToFolio.Duration-0.001f),Is.False);
            Assert.That(first.HasArrived(FlyToFolio.Duration),Is.True);
            Assert.That((first.Position(FlyToFolio.Duration,layout[first.Slot].Position)-layout[1].Position).Length,Is.LessThan(0.000001));
            Assert.That((second.Position(FlyToFolio.Duration,layout[second.Slot].Position)-layout[2].Position).Length,Is.LessThan(0.000001));
            slots.Insert(Paint(4)); slots.Insert(Paint(5));
            Assert.That(first.IsCurrent(slots),Is.False);
            Assert.That(second.IsCurrent(slots),Is.True);
        }
        [Test]
        public void FlightIsCubicInOutWithParabolicRiseAndClampedEndpoints()
        {
            var flight=new FlyToFolio(new FolioSlots(),Paint(1),new FolioVector(0,0,0));
            var target=new FolioVector(1,0,0);
            Assert.That(FlyToFolio.Ease,Is.EqualTo(OrionTokens.MotionChipFlyToFolioEase));
            Assert.That(FlyToFolio.Duration,Is.EqualTo(OrionTokens.MotionChipFlyToFolioSeconds));
            Assert.That(FlyToFolio.Progress(-1),Is.Zero);
            Assert.That(FlyToFolio.Progress(FlyToFolio.Duration/4),Is.EqualTo(0.0625f).Within(0.000001));
            Assert.That(FlyToFolio.Progress(FlyToFolio.Duration*3/4),Is.EqualTo(0.9375f).Within(0.000001));
            Assert.That(FlyToFolio.Progress(FlyToFolio.Duration+1),Is.EqualTo(1));
            Assert.That(flight.Position(FlyToFolio.Duration/2,target).Y,Is.EqualTo(0.25f));
        }
        [Test]
        public void TouchTenTremorsProduceOnePickAndSixMillimetersRearms()
        {
            var slots=new FolioSlots(); var paint=Paint(1); slots.Insert(paint);
            var interaction=new FolioInteraction(slots); int picked=0;
            interaction.Picked+=record=> { Assert.That(record,Is.SameAs(paint)); picked++; };
            interaction.Step(0,0,0,Exit);
            for (int i=0;i<10;i++)
            {
                interaction.Step(0,0,0,Enter-0.0001f);
                interaction.Step(0,0,0,Enter+0.0001f);
            }
            Assert.That(picked,Is.EqualTo(1));
            interaction.Step(0,0,0,Exit-0.0001f); interaction.Step(0,0,0,Enter);
            Assert.That(picked,Is.EqualTo(1));
            interaction.Step(0,0,0,Exit); interaction.Step(0,0,0,Enter);
            Assert.That(picked,Is.EqualTo(2));
        }
        [Test]
        public void ClearTouchWashesExactlyOnceAndDoesNotDeleteCollection()
        {
            var slots=new FolioSlots(); slots.Insert(Paint(1));
            var interaction=new FolioInteraction(slots); int washed=0,picked=0;
            interaction.Washed+=()=>washed++; interaction.Picked+=r=>picked++;
            interaction.Step(slots.ClearIndex,0,0,Exit);
            for (int i=0;i<10;i++) interaction.Step(slots.ClearIndex,0,0,Enter);
            Assert.That(washed,Is.EqualTo(1)); Assert.That(picked,Is.Zero);
            Assert.That(slots[0],Is.Not.Null);
        }
        [Test]
        public void TouchRequiresFrontApproachAndDoesNotRearmOnTrackingLossOrSlide()
        {
            var touch=new ChipTouch();
            Assert.That(touch.Step(0,0,0,Enter),Is.False);
            Assert.That(touch.Step(0,0,0,-Exit),Is.False);
            touch.Step(0,0,0,Exit);
            Assert.That(touch.Step(0,0,0,Enter),Is.True);
            Assert.That(touch.Step(1,0,0,Exit),Is.False);
            Assert.That(touch.Step(1,0,0,Enter),Is.False);
            touch.LoseTracking();
            Assert.That(touch.Step(0,0,0,Enter),Is.False);
            touch.Step(0,0,0,Exit);
            Assert.That(touch.Step(0,0,0,Enter),Is.True);
        }
        [Test]
        public void HitBoxEnforcesMinimumAndRejectsOutOfBoundsAndEmptySlot()
        {
            var touch=new ChipTouch(0.001f,0.001f);
            Assert.That(touch.Width,Is.EqualTo(OrionTokens.HysteresisHitMinSizeMeters0));
            Assert.That(touch.Height,Is.EqualTo(OrionTokens.HysteresisHitMinSizeMeters1));
            touch.Step(0,0,0,Exit);
            Assert.That(touch.Step(0,touch.Width,0,Enter),Is.False);
            Assert.That(touch.Step(0,0,touch.Height,Enter),Is.False);
            Assert.That(touch.Step(0,touch.Width/2,touch.Height/2,Enter),Is.True);
            var interaction=new FolioInteraction(new FolioSlots());
            Assert.That(interaction.Step(0,0,0,Exit),Is.False);
            Assert.That(interaction.Step(0,0,0,Enter),Is.False);
        }
    }
}
