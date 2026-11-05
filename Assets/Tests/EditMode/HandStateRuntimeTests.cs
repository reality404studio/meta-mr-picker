using System;
using System.Reflection;
using MoodPicker.Catch;
using MoodPicker.HandStates;
using MoodPicker.Portal;
using NUnit.Framework;
using UnityEngine;

namespace MoodPicker.Tests
{
    public class HandStateRuntimeTests
    {
        GameObject _handObject, _portalObject;
        HandStateController _hand;
        PortalController _portal;

        [SetUp]
        public void SetUp()
        {
            _portalObject = new GameObject("HandStateTestPortal");
            _portalObject.SetActive(false);
            _portal = _portalObject.AddComponent<PortalController>();
            _handObject = new GameObject("HandStateTestHand");
            _hand = _handObject.AddComponent<HandStateController>();
            _hand.BindPortal(_portal);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_handObject);
            UnityEngine.Object.DestroyImmediate(_portalObject);
        }

        static Delegate Subscribers(PortalController portal, string name) =>
            (Delegate)typeof(PortalController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(portal);

        static void Lifecycle(HandStateController hand, string method) =>
            typeof(HandStateController).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hand, null);

        void Pick(string hex) => ((Action<PaintRecord, Texture2D>)Subscribers(_portal, "PaintPicked"))?
            .Invoke(new PaintRecord { lightTone = hex }, null);

        void Wash() => ((Action)Subscribers(_portal, "Washed"))?.Invoke();

        [Test]
        public void PortalLightToneAndWashPublishRightHandStateAndColor()
        {
            int changes = 0;
            HandState observed = HandState.Bare;
            Color color = default;
            _hand.StateChanged += (state, tint) => { changes++; observed = state; color = tint; };
            Pick("#123456");
            Assert.That(observed, Is.EqualTo(HandState.Paint));
            Assert.That(color, Is.EqualTo(new Color(18f / 255f, 52f / 255f, 86f / 255f)));
            Assert.That(_hand.IsAllowed(HandAction.GrabLetter), Is.False);
            Assert.That(_hand.Submit(new HandEvent(HandEventKind.GrabLetter)).Ignored, Is.True);
            Assert.That(changes, Is.EqualTo(1));
            Wash();
            Assert.That(observed, Is.EqualTo(HandState.Bare));
            Assert.That(color, Is.EqualTo(_hand.CurrentColor));
            Assert.That(changes, Is.EqualTo(2));
            Assert.That(_hand.LeftHand, Is.EqualTo(HandState.Bare));
        }

        [Test]
        public void DisableReenableRebindAndDestroyManageSubscriptions()
        {
            _hand.BindPortal(_portal);
            Assert.That(Subscribers(_portal, "PaintPicked").GetInvocationList().Length, Is.EqualTo(1));
            Assert.That(Subscribers(_portal, "Washed").GetInvocationList().Length, Is.EqualTo(1));
            _hand.enabled = false;
            Lifecycle(_hand, "OnDisable");
            Assert.That(Subscribers(_portal, "PaintPicked"), Is.Null);
            Assert.That(Subscribers(_portal, "Washed"), Is.Null);
            Assert.That(_hand.Submit(new HandEvent(HandEventKind.GrabStamp)).Ignored, Is.True);
            _hand.enabled = true;
            Lifecycle(_hand, "OnEnable");
            Pick("#123456");
            Assert.That(_hand.RightHand.Mode, Is.EqualTo(HandState.Paint));
            _hand.BindPortal(null);
            Assert.That(Subscribers(_portal, "PaintPicked"), Is.Null);
            Assert.That(Subscribers(_portal, "Washed"), Is.Null);
            _hand.BindPortal(_portal);
            Wash();
            Lifecycle(_hand, "OnDisable");
            UnityEngine.Object.DestroyImmediate(_handObject);
            Assert.That(Subscribers(_portal, "PaintPicked"), Is.Null);
            Assert.That(Subscribers(_portal, "Washed"), Is.Null);
        }

        [Test]
        public void SameModeColorChangeNotifiesButIgnoredAndNoChangeEventsDoNot()
        {
            int changes = 0;
            _hand.StateChanged += (_, __) => changes++;
            _hand.Submit(new HandEvent(HandEventKind.GrabDropper));
            var first = new HandColor(0.1f, 0.2f, 0.3f);
            var last = new HandColor(0.5f, 0.6f, 0.7f);
            _hand.Submit(new HandEvent(HandEventKind.DropperPicked, first));
            _hand.Submit(new HandEvent(HandEventKind.DropperPicked, last));
            Assert.That(changes, Is.EqualTo(3));
            Assert.That(_hand.CurrentColor, Is.EqualTo(new Color(last.R, last.G, last.B, last.A)));
            _hand.Submit(new HandEvent(HandEventKind.DropperPicked, last));
            Pick("#123456");
            Wash();
            Assert.That(changes, Is.EqualTo(3));
            Assert.That(_hand.LeftHand, Is.EqualTo(HandState.Bare));
        }

        [Test]
        public void InvalidPaintRecordIsIgnored()
        {
            Pick("not-a-color");
            ((Action<PaintRecord, Texture2D>)Subscribers(_portal, "PaintPicked")).Invoke(null, null);
            Assert.That(_hand.RightHand.Mode, Is.EqualTo(HandState.Bare));
        }
    }
}
