using UnityEngine;

namespace MoodPicker.Splatter
{
    public sealed class SplatFilmMode : MonoBehaviour
    {
        const string Tag = "[MP]";
        static readonly int InflateId = Shader.PropertyToID("_Inflate");
        static readonly int EdgeId = Shader.PropertyToID("_EdgeSoft");

        [SerializeField] SplatTuning tuning;
        [SerializeField] Camera[] cameras;
        [SerializeField] Material handMaterial;

        bool _applied;
        bool _film;
        float _background = float.NaN;
        float _inflate = float.NaN;
        float _edge = float.NaN;

        void LateUpdate()
        {
            if (tuning == null) return;
            bool film = tuning.filmVr;
            if (!_applied || film != _film || !Mathf.Approximately(tuning.filmBackground, _background))
            {
                _applied = true;
                if (film != _film) Debug.Log($"{Tag} film vr {(film ? "on" : "off")} bg={tuning.filmBackground:0.##}");
                _film = film;
                _background = tuning.filmBackground;
                float grey = SplatFilmLook.ClearGrey(film, _background);
                var clear = new Color(grey, grey, grey, SplatFilmLook.ClearAlpha(film));
                if (cameras != null)
                {
                    foreach (Camera cam in cameras)
                    {
                        if (cam == null) continue;
                        cam.backgroundColor = clear;
                        cam.cullingMask = SplatFilmLook.CullingMask(cam.cullingMask, film);
                    }
                }
            }
            if (handMaterial != null && (!Mathf.Approximately(tuning.filmHandInflate, _inflate) || !Mathf.Approximately(tuning.filmHandEdge, _edge)))
            {
                _inflate = tuning.filmHandInflate;
                _edge = tuning.filmHandEdge;
                handMaterial.SetFloat(InflateId, _inflate);
                handMaterial.SetFloat(EdgeId, _edge);
            }
        }
    }
}
