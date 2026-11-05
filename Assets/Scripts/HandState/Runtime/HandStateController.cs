using System;
using MoodPicker.Catch;
using MoodPicker.Portal;
using UnityEngine;

namespace MoodPicker.HandStates
{
    public sealed class HandStateController : MonoBehaviour
    {
        [SerializeField] PortalController portal;
        PortalController _subscribedPortal;
        readonly HandStyleSettings _style = new HandStyleSettings();

        public HandSnapshot RightHand { get; private set; }
        public HandState LeftHand => HandState.Bare;
        public Color CurrentColor => ToUnity(_style.ColorFor(RightHand));
        public event Action<HandState, Color> StateChanged;

        public bool IsAllowed(HandAction action) => HandStateMachine.IsAllowed(RightHand.Mode, action);

        public (HandSnapshot State, bool Ignored) Submit(HandEvent input)
        {
            if (!isActiveAndEnabled) return (RightHand, true);
            var result = HandStateMachine.Apply(RightHand, input);
            if (!result.Ignored && !RightHand.Equals(result.State))
            {
                RightHand = result.State;
                StateChanged?.Invoke(RightHand.Mode, CurrentColor);
            }
            return result;
        }

        public void BindPortal(PortalController source)
        {
            Unsubscribe();
            portal = source;
            if (isActiveAndEnabled) Subscribe();
        }

        void OnEnable() => Subscribe();
        void OnDisable() => Unsubscribe();

        void Subscribe()
        {
            Unsubscribe();
            if (portal == null) return;
            _subscribedPortal = portal;
            _subscribedPortal.PaintPicked += OnPaintPicked;
            _subscribedPortal.Washed += OnWashed;
        }

        void Unsubscribe()
        {
            if (_subscribedPortal == null) return;
            _subscribedPortal.PaintPicked -= OnPaintPicked;
            _subscribedPortal.Washed -= OnWashed;
            _subscribedPortal = null;
        }

        void OnPaintPicked(PaintRecord paint, Texture2D tile)
        {
            if (paint == null || !ColorUtility.TryParseHtmlString(paint.lightTone, out var color)) return;
            Submit(new HandEvent(HandEventKind.PortalDipPaint, new HandColor(color.r, color.g, color.b, color.a)));
        }

        void OnWashed() => Submit(new HandEvent(HandEventKind.Washed));
        static Color ToUnity(HandColor color) => new Color(color.R, color.G, color.B, color.A);
    }
}
