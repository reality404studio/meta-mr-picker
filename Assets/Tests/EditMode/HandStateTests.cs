using System;
using System.Collections.Generic;
using System.Linq;
using MoodPicker.HandStates;
using NUnit.Framework;

namespace MoodPicker.Tests
{
    public class HandStateTests
    {
        static readonly HandColor Chip = new HandColor(0.2f, 0.4f, 0.6f);
        static readonly HandColor Picked = new HandColor(0.7f, 0.3f, 0.1f);

        static HandSnapshot InState(HandState state)
        {
            switch (state)
            {
                case HandState.Bare: return default;
                case HandState.Paint: return Send(default, HandEventKind.PortalDipPaint, Chip);
                case HandState.Stamp: return Send(default, HandEventKind.GrabStamp);
                case HandState.Dropper: return Send(default, HandEventKind.GrabDropper);
                default: throw new ArgumentOutOfRangeException(nameof(state));
            }
        }

        static HandSnapshot Send(HandSnapshot state, HandEventKind input, HandColor? color = null)
        {
            var result = HandStateMachine.Apply(state, new HandEvent(input, color));
            Assert.That(result.Ignored, Is.False, input.ToString());
            return result.State;
        }

        public static IEnumerable<TestCaseData> AllTransitions()
        {
            foreach (HandState state in Enum.GetValues(typeof(HandState)))
                foreach (HandEventKind input in Enum.GetValues(typeof(HandEventKind)))
                    yield return new TestCaseData(state, input);
        }

        [TestCaseSource(nameof(AllTransitions))]
        public void EveryStateEventPairMatchesIndependentSpecification(HandState mode, HandEventKind input)
        {
            var expected = new Dictionary<(HandState, HandEventKind), HandState>
            {
                [(HandState.Bare, HandEventKind.PortalDipPaint)] = HandState.Paint,
                [(HandState.Bare, HandEventKind.GrabStamp)] = HandState.Stamp,
                [(HandState.Bare, HandEventKind.GrabDropper)] = HandState.Dropper,
                [(HandState.Bare, HandEventKind.GrabLetter)] = HandState.Bare,
                [(HandState.Bare, HandEventKind.ReleaseLetter)] = HandState.Bare,
                [(HandState.Bare, HandEventKind.TapScreen)] = HandState.Bare,
                [(HandState.Paint, HandEventKind.Washed)] = HandState.Bare,
                [(HandState.Paint, HandEventKind.GrabStamp)] = HandState.Stamp,
                [(HandState.Paint, HandEventKind.GrabDropper)] = HandState.Dropper,
                [(HandState.Paint, HandEventKind.LetterTouch)] = HandState.Paint,
                [(HandState.Stamp, HandEventKind.ReleaseTool)] = HandState.Bare,
                [(HandState.Stamp, HandEventKind.StampFired)] = HandState.Stamp,
                [(HandState.Dropper, HandEventKind.ReleaseTool)] = HandState.Bare,
                [(HandState.Dropper, HandEventKind.DropperPicked)] = HandState.Dropper
            };
            var before = InState(mode);
            var result = HandStateMachine.Apply(before, new HandEvent(input, Picked));
            bool accepted = expected.TryGetValue((mode, input), out var next);
            Assert.That(result.Ignored, Is.EqualTo(!accepted));
            if (!accepted)
            {
                Assert.That(result.State, Is.EqualTo(before));
                return;
            }
            Assert.That(result.State.Mode, Is.EqualTo(next));
            Assert.That(result.State.PaintColor, Is.EqualTo(next == HandState.Paint
                ? (input == HandEventKind.PortalDipPaint ? Picked : Chip) : (HandColor?)null));
            Assert.That(result.State.LastPickedColor, Is.EqualTo(input == HandEventKind.DropperPicked
                ? Picked : (HandColor?)null));
        }

        [Test]
        public void AllowedActionsSnapshotRejectsAnyAddedOrRemovedPermission()
        {
            string actual = string.Join("\n", HandStateMachine.AllowedActions.OrderBy(pair => pair.Key)
                .Select(pair => pair.Key + ":" + string.Join(",", pair.Value.OrderBy(action => action))));
            Assert.That(actual, Is.EqualTo(
                "Bare:TapScreen,GrabLetter,TouchLetterPulse,ThrowWord,StirBench\n" +
                "Paint:FillLetter\nStamp:Capture\nDropper:Pick,DropDrop"));
        }

        [Test]
        public void EveryStateActionPairAllowsOnlyItsSpecifiedActions()
        {
            var expected = new[]
            {
                new[] { HandAction.TapScreen, HandAction.GrabLetter, HandAction.TouchLetterPulse,
                    HandAction.ThrowWord, HandAction.StirBench },
                new[] { HandAction.FillLetter },
                new[] { HandAction.Capture },
                new[] { HandAction.Pick, HandAction.DropDrop }
            };
            foreach (HandState state in Enum.GetValues(typeof(HandState)))
                foreach (HandAction action in Enum.GetValues(typeof(HandAction)))
                    Assert.That(HandStateMachine.IsAllowed(state, action),
                        Is.EqualTo(expected[(int)state].Contains(action)), state + "/" + action);
        }

        [Test]
        public void TablesCannotBeMutatedThroughCollectionInterfaces()
        {
            var actions = (IDictionary<HandState, IReadOnlyList<HandAction>>)HandStateMachine.AllowedActions;
            Assert.Throws<NotSupportedException>(() => actions[HandState.Paint] = new[] { HandAction.GrabLetter });
            var row = (IList<HandAction>)actions[HandState.Paint];
            Assert.Throws<NotSupportedException>(() => row[0] = HandAction.GrabLetter);
            var transitions = (IDictionary<(HandState, HandEventKind), HandState>)HandStateMachine.Transitions;
            Assert.Throws<NotSupportedException>(() => transitions.Clear());
        }

        [Test]
        public void AllFourModesMapToExpectedColors()
        {
            var style = new HandStyleSettings();
            var bare = new HandColor(240f / 255f, 238f / 255f, 230f / 255f);
            Assert.That(style.ColorFor(InState(HandState.Bare)), Is.EqualTo(bare));
            Assert.That(style.ColorFor(InState(HandState.Paint)), Is.EqualTo(Chip));
            Assert.That(style.ColorFor(InState(HandState.Stamp)),
                Is.EqualTo(new HandColor(37f / 255f, 89f / 255f, 180f / 255f)));
            Assert.That(style.ColorFor(InState(HandState.Dropper)), Is.EqualTo(bare));
            var dropper = Send(InState(HandState.Dropper), HandEventKind.DropperPicked, Picked);
            Assert.That(style.ColorFor(dropper), Is.EqualTo(Picked));
        }

        [Test]
        public void PaintFollowsEachChipAndSurvivesFillingLettersUntilWashed()
        {
            var style = new HandStyleSettings();
            var state = InState(HandState.Paint);
            state = Send(state, HandEventKind.LetterTouch);
            Assert.That(style.ColorFor(state), Is.EqualTo(Chip));
            state = Send(state, HandEventKind.Washed);
            Assert.That(state.PaintColor, Is.Null);
            Assert.That(style.ColorFor(state), Is.EqualTo(style.Bare));
            state = Send(state, HandEventKind.PortalDipPaint, Picked);
            Assert.That(style.ColorFor(state), Is.EqualTo(Picked));
        }

        [TestCase(HandEventKind.GrabStamp, HandState.Stamp)]
        [TestCase(HandEventKind.GrabDropper, HandState.Dropper)]
        public void GrabbingToolClearsPaintWithoutWashing(HandEventKind input, HandState tool)
        {
            var state = Send(InState(HandState.Paint), input);
            Assert.That(state.Mode, Is.EqualTo(tool));
            Assert.That(state.PaintColor, Is.Null);
            state = Send(state, HandEventKind.ReleaseTool);
            Assert.That(state.Mode, Is.EqualTo(HandState.Bare));
            Assert.That(state.PaintColor, Is.Null);
        }

        [Test]
        public void DropperRemembersLatestColorAcrossReleasePaintAndToolSwitch()
        {
            var style = new HandStyleSettings();
            var state = Send(InState(HandState.Dropper), HandEventKind.DropperPicked, Chip);
            state = Send(state, HandEventKind.DropperPicked, Picked);
            state = Send(state, HandEventKind.ReleaseTool);
            Assert.That(style.ColorFor(state), Is.EqualTo(style.Bare));
            state = Send(state, HandEventKind.PortalDipPaint, Chip);
            Assert.That(style.ColorFor(state), Is.EqualTo(Chip));
            state = Send(state, HandEventKind.GrabStamp);
            state = Send(state, HandEventKind.StampFired);
            state = Send(state, HandEventKind.ReleaseTool);
            state = Send(state, HandEventKind.GrabDropper);
            Assert.That(style.ColorFor(state), Is.EqualTo(Picked));
            Assert.That(state.PaintColor, Is.Null);
            var ignored = HandStateMachine.Apply(state, new HandEvent(HandEventKind.GrabLetter, Chip));
            Assert.That(ignored.Ignored, Is.True);
            Assert.That(ignored.State, Is.EqualTo(state));
        }

        [Test]
        public void MissingColorAndUnknownInputsAreIgnored()
        {
            foreach (var mode in new[] { HandState.Bare, HandState.Dropper })
            {
                var state = InState(mode);
                foreach (var input in new[] { HandEventKind.PortalDipPaint, HandEventKind.DropperPicked, (HandEventKind)999 })
                {
                    var result = HandStateMachine.Apply(state, new HandEvent(input));
                    Assert.That(result.Ignored, Is.True);
                    Assert.That(result.State, Is.EqualTo(state));
                }
            }
            Assert.That(HandStateMachine.IsAllowed((HandState)999, HandAction.GrabLetter), Is.False);
            Assert.That(HandStateMachine.IsAllowed(HandState.Bare, (HandAction)999), Is.False);
        }

        [Test]
        public void StyleOverridesAndIndependentSnapshotsDoNotShareState()
        {
            var style = new HandStyleSettings { Bare = Chip, Stamp = Picked };
            HandSnapshot untouched = default;
            var right = Send(untouched, HandEventKind.GrabStamp);
            Assert.That(untouched.Mode, Is.EqualTo(HandState.Bare));
            Assert.That(style.ColorFor(untouched), Is.EqualTo(Chip));
            Assert.That(style.ColorFor(right), Is.EqualTo(Picked));
        }
    }
}
