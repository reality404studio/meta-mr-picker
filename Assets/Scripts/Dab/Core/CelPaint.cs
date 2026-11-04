using MoodPicker.Catch;
using UnityEngine;

namespace MoodPicker.Dab
{
    public static class CelPaint
    {
        public const string ShaderName = "MoodPicker/CelPaint";

        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        public static readonly int LightTone = Shader.PropertyToID("_LightTone");
        public static readonly int ShadowTone = Shader.PropertyToID("_ShadowTone");
        public static readonly int LightDir = Shader.PropertyToID("_LightDir");
        public static readonly int ToneMix = Shader.PropertyToID("_ToneMix");

        public static readonly Vector3 FallbackLightDir = Vector3.up;

        public static void Apply(Material m, PaintRecord r, Texture caught)
        {
            if (m == null || r == null) return;
            if (caught != null)
            {
                caught.wrapMode = TextureWrapMode.Repeat;
                m.SetTexture(BaseMap, caught);
            }
            m.SetColor(LightTone, ToneOf(r.lightTone, m.GetColor(LightTone)));
            m.SetColor(ShadowTone, ToneOf(r.shadowTone, m.GetColor(ShadowTone)));
            Vector3 d = LightDirOf(r);
            m.SetVector(LightDir, new Vector4(d.x, d.y, d.z, 0f));
        }

        public static Vector3 LightDirOf(PaintRecord r)
        {
            Vector3 d;
            if (TryVec(r.lightDir, out d)) return d;
            if (TryVec(r.lightDirView, out d)) return d;
            return FallbackLightDir;
        }

        public static Color ToneOf(string hex, Color fallback)
        {
            Color c;
            return !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out c) ? c : fallback;
        }

        public static string TexturePath(PaintStore store, PaintRecord r)
        {
            string tile = store.PathOf(r, PaintStore.TileFile);
            return System.IO.File.Exists(tile) ? tile : store.PathOf(r, PaintStore.StillFile);
        }

        static bool TryVec(float[] a, out Vector3 v)
        {
            v = Vector3.zero;
            if (a == null || a.Length < 3) return false;
            v = new Vector3(a[0], a[1], a[2]);
            if (v.sqrMagnitude < 1e-6f) return false;
            v.Normalize();
            return true;
        }
    }
}
