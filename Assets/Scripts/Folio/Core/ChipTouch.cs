using System;
using MoodPicker.Catch;
using MoodPicker.Tokens;

namespace MoodPicker.Folio
{
    public sealed class ChipTouch
    {
        bool _armed;
        public int LatchedSlot { get; private set; } = -1;
        public bool IsArmed => _armed;
        public float Width { get; }
        public float Height { get; }
        public ChipTouch(float width=ChipMeshBuilder.TileWidth,float height=ChipMeshBuilder.TileHeight)
        {
            Width=Math.Max(width,OrionTokens.HysteresisHitMinSizeMeters0);
            Height=Math.Max(height,OrionTokens.HysteresisHitMinSizeMeters1);
        }
        public void LoseTracking() { _armed=false; }
        public bool Step(int slot,float x,float y,float signedDistance,bool tracked=true)
        {
            if (!tracked || float.IsNaN(signedDistance) || float.IsNaN(x) || float.IsNaN(y)) { LoseTracking(); return false; }
            if (LatchedSlot>=0 && slot!=LatchedSlot) return false;
            if (signedDistance>=OrionTokens.HysteresisTouchDabExitMeters)
            {
                _armed=true; LatchedSlot=-1; return false;
            }
            if (!_armed || slot<0 || Math.Abs(x)>Width/2 || Math.Abs(y)>Height/2 ||
                signedDistance>OrionTokens.HysteresisTouchDabEnterMeters || signedDistance < -OrionTokens.HysteresisTouchDabEnterMeters) return false;
            _armed=false; LatchedSlot=slot;
            return true;
        }
    }

    public sealed class FolioInteraction
    {
        readonly FolioSlots _slots;
        public ChipTouch Touch { get; } = new ChipTouch();
        public event Action<PaintRecord> Picked;
        public event Action Washed;
        public FolioInteraction(FolioSlots slots) { _slots=slots ?? throw new ArgumentNullException(nameof(slots)); }
        public bool Step(int slot,float x,float y,float signedDistance,bool tracked=true)
        {
            if (slot<0 || slot>=_slots.Count || (slot!=_slots.ClearIndex && _slots[slot]==null)) return false;
            if (!Touch.Step(slot,x,y,signedDistance,tracked)) return false;
            if (slot==_slots.ClearIndex) Washed?.Invoke(); else Picked?.Invoke(_slots[slot]);
            return true;
        }
    }
}
