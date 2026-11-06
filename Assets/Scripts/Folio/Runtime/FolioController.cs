using System;
using MoodPicker.Catch;
using MoodPicker.Hands;
using MoodPicker.HandStates;
using MoodPicker.Tokens;
using UnityEngine;

namespace MoodPicker.Folio
{
    public sealed class FolioController : MonoBehaviour, IPaintSource
    {
        [SerializeField] MonoBehaviour handInput;
        [SerializeField] Transform dockFrame;
        [SerializeField] Material chipMaterial;
        [SerializeField] HandStateController handState;
        [SerializeField] int slotCount=(int)OrionTokens.GeometryFolioSlots0;
        IHandInput _input;
        FolioLayout _layout;
        FolioSlots _slots;
        FolioSummon _summon;
        FolioInteraction _interaction;
        FolioHandStateAdapter _adapter;
        Mesh _mesh;
        MeshRenderer[] _chips;
        Texture2D[] _tiles;
        FlyToFolio[] _flights;
        float[] _started;
        MaterialPropertyBlock _block;
        Vector3 _lastPalm, _lastNormal;
        public event Action<PaintRecord,Texture2D> FolioChipPicked;
        public event Action Washed;
        public FolioSlots Slots => _slots;
        public bool IsSummoned => _summon!=null && _summon.IsSummoned;

        public void Configure(IHandInput input,Transform dock,Material material,HandStateController target=null,int count=(int)OrionTokens.GeometryFolioSlots0)
        {
            Release();
            _input=input ?? throw new ArgumentNullException(nameof(input));
            dockFrame=dock; chipMaterial=material; handState=target; slotCount=count;
            if (isActiveAndEnabled) Initialize();
        }
        void OnEnable() { if (_input==null) _input=handInput as IHandInput; Initialize(); }
        void Initialize()
        {
            if (_slots!=null || _input==null || dockFrame==null || chipMaterial==null) return;
            _layout=new FolioLayout(slotCount); _slots=new FolioSlots(slotCount); _summon=new FolioSummon();
            _interaction=new FolioInteraction(_slots);
            _interaction.Picked+=OnPicked; _interaction.Washed+=OnWashed;
            _block=new MaterialPropertyBlock();
            _mesh=ChipMesh.Create(); _chips=new MeshRenderer[slotCount]; _tiles=new Texture2D[slotCount];
            _flights=new FlyToFolio[slotCount]; _started=new float[slotCount];
            for (int i=0;i<slotCount;i++)
            {
                var go=new GameObject(i==_layout.ClearIndex ? "CLEAR" : "Folio Chip "+(i+1));
                go.transform.SetParent(transform,false);
                go.AddComponent<MeshFilter>().sharedMesh=_mesh;
                _chips[i]=go.AddComponent<MeshRenderer>(); _chips[i].sharedMaterial=chipMaterial;
                _chips[i].shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                _chips[i].receiveShadows=false;
                PlaceSlot(i);
                ChipMesh.Apply(_chips[i],_block,null,i+1,false,i==_layout.ClearIndex);
                go.SetActive(i==_layout.ClearIndex);
            }
            if (handState!=null) _adapter=new FolioHandStateAdapter(this,handState);
            UpdatePlacement();
        }
        public int AddPaint(PaintRecord record,Texture2D tile,Vector3 worldOrigin)
        {
            if (_slots==null) throw new InvalidOperationException("Configure active FolioController first");
            if (record==null) throw new ArgumentNullException(nameof(record));
            if (tile==null) throw new ArgumentNullException(nameof(tile));
            var flight=new FlyToFolio(_slots,record,ChipMesh.FromUnity(worldOrigin));
            int i=flight.Slot; _flights[i]=flight; _started[i]=Time.unscaledTime; _tiles[i]=tile;
            ChipMesh.Apply(_chips[i],_block,tile,record.number,record.HasLoop);
            _chips[i].gameObject.SetActive(true); _chips[i].transform.position=worldOrigin;
            return i;
        }
        void Update() => Tick(Time.unscaledTime,Time.unscaledDeltaTime);
        public void Tick(float now,float deltaSeconds)
        {
            if (_slots==null) return;
            bool available=_input.TryGetFrame(out var frame);
            bool left=available && frame.left.tracked && frame.left.palmNormal.sqrMagnitude>0;
            if (left) { _lastPalm=frame.left.palm; _lastNormal=frame.left.palmNormal.normalized; }
            _summon.Step(left,left ? Vector3.Dot(_lastNormal,Vector3.up) : 0,deltaSeconds);
            UpdatePlacement();
            for (int i=0;i<_slots.ClearIndex;i++)
            {
                var flight=_flights[i];
                if (flight==null) continue;
                float elapsed=now-_started[i];
                var target=transform.TransformPoint(ChipMesh.ToUnity(_layout[i].Position));
                _chips[i].transform.position=ChipMesh.ToUnity(flight.Position(elapsed,ChipMesh.FromUnity(target)));
                if (flight.HasArrived(elapsed)) { _flights[i]=null; PlaceSlot(i); }
            }
            if (!available || !frame.right.tracked) { _interaction.Touch.LoseTracking(); return; }
            Touch(frame.right.indexTip);
        }
        void UpdatePlacement()
        {
            var forward=Vector3.ProjectOnPlane(dockFrame.forward,Vector3.up).normalized;
            if (forward.sqrMagnitude==0) forward=Vector3.forward;
            var yaw=Quaternion.LookRotation(forward,Vector3.up);
            transform.position=IsSummoned ? ChipMesh.ToUnity(FolioPlacement.AbovePalm(ChipMesh.FromUnity(_lastPalm),ChipMesh.FromUnity(_lastNormal))) :
                dockFrame.position+yaw*ChipMesh.ToUnity(FolioPlacement.DockOffset);
            transform.rotation=Quaternion.LookRotation(-forward,Vector3.up);
        }
        void PlaceSlot(int i)
        {
            _chips[i].transform.localPosition=ChipMesh.ToUnity(_layout[i].Position);
            _chips[i].transform.localRotation=Quaternion.Euler(0,_layout[i].YawDegrees,0);
        }
        void Touch(Vector3 tip)
        {
            int selected=_interaction.Touch.LatchedSlot;
            if (selected<0)
            {
                float closest=float.PositiveInfinity;
                for (int i=0;i<slotCount;i++)
                {
                    if ((i!=_slots.ClearIndex && _slots[i]==null) || _flights[i]!=null) continue;
                    var p=_chips[i].transform.InverseTransformPoint(tip);
                    if (Mathf.Abs(p.x)>_interaction.Touch.Width/2 || Mathf.Abs(p.y-ChipMeshBuilder.TileCenterY)>_interaction.Touch.Height/2) continue;
                    float distance=Mathf.Abs(p.z-ChipMeshBuilder.FrontZ);
                    if (distance>=closest) continue;
                    closest=distance; selected=i;
                }
            }
            if (selected<0) return;
            var point=_chips[selected].transform.InverseTransformPoint(tip);
            _interaction.Step(selected,point.x,point.y-ChipMeshBuilder.TileCenterY,point.z-ChipMeshBuilder.FrontZ);
        }
        void OnPicked(PaintRecord record)
        {
            int slot=_interaction.Touch.LatchedSlot;
            FolioChipPicked?.Invoke(record,_tiles[slot]);
        }
        void OnWashed() => Washed?.Invoke();
        void OnDisable() => Release();
        void Release()
        {
            _adapter?.Dispose(); _adapter=null;
            if (_interaction!=null) { _interaction.Picked-=OnPicked; _interaction.Washed-=OnWashed; }
            if (_chips!=null) foreach (var chip in _chips) if (chip!=null) DestroyOwned(chip.gameObject);
            if (_mesh!=null) DestroyOwned(_mesh);
            _slots=null; _chips=null; _mesh=null; _interaction=null; _summon=null;
        }
        static void DestroyOwned(UnityEngine.Object item)
        {
            if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
        }
    }
}
