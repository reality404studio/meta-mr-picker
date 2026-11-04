using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MoodPicker.Catch
{
    public class PunchCueView
    {
        public static readonly Color Navy = new Color(0x15 / 255f, 0x31 / 255f, 0x7C / 255f, 1f);
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        readonly CatchSettings _s;
        readonly Strip _white, _navy, _red;
        readonly List<(Vector3, Vector3)> _w = new List<(Vector3, Vector3)>();
        readonly List<(Vector3, Vector3)> _r = new List<(Vector3, Vector3)>();
        readonly List<(Vector3, Vector3)> _frame = new List<(Vector3, Vector3)>();

        public Material NavyMaterial { get; }
        public int WhiteSegments => _w.Count;
        public int RedSegments => _r.Count;

        public PunchCueView(Transform parent, Material white, Material red, CatchSettings s)
        {
            _s = s;
            NavyMaterial = new Material(white) { name = "Catch_CueNavy" };
            NavyMaterial.SetColor(BaseColor, Navy);
            _navy = new Strip(parent, "CatchCueBacking", NavyMaterial);
            _white = new Strip(parent, "CatchCue", white);
            _red = new Strip(parent, "CatchCueFire", red);
        }

        public void Draw(PunchCueState st, CatchSquareView hole, Matrix4x4 space, Vector3 eyeWorld)
        {
            _w.Clear(); _r.Clear(); _frame.Clear();
            PunchCue.Polygon(hole.Corners(), CueLine.Solid, _s, _frame);
            PunchCue.Segments(st.leaderFrom, st.leaderTo, st.leader, _s, _w);
            if (st.lever != CueLine.Hidden)
            {
                LeverPose lp = st.leverPose;
                PunchCue.Polygon(lp.plate, st.lever, _s, _w);
                PunchCue.Segments(lp.hinge, lp.armTip, st.lever, _s, _w);
                PunchCue.Segments(lp.hinge, lp.plate[2], st.lever, _s, _w);
                if (st.lever == CueLine.Solid && lp.progress > 0f)
                {
                    Vector3 drop = -lp.up * _s.cueLineWidth * 1.6f;
                    _r.Add((lp.hinge + drop, Vector3.Lerp(lp.hinge, lp.armTip, lp.progress) + drop));
                }
            }
            if (st.blade != CueLine.Hidden)
            {
                PunchCue.Segments(st.bladeA, st.bladeB, st.blade, _s, _w);
                if (st.blade == CueLine.Solid && st.bladeProgress > 0f)
                {
                    Vector3 down = -hole.Up * _s.cueLineWidth * 1.6f;
                    _r.Add((st.bladeA + down, Vector3.Lerp(st.bladeA, st.bladeB, st.bladeProgress) + down));
                }
            }

            float w = _s.cueLineWidth;
            _red.Build(_r, space, eyeWorld, w * 1.4f, -0.0005f);
            _w.AddRange(_frame);
            _white.Build(_w, space, eyeWorld, w, 0f);
            _navy.Build(_w, space, eyeWorld, w + _s.cueBackingWidth * 2f, 0.0015f);
        }

        public void Hide()
        {
            _white.Clear(); _red.Clear(); _navy.Clear();
        }

        class Strip
        {
            readonly Mesh _mesh;
            readonly List<Vector3> _v = new List<Vector3>();
            readonly List<int> _i = new List<int>();

            public Strip(Transform parent, string name, Material m)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                _mesh = new Mesh { name = name };
                _mesh.MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh = _mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            public void Clear() { _mesh.Clear(); }

            public void Build(List<(Vector3 a, Vector3 b)> segs, Matrix4x4 space, Vector3 eye, float width, float push)
            {
                _v.Clear(); _i.Clear();
                foreach (var (la, lb) in segs)
                {
                    Vector3 a = space.MultiplyPoint3x4(la), b = space.MultiplyPoint3x4(lb);
                    Vector3 dir = b - a;
                    if (dir.sqrMagnitude < 1e-10f) continue;
                    Vector3 mid = (a + b) * 0.5f, view = (mid - eye).normalized;
                    Vector3 side = Vector3.Cross(dir, view).normalized * (width * 0.5f);
                    Vector3 ext = dir.normalized * (width * 0.5f);
                    Vector3 off = view * push;
                    int n = _v.Count;
                    _v.Add(a - ext - side + off); _v.Add(a - ext + side + off);
                    _v.Add(b + ext + side + off); _v.Add(b + ext - side + off);
                    _i.Add(n); _i.Add(n + 1); _i.Add(n + 2);
                    _i.Add(n); _i.Add(n + 2); _i.Add(n + 3);
                    _i.Add(n); _i.Add(n + 2); _i.Add(n + 1);
                    _i.Add(n); _i.Add(n + 3); _i.Add(n + 2);
                }
                _mesh.Clear();
                _mesh.indexFormat = _v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                _mesh.SetVertices(_v);
                _mesh.SetTriangles(_i, 0, false);
                _mesh.bounds = new Bounds(eye, Vector3.one * 20f);
            }
        }
    }
}
