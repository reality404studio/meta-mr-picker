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

        [Header("Placement (baseline)")]
        [SerializeField] float placeDistance = 0.45f;
        [SerializeField] float placeBelowEye = 0.35f;
        [SerializeField] float tipRadius = 0.005f;

        IHandInput _input;
        DabBucket<PaintRecord> _bucket;
        Texture2D _maskTex;
        readonly byte[] _maskByte = new byte[1];
        Material _mat;
        SphereCollider _tip;
        PaintRecord _paint;
        Texture2D _paintTile;
        float _shownFill = -1f;

        void Awake()
        {
            _input = handInputSource as IHandInput;
            if (settings == null) settings = DabSettings.Defaults();
            _bucket = new DabBucket<PaintRecord>(settings);
            _bucket.Filled += OnFilled;
            _maskTex = new Texture2D(1, 1, TextureFormat.R8, false, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            SetFill(0f);
            _mat = target.material;
            _mat.SetTexture(PaintMaskId, _maskTex);
            var tipGo = new GameObject("DabTipProbe");
            tipGo.transform.SetParent(transform, false);
            _tip = tipGo.AddComponent<SphereCollider>();
            _tip.radius = tipRadius;
            _tip.isTrigger = true;
            _tip.enabled = false;
            if (portal != null) portal.PaintPicked += OnPaintPicked;
            Debug.Log($"{Tag} dab ready bucket fill={settings.fillSeconds:0.00}s tris={MeshTris()} paint=none");
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
            _paintTile = tile;
            Debug.Log($"{Tag} dab fingertip paint No.{r.number:000} light={r.lightTone} shadow={r.shadowTone}");
        }

        PaintRecord Fingertip() => portal != null && portal.CurrentPaint == null ? null : _paint;

        void OnFilled(PaintRecord r, float t)
        {
            CelPaint.Apply(_mat, r, _paintTile);
            SetFill(0f);
            Debug.Log($"{Tag} dab bucket fill No.{r.number:000} t={t:0.000}");
        }

        void Update()
        {
            float t = Time.time;
            if (_input != null && _input.TryGetFrame(out HandFrame f))
            {
                bool tracked = f.right.tracked;
                float d = tracked ? SignedDistance(trackingSpace.TransformPoint(f.right.indexTip), out _) : 1f;
                bool wasTouching = _bucket.Touching;
                _bucket.Update(t, d, tracked, Fingertip());
                if (_bucket.Touching && !wasTouching && Fingertip() == null) Debug.Log($"{Tag} dab touch ignored: no paint on fingertip");
            }
            float fill = _bucket.Fill(t);
            if (!Mathf.Approximately(fill, _shownFill)) SetFill(fill);
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

        void SetFill(float v)
        {
            _shownFill = v;
            _maskByte[0] = (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
            _maskTex.LoadRawTextureData(_maskByte);
            _maskTex.Apply(false, false);
        }
    }
}
