using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoodPicker.Ritual
{
    public sealed class TimePullController : MonoBehaviour
    {
        const string Tag = "[MP]";
        const float Grace = 0.15f;

        struct Pull
        {
            public bool active;
            public TimeHeap heap;
            public TimePieceView piece;
            public Vector3 start;
            public Vector3 tip;
            public float raw;
            public int units;
            public float lost;
        }

        [SerializeField] TimeHeap[] heaps;

        readonly Pull[] _pulls = new Pull[2];

        public event Action<TimePieceView, int, int> UnitChanged;
        public event Action<TimePieceView> Released;
        public event Action<TimePieceView> Cancelled;
        public TimeHeap[] Heaps => heaps;

        public bool IsPulling(int hand) => _pulls[hand].active;

        public TimeHeap HeapAt(Vector3 point, float reach, out float distance)
        {
            TimeHeap best = null;
            distance = float.MaxValue;
            if (heaps == null) return null;
            for (int i = 0; i < heaps.Length; i++)
            {
                TimeHeap heap = heaps[i];
                if (heap == null || !heap.Present) continue;
                float d = Vector3.Distance(point, heap.transform.position) - heap.Radius;
                if (d < reach && d < distance)
                {
                    best = heap;
                    distance = d;
                }
            }
            return best;
        }

        public TimePieceView Begin(int hand, TimeHeap heap, Vector3 tip, Func<TimeHeap, TimePieceView> create)
        {
            ref Pull p = ref _pulls[hand];
            Vector3 center = heap.transform.position;
            Vector3 toTip = tip - center;
            Vector3 dir = toTip.sqrMagnitude > 1e-6f ? toTip.normalized : -heap.transform.forward;
            p.active = true;
            p.heap = heap;
            p.start = tip;
            p.tip = tip;
            p.raw = 0f;
            p.units = 0;
            p.lost = 0f;
            p.piece = create(heap);
            p.piece.BeginPull(center + dir * (heap.Radius * 0.7f), dir);
            Debug.Log($"{Tag} ritual pull start {heap.Category?.id} hand={hand}");
            return p.piece;
        }

        public void Tick(int hand, in RitualHand state, TimeRitualConfig config, float dt)
        {
            ref Pull p = ref _pulls[hand];
            if (!p.active) return;
            if (p.piece == null || !p.piece.Alive)
            {
                p = default;
                return;
            }
            bool holding = state.tracked && state.pinching;
            if (holding)
            {
                p.lost = 0f;
                p.tip = state.pinch;
            }
            else
            {
                p.lost += dt;
            }
            if (holding || p.lost < Grace)
            {
                float raw = Vector3.Distance(p.tip, p.start);
                Vector3 away = p.tip - p.heap.transform.position;
                if (away.sqrMagnitude > 1e-8f) p.heap.Spin(raw - p.raw, away.normalized, dt);
                p.raw = raw;
                int units = TimeUnits.Step(raw, p.units, config.unitLength, config.hysteresis, config.maxUnits);
                int before = p.units;
                bool changed = p.piece.UpdatePull(p.tip, units, raw, dt);
                p.units = units;
                if (changed)
                {
                    p.heap.Pulse();
                    UnitChanged?.Invoke(p.piece, before, units);
                    Debug.Log($"{Tag} ritual units {p.heap.Category?.id} {units} ({TimeUnits.Minutes(units)}m) raw={raw:0.000}");
                }
                return;
            }
            TimePieceView piece = p.piece;
            TimeHeap heap = p.heap;
            int final = p.units;
            p = default;
            if (final <= 0)
            {
                piece.Dissolve(heap.transform.position);
                Cancelled?.Invoke(piece);
                Debug.Log($"{Tag} ritual pull cancel {heap.Category?.id}");
            }
            else
            {
                piece.ReleasePull();
                Released?.Invoke(piece);
                Debug.Log($"{Tag} ritual piece {heap.Category?.id} units={final}");
            }
        }

        public void Abort()
        {
            for (int h = 0; h < _pulls.Length; h++)
            {
                if (_pulls[h].active && _pulls[h].piece != null) _pulls[h].piece.Kill();
                _pulls[h] = default;
            }
        }
    }
}
