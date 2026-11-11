using System;
using Meta.XR;
using UnityEngine;

namespace MoodPicker.Splatter
{
    public sealed class SplatCameraCatch : MonoBehaviour
    {
        const string Tag = "[MP]";
        const int CaughtSize = 512;

        [SerializeField] SplatWordController wordController;
        [SerializeField] SplatShelf shelf;
        [SerializeField] SplatTuning tuning;
        [SerializeField] PassthroughCameraAccess cameraAccess;

        readonly SplatSurfaceMaterial _live = new SplatSurfaceMaterial { label = "live", mirrorTiled = true };
        bool _catchRequested;
        bool _loggedLive;

        void OnEnable()
        {
            if (tuning == null) return;
            tuning.CatchRequested += RequestCatch;
            tuning.ReleaseCatchRequested += ReleaseCatch;
        }

        void OnDisable()
        {
            if (tuning == null) return;
            tuning.CatchRequested -= RequestCatch;
            tuning.ReleaseCatchRequested -= ReleaseCatch;
        }

        public void RequestCatch()
        {
            _catchRequested = true;
            if (tuning != null) tuning.liveCamera = true;
        }

        void ReleaseCatch()
        {
            _catchRequested = false;
            if (tuning != null) tuning.liveCamera = false;
        }

        public static Vector4 CenterCrop(int width, int height, float fraction)
        {
            float side = Mathf.Clamp01(fraction) * Mathf.Min(width, height);
            float sx = side / Mathf.Max(1, width);
            float sy = side / Mathf.Max(1, height);
            return new Vector4(sx, sy, (1f - sx) * 0.5f, (1f - sy) * 0.5f);
        }

        void Update()
        {
            if (tuning == null || cameraAccess == null || wordController == null) return;
            bool live = tuning.liveCamera;
            if (cameraAccess.enabled != live)
            {
                cameraAccess.enabled = live;
                _loggedLive = false;
                Debug.Log($"{Tag} camera live={live}");
            }
            if (!live || !cameraAccess.IsPlaying)
            {
                wordController.ClearPreview();
                return;
            }
            Texture texture = cameraAccess.GetTexture();
            if (texture == null)
            {
                wordController.ClearPreview();
                return;
            }

            Vector4 crop = CenterCrop(texture.width, texture.height, tuning.cameraCrop);
            if (!_loggedLive)
            {
                _loggedLive = true;
                Debug.Log($"{Tag} camera texture {texture.width}x{texture.height} res={cameraAccess.CurrentResolution} crop={crop}");
            }
            if (_catchRequested)
            {
                _catchRequested = false;
                var caught = new RenderTexture(CaughtSize, CaughtSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    name = "SplatCaught",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                Graphics.Blit(texture, caught, new Vector2(crop.x, crop.y), new Vector2(crop.z, crop.w));
                string label = SplatStillNames.Label(DateTime.Now);
                if (shelf != null) shelf.Add(SplatSurfaceMaterial.FromStill(caught, label, SplatSurfaceMaterial.WholeTexture, tuning.cameraGamma, true));
                tuning.liveCamera = false;
                wordController.ClearPreview();
                Debug.Log($"{Tag} catch still {CaughtSize}px from {texture.width}x{texture.height} crop={crop} -> shelf {label}");
                return;
            }
            _live.texture = texture;
            _live.crop = crop;
            _live.gamma = tuning.cameraGamma;
            wordController.SetPreview(_live);
        }
    }
}
