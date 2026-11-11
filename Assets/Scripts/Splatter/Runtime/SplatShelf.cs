using System.Collections.Generic;
using MoodPicker.Splatter.Sim;
using UnityEngine;

namespace MoodPicker.Splatter
{
    public sealed class SplatShelf : MonoBehaviour
    {
        const string Tag = "[MP]";

        public enum BlobState
        {
            Resting = 0,
            Held = 1,
            Returning = 2,
            Absorbed = 3
        }

        sealed class Blob
        {
            public GameObject go;
            public SplatLetterRenderer body;
            public SplatSurfaceMaterial material;
            public TextMesh label;
            public BlobState state;
            public int hand = -1;
            public float since;
            public Vector3 from;
            public bool preset;
        }

        [SerializeField] SplatWordController shape;
        [SerializeField] SplatTuning tuning;
        [SerializeField] Transform head;
        [SerializeField] SplatPigmentSlotAsset[] presets;
        [SerializeField] Material splatMaterial;
        [SerializeField] Mesh blobMesh;
        [SerializeField] Font labelFont;
        [SerializeField] int blobSplats = 1800;
        [SerializeField] float blobRadius = 0.026f;
        [SerializeField] float textureSize = 0.18f;
        [SerializeField] float spacing = 0.075f;
        [SerializeField] Vector3 offsetFromShape = new Vector3(0f, -0.17f, -0.07f);
        [SerializeField] float labelDrop = 0.045f;
        [SerializeField] float labelCharacterSize = 0.0011f;
        [SerializeField] float returnSeconds = 0.35f;
        [SerializeField] int maxBlobs = 8;

        readonly List<Blob> _blobs = new List<Blob>();
        readonly SplatCollider[] _masked = new SplatCollider[SplatWordController.ColliderSlots];
        bool _placed;
        bool _seeded;
        float _now;

        public int Count => _blobs.Count;
        public BlobState StateOf(int index) => _blobs[index].state;
        public Vector3 SlotWorld(int index) => transform.TransformPoint(new Vector3(SplatShelfLayout.SlotX(index, _blobs.Count, spacing), 0f, 0f));

        void Start()
        {
            SeedPresets();
        }

        public void SeedPresets()
        {
            if (_seeded) return;
            _seeded = true;
            if (presets == null) return;
            for (int i = 0; i < presets.Length; i++)
            {
                SplatSurfaceMaterial m = SplatSurfaceMaterial.FromAsset(presets[i]);
                if (m != null) Add(m, true);
            }
        }

        public void Place(Vector3 shapePosition, Vector3 forward)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            transform.SetPositionAndRotation(
                shapePosition + right * offsetFromShape.x + Vector3.up * offsetFromShape.y + forward * offsetFromShape.z,
                Quaternion.LookRotation(forward, Vector3.up));
            _placed = true;
        }

        public void Add(SplatSurfaceMaterial material, bool preset = false)
        {
            if (material == null || splatMaterial == null || blobMesh == null) return;
            if (_blobs.Count >= maxBlobs)
            {
                int victim = _blobs.FindIndex(b => !b.preset && b.state != BlobState.Held);
                if (victim < 0) return;
                Remove(victim);
            }
            var go = new GameObject("Blob " + material.label);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var body = go.AddComponent<SplatLetterRenderer>();
            body.RebuildFromMesh(blobMesh, splatMaterial, blobSplats, textureSize, false, 4242 + _blobs.Count * 17);
            body.Mode = SplatShaderMode.Raw;

            var blob = new Blob { go = go, body = body, material = material, preset = preset, state = BlobState.Absorbed, since = -100f };
            blob.label = MakeLabel(material.label);
            _blobs.Add(blob);
            SplatSimRunner sim = body.EnsureSim(ClusterSize);
            sim?.Hide();
            Debug.Log($"{Tag} shelf add {material.label} count={_blobs.Count}");
        }

        void Remove(int index)
        {
            Blob b = _blobs[index];
            if (b.label != null) Destroy(b.label.gameObject);
            Destroy(b.go);
            if (shape == null || shape.CurrentMaterial != b.material) b.material.Release();
            _blobs.RemoveAt(index);
        }

        TextMesh MakeLabel(string text)
        {
            if (labelFont == null) return null;
            var go = new GameObject("Label " + text);
            go.transform.SetParent(transform, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = labelFont;
            tm.fontSize = 60;
            tm.characterSize = labelCharacterSize;
            tm.anchor = TextAnchor.UpperCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.94f, 0.93f, 0.90f, 1f);
            tm.text = text;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = labelFont.material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return tm;
        }

        int ClusterSize => tuning != null ? Mathf.Max(8, tuning.particlesPerCluster) : 300;
        float GrabReach => tuning != null ? tuning.grabReach : 0.035f;

        public bool TryGrab(int hand, Vector3 pinchWorld)
        {
            int best = -1;
            float bestDist = blobRadius + GrabReach;
            for (int i = 0; i < _blobs.Count; i++)
            {
                Blob b = _blobs[i];
                if (b.state != BlobState.Resting && b.state != BlobState.Returning) continue;
                float d = Vector3.Distance(b.go.transform.position, pinchWorld);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }
            if (best < 0) return false;
            Blob g = _blobs[best];
            g.state = BlobState.Held;
            g.hand = hand;
            g.since = _now;
            g.body.EnsureSim(ClusterSize);
            Debug.Log($"{Tag} shelf grab {g.material.label} hand={hand}");
            return true;
        }

        Blob HeldBy(int hand)
        {
            for (int i = 0; i < _blobs.Count; i++)
                if (_blobs[i].state == BlobState.Held && _blobs[i].hand == hand) return _blobs[i];
            return null;
        }

        public bool UpdateHeld(int hand, Vector3 pinchWorld, Vector3 palmWorld)
        {
            Blob b = HeldBy(hand);
            if (b == null) return false;
            Vector3 outward = pinchWorld - palmWorld;
            Vector3 center = pinchWorld + (outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.zero) * (blobRadius * 0.55f);
            b.go.transform.position = center;
            if (shape == null) return true;
            float contact = (tuning != null ? tuning.contactFraction : 0.8f) * blobRadius;
            if (shape.SurfaceDistance(center, out Vector3 touch) > contact) return true;
            shape.ApplyMaterial(b.material, touch);
            Absorb(b);
            return false;
        }

        void Absorb(Blob b)
        {
            b.state = BlobState.Absorbed;
            b.hand = -1;
            b.since = _now;
            b.body.EnsureSim(ClusterSize)?.Hide();
            Debug.Log($"{Tag} shelf absorbed {b.material.label}");
        }

        public void Release(int hand)
        {
            Blob b = HeldBy(hand);
            if (b == null) return;
            b.state = BlobState.Returning;
            b.hand = -1;
            b.since = _now;
            b.from = b.go.transform.position;
        }

        void Update()
        {
            Tick(Time.time, Time.deltaTime);
        }

        public void Tick(float now, float dt)
        {
            _now = now;
            if (!_placed || _blobs.Count == 0) return;
            float regrow = tuning != null ? tuning.regrowSeconds : 0.9f;
            float floorY = shape != null ? shape.FloorY : transform.position.y - 0.4f;
            SplatSimParams p = tuning != null ? tuning.ToSimParams(floorY) : SplatSimParams.WaterDroplet;
            SplatLook look = tuning != null ? tuning.ToLook() : SplatLook.Default;
            SplatCollider[] colliders = shape != null ? shape.Colliders : null;
            int colliderCount = shape != null ? shape.ColliderCount : 0;

            for (int i = 0; i < _blobs.Count; i++)
            {
                Blob b = _blobs[i];
                Vector3 slot = SlotWorld(i);
                switch (b.state)
                {
                    case BlobState.Resting:
                        b.go.transform.position = slot;
                        break;
                    case BlobState.Returning:
                    {
                        float t = Mathf.Clamp01((now - b.since) / Mathf.Max(0.05f, returnSeconds));
                        float e = 1f - (1f - t) * (1f - t) * (1f - t);
                        b.go.transform.position = Vector3.Lerp(b.from, slot, e);
                        if (t >= 1f) b.state = BlobState.Resting;
                        break;
                    }
                    case BlobState.Absorbed:
                        b.go.transform.position = slot;
                        if (now - b.since >= regrow)
                        {
                            SplatSimRunner regrowSim = b.body.EnsureSim(ClusterSize);
                            regrowSim?.BeginSummon(b.go.transform.localToWorldMatrix, slot, 0.35f, 0.6f);
                            b.state = BlobState.Resting;
                        }
                        break;
                }
                b.go.transform.rotation = transform.rotation;

                SplatSimRunner sim = b.body.EnsureSim(ClusterSize);
                if (sim != null)
                {
                    SplatCollider[] use = colliders;
                    if (b.state == BlobState.Held && colliders != null)
                    {
                        System.Array.Copy(colliders, _masked, Mathf.Min(colliders.Length, _masked.Length));
                        int start = b.hand * SplatHandPoints.Count;
                        for (int k = 0; k < SplatHandPoints.Count && start + k < _masked.Length; k++) _masked[start + k].strength = 0f;
                        use = _masked;
                    }
                    SplatSimParams bp = p;
                    if (b.material.hasFeel) SplatFeelMap.Apply(b.material.feel, ref bp);
                    sim.Step(b.go.transform.localToWorldMatrix, dt, bp, use, colliderCount, slot, Vector3.zero);
                }

                if (shape != null)
                {
                    SplatSurfaceState state = shape.SurfaceStateFor(b.material, now);
                    b.body.Mode = (SplatShaderMode)b.material.ResolveMode(tuning != null ? tuning.shaderMode : -1);
                    SplatLook blobLook = look;
                    if (b.material.hasFeel)
                    {
                        SplatFeelLook fl = SplatFeelMap.Look(b.material.feel);
                        blobLook.grain = fl.grain;
                        blobLook.stretchMax = fl.stretchMax;
                        blobLook.stretchSeconds = fl.stretchSeconds;
                    }
                    b.body.SetLook(blobLook);
                    b.body.ApplyState(state);
                }

                if (b.label != null)
                {
                    Transform lt = b.label.transform;
                    lt.position = slot + Vector3.down * labelDrop;
                    Vector3 view = head != null ? Vector3.ProjectOnPlane(lt.position - head.position, Vector3.up) : transform.forward;
                    if (view.sqrMagnitude > 1e-6f) lt.rotation = Quaternion.LookRotation(view.normalized, Vector3.up);
                }
            }
        }

        void OnDestroy()
        {
            for (int i = 0; i < _blobs.Count; i++) _blobs[i].material.Release();
        }
    }
}
