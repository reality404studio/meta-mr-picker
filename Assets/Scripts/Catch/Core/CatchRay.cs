using UnityEngine;

namespace MoodPicker.Catch
{
    public struct CatchSquareView
    {
        public Vector3 center;
        public Quaternion rotation;
        public float size;

        public Vector3 Right => rotation * Vector3.right;
        public Vector3 Up => rotation * Vector3.up;

        public Vector3[] Corners()
        {
            float h = size * 0.5f;
            Vector3 r = Right * h, u = Up * h;
            return new[] { center - r - u, center + r - u, center + r + u, center - r + u };
        }
    }

    public static class CatchRay
    {
        public static Vector3 Forward(Pose head) => head.rotation * Vector3.forward;

        public static Vector3 Center(Pose head, float distance) => head.position + Forward(head) * distance;

        public static float DistanceAlong(Pose head, Vector3 p) => Vector3.Dot(p - head.position, Forward(head));

        public static CatchSquareView Square(Pose head, float distance, float size) =>
            new CatchSquareView { center = Center(head, distance), rotation = head.rotation, size = size };

        public static float SideFromDiagonal(Pose head, Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            float x = Mathf.Abs(Vector3.Dot(d, head.rotation * Vector3.right));
            float y = Mathf.Abs(Vector3.Dot(d, head.rotation * Vector3.up));
            return Mathf.Max(x, y);
        }
    }
}
