using System;
using System.Collections;
using System.Collections.Generic;
using MoodPicker.Hands;
using Meta.XR;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace MoodPicker.Catch
{
    public class CatchController : MonoBehaviour
    {
        const string Tag = "[MP]";

        [SerializeField] OVRCameraRig rig;
        [SerializeField] PassthroughCameraAccess cameraAccess;
        [SerializeField] MonoBehaviour handInputSource;
        [SerializeField] MonoBehaviour pinchGateSource;
        [SerializeField] CatchSettings settings;
        [SerializeField] Material lineMaterial;
        [SerializeField] Material dotMaterial;
        [SerializeField] Material chipMaterial;

        [Header("HUD (baseline)")]
        [SerializeField] float lineWidth = 0.0025f;
        [SerializeField] float chipRiseSeconds = 0.5f;
        [SerializeField] float chipHoldSeconds = 2.0f;

        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        IHandInput _input;
        IPinchGate _gate;
        CatchPunch _punch;
        PaintStore _store;
        LineRenderer _frame, _progress, _marker;
        Transform _dot;
        Renderer _chip;
        Texture2D _chipTex;
        bool _busy;
        CatchShot _pendingShot;
        CropRect _pendingCrop;
        Pose _pendingCamPose;
        float _holdAt = -1f;
        bool _wasDragging;

        void Awake()
        {
            _input = handInputSource as IHandInput;
            _gate = pinchGateSource as IPinchGate;
            if (settings == null) settings = CatchSettings.Defaults();
            _punch = new CatchPunch(settings);
            _punch.HoldStarted += t => { _holdAt = t; Debug.Log($"{Tag} catch hold start mode={_punch.Mode} t={t:0.000}"); };
            _punch.HoldCancelled += (held, t) => Debug.Log($"{Tag} catch hold released {held:0.00}s t={t:0.000}");
            _punch.ModeChanged += (m, t) => Debug.Log($"{Tag} catch mode={m} t={t:0.000}");
            _punch.Caught += OnCaught;
            _store = new PaintStore(PaintStore.DefaultRoot);

            _frame = Line("CatchFrame", lineMaterial, true);
            _progress = Line("CatchProgress", lineMaterial, false);
            _marker = Line("CatchMode", lineMaterial, true);
            _dot = Quad("CatchRecDot", dotMaterial).transform;
            _chip = Quad("CatchChip", chipMaterial);
            _chip.gameObject.SetActive(false);
            _dot.gameObject.SetActive(false);
            Debug.Log($"{Tag} catch ready paints={_store.LoadAll().Count} root={_store.Root} hold={settings.holdSeconds}s punch={settings.punchSize * 100f:0}cm@{settings.punchDistance:0.00}m");
        }

        LineRenderer Line(string name, Material m, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = m;
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.widthMultiplier = lineWidth;
            lr.numCapVertices = 0;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.positionCount = 0;
            return lr;
        }

        Renderer Quad(string name, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        Pose HeadInTrackingSpace()
        {
            Transform space = rig.trackingSpace, eye = rig.centerEyeAnchor;
            return new Pose(space.InverseTransformPoint(eye.position), Quaternion.Inverse(space.rotation) * eye.rotation);
        }

        void Update()
        {
            if (_input == null || rig == null || !_input.TryGetFrame(out HandFrame f)) return;
            _punch.Update(PinchGate.Apply(f, _gate), HeadInTrackingSpace());
            if (_wasDragging && !_punch.Dragging) Debug.Log($"{Tag} catch drag offset=({_punch.Offset.x * 100f:0.0},{_punch.Offset.y * 100f:0.0})cm");
            _wasDragging = _punch.Dragging;
            DrawHud();
        }

        void DrawHud()
        {
            Transform space = rig.trackingSpace;
            CatchSquareView sq = _punch.Hole;
            Vector3[] c = sq.Corners();
            for (int i = 0; i < 4; i++) c[i] = space.TransformPoint(c[i]);
            _frame.positionCount = 4;
            _frame.SetPositions(c);

            var pts = new List<Vector3>();
            float p = _punch.HoldProgress;
            if (p > 0f)
            {
                Vector3[] path = { c[3], c[2], c[1], c[0], c[3] };
                float todo = p * 4f;
                pts.Add(path[0]);
                for (int i = 0; i < 4 && todo > 0f; i++)
                {
                    float seg = Mathf.Min(1f, todo);
                    pts.Add(Vector3.Lerp(path[i], path[i + 1], seg));
                    todo -= seg;
                }
            }
            _progress.widthMultiplier = lineWidth * 2f;
            _progress.positionCount = pts.Count;
            _progress.SetPositions(pts.ToArray());

            Vector3 right = space.TransformDirection(sq.Right), up = space.TransformDirection(sq.Up);
            Vector3 anchor = c[3] + up * 0.012f + right * 0.006f;
            float m = 0.004f;
            if (_punch.Mode == CatchMode.Still)
            {
                _marker.positionCount = 4;
                _marker.SetPositions(new[] { anchor - right * m - up * m, anchor + right * m - up * m, anchor + right * m + up * m, anchor - right * m + up * m });
            }
            else
            {
                const int n = 12;
                _marker.positionCount = n;
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    _marker.SetPosition(i, anchor + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * m);
                }
            }

            bool rec = _punch.Holding;
            _dot.gameObject.SetActive(rec);
            if (rec)
            {
                _dot.position = c[2] + up * 0.012f - right * 0.006f;
                _dot.rotation = space.rotation * sq.rotation;
                _dot.localScale = Vector3.one * 0.006f;
            }
        }

        void OnCaught(CatchShot shot)
        {
            if (_busy) { Debug.Log($"{Tag} catch skipped: previous readback busy"); return; }
            if (cameraAccess == null || !cameraAccess.IsPlaying) { Debug.Log($"{Tag} catch failed: camera not playing"); return; }
            Texture tex = cameraAccess.GetTexture();
            if (tex == null) { Debug.Log($"{Tag} catch failed: camera texture null"); return; }

            Pose cam = cameraAccess.GetCameraPose();
            Transform space = rig.trackingSpace;
            var vp = new Vector2[4];
            Vector3[] corners = shot.square.Corners();
            for (int i = 0; i < 4; i++) corners[i] = space.TransformPoint(corners[i]);
            Vector3[] scene = CatchProjection.ThroughSquare(space.TransformPoint(shot.head.position), corners, settings.sceneDistance);
            for (int i = 0; i < 4; i++)
            {
                Vector3 w = scene[i];
                if ((Quaternion.Inverse(cam.rotation) * (w - cam.position)).z <= 0f) { Debug.Log($"{Tag} catch failed: corner behind camera"); return; }
                Vector2 v = cameraAccess.WorldToViewportPoint(w, cam);
                vp[i] = settings.flipCameraY ? new Vector2(v.x, 1f - v.y) : v;
            }
            Vector2Int res = cameraAccess.CurrentResolution;
            if (!CropMath.TrySquare(vp, res, settings.overlapFrac, settings.minCropPixels, out CropRect crop))
            {
                Debug.Log($"{Tag} catch failed: square outside camera image vp0={vp[0]} vp2={vp[2]} res={res}");
                return;
            }

            _busy = true;
            _pendingShot = shot;
            _pendingCrop = crop;
            _pendingCamPose = cam;
            AsyncGPUReadback.Request(tex, 0, crop.big.x, crop.big.width, crop.big.y, crop.big.height, 0, 1, TextureFormat.RGBA32, OnReadback);
            Debug.Log($"{Tag} catch readback rect={crop.big} side={crop.side} requested={crop.requestedSide}{(crop.Shrunk ? " SHRUNK" : "")} overlap={crop.overlap} res={res} " +
                      $"square={shot.square.size * 100f:0.0}cm dist={CatchRay.DistanceAlong(shot.head, shot.square.center):0.000}m scene={settings.sceneDistance:0.0}m f={cameraAccess.Intrinsics.FocalLength} lens={cameraAccess.Intrinsics.LensOffset.position}");
        }

        void OnReadback(AsyncGPUReadbackRequest req)
        {
            try
            {
                if (req.hasError) { Debug.Log($"{Tag} catch failed: readback error"); return; }
                NativeArray<Color32> data = req.GetData<Color32>();
                CropRect c = _pendingCrop;
                if (data.Length != c.BigSize * c.BigSize) { Debug.Log($"{Tag} catch failed: readback size {data.Length}"); return; }
                Color32[] big = data.ToArray();
                if (settings.flipCameraY) big = CropMath.FlipRows(big, c.BigSize, c.BigSize);
                for (int i = 0; i < big.Length; i++) big[i].a = 255;

                Color32[] still = CropMath.StillFromBig(big, c);
                Color32[] tile = SeamlessTile.FromBig(big, c);
                CelResult cel = CelExtract.Run(still, c.side, c.side, settings);

                Texture2D stillTex = ToTexture(still, c.side);
                Texture2D tileTex = ToTexture(tile, c.side);
                var draft = new PaintDraft
                {
                    mode = CatchMode.Still,
                    capturedAt = DateTime.Now,
                    gazeSeconds = _pendingShot.holdSeconds,
                    cel = cel,
                    lightDirWorld = (_pendingCamPose.rotation * cel.lightDirView).normalized,
                    size = c.side,
                    stillPng = stillTex.EncodeToPNG(),
                    tilePng = tileTex.EncodeToPNG(),
                    build = Reality404.BuildGate.BuildIdLog.Id,
                };
                Destroy(tileTex);
                SaveResult res = _store.Save(draft);
                if (!res.ok)
                {
                    Destroy(stillTex);
                    Debug.Log($"{Tag} catch save failed: {res.error}");
                    return;
                }
                Debug.Log($"{Tag} catch saved No.{res.record.number:000} id={res.record.id} mode={_pendingShot.mode} size={c.side}px " +
                          $"light={res.record.lightTone} mid={res.record.midTone} shadow={res.record.shadowTone} lightDir=({res.record.lightDir[0]},{res.record.lightDir[1]},{res.record.lightDir[2]})" +
                          (_pendingShot.mode == CatchMode.Loop ? " loopFrames=pending(loop card)" : ""));
                ShowChip(stillTex, _pendingShot.square);
            }
            catch (Exception e) { Debug.LogException(e); }
            finally { _busy = false; }
        }

        static Texture2D ToTexture(Color32[] px, int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            t.SetPixels32(px);
            t.Apply(false, false);
            return t;
        }

        void ShowChip(Texture2D tex, CatchSquareView sq)
        {
            if (_chipTex != null) Destroy(_chipTex);
            _chipTex = tex;
            _chip.material.SetTexture(BaseMap, tex);
            StopAllCoroutines();
            StartCoroutine(RiseChip(sq));
        }

        IEnumerator RiseChip(CatchSquareView sq)
        {
            Transform space = rig.trackingSpace, t = _chip.transform;
            Vector3 from = space.TransformPoint(sq.center), up = space.TransformDirection(sq.Up);
            Vector3 to = from + up * sq.size * 0.6f;
            t.rotation = space.rotation * sq.rotation;
            t.localScale = Vector3.one * sq.size * 0.8f;
            _chip.gameObject.SetActive(true);
            for (float e = 0f; e < chipRiseSeconds; e += Time.deltaTime)
            {
                float k = 1f - Mathf.Pow(1f - e / chipRiseSeconds, 3f);
                t.position = Vector3.Lerp(from, to, k);
                yield return null;
            }
            t.position = to;
            yield return new WaitForSeconds(chipHoldSeconds);
            _chip.gameObject.SetActive(false);
        }
    }
}
