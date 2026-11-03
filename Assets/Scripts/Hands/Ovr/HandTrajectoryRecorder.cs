using System.IO;
using UnityEngine;

namespace MoodPicker.Hands.Ovr
{
    /// 시작 후 seconds 동안 손 궤적을 기록해 persistentDataPath/hand_<시각>.json 에 저장.
    /// 꺼내기: adb pull /sdcard/Android/data/<패키지>/files/hand_*.json
    public class HandTrajectoryRecorder : MonoBehaviour
    {
        [SerializeField] OvrHandInput input;
        [SerializeField] float startDelay = 3f;
        [SerializeField] float seconds = 15f;
        [SerializeField] string source = "device";

        HandTrajectory _traj;
        float _t0 = -1f;
        bool _saved;

        void Update()
        {
            if (_saved || Time.time < startDelay) return;
            if (_traj == null)
            {
                _traj = new HandTrajectory { source = source, build = Reality404.BuildGate.BuildIdLog.Id };
                _t0 = Time.time;
                Debug.Log($"[MP] hand record start {seconds}s");
            }
            if (input.TryGetFrame(out var f))
            {
                f.t = Time.time - _t0;
                _traj.frames.Add(f);
            }
            if (Time.time - _t0 >= seconds) Save();
        }

        void OnApplicationPause(bool paused) { if (paused && _traj != null && !_saved) Save(); }

        void Save()
        {
            _saved = true;
            string path = Path.Combine(Application.persistentDataPath, $"hand_{System.DateTime.Now:MMdd-HHmmss}.json");
            File.WriteAllText(path, _traj.ToJson());
            Debug.Log($"[MP] hand record saved frames={_traj.frames.Count} path={path}");
        }
    }
}
