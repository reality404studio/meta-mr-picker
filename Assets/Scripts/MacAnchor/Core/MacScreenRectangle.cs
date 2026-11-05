using System;
using MoodPicker.DeskKeyboard;
using UnityEngine;

namespace MoodPicker.MacAnchoring
{
    [Serializable]
    public struct MacScreenRectangle
    {
        public Vector3 center;
        public Vector3 normal;
        public Vector3 up;
        public float widthMeters;
        public float heightMeters;

        public DeskPlane Plane => new DeskPlane
        {
            origin = center, right = Vector3.Cross(normal, up), forward = up, up = normal
        };

        public bool IsValid(MacAnchorSettings settings)
        {
            if (settings == null || !settings.IsValid || !Finite(center) || !Finite(normal) || !Finite(up) ||
                !MacAnchorSettings.Finite(widthMeters) || !MacAnchorSettings.Finite(heightMeters)) return false;
            return widthMeters >= settings.MinSizeMeters && widthMeters <= settings.MaxSizeMeters &&
                heightMeters >= settings.MinSizeMeters && heightMeters <= settings.MaxSizeMeters &&
                Mathf.Abs(normal.magnitude - 1f) <= settings.BasisTolerance &&
                Mathf.Abs(up.magnitude - 1f) <= settings.BasisTolerance &&
                Mathf.Abs(Vector3.Dot(normal, up)) <= settings.BasisTolerance &&
                Mathf.Abs(Vector3.Dot(normal, Vector3.up)) <=
                    Mathf.Sin(settings.MaxVerticalTiltDegrees * Mathf.Deg2Rad) + settings.BasisTolerance;
        }

        public static bool TryFromCorners(Vector3 topLeft, Vector3 bottomLeft, Vector3 bottomRight,
            Vector3 topRight, MacAnchorSettings settings, out MacScreenRectangle rectangle)
        {
            rectangle = default;
            if (settings == null || !settings.IsValid || !Finite(topLeft) || !Finite(bottomLeft) ||
                !Finite(bottomRight) || !Finite(topRight)) return false;
            Vector3 horizontal = (bottomRight + topRight - bottomLeft - topLeft) * 0.5f;
            Vector3 vertical = (topLeft + topRight - bottomLeft - bottomRight) * 0.5f;
            Vector3 right = horizontal.normalized;
            Vector3 up = (vertical - Vector3.Dot(vertical, right) * right).normalized;
            var candidate = new MacScreenRectangle
            {
                center = (topLeft + bottomLeft + bottomRight + topRight) * 0.25f,
                normal = Vector3.Cross(up, right).normalized,
                up = up,
                widthMeters = horizontal.magnitude,
                heightMeters = Vector3.Dot(vertical, up)
            };
            if (!candidate.IsValid(settings)) return false;
            var plane = candidate.Plane;
            float w = candidate.widthMeters * 0.5f, h = candidate.heightMeters * 0.5f;
            if (Vector3.Distance(topLeft, plane.ToWorld(new Vector3(-w, 0f, h))) > settings.RectangleToleranceMeters ||
                Vector3.Distance(bottomLeft, plane.ToWorld(new Vector3(-w, 0f, -h))) > settings.RectangleToleranceMeters ||
                Vector3.Distance(bottomRight, plane.ToWorld(new Vector3(w, 0f, -h))) > settings.RectangleToleranceMeters ||
                Vector3.Distance(topRight, plane.ToWorld(new Vector3(w, 0f, h))) > settings.RectangleToleranceMeters)
                return false;
            rectangle = candidate;
            return true;
        }

        internal static bool Finite(Vector3 value) => MacAnchorSettings.Finite(value.x) &&
            MacAnchorSettings.Finite(value.y) && MacAnchorSettings.Finite(value.z);
    }
}
