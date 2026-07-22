using System;
using System.Collections.Generic;

public static class EventBus
{
    private static readonly Dictionary<Type, List<Listener>> Listeners = new Dictionary<Type, List<Listener>>();
    private static readonly List<Listener> PendingListeners = new List<Listener>();
    private static long _nextListenerId;
    private static int _publishDepth;

    public static IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var eventType = typeof(TEvent);
        var listenerId = ++_nextListenerId;

        var listener = new Listener(listenerId, eventType, handler);
        if (_publishDepth == 0)
        {
            AddListener(listener);
        }
        else
        {
            PendingListeners.Add(listener);
        }

        return new Subscription(() => Unsubscribe(eventType, listenerId));
    }

    public static void Publish<TEvent>(TEvent eventData)
        where TEvent : struct
    {
        var eventType = typeof(TEvent);
        List<Listener> listeners;
        if (!Listeners.TryGetValue(eventType, out listeners) || listeners.Count == 0)
        {
            return;
        }

        _publishDepth++;

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
            _publishDepth--;
            if (_publishDepth == 0)
            {
                ApplyPendingChanges();
            }
        }
    }

    public static void Clear()
    {
        Listeners.Clear();
        PendingListeners.Clear();
    }

    private static void Unsubscribe(Type eventType, long listenerId)
    {
        List<Listener> listeners;
        if (Listeners.TryGetValue(eventType, out listeners) && MarkDisposed(listeners, listenerId))
        {
            if (_publishDepth == 0)
            {
                RemoveDisposed(listeners);
                if (listeners.Count == 0)
                {
                    Listeners.Remove(eventType);
                }
            }

            return;
        }

        MarkDisposed(PendingListeners, listenerId);
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
        if (!Listeners.TryGetValue(listener.EventType, out listeners))
        {
            listeners = new List<Listener>();
            Listeners.Add(listener.EventType, listeners);
        }

        listeners.Add(listener);
    }

    private static void ApplyPendingChanges()
    {
        foreach (var pair in Listeners)
        {
            RemoveDisposed(pair.Value);
        }

        for (var i = 0; i < PendingListeners.Count; i++)
        {
            var listener = PendingListeners[i];
            if (!listener.IsDisposed)
            {
                AddListener(listener);
            }
        }

        PendingListeners.Clear();
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
        private Action _unsubscribe;

        public Subscription(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            var unsubscribe = _unsubscribe;
            _unsubscribe = null;

            if (unsubscribe != null)
            {
                unsubscribe();
            }
        }
    }
}
