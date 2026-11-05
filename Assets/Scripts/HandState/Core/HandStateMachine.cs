using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MoodPicker.HandStates
{
    public static class HandStateMachine
    {
        public static readonly IReadOnlyDictionary<HandState, IReadOnlyList<HandAction>> AllowedActions =
            new ReadOnlyDictionary<HandState, IReadOnlyList<HandAction>>(
                new Dictionary<HandState, IReadOnlyList<HandAction>>
                {
                    [HandState.Bare] = Array.AsReadOnly(new[] { HandAction.TapScreen, HandAction.GrabLetter,
                        HandAction.TouchLetterPulse, HandAction.ThrowWord, HandAction.StirBench }),
                    [HandState.Paint] = Array.AsReadOnly(new[] { HandAction.FillLetter }),
                    [HandState.Stamp] = Array.AsReadOnly(new[] { HandAction.Capture }),
                    [HandState.Dropper] = Array.AsReadOnly(new[] { HandAction.Pick, HandAction.DropDrop })
                });

        public static readonly IReadOnlyDictionary<(HandState State, HandEventKind Event), HandState> Transitions =
            new ReadOnlyDictionary<(HandState, HandEventKind), HandState>(
                new Dictionary<(HandState, HandEventKind), HandState>
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
                });

        public static bool IsAllowed(HandState state, HandAction action)
        {
            if (!AllowedActions.TryGetValue(state, out var actions)) return false;
            for (int i = 0; i < actions.Count; i++)
                if (actions[i] == action) return true;
            return false;
        }

        public static (HandSnapshot State, bool Ignored) Apply(HandSnapshot state, HandEvent input)
        {
            if (!Transitions.TryGetValue((state.Mode, input.Kind), out var next)) return (state, true);
            bool dip = input.Kind == HandEventKind.PortalDipPaint;
            bool pick = input.Kind == HandEventKind.DropperPicked;
            if ((dip || pick) && !input.Color.HasValue) return (state, true);
            var paint = next == HandState.Paint ? (dip ? input.Color : state.PaintColor) : null;
            var lastPicked = pick ? input.Color : state.LastPickedColor;
            return (new HandSnapshot(next, paint, lastPicked), false);
        }
    }
}
