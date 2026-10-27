using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PschLib.Unity.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace PschLib.Tests.UI
{
    public sealed class CallbackPopup : PopupBase
    {
        public Action Initializing;
        public Action Opening;
        public Action Closing;
        public Action Refreshing;
        public Action Enabling;
        public Action Disabling;

        protected override void OnInitialize() => Initializing?.Invoke();
        protected override void OnOpen() => Opening?.Invoke();
        protected override void OnClose() => Closing?.Invoke();
        protected override void OnRefresh() => Refreshing?.Invoke();
        private void OnEnable() => Enabling?.Invoke();
        private void OnDisable() => Disabling?.Invoke();
    }

    public sealed class PopupHostTests
    {
        private GameObject root;
        private PopupHost host;
        private CallbackPopup popup;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Popup test host");
            host = root.AddComponent<PopupHost>();
            var child = new GameObject("Popup");
            child.transform.SetParent(root.transform, false);
            popup = child.AddComponent<CallbackPopup>();
            Assert.That(host.Register(popup), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            if (popup != null)
            {
                popup.Enabling = null;
                popup.Disabling = null;
            }
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void ExpectBlockedOperation()
        {
            LogAssert.Expect(LogType.Warning, new Regex("PopupHost operations cannot be called"));
        }

        [Test]
        public void InitializeReopensSelf_DoesNotDuplicateStack()
        {
            popup.Initializing = () =>
            {
                ExpectBlockedOperation();
                Assert.That(host.Open<CallbackPopup>(), Is.Null);
            };

            Assert.That(host.Open<CallbackPopup>(), Is.SameAs(popup));
            Assert.That(host.Count, Is.EqualTo(1));
            Assert.That(host.CloseTop(), Is.True);
            Assert.That(host.Count, Is.Zero);
            Assert.That(popup.IsOpen, Is.False);
            Assert.That(popup.IsVisible, Is.False);
        }

        [Test]
        public void OpenCallbackUnregistersSelf_RegistrationRemainsConsistent()
        {
            popup.Opening = () =>
            {
                ExpectBlockedOperation();
                Assert.That(host.Unregister(popup), Is.False);
            };
            host.Open<CallbackPopup>();

            Assert.That(host.TryGet(out CallbackPopup registered), Is.True);
            Assert.That(registered, Is.SameAs(popup));
            Assert.That(host.Current, Is.SameAs(popup));
        }

        [Test]
        public void VisibilityCallbackReopensPopup_DoesNotMutateStack()
        {
            host.Open<CallbackPopup>();
            popup.Disabling = () =>
            {
                ExpectBlockedOperation();
                Assert.That(host.Open<CallbackPopup>(), Is.Null);
            };

            Assert.That(host.CloseTop(), Is.True);
            Assert.That(host.Count, Is.Zero);
            Assert.That(popup.IsOpen, Is.False);
            Assert.That(popup.IsVisible, Is.False);
        }

        [Test]
        public void RefreshCallbackClosesPopup_IsRejectedUntilRefreshFinishes()
        {
            host.Open<CallbackPopup>();
            popup.Refreshing = () =>
            {
                ExpectBlockedOperation();
                Assert.That(host.CloseTop(), Is.False);
            };

            Assert.That(host.RefreshTop(), Is.True);
            Assert.That(host.Count, Is.EqualTo(1));
            Assert.That(host.CloseTop(), Is.True);
        }

        [Test]
        public void OpenThrows_ReleasesOperationGuardAndCanRetry()
        {
            popup.Opening = () => throw new InvalidOperationException("Expected open failure");
            LogAssert.Expect(LogType.Exception, new Regex("Expected open failure"));
            Assert.That(host.Open<CallbackPopup>(), Is.Null);
            Assert.That(host.Count, Is.Zero);

            popup.Opening = null;
            Assert.That(host.Open<CallbackPopup>(), Is.SameAs(popup));
            Assert.That(host.Unregister(popup), Is.True);
            Assert.That(host.Count, Is.Zero);
        }

        [Test]
        public void DestroyedTop_RestoresPreviousPopupAndBackClosesIt()
        {
            host.Open<CallbackPopup>();
            var secondObject = new GameObject("Second popup");
            secondObject.transform.SetParent(root.transform, false);
            var second = secondObject.AddComponent<SecondPopup>();
            host.Register(second);
            host.Open<SecondPopup>();
            Assert.That(popup.IsVisible, Is.False);

            UnityEngine.Object.DestroyImmediate(secondObject);

            Assert.That(host.Current, Is.SameAs(popup));
            Assert.That(host.Count, Is.EqualTo(1));
            Assert.That(popup.IsVisible, Is.True);
            Assert.That(host.HandleBack(), Is.True);
            Assert.That(host.Count, Is.Zero);
        }

        [Test]
        public void DestroyedRegistration_CanRegisterReplacementOfSameType()
        {
            UnityEngine.Object.DestroyImmediate(popup.gameObject);
            Assert.That(host.TryGet(out CallbackPopup missing), Is.False);
            Assert.That(missing, Is.Null);

            var replacementObject = new GameObject("Replacement popup");
            replacementObject.transform.SetParent(root.transform, false);
            popup = replacementObject.AddComponent<CallbackPopup>();
            Assert.That(host.Register(popup), Is.True);
            Assert.That(host.Open<CallbackPopup>(), Is.SameAs(popup));
        }

        [Test]
        public void DestroyedHiddenPopup_DoesNotPreventClosingSurvivingTop()
        {
            host.Open<CallbackPopup>();
            var secondObject = new GameObject("Second popup");
            secondObject.transform.SetParent(root.transform, false);
            var second = secondObject.AddComponent<SecondPopup>();
            host.Register(second);
            host.Open<SecondPopup>();
            UnityEngine.Object.DestroyImmediate(popup.gameObject);

            Assert.That(host.Count, Is.EqualTo(1));
            Assert.That(host.Current, Is.SameAs(second));
            Assert.That(host.CloseTop(), Is.True);
            Assert.That(host.Count, Is.Zero);
        }

        [Test]
        public void InitializeDestroysSelf_DoesNotCallOpenOrLeaveRegistration()
        {
            var opened = false;
            popup.Initializing = () => UnityEngine.Object.DestroyImmediate(popup.gameObject);
            popup.Opening = () => opened = true;
            Assert.That(host.Open<CallbackPopup>(), Is.Null);
            Assert.That(opened, Is.False);
            Assert.That(host.Count, Is.Zero);
            Assert.That(host.TryGet(out CallbackPopup missing), Is.False);
        }

        [Test]
        public void CloseDestroysSelf_StillClearsStack()
        {
            popup.Closing = () => UnityEngine.Object.DestroyImmediate(popup.gameObject);
            host.Open<CallbackPopup>();
            Assert.That(host.CloseTop(), Is.True);
            Assert.That(host.Count, Is.Zero);
            Assert.That(host.TryGet(out CallbackPopup missing), Is.False);
        }
    }

    public sealed class SecondPopup : PopupBase { }
}
