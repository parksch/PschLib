using System;
using System.Collections.Generic;

namespace PschLib.Messaging
{
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Listener>> listenersByType = new Dictionary<Type, List<Listener>>();
        private static readonly List<Listener> pendingListeners = new List<Listener>();
        private static long nextListenerId;
        private static int publishDepth;
#if UNITY_EDITOR
        public static event Action DebugListenersChanged;
#endif

        public static IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var eventType = typeof(TEvent);
            var listenerId = ++nextListenerId;

            var listener = new Listener(listenerId, eventType, handler);
            if (publishDepth == 0)
            {
                AddListener(listener);
                NotifyDebugListenersChanged();
            }
            else
            {
                pendingListeners.Add(listener);
            }

            return new Subscription(() => Unsubscribe(eventType, listenerId));
        }

        public static void Publish<TEvent>(TEvent eventData) where TEvent : struct
        {
            var eventType = typeof(TEvent);
            List<Listener> listeners;
            if (!listenersByType.TryGetValue(eventType, out listeners) || listeners.Count == 0)
            {
                return;
            }

            publishDepth++;

            try
            {
                var listenerCount = listeners.Count;
                for (var i = 0; i < listenerCount; i++)
                {
                    var listener = listeners[i];
                    if (!listener.IsDisposed)
                    {
                        ((Action<TEvent>)listener.Handler)(eventData);
                    }
                }
            }
            finally
            {
                publishDepth--;
                if (publishDepth == 0)
                {
                    ApplyPendingChanges();
                }
            }
        }

        public static void Clear()
        {
            listenersByType.Clear();
            pendingListeners.Clear();
            NotifyDebugListenersChanged();
        }

#if UNITY_EDITOR
        public static void GetDebugInfo(List<DebugInfo> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            foreach (var pair in listenersByType)
            {
                var listeners = pair.Value;

                for (var i = 0; i < listeners.Count; i++)
                {
                    var listener = listeners[i];
                    if (!listener.IsDisposed)
                    {
                        results.Add(new DebugInfo(
                            listener.Id,
                            pair.Key,
                            listener.Handler.Target,
                            listener.Handler.Method.DeclaringType,
                            listener.Handler.Method.Name));
                    }
                }
            }
        }

        public readonly struct DebugInfo
        {
            public readonly long ListenerId;
            public readonly Type EventType;
            public readonly object Target;
            public readonly Type DeclaringType;
            public readonly string MethodName;

            public DebugInfo(long listenerId, Type eventType, object target, Type declaringType, string methodName)
            {
                ListenerId = listenerId;
                EventType = eventType;
                Target = target;
                DeclaringType = declaringType;
                MethodName = methodName;
            }
        }
#endif

        private static void Unsubscribe(Type eventType, long listenerId)
        {
            List<Listener> listeners;
            if (listenersByType.TryGetValue(eventType, out listeners) && MarkDisposed(listeners, listenerId))
            {
                if (publishDepth == 0)
                {
                    RemoveDisposed(listeners);
                    if (listeners.Count == 0)
                    {
                        listenersByType.Remove(eventType);
                    }
                }

                NotifyDebugListenersChanged();

                return;
            }

            MarkDisposed(pendingListeners, listenerId);
        }

        private static bool MarkDisposed(List<Listener> listeners, long listenerId)
        {
            for (var i = 0; i < listeners.Count; i++)
            {
                if (listeners[i].Id == listenerId)
                {
                    listeners[i].IsDisposed = true;
                    return true;
                }
            }

            return false;
        }

        private static void AddListener(Listener listener)
        {
            List<Listener> listeners;
            if (!listenersByType.TryGetValue(listener.EventType, out listeners))
            {
                listeners = new List<Listener>();
                listenersByType.Add(listener.EventType, listeners);
            }

            listeners.Add(listener);
        }

        private static void ApplyPendingChanges()
        {
            var hasPendingListeners = pendingListeners.Count > 0;

            foreach (var pair in listenersByType)
            {
                RemoveDisposed(pair.Value);
            }

            for (var i = 0; i < pendingListeners.Count; i++)
            {
                var listener = pendingListeners[i];
                if (!listener.IsDisposed)
                {
                    AddListener(listener);
                }
            }

            pendingListeners.Clear();

            if (hasPendingListeners)
            {
                NotifyDebugListenersChanged();
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void NotifyDebugListenersChanged()
        {
#if UNITY_EDITOR
            DebugListenersChanged?.Invoke();
#endif
        }

        private static void RemoveDisposed(List<Listener> listeners)
        {
            for (var i = listeners.Count - 1; i >= 0; i--)
            {
                if (listeners[i].IsDisposed)
                {
                    listeners.RemoveAt(i);
                }
            }
        }

        private sealed class Listener
        {
            public readonly long Id;
            public readonly Type EventType;
            public readonly Delegate Handler;
            public bool IsDisposed;

            public Listener(long id, Type eventType, Delegate handler)
            {
                Id = id;
                EventType = eventType;
                Handler = handler;
            }
        }

        private sealed class Subscription : IDisposable
        {
            private Action unsubscribe;

            public Subscription(Action unsubscribe)
            {
                this.unsubscribe = unsubscribe;
            }

            public void Dispose()
            {
                var callback = unsubscribe;
                unsubscribe = null;

                if (callback != null)
                {
                    callback();
                }
            }
        }
    }
}
