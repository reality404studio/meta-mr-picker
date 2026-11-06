using System;
using MoodPicker.Tokens;

namespace MoodPicker.Folio
{
    public readonly struct FolioSlotPose
    {
        public readonly FolioVector Position;
        public readonly float YawDegrees;
        public FolioSlotPose(FolioVector position,float yaw) { Position=position; YawDegrees=yaw; }
    }

    public sealed class FolioLayout
    {
        public int Count { get; }
        public int ClearIndex => Count-1;
        public FolioLayout(int count=(int)OrionTokens.GeometryFolioSlots0)
        {
            if (count < OrionTokens.GeometryFolioSlots0 || count > OrionTokens.GeometryFolioSlots1) throw new ArgumentOutOfRangeException(nameof(count));
            Count=count;
        }
        public FolioSlotPose this[int slot]
        {
            get
            {
                if (slot<0 || slot>=Count) throw new ArgumentOutOfRangeException(nameof(slot));
                float angle=OrionTokens.GeometryFolioFoldAngleDeg;
                float stride=ChipMeshBuilder.Width*(float)Math.Cos(angle*Math.PI/180)+OrionTokens.GeometryFolioSlotGapMeters;
                return new FolioSlotPose(new FolioVector((slot-(Count-1)/2f)*stride,0,0),slot%2==0 ? angle : -angle);
            }
        }
    }

    public static class FolioPlacement
    {
        public static FolioVector DockOffset => new FolioVector(OrionTokens.ZoneHeroFolioX,OrionTokens.ZoneHeroY,OrionTokens.ZoneHeroZ);
        public static FolioVector AbovePalm(FolioVector palm,FolioVector normal)
        {
            if (normal.Length==0) throw new ArgumentException("palm normal must be nonzero",nameof(normal));
            return palm+normal.Normalized*OrionTokens.ZoneHandFolioAboveMeters;
        }
    }
}
