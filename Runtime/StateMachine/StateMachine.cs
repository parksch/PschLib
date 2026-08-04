using System;
using System.Collections.Generic;

namespace PschLib
{
    public sealed class StateMachine<TState, TContext> where TState : struct, Enum
    {
        private const int MaxTransitionsPerProcess = 32;

        private readonly Dictionary<TState, IState<TContext>> _states = new Dictionary<TState, IState<TContext>>();
        private readonly TContext _context;

        private IState<TContext> _currentState;
        private TState _currentStateKey;
        private bool _isStarted;

        private bool _isExecutingCallback;
        private bool _hasPendingState;
        private TState _pendingStateKey;

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

            _isExecutingCallback = true;

            try
            {
                _currentState.Enter(_context);
            }
            finally
            {
                _isExecutingCallback = false;
            }

            ProcessPendingStateChanges();
        }

        public void Update()
        {
            EnsureStarted();

            _isExecutingCallback = true;

            try
            {
                _currentState.Update(_context);
            }
            finally
            {
                _isExecutingCallback = false;
            }

            ProcessPendingStateChanges();
        }

        public bool ChangeState(TState key)
        {
            EnsureStarted();

            if (_isExecutingCallback)
            {
                GetRegisteredState(key);

                if (EqualityComparer<TState>.Default.Equals(_currentStateKey, key))
                {
                    return false;
                }

                _pendingStateKey = key;
                _hasPendingState = true;
                return true;
            }

            if (EqualityComparer<TState>.Default.Equals(_currentStateKey, key))
            {
                return false;
            }

            ChangeStateImmediately(key);
            ProcessPendingStateChanges();
            return true;
        }

        private void EnsureStarted()
        {
            if (!_isStarted)
            {
                throw new InvalidOperationException("StateMachine is not started.");
            }
        }

        private void ProcessPendingStateChanges()
        {
            var transitionCount = 0;

            while (_hasPendingState)
            {
                if (++transitionCount > MaxTransitionsPerProcess)
                {
                    _hasPendingState = false;
                    throw new InvalidOperationException(
                        $"State transition limit exceeded: {MaxTransitionsPerProcess}.");
                }

                var key = _pendingStateKey;
                _hasPendingState = false;

                if (EqualityComparer<TState>.Default.Equals(_currentStateKey, key))
                {
                    continue;
                }

                ChangeStateImmediately(key);
            }
        }

        private void ChangeStateImmediately(TState key)
        {
            var nextState = GetRegisteredState(key);

            _isExecutingCallback = true;

            try
            {
                _currentState.Exit(_context);

                _currentStateKey = key;
                _currentState = nextState;

                _currentState.Enter(_context);
            }
            finally
            {
                _isExecutingCallback = false;
            }
        }

        private IState<TContext> GetRegisteredState(TState key)
        {
            if (!_states.TryGetValue(key, out var state))
            {
                throw new KeyNotFoundException($"State '{key}' is not registered.");
            }

            return state;
        }
    }
}
