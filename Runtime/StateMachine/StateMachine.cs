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
        private readonly Dictionary<TState, IState<TContext>> _states = new Dictionary<TState, IState<TContext>>();
        private readonly TContext _context;

        private IState<TContext> _currentState;
        private TState _currentStateKey;
        private bool _isStarted;

        private bool _hasPendingState;
        private TState _pendingStateKey;
        private int _pendingPriority;

        public event Action<TState, TState> StateChanged;
#if UNITY_EDITOR
        public event Action DebugStateChanged;
#endif

        public TContext Context => _context;
        public bool IsStarted => _isStarted;

        public TState CurrentStateKey
        {
            get
            {
                EnsureStarted();
                return _currentStateKey;
            }
        }

        public StateMachine(TContext context)
        {
            if (ReferenceEquals(context, null))
            {
                throw new ArgumentNullException(nameof(context));
            }

            _context = context;
        }

        public void Register(TState key, IState<TContext> state)
        {
            if (ReferenceEquals(state, null))
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (_states.ContainsKey(key))
            {
                throw new ArgumentException($"State '{key}' is already registered.", nameof(key));
            }

            _states.Add(key, state);
            NotifyDebugStateChanged();
        }

        public void Start(TState key)
        {
            if (_isStarted)
            {
                throw new InvalidOperationException("StateMachine is already started.");
            }

            var state = GetRegisteredState(key);

            _currentStateKey = key;
            _currentState = state;
            _isStarted = true;

            _currentState.Enter(_context);
            NotifyDebugStateChanged();
        }

        public void Update()
        {
            EnsureStarted();
            _currentState.Update(_context);
            ProcessStateChangeRequest();
        }

        public void Stop()
        {
            EnsureStarted();

            try
            {
                _currentState.Exit(_context);
            }
            finally
            {
                _currentState = null;
                _currentStateKey = default;
                _isStarted = false;

                _hasPendingState = false;
                _pendingStateKey = default;
                _pendingPriority = 0;
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

            if (EqualityComparer<TState>.Default.Equals(_currentStateKey, key))
            {
                return false;
            }

            if (_hasPendingState && priority <= _pendingPriority)
            {
                return false;
            }

            _pendingStateKey = key;
            _pendingPriority = priority;
            _hasPendingState = true;
            return true;
        }

        private void EnsureStarted()
        {
            if (!_isStarted)
            {
                throw new InvalidOperationException("StateMachine is not started.");
            }
        }

        private void ProcessStateChangeRequest()
        {
            if (!_hasPendingState)
            {
                return;
            }

            var key = _pendingStateKey;
            _hasPendingState = false;
            _pendingPriority = 0;

            if (EqualityComparer<TState>.Default.Equals(_currentStateKey, key))
            {
                return;
            }

            ApplyStateChange(key, GetRegisteredState(key));
        }

        private void ApplyStateChange(TState key, IState<TContext> nextState)
        {
            var previousStateKey = _currentStateKey;

            _currentState.Exit(_context);

            _currentStateKey = key;
            _currentState = nextState;

            _currentState.Enter(_context);
            StateChanged?.Invoke(previousStateKey, key);
            NotifyDebugStateChanged();
        }

        private IState<TContext> GetRegisteredState(TState key)
        {
            if (!_states.TryGetValue(key, out var state))
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
        string IStateMachineDebugInfo.CurrentStateName => _isStarted ? _currentStateKey.ToString() : "Not Started";
        bool IStateMachineDebugInfo.IsStarted => _isStarted;

        void IStateMachineDebugInfo.GetRegisteredStateNames(List<string> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            foreach (var key in _states.Keys)
            {
                results.Add(key.ToString());
            }
        }
#endif
    }
}
