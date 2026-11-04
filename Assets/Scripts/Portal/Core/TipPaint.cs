using System;

namespace MoodPicker.Portal
{
    public class TipPaint<TPaint, TTile> where TPaint : class where TTile : class
    {
        readonly Func<int, (TPaint, TTile)> _lookup;

        public TPaint Paint { get; private set; }
        public TTile Tile { get; private set; }
        public event Action<TPaint, TTile, float> Picked;
        public event Action<float> Washed;

        public TipPaint(PalettePortal portal, Func<int, (TPaint, TTile)> lookup)
        {
            _lookup = lookup;
            portal.Dipped += OnDipped;
            portal.Washed += Wash;
        }

        void OnDipped(DipEvent e)
        {
            var (p, t) = _lookup(e.chip);
            if (p == null) return;
            Paint = p;
            Tile = t;
            Picked?.Invoke(p, t, e.t);
        }

        public void Wash(float t)
        {
            Paint = null;
            Tile = null;
            Washed?.Invoke(t);
        }
    }
}
