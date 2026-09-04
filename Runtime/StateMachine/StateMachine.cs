using System;
using System.Collections.Generic;

namespace PschLib.StateMachines
{
    public sealed class StateMachine<TState, TContext>
#if UNITY_EDITOR
        : IStateMachineDebugInfo
#endif
        where TState : struct, Enum
    {
        private readonly Dictionary<TState, IState<TContext>> states = new Dictionary<TState, IState<TContext>>();
        private readonly TContext context;

        private IState<TContext> currentState;
        private TState currentStateKey;
        private bool isStarted;

        private bool hasPendingState;
        private TState pendingStateKey;
        private int pendingPriority;

        public event Action<TState, TState> StateChanged;
#if UNITY_EDITOR
        public event Action DebugStateChanged;
#endif

        public TContext Context => context;
        public bool IsStarted => isStarted;

        public TState CurrentStateKey
        {
            get
            {
                EnsureStarted();
                return currentStateKey;
            }
        }

        public StateMachine(TContext context)
        {
            if (ReferenceEquals(context, null))
            {
                throw new ArgumentNullException(nameof(context));
            }

            this.context = context;
        }

        public void Register(TState key, IState<TContext> state)
        {
            if (ReferenceEquals(state, null))
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (states.ContainsKey(key))
            {
                throw new ArgumentException($"State '{key}' is already registered.", nameof(key));
            }

            states.Add(key, state);
            NotifyDebugStateChanged();
        }

        public void Start(TState key)
        {
            if (isStarted)
            {
                throw new InvalidOperationException("StateMachine is already started.");
            }

            var state = GetRegisteredState(key);

            currentStateKey = key;
            currentState = state;
            isStarted = true;

            currentState.Enter(context);
            NotifyDebugStateChanged();
        }

        public void Update()
        {
            EnsureStarted();
            currentState.Update(context);
            ProcessStateChangeRequest();
        }

        public void Stop()
        {
            EnsureStarted();

            try
            {
                currentState.Exit(context);
            }
            finally
            {
                currentState = null;
                currentStateKey = default;
                isStarted = false;

                hasPendingState = false;
                pendingStateKey = default;
                pendingPriority = 0;
                NotifyDebugStateChanged();
            }
        }

        public bool ChangeState(TState key)
        {
            return ChangeState(key, 0);
        }

        public bool ChangeState(TState key, int priority)
        {
            EnsureStarted();
            GetRegisteredState(key);

            if (EqualityComparer<TState>.Default.Equals(currentStateKey, key))
            {
                return false;
            }

            if (hasPendingState && priority <= pendingPriority)
            {
                return false;
            }

            pendingStateKey = key;
            pendingPriority = priority;
            hasPendingState = true;
            return true;
        }

        private void EnsureStarted()
        {
            if (!isStarted)
            {
                throw new InvalidOperationException("StateMachine is not started.");
            }
        }

        private void ProcessStateChangeRequest()
        {
            if (!hasPendingState)
            {
                return;
            }

            var key = pendingStateKey;
            hasPendingState = false;
            pendingPriority = 0;

            if (EqualityComparer<TState>.Default.Equals(currentStateKey, key))
            {
                return;
            }

            ApplyStateChange(key, GetRegisteredState(key));
        }

        private void ApplyStateChange(TState key, IState<TContext> nextState)
        {
            var previousStateKey = currentStateKey;

            currentState.Exit(context);

            currentStateKey = key;
            currentState = nextState;

            currentState.Enter(context);
            StateChanged?.Invoke(previousStateKey, key);
            NotifyDebugStateChanged();
        }

        private IState<TContext> GetRegisteredState(TState key)
        {
            if (!states.TryGetValue(key, out var state))
            {
                throw new KeyNotFoundException($"State '{key}' is not registered.");
            }

            return state;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void NotifyDebugStateChanged()
        {
#if UNITY_EDITOR
            DebugStateChanged?.Invoke();
#endif
        }

#if UNITY_EDITOR
        string IStateMachineDebugInfo.StateTypeName => typeof(TState).FullName ?? typeof(TState).Name;
        string IStateMachineDebugInfo.CurrentStateName => isStarted ? currentStateKey.ToString() : "Not Started";
        bool IStateMachineDebugInfo.IsStarted => isStarted;

        void IStateMachineDebugInfo.GetRegisteredStateNames(List<string> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            foreach (var key in states.Keys)
            {
                results.Add(key.ToString());
            }
        }
#endif
    }
}
