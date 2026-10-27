using System;
using System.Collections.Generic;
using NUnit.Framework;
using PschLib.StateMachines;

namespace PschLib.Tests
{
    public sealed class StateMachineTests
    {
        private enum Key { A, B, C }
        private sealed class State : IState<List<string>>
        {
            private readonly string name;
            public Action OnEnter;
            public Action OnExit;
            public State(string name) => this.name = name;
            public void Enter(List<string> context) { context.Add(name + ".Enter"); OnEnter?.Invoke(); }
            public void Update(List<string> context) => context.Add(name + ".Update");
            public void Exit(List<string> context) { context.Add(name + ".Exit"); OnExit?.Invoke(); }
        }

        [Test]
        public void InvalidLifecycleAndRegistration_AreRejected()
        {
            var machine = new StateMachine<Key, List<string>>(new List<string>());
            Assert.Throws<InvalidOperationException>(() => machine.Update());
            Assert.Throws<InvalidOperationException>(() => machine.Stop());
            Assert.Throws<KeyNotFoundException>(() => machine.Start(Key.A));
            Assert.Throws<ArgumentNullException>(() => machine.Register(Key.A, null));
            machine.Register(Key.A, new State("A"));
            Assert.Throws<ArgumentException>(() => machine.Register(Key.A, new State("A")));
            machine.Start(Key.A);
            Assert.Throws<InvalidOperationException>(() => machine.Start(Key.A));
        }

        [Test]
        public void DeferredTransition_HighestPriorityWinsAndEqualPriorityKeepsFirst()
        {
            var log = new List<string>();
            var machine = new StateMachine<Key, List<string>>(log);
            machine.Register(Key.A, new State("A"));
            machine.Register(Key.B, new State("B"));
            machine.Register(Key.C, new State("C"));
            machine.StateChanged += (from, to) => log.Add(from + "->" + to);
            machine.Start(Key.A);
            Assert.That(machine.ChangeState(Key.A), Is.False);
            Assert.That(machine.ChangeState(Key.B, 1), Is.True);
            Assert.That(machine.ChangeState(Key.C, 1), Is.False);
            Assert.That(machine.ChangeState(Key.C, 2), Is.True);
            Assert.That(machine.CurrentStateKey, Is.EqualTo(Key.A));
            machine.Update();
            Assert.That(log, Is.EqualTo(new[] { "A.Enter", "A.Update", "A.Exit", "C.Enter", "A->C" }));
        }

        [Test]
        public void RequestFromEnter_IsProcessedOnFollowingUpdate()
        {
            var machine = new StateMachine<Key, List<string>>(new List<string>());
            machine.Register(Key.A, new State("A"));
            machine.Register(Key.B, new State("B") { OnEnter = () => machine.ChangeState(Key.C) });
            machine.Register(Key.C, new State("C"));
            machine.Start(Key.A);
            machine.ChangeState(Key.B);
            machine.Update();
            Assert.That(machine.CurrentStateKey, Is.EqualTo(Key.B));
            machine.Update();
            Assert.That(machine.CurrentStateKey, Is.EqualTo(Key.C));
        }

        [Test]
        public void Stop_WhenExitThrows_StillClearsPendingTransitionAndAllowsRestart()
        {
            var machine = new StateMachine<Key, List<string>>(new List<string>());
            var state = new State("A") { OnExit = () => throw new InvalidOperationException("Expected") };
            machine.Register(Key.A, state);
            machine.Register(Key.B, new State("B"));
            machine.Start(Key.A);
            machine.ChangeState(Key.B);
            Assert.Throws<InvalidOperationException>(() => machine.Stop());
            Assert.That(machine.IsStarted, Is.False);
            state.OnExit = null;
            machine.Start(Key.A);
            machine.Update();
            Assert.That(machine.CurrentStateKey, Is.EqualTo(Key.A));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EnterFailure_ClearsRequestsAndAllowsRestart(bool duringTransition)
        {
            var log = new List<string>();
            var machine = new StateMachine<Key, List<string>>(log);
            var expected = new InvalidOperationException("Expected");
            machine.Register(Key.A, new State("A"));
            machine.Register(Key.B, new State("B") { OnEnter = () =>
            {
                machine.ChangeState(Key.C);
                throw expected;
            }});
            machine.Register(Key.C, new State("C"));
            if (duringTransition)
            {
                machine.Start(Key.A);
                machine.ChangeState(Key.B);
                Assert.That(Assert.Throws<InvalidOperationException>(() => machine.Update()), Is.SameAs(expected));
            }
            else
            {
                Assert.That(Assert.Throws<InvalidOperationException>(() => machine.Start(Key.B)), Is.SameAs(expected));
            }
            Assert.Throws<InvalidOperationException>(() => machine.Update());
            Assert.That(log, Does.Not.Contain("B.Exit"));
            machine.Start(Key.A);
            machine.Update();
            Assert.That(machine.CurrentStateKey, Is.EqualTo(Key.A));
        }

        [Test]
        public void TransitionExitThrows_StopsWithoutEnteringDestinationAndAllowsRestart()
        {
            var log = new List<string>();
            var machine = new StateMachine<Key, List<string>>(log);
            var expected = new InvalidOperationException("Expected");
            machine.Register(Key.A, new State("A") { OnExit = () => throw expected });
            machine.Register(Key.B, new State("B"));
            machine.Start(Key.A);
            machine.ChangeState(Key.B);
            Assert.That(Assert.Throws<InvalidOperationException>(() => machine.Update()), Is.SameAs(expected));
            Assert.That(machine.IsStarted, Is.False);
            Assert.That(log, Does.Not.Contain("B.Enter"));
            machine.Start(Key.B);
            machine.Update();
            Assert.That(machine.CurrentStateKey, Is.EqualTo(Key.B));
        }

        [Test]
        public void StartEnterThrows_StopsMachine()
        {
            var machine = new StateMachine<Key, List<string>>(new List<string>());
            machine.Register(Key.A, new State("A") { OnEnter = () => throw new InvalidOperationException("Expected") });
            Assert.Throws<InvalidOperationException>(() => machine.Start(Key.A));
            Assert.That(machine.IsStarted, Is.False);
            Assert.Throws<InvalidOperationException>(() => { var key = machine.CurrentStateKey; });
        }

        [Test]
        public void TransitionEnterThrows_StopsWithoutNotification()
        {
            var machine = new StateMachine<Key, List<string>>(new List<string>());
            var notifications = 0;
            machine.Register(Key.A, new State("A"));
            machine.Register(Key.B, new State("B") { OnEnter = () => throw new InvalidOperationException("Expected") });
            machine.StateChanged += (_, __) => notifications++;
            machine.Start(Key.A);
            machine.ChangeState(Key.B);
            Assert.Throws<InvalidOperationException>(() => machine.Update());
            Assert.That(machine.IsStarted, Is.False);
            Assert.That(notifications, Is.Zero);
        }
    }
}
