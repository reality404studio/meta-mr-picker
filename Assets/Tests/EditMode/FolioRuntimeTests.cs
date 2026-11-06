using System;
using MoodPicker.Catch;
using MoodPicker.Folio;
using MoodPicker.Hands;
using MoodPicker.HandStates;
using MoodPicker.Tokens;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MoodPicker.Tests
{
    public class FolioRuntimeTests
    {
        sealed class Input : IHandInput
        {
            public HandFrame Frame;
            public bool Available=true;
            public bool TryGetFrame(out HandFrame frame) { frame=Frame; return Available; }
        }
        sealed class Source : IPaintSource
        {
            public event Action<PaintRecord,Texture2D> FolioChipPicked;
            public event Action Washed;
            public void Pick() => FolioChipPicked?.Invoke(new PaintRecord { lightTone=OrionTokens.ColorPrintCobaltHex },Texture2D.whiteTexture);
            public void Wash() => Washed?.Invoke();
        }
        [Test]
        public void AdapterSubmitsExistingHandEventsAndUnsubscribesOnDispose()
        {
            var go=new GameObject("HandStateTest");
            try
            {
                var state=go.AddComponent<HandStateController>(); var source=new Source();
                using (var adapter=new FolioHandStateAdapter(source,state))
                {
                    source.Pick(); Assert.That(state.RightHand.Mode,Is.EqualTo(HandState.Paint));
                    source.Wash(); Assert.That(state.RightHand.Mode,Is.EqualTo(HandState.Bare));
                }
                source.Pick(); Assert.That(state.RightHand.Mode,Is.EqualTo(HandState.Bare));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        static void Lifecycle(FolioController controller,string method) => typeof(FolioController).GetMethod(method,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(controller,null);
        [Test]
        public void ControllerDeliversOriginalRecordAndTextureExactlyOnceThenClearWashes()
        {
            var root=new GameObject("FolioTest"); var dock=new GameObject("DockTest");
            try
            {
                var input=new Input(); var controller=root.AddComponent<FolioController>();
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Orion/Chip.mat");
                controller.Configure(input,dock.transform,material);
                var record=new PaintRecord { number=1,id="runtime",lightTone=OrionTokens.ColorPaperFaceHex };
                var tile=Texture2D.whiteTexture;
                int slot=controller.AddPaint(record,tile,Vector3.zero), picked=0,washed=0;
                controller.FolioChipPicked+=(r,t)=> { Assert.That(r,Is.SameAs(record)); Assert.That(t,Is.SameAs(tile)); picked++; };
                controller.Washed+=()=>washed++;
                float now=Time.unscaledTime+FlyToFolio.Duration;
                controller.Tick(now,0);
                Assert.That(root.transform.childCount,Is.EqualTo(OrionTokens.GeometryFolioSlots0));
                var chip=root.transform.GetChild(slot);
                Assert.That(chip.GetComponent<MeshFilter>().sharedMesh.subMeshCount,Is.EqualTo(1));
                Assert.That(chip.GetComponent<MeshRenderer>().sharedMaterial,Is.SameAs(material));
                void Finger(Transform target,float distance)
                {
                    input.Frame.right=new HandPose { tracked=true,indexTip=target.TransformPoint(new Vector3(0,ChipMeshBuilder.TileCenterY,ChipMeshBuilder.FrontZ+distance)) };
                    controller.Tick(now,0);
                }
                Finger(chip,OrionTokens.HysteresisTouchDabExitMeters+0.00001f);
                for (int i=0;i<10;i++) Finger(chip,OrionTokens.HysteresisTouchDabEnterMeters-0.00001f);
                Assert.That(picked,Is.EqualTo(1));
                Finger(chip,OrionTokens.HysteresisTouchDabExitMeters+0.00001f);
                var clear=root.transform.GetChild(controller.Slots.ClearIndex);
                Finger(clear,OrionTokens.HysteresisTouchDabExitMeters+0.00001f);
                for (int i=0;i<10;i++) Finger(clear,OrionTokens.HysteresisTouchDabEnterMeters-0.00001f);
                Assert.That(washed,Is.EqualTo(1)); Assert.That(picked,Is.EqualTo(1));
                controller.enabled=false; Lifecycle(controller,"OnDisable");
                Assert.That(root.transform.childCount,Is.Zero);
                Assert.That(controller.Slots,Is.Null);
                controller.enabled=true; Lifecycle(controller,"OnEnable");
                Assert.That(root.transform.childCount,Is.EqualTo(OrionTokens.GeometryFolioSlots0));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(dock); }
        }
        [Test]
        public void ControllerSummonsSevenCentimetersAlongPalmNormalAndReturnsToDock()
        {
            var root=new GameObject("FolioTest"); var dock=new GameObject("DockTest");
            try
            {
                dock.transform.position=new Vector3(1,2,3); dock.transform.rotation=Quaternion.Euler(0,60,0);
                var input=new Input(); var controller=root.AddComponent<FolioController>();
                controller.Configure(input,dock.transform,AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Orion/Chip.mat"));
                var docked=root.transform.position;
                Assert.That(Vector3.Distance(docked,dock.transform.TransformPoint(ChipMesh.ToUnity(FolioPlacement.DockOffset))),Is.LessThan(0.00001));
                var palm=new Vector3(0.2f,0.3f,0.4f); var normal=new Vector3(0,0.9f,0.1f).normalized;
                input.Frame.left=new HandPose { tracked=true,palm=palm,palmNormal=normal };
                controller.Tick(0,OrionTokens.HysteresisPalmUpHoldSeconds);
                Assert.That(controller.IsSummoned,Is.True);
                Assert.That(Vector3.Distance(root.transform.position,palm+normal*OrionTokens.ZoneHandFolioAboveMeters),Is.LessThan(0.01f));
                input.Available=false;
                controller.Tick(1,OrionTokens.HysteresisPalmUpReleaseSeconds);
                Assert.That(controller.IsSummoned,Is.False);
                Assert.That(Vector3.Distance(root.transform.position,docked),Is.LessThan(0.00001));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(dock); }
        }
    }
}
