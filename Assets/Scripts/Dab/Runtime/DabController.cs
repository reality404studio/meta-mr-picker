using System.Collections;
using MoodPicker.Catch;
using MoodPicker.Hands;
using MoodPicker.Portal;
using UnityEngine;

namespace MoodPicker.Dab
{
    public class DabController : MonoBehaviour
    {
        const string Tag = "[MP]";
        static readonly int PaintMaskId = Shader.PropertyToID("_PaintMask");

        [SerializeField] Transform trackingSpace;
        [SerializeField] Transform head;
        [SerializeField] MonoBehaviour handInputSource;
        [SerializeField] PortalController portal;
        [SerializeField] DabSettings settings;
        [SerializeField] Renderer target;
        [SerializeField] MeshCollider contactCollider;
        [SerializeField] MeshCollider uvCollider;

        [Header("Placement (baseline)")]
        [SerializeField] float placeDistance = 0.45f;
        [SerializeField] float placeBelowEye = 0.35f;
        [SerializeField] float tipRadius = 0.005f;

        IHandInput _input;
        HandJudge _judge;
        PaintMask _mask;
        Texture2D _maskTex;
        byte[] _maskBytes;
        Material _mat;
        SphereCollider _tip;
        PaintRecord _paint;
        Vector2 _contactUV;
        bool _hasContactUV, _wasContact;

        void Awake()
        {
            _input = handInputSource as IHandInput;
            if (settings == null) settings = DabSettings.Defaults();
            _judge = new HandJudge(settings);
            _judge.Judged += OnJudged;
            _mask = new PaintMask(settings);
            _maskTex = new Texture2D(_mask.Size, _mask.Size, TextureFormat.R8, false, true) { wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            _maskBytes = new byte[_mask.Size * _mask.Size];
            UploadMask();
            _mat = target.material;
            _mat.SetTexture(PaintMaskId, _maskTex);
            var tipGo = new GameObject("DabTipProbe");
            tipGo.transform.SetParent(transform, false);
            _tip = tipGo.AddComponent<SphereCollider>();
            _tip.radius = tipRadius;
            _tip.isTrigger = true;
            _tip.enabled = false;
            if (portal != null) portal.PaintPicked += OnPaintPicked;
            Debug.Log($"{Tag} dab ready mask={_mask.Size}px tris={MeshTris()} paint=none");
        }

        int MeshTris()
        {
            var mf = target.GetComponent<MeshFilter>();
            return mf != null && mf.sharedMesh != null ? mf.sharedMesh.triangles.Length / 3 : 0;
        }

        IEnumerator Start()
        {
            yield return null;
            Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            target.transform.position = head.position + fwd * placeDistance - Vector3.up * placeBelowEye;
            target.transform.rotation = Quaternion.LookRotation(fwd);
            Debug.Log($"{Tag} dab bottle at {target.transform.position}");
        }

        void OnDestroy()
        {
            if (portal != null) portal.PaintPicked -= OnPaintPicked;
            if (_maskTex != null) Destroy(_maskTex);
        }

        void OnPaintPicked(PaintRecord r, Texture2D tile)
        {
            _paint = r;
            CelPaint.Apply(_mat, r, tile);
            Debug.Log($"{Tag} dab paint No.{r.number:000} light={r.lightTone} shadow={r.shadowTone}");
        }

        void Update()
        {
            float t = Time.time;
            if (_input != null && _input.TryGetFrame(out HandFrame f))
            {
                bool tracked = f.right.tracked;
                float d = 1f;
                if (tracked)
                {
                    Vector3 tip = trackingSpace.TransformPoint(f.right.indexTip);
                    d = SignedDistance(tip, out Vector3 normal);
                    _judge.Update(t, d, true);
                    if (_judge.InContact && !_wasContact) _hasContactUV = TryUV(tip, normal, out _contactUV);
                }
                else _judge.Update(t, d, false);
                _wasContact = _judge.InContact;
            }
            _mask.Update(t);
            if (_mask.Dirty) UploadMask();
        }

        float SignedDistance(Vector3 tip, out Vector3 normal)
        {
            Vector3 c = contactCollider.ClosestPoint(tip);
            Vector3 off = tip - c;
            float dist = off.magnitude;
            if (dist > tipRadius)
            {
                normal = off / dist;
                return dist;
            }
            _tip.enabled = true;
            bool hit = Physics.ComputePenetration(_tip, tip, Quaternion.identity, contactCollider, contactCollider.transform.position, contactCollider.transform.rotation, out Vector3 dir, out float pen);
            _tip.enabled = false;
            if (!hit) { normal = dist > 1e-6f ? off / dist : Vector3.up; return dist; }
            normal = dir;
            return tipRadius - pen;
        }

        bool TryUV(Vector3 tip, Vector3 normal, out Vector2 uv)
        {
            uv = default;
            var ray = new Ray(tip + normal * 0.05f, -normal);
            if (!uvCollider.Raycast(ray, out RaycastHit hit, 0.1f)) return false;
            uv = hit.textureCoord;
            return true;
        }

        void OnJudged(DabJudgement j)
        {
            if (portal != null && portal.CurrentPaint == null) _paint = null;
            if (_paint == null) { Debug.Log($"{Tag} dab {j.kind} ignored: no paint on fingertip"); return; }
            if (!_hasContactUV) { Debug.Log($"{Tag} dab {j.kind} ignored: no contact uv"); return; }
            _mask.Stamp(_contactUV, 1f, Time.time, j.kind == DabKind.Press);
            Debug.Log($"{Tag} dab {j.kind} uv={_contactUV} depth={j.maxDepth * 1000f:0.0}mm speed={j.approachSpeed:0.00}m/s judge={(j.t - j.contactT) * 1000f:0}ms");
        }

        void UploadMask()
        {
            _mask.CopyTo(_maskBytes);
            _maskTex.LoadRawTextureData(_maskBytes);
            _maskTex.Apply(false, false);
        }
    }
}
