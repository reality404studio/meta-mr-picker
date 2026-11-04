using System;

namespace MoodPicker.Dab
{
    public enum DabKind { Tap, Press }

    public struct DabJudgement
    {
        public DabKind kind;
        public float t;
        public float contactT;
        public float maxDepth;
        public float approachSpeed;
    }

    public class HandJudge
    {
        readonly DabSettings _s;
        bool _hasPrev, _inContact, _decided, _armed = true;
        float _prevT, _prevDist, _contactT, _maxDepth, _approach;

        public event Action<DabJudgement> Judged;
        public bool InContact => _inContact;

        public HandJudge(DabSettings settings) { _s = settings; }

        public void Update(float t, float signedDistance, bool tracked)
        {
            if (!tracked) { _inContact = false; _hasPrev = false; return; }
            float speed = _hasPrev && t > _prevT ? (_prevDist - signedDistance) / (t - _prevT) : 0f;

            if (!_inContact)
            {
                if (signedDistance > _s.releaseHeight) _armed = true;
                if (_armed && signedDistance <= -_s.contactDepth && _hasPrev && speed >= _s.minApproachSpeed)
                {
                    _inContact = true;
                    _decided = false;
                    _armed = false;
                    _contactT = t;
                    _maxDepth = -signedDistance;
                    _approach = speed;
                }
            }
            else
            {
                _maxDepth = Math.Max(_maxDepth, -signedDistance);
                bool released = signedDistance > _s.releaseHeight;
                float held = t - _contactT;
                if (!_decided)
                {
                    if (_maxDepth >= _s.pressDepth || held >= _s.pressHoldSeconds) Decide(DabKind.Press, t);
                    else if (released || held >= _s.decideSeconds) Decide(DabKind.Tap, t);
                }
                if (released || held > _s.maxContactSeconds) _inContact = false;
            }
            _prevT = t;
            _prevDist = signedDistance;
            _hasPrev = true;
        }

        void Decide(DabKind k, float t)
        {
            _decided = true;
            Judged?.Invoke(new DabJudgement { kind = k, t = t, contactT = _contactT, maxDepth = _maxDepth, approachSpeed = _approach });
        }
    }
}
