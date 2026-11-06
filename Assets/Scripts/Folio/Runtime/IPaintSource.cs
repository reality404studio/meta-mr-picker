using System;
using MoodPicker.Catch;
using MoodPicker.HandStates;
using UnityEngine;

namespace MoodPicker.Folio
{
    public interface IPaintSource
    {
        event Action<PaintRecord,Texture2D> FolioChipPicked;
        event Action Washed;
    }

    public sealed class FolioHandStateAdapter : IDisposable
    {
        readonly IPaintSource _source;
        readonly HandStateController _target;
        bool _disposed;
        public FolioHandStateAdapter(IPaintSource source,HandStateController target)
        {
            _source=source ?? throw new ArgumentNullException(nameof(source));
            _target=target != null ? target : throw new ArgumentNullException(nameof(target));
            _source.FolioChipPicked+=Picked; _source.Washed+=Washed;
        }
        void Picked(PaintRecord record,Texture2D tile)
        {
            if (_target==null || record==null || !ColorUtility.TryParseHtmlString(record.lightTone,out var color)) return;
            _target.Submit(new HandEvent(HandEventKind.PortalDipPaint,new HandColor(color.r,color.g,color.b,color.a)));
        }
        void Washed()
        {
            if (_target!=null) _target.Submit(new HandEvent(HandEventKind.Washed));
        }
        public void Dispose()
        {
            if (_disposed) return;
            _source.FolioChipPicked-=Picked; _source.Washed-=Washed; _disposed=true;
        }
    }
}
