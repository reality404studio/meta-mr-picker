using System;
using MoodPicker.Catch;
using MoodPicker.Tokens;

namespace MoodPicker.Folio
{
    public sealed class FlyToFolio
    {
        public int Slot { get; }
        public long Revision { get; }
        public FolioVector Origin { get; }
        public const float Duration = OrionTokens.MotionChipFlyToFolioSeconds;
        public const string Ease = OrionTokens.MotionChipFlyToFolioEase;
        public FlyToFolio(FolioSlots slots,PaintRecord record,FolioVector origin)
        {
            if (slots==null) throw new ArgumentNullException(nameof(slots));
            Slot=slots.Insert(record); Revision=slots.Revision(Slot); Origin=origin;
        }
        public bool IsCurrent(FolioSlots slots) => slots.Revision(Slot)==Revision;
        public bool HasArrived(float elapsed) => elapsed>=Duration;
        public static float Progress(float elapsed)
        {
            float t=Math.Max(0,Math.Min(1,elapsed/Duration));
            return t<0.5f ? 4*t*t*t : 1-(float)Math.Pow(-2*t+2,3)/2;
        }
        public FolioVector Position(float elapsed,FolioVector target)
        {
            float t=Progress(elapsed);
            float height=(target-Origin).Length/4;
            return Origin+(target-Origin)*t+new FolioVector(0,4*height*t*(1-t),0);
        }
    }
}
