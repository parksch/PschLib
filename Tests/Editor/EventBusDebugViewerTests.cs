using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PschLib.Messaging;
using PschLib.Unity.Debugging;
using PschLib.Unity.Editor.Messaging;
using PschLib.Unity.Messaging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PschLib.Tests
{
    public sealed class EventBusDebugViewerTests
    {
        private GameObject viewerObject;
        private EventBusDebugViewer viewer;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            viewerObject = new GameObject("EventBus debug viewer test");
            viewer = viewerObject.AddComponent<EventBusDebugViewer>();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
            Object.DestroyImmediate(viewerObject);
        }

        [Test]
        public void GetDebugInfo_ReportsCurrentEventBusListeners()
        {
            var results = new List<EventBus.DebugInfo>();
            var subscription = EventBus.Subscribe<TestEvent>(HandleTestEvent);

            try
            {
                viewer.GetDebugInfo(results);

                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results[0].EventType, Is.EqualTo(typeof(TestEvent)));
                Assert.That(results[0].MethodName, Is.EqualTo(nameof(HandleTestEvent)));
            }
            finally
            {
                subscription.Dispose();
            }
        }

        [Test]
        public void DebugStateChanged_ForwardsEventBusChangesAndAllowsUnsubscribe()
        {
            var notificationCount = 0;
            Action callback = () => notificationCount++;
            viewer.DebugStateChanged += callback;

            var subscription = EventBus.Subscribe<TestEvent>(HandleTestEvent);
            Assert.That(notificationCount, Is.EqualTo(1));

            viewer.DebugStateChanged -= callback;
            subscription.Dispose();

            Assert.That(notificationCount, Is.EqualTo(1));
        }

        [Test]
        public void CustomEditor_CommonBaseDiscoversViewerAsDebugSource()
        {
            var customEditor = UnityEditor.Editor.CreateEditor(viewer);

            try
            {
                Assert.That(customEditor, Is.TypeOf<EventBusDebugViewerEditor>());

                var commonBaseType = typeof(DebugViewerEditorBase<EventBusDebugViewer, EventBusDebugViewer>);
                Assert.That(customEditor.GetType().BaseType, Is.EqualTo(commonBaseType));

                var refreshTargetsMethod = commonBaseType.GetMethod(
                    "RefreshTargets",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var targetsField = commonBaseType.GetField(
                    "debugTargets",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(refreshTargetsMethod, Is.Not.Null);
                Assert.That(targetsField, Is.Not.Null);
                refreshTargetsMethod.Invoke(customEditor, null);

                var targets = targetsField.GetValue(customEditor) as ICollection;
                Assert.That(targets, Is.Not.Null);
                Assert.That(targets.Count, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(customEditor);
            }
        }

        private static void HandleTestEvent(TestEvent eventData)
        {
        }

        private readonly struct TestEvent
        {
        }
    }
}
