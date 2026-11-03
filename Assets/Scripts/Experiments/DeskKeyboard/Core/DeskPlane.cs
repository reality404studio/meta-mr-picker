using System;
using UnityEngine;

namespace MoodPicker.DeskKeyboard
{
    [Serializable]
    public struct DeskPlane
    {
        public Vector3 origin;
        public Vector3 right;
        public Vector3 forward;
        public Vector3 up;

        public static DeskPlane FromThreePoints(Vector3 frontLeft, Vector3 frontRight, Vector3 back)
        {
            Vector3 x = frontRight - frontLeft;
            if (x.sqrMagnitude < 1e-6f) throw new ArgumentException("앞 두 점이 같음");
            x.Normalize();
            Vector3 n = Vector3.Cross(back - frontLeft, x);
            if (n.sqrMagnitude < 1e-8f) throw new ArgumentException("세 점이 한 줄");
            n.Normalize();
            if (n.y < 0f) n = -n;
            Vector3 z = Vector3.Cross(x, n).normalized;
            if (Vector3.Dot(z, back - frontLeft) < 0f) z = -z;
            x = Vector3.Cross(n, z).normalized;
            return new DeskPlane { origin = frontLeft, right = x, forward = z, up = n };
        }

        public static DeskPlane HorizontalAt(Vector3 center, Vector3 rightDirection)
        {
            Vector3 x = rightDirection;
            x.y = 0f;
            if (x.sqrMagnitude < 1e-6f) throw new ArgumentException("좌우 방향이 없음");
            x.Normalize();
            Vector3 z = Vector3.Cross(x, Vector3.up).normalized;
            return new DeskPlane { origin = center, right = x, forward = z, up = Vector3.up };
        }

        public float Height(Vector3 p) => Vector3.Dot(p - origin, up);

        public Vector3 ToLocal(Vector3 p)
        {
            Vector3 d = p - origin;
            return new Vector3(Vector3.Dot(d, right), Vector3.Dot(d, up), Vector3.Dot(d, forward));
        }

        public Vector3 ToWorld(Vector3 local) => origin + right * local.x + up * local.y + forward * local.z;

        public Quaternion Rotation => Quaternion.LookRotation(forward, up);
    }
}
