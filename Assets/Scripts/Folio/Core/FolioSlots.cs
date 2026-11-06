using System;
using MoodPicker.Catch;
using MoodPicker.Tokens;

namespace MoodPicker.Folio
{
    public sealed class FolioSlots
    {
        readonly PaintRecord[] _records;
        readonly long[] _order;
        long _next;
        public int Count => _records.Length;
        public int ClearIndex => Count-1;
        public PaintRecord this[int slot] => _records[slot];
        public long Revision(int slot) => _order[slot];
        public FolioSlots(int count=(int)OrionTokens.GeometryFolioSlots0)
        {
            var layout=new FolioLayout(count);
            _records=new PaintRecord[layout.Count]; _order=new long[layout.Count];
        }
        public int FirstEmpty
        {
            get { for (int i=0;i<ClearIndex;i++) if (_records[i]==null) return i; return -1; }
        }
        public int NextSlot
        {
            get
            {
                int empty=FirstEmpty;
                if (empty>=0) return empty;
                int oldest=0;
                for (int i=1;i<ClearIndex;i++) if (_order[i]<_order[oldest]) oldest=i;
                return oldest;
            }
        }
        public int Insert(PaintRecord record)
        {
            if (record==null) throw new ArgumentNullException(nameof(record));
            int slot=NextSlot;
            _records[slot]=record; _order[slot]=++_next;
            return slot;
        }
        public void Remove(int slot)
        {
            if (slot<0 || slot>=ClearIndex) throw new ArgumentOutOfRangeException(nameof(slot));
            _records[slot]=null; _order[slot]=++_next;
        }
    }
}
