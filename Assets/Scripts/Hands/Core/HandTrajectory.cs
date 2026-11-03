using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoodPicker.Hands
{
    [Serializable]
    public class HandTrajectory
    {
        public int version = 1;
        public string source = "";
        public string build = "";
        public List<HandFrame> frames = new List<HandFrame>();

        public float Duration => frames.Count == 0 ? 0f : frames[frames.Count - 1].t - frames[0].t;

        public string ToJson(bool pretty = false) => JsonUtility.ToJson(this, pretty);

        public static HandTrajectory FromJson(string json)
        {
            var t = JsonUtility.FromJson<HandTrajectory>(json);
            if (t == null || t.frames == null) throw new FormatException("HandTrajectory JSON 이 아님");
            if (t.version != 1) throw new FormatException("HandTrajectory version " + t.version + " 은 모름");
            return t;
        }
    }

    public class RecordedHandInput : IHandInput
    {
        readonly HandTrajectory _traj;
        int _next;

        public RecordedHandInput(HandTrajectory traj) { _traj = traj; }

        public bool Finished => _next >= _traj.frames.Count;

        public bool TryGetFrame(out HandFrame frame)
        {
            if (Finished) { frame = default; return false; }
            frame = _traj.frames[_next++];
            return true;
        }
    }
}
