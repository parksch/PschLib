using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PschLib.Messaging;
using PschLib.Scheduling;
using PschLib.StateMachines;
using PschLib.Unity.Pooling;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PschLib.Tests
{
    public sealed class DebugNotificationIsolationTests
    {
        private const string DebugFailureMessage = "Expected debug observer failure";

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
        }

        [Test]
        public void EventBus_BrokenDebugListener_DoesNotLoseSubscriptionOrSkipOtherListeners()
        {
            var healthyCalls = 0;
            var eventCalls = 0;
            Action broken = ThrowDebugFailure;
            Action healthy = () => healthyCalls++;
            IDisposable subscription = null;

            EventBus.DebugListenersChanged += broken;
            EventBus.DebugListenersChanged += healthy;

            try
            {
                Assert.DoesNotThrow(() =>
                    subscription = EventBus.Subscribe<TestEvent>(_ => eventCalls++));

                Assert.That(subscription, Is.Not.Null);
                Assert.That(healthyCalls, Is.EqualTo(1));

                EventBus.Publish(new TestEvent());
                Assert.That(eventCalls, Is.EqualTo(1));

                Assert.DoesNotThrow(() => subscription.Dispose());
                Assert.That(healthyCalls, Is.EqualTo(2));
            }
            finally
            {
                EventBus.DebugListenersChanged -= broken;
                EventBus.DebugListenersChanged -= healthy;
                subscription?.Dispose();
            }
        }

        [Test]
        public void EventBus_DebugFailure_DoesNotMaskHandlerException()
        {
            var expected = new InvalidOperationException("Expected event handler failure");
            Action broken = ThrowDebugFailure;
            IDisposable subscription = null;
            IDisposable pendingSubscription = null;

            EventBus.DebugListenersChanged += broken;

            try
            {
                subscription = EventBus.Subscribe<TestEvent>(_ =>
                {
                    pendingSubscription = EventBus.Subscribe<TestEvent>(ignored => { });
                    throw expected;
                });

                var actual = Assert.Throws<InvalidOperationException>(() =>
                    EventBus.Publish(new TestEvent()));

                Assert.That(actual, Is.SameAs(expected));
            }
            finally
            {
                EventBus.DebugListenersChanged -= broken;
                subscription?.Dispose();
                pendingSubscription?.Dispose();
            }
        }

        [Test]
        public void EventBus_ReentrantDebugNotification_IsSuppressed()
        {
            var calls = 0;
            IDisposable subscription = null;
            IDisposable nestedSubscription = null;
            Action reentrant = () =>
            {
                calls++;

                if (nestedSubscription == null)
                {
                    nestedSubscription = EventBus.Subscribe<TestEvent>(ignored => { });
                }
            };

            EventBus.DebugListenersChanged += reentrant;

            try
            {
                subscription = EventBus.Subscribe<TestEvent>(ignored => { });
                Assert.That(calls, Is.EqualTo(1));
            }
            finally
            {
                EventBus.DebugListenersChanged -= reentrant;
                subscription?.Dispose();
                nestedSubscription?.Dispose();
            }
        }

        [Test]
        public void TimerScheduler_BrokenDebugListener_DoesNotLoseHandleOrMaskCallbackException()
        {
            var scheduler = new TimerScheduler();
            var healthyCalls = 0;
            Action broken = ThrowDebugFailure;
            Action healthy = () => healthyCalls++;

            scheduler.DebugStateChanged += broken;
            scheduler.DebugStateChanged += healthy;

            try
            {
                TimerHandle handle = null;
                Assert.DoesNotThrow(() => handle = scheduler.Schedule(10f));
                Assert.That(handle, Is.Not.Null);
                Assert.That(scheduler.Count, Is.EqualTo(1));
                Assert.That(healthyCalls, Is.EqualTo(1));

                var expected = new InvalidOperationException("Expected timer callback failure");
                scheduler.Schedule(0f, () => throw expected);

                var actual = Assert.Throws<InvalidOperationException>(() => scheduler.Tick(0f, 0f));
                Assert.That(actual, Is.SameAs(expected));
            }
            finally
            {
                scheduler.DebugStateChanged -= broken;
                scheduler.DebugStateChanged -= healthy;
                scheduler.Clear();
            }
        }

        [Test]
        public void StateMachine_BrokenDebugListener_DoesNotChangeOperationResult()
        {
            var machine = new StateMachine<TestStateKey, TestContext>(new TestContext());
            var healthyCalls = 0;
            Action broken = ThrowDebugFailure;
            Action healthy = () => healthyCalls++;

            machine.DebugStateChanged += broken;
            machine.DebugStateChanged += healthy;

            try
            {
                Assert.DoesNotThrow(() => machine.Register(TestStateKey.Idle, new TestState()));
                Assert.DoesNotThrow(() => machine.Start(TestStateKey.Idle));

                Assert.That(machine.IsStarted, Is.True);
                Assert.That(machine.CurrentStateKey, Is.EqualTo(TestStateKey.Idle));
                Assert.That(healthyCalls, Is.EqualTo(2));
            }
            finally
            {
                machine.DebugStateChanged -= broken;
                machine.DebugStateChanged -= healthy;

                if (machine.IsStarted)
                {
                    machine.Stop();
                }
            }
        }

        [Test]
        public void PrefabPoolManager_BrokenDebugListener_DoesNotChangeRegisterResult()
        {
            var managerObject = new GameObject("Debug notification test manager");
            var prefab = new GameObject("Debug notification test prefab");
            managerObject.SetActive(false);
            var manager = managerObject.AddComponent<PrefabPoolManager>();
            var healthyCalls = 0;
            Action broken = ThrowDebugFailure;
            Action healthy = () => healthyCalls++;

            manager.DebugStateChanged += broken;
            manager.DebugStateChanged += healthy;
            ExpectUnityDebugFailure();

            try
            {
                var registered = false;
                Assert.DoesNotThrow(() => registered = manager.Register("test", prefab, 0, 1));
                Assert.That(registered, Is.True);
                Assert.That(manager.Count, Is.EqualTo(1));
                Assert.That(healthyCalls, Is.EqualTo(1));
            }
            finally
            {
                manager.DebugStateChanged -= broken;
                manager.DebugStateChanged -= healthy;
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(prefab);
            }
        }

        [TestCase("PschLib.AssetLoading.Resources.ResourcesLoader, PschLib.AssetLoading")]
        [TestCase("PschLib.AssetLoading.Addressables.AddressablesLoader, PschLib.AssetLoading.Addressables")]
        public void AssetLoader_BrokenDebugListener_DoesNotChangeClearResult(string assemblyQualifiedTypeName)
        {
            var loaderType = Type.GetType(assemblyQualifiedTypeName, false);
            if (loaderType == null)
            {
                Assert.Ignore($"Optional loader is not installed: {assemblyQualifiedTypeName}");
            }

            var loader = Activator.CreateInstance(loaderType);
            var debugEvent = loaderType.GetEvent("DebugStateChanged", BindingFlags.Instance | BindingFlags.Public);
            var clearMethod = loaderType.GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public);
            var healthyCalls = 0;
            Action broken = ThrowDebugFailure;
            Action healthy = () => healthyCalls++;

            Assert.That(debugEvent, Is.Not.Null);
            Assert.That(clearMethod, Is.Not.Null);

            debugEvent.AddEventHandler(loader, broken);
            debugEvent.AddEventHandler(loader, healthy);
            ExpectUnityDebugFailure();

            try
            {
                Assert.DoesNotThrow(() => clearMethod.Invoke(loader, null));
                Assert.That(healthyCalls, Is.EqualTo(1));
            }
            finally
            {
                debugEvent.RemoveEventHandler(loader, broken);
                debugEvent.RemoveEventHandler(loader, healthy);
            }
        }

        private static void ExpectUnityDebugFailure()
        {
            LogAssert.Expect(LogType.Exception, new Regex(DebugFailureMessage));
        }

        private static void ThrowDebugFailure()
        {
            throw new Exception(DebugFailureMessage);
        }

        private readonly struct TestEvent
        {
        }

        private enum TestStateKey
        {
            Idle
        }

        private sealed class TestContext
        {
        }

        private sealed class TestState : IState<TestContext>
        {
            public void Enter(TestContext context)
            {
            }

            public void Update(TestContext context)
            {
            }

            public void Exit(TestContext context)
            {
            }
        }
    }
}
