using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MoodPicker.Splatter
{
    public sealed class SplatStillCapture : MonoBehaviour
    {
        const string Tag = "[MP]";

        [SerializeField] int layer = 25;
        [SerializeField] int size = 2048;
        [SerializeField] float margin = 1.12f;
        [SerializeField] TextMesh toast;
        [SerializeField] float toastSeconds = 2.2f;
        [SerializeField] float toastDrop = 0.13f;

        Camera _camera;
        RenderTexture _msaa;
        RenderTexture _resolved;
        bool _busy;
        float _toastUntil = -1f;
        volatile string _finished;
        volatile string _failed;
        Transform _toastAnchor;
        Transform _head;

        public bool Busy => _busy;
        public string LastPath { get; private set; }

        public static string Folder => Path.Combine(Application.persistentDataPath, "stills");

        void Ensure()
        {
            if (_camera == null)
            {
                var go = new GameObject("StillCamera");
                go.transform.SetParent(transform, false);
                _camera = go.AddComponent<Camera>();
                _camera.enabled = false;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                _camera.cullingMask = 1 << layer;
                _camera.allowMSAA = true;
                _camera.allowHDR = false;
                var data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = false;
                data.renderShadows = false;
                data.allowXRRendering = false;
            }
            if (_msaa == null)
            {
                _msaa = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4, name = "StillMsaa" };
                _msaa.Create();
                _resolved = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "StillResolved" };
                _resolved.Create();
            }
        }

        public bool Capture(Transform head, SplatLetterRenderer target)
        {
            if (_busy || head == null || target == null || target.Sim == null) return false;
            Ensure();
            Transform t = target.transform;
            Vector3 center = t.TransformPoint(target.Sim.HomeCenter);
            float radius = target.Sim.HomeRadius * t.lossyScale.x;
            float distance = Mathf.Max(0.05f, Vector3.Distance(head.position, center));
            _camera.transform.SetPositionAndRotation(head.position, Quaternion.LookRotation(center - head.position, Vector3.up));
            _camera.fieldOfView = 2f * Mathf.Atan(radius * margin / distance) * Mathf.Rad2Deg;
            _camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 3f);
            _camera.farClipPlane = distance + radius * 3f;
            _camera.aspect = 1f;

            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = _msaa };
            if (RenderPipeline.SupportsRenderRequest(_camera, request))
            {
                RenderPipeline.SubmitRenderRequest(_camera, request);
            }
            else
            {
                _camera.targetTexture = _msaa;
                _camera.Render();
                _camera.targetTexture = null;
            }
            Graphics.Blit(_msaa, _resolved);

            _busy = true;
            _toastAnchor = t;
            _head = head;
            string folder = Folder;
            string path = Path.Combine(folder, SplatStillNames.File(DateTime.Now));
            int w = _resolved.width, h = _resolved.height;
            AsyncGPUReadback.Request(_resolved, 0, TextureFormat.RGBA32, r => OnReadback(r, folder, path, w, h));
            return true;
        }

        void OnReadback(AsyncGPUReadbackRequest request, string folder, string path, int w, int h)
        {
            if (request.hasError)
            {
                _busy = false;
                Debug.Log($"{Tag} still failed: readback error");
                return;
            }
            byte[] rgba = request.GetData<byte>().ToArray();
            Task.Run(() =>
            {
                try
                {
                    Directory.CreateDirectory(folder);
                    byte[] png = ImageConversion.EncodeArrayToPNG(rgba, GraphicsFormat.R8G8B8A8_SRGB, (uint)w, (uint)h);
                    File.WriteAllBytes(path, png);
                    _finished = path;
                }
                catch (Exception e)
                {
                    _failed = e.Message;
                }
            });
        }

        void Update()
        {
            Poll();
        }

        public void Poll()
        {
            string done = _finished;
            if (done != null)
            {
                _finished = null;
                _busy = false;
                LastPath = done;
                Debug.Log($"{Tag} still saved {done}");
                ShowToast("saved  " + DateTime.Now.ToString("HH:mm:ss"));
            }
            string failed = _failed;
            if (failed != null)
            {
                _failed = null;
                _busy = false;
                Debug.Log($"{Tag} still failed: {failed}");
            }
            if (toast == null) return;
            bool show = Time.unscaledTime < _toastUntil;
            if (toast.gameObject.activeSelf != show) toast.gameObject.SetActive(show);
            if (!show || _toastAnchor == null) return;
            Vector3 pos = _toastAnchor.position + Vector3.down * toastDrop * _toastAnchor.lossyScale.x;
            Vector3 view = _head != null ? Vector3.ProjectOnPlane(pos - _head.position, Vector3.up) : _toastAnchor.forward;
            toast.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(view.sqrMagnitude > 1e-6f ? view.normalized : Vector3.forward, Vector3.up));
        }

        public void ShowToast(string text, Transform anchor, Transform head)
        {
            _toastAnchor = anchor;
            _head = head;
            ShowToast(text);
        }

        void ShowToast(string text)
        {
            if (toast == null) return;
            toast.text = text;
            _toastUntil = Time.unscaledTime + toastSeconds;
        }

        void OnDestroy()
        {
            if (_msaa != null) _msaa.Release();
            if (_resolved != null) _resolved.Release();
        }
    }
}
