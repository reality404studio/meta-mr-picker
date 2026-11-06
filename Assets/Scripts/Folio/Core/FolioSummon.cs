using System;
using MoodPicker.Tokens;

namespace MoodPicker.Folio
{
    public sealed class FolioSummon
    {
        public bool IsSummoned { get; private set; }
        float _held, _released;
        public bool Step(bool tracked,float palmUpDot,float deltaSeconds)
        {
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds<0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (!IsSummoned)
            {
                _released=0;
                _held=tracked && palmUpDot>=OrionTokens.HysteresisPalmUpEnterDot ? _held+deltaSeconds : 0;
                if (_held>=OrionTokens.HysteresisPalmUpHoldSeconds) { IsSummoned=true; _held=0; return true; }
            }
            else
            {
                _held=0;
                _released=!tracked || float.IsNaN(palmUpDot) || palmUpDot<OrionTokens.HysteresisPalmUpExitDot ? _released+deltaSeconds : 0;
                if (_released>=OrionTokens.HysteresisPalmUpReleaseSeconds) { IsSummoned=false; _released=0; return true; }
            }
            return false;
        }
    }
}
