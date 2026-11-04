using UnityEngine;

namespace MoodPicker.Catch
{
    public static class CatchProjection
    {
        public static Vector3[] ThroughSquare(Vector3 eye, Vector3[] corners, float sceneDistance)
        {
            var pts = new Vector3[corners.Length];
            for (int i = 0; i < corners.Length; i++)
                pts[i] = eye + (corners[i] - eye).normalized * sceneDistance;
            return pts;
        }
    }
}
