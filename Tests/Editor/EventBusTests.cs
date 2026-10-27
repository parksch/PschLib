using System;
using System.Collections.Generic;
using NUnit.Framework;
using PschLib.Messaging;

namespace PschLib.Tests
{
    public sealed class EventBusTests
    {
        private struct Message { public int Value; }
        private struct OtherMessage { }

        [SetUp] public void SetUp() => EventBus.Clear();
        [TearDown] public void TearDown() => EventBus.Clear();

        [Test]
        public void Publish_DeliversPayloadOnlyToMatchingType()
        {
            var received = 0;
            EventBus.Subscribe<Message>(message => received = message.Value);
            EventBus.Subscribe<OtherMessage>(_ => Assert.Fail("Wrong event type."));
            EventBus.Publish(new Message { Value = 42 });
            Assert.That(received, Is.EqualTo(42));
        }

        [Test]
        public void Dispose_IsIdempotentAndRemovesOnlyItsSubscription()
        {
            var calls = 0;
            Action<Message> handler = _ => calls++;
            var first = EventBus.Subscribe(handler);
            EventBus.Subscribe(handler);
            first.Dispose();
            first.Dispose();
            EventBus.Publish(new Message());
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void DisposeDuringPublish_SkipsLaterListener()
        {
            IDisposable later = null;
            EventBus.Subscribe<Message>(_ => later.Dispose());
            later = EventBus.Subscribe<Message>(_ => Assert.Fail("Disposed listener ran."));
            EventBus.Publish(new Message());
        }

        [Test]
        public void SubscribeDuringNestedPublish_WaitsUntilOutermostPublishFinishes()
        {
            var calls = new List<int>();
            EventBus.Subscribe<Message>(message =>
            {
                if (message.Value != 0) return;
                EventBus.Subscribe<Message>(nested => calls.Add(nested.Value));
                EventBus.Publish(new Message { Value = 1 });
            });
            EventBus.Publish(new Message());
            Assert.That(calls, Is.Empty);
            EventBus.Publish(new Message { Value = 2 });
            Assert.That(calls, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void PendingSubscription_DisposedBeforeCommit_NeverRuns()
        {
            EventBus.Subscribe<Message>(_ =>
                EventBus.Subscribe<Message>(__ => Assert.Fail("Pending listener ran.")).Dispose());
            EventBus.Publish(new Message());
            EventBus.Publish(new Message());
        }

        [Test]
        public void HandlerThrows_PendingChangesStillCommitAndBusRemainsUsable()
        {
            var calls = 0;
            var subscription = EventBus.Subscribe<Message>(_ =>
            {
                EventBus.Subscribe<Message>(__ => calls++);
                throw new InvalidOperationException("Expected");
            });
            Assert.Throws<InvalidOperationException>(() => EventBus.Publish(new Message()));
            subscription.Dispose();
            EventBus.Publish(new Message());
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void ClearDuringNestedPublish_StopsBothDispatchesAndKeepsOnlyNewSubscriptions()
        {
            var calls = new List<int>();
            EventBus.Subscribe<Message>(message =>
            {
                if (message.Value == 0)
                {
                    EventBus.Subscribe<Message>(_ => Assert.Fail("Pre-clear pending listener ran."));
                    EventBus.Publish(new Message { Value = 1 });
                    return;
                }
                EventBus.Clear();
                EventBus.Subscribe<Message>(next => calls.Add(next.Value));
                EventBus.Publish(new Message { Value = 2 });
            });
            EventBus.Subscribe<Message>(_ => Assert.Fail("Cleared listener ran."));
            EventBus.Publish(new Message());
            Assert.That(calls, Is.Empty);
            EventBus.Publish(new Message { Value = 3 });
            Assert.That(calls, Is.EqualTo(new[] { 3 }));
        }

        [Test]
        public void ClearDuringPublish_StopsOldListenersEvenAfterDispose()
        {
            var calls = 0;
            IDisposable later = null;
            EventBus.Subscribe<Message>(_ => { EventBus.Clear(); later.Dispose(); });
            later = EventBus.Subscribe<Message>(_ => calls++);
            EventBus.Publish(new Message());
            Assert.That(calls, Is.Zero);
            EventBus.Publish(new Message());
            Assert.That(calls, Is.Zero);
        }
    }
}
