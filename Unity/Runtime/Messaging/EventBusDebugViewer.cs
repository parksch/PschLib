#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using PschLib.Messaging;
using UnityEngine;

namespace PschLib.Unity.Messaging
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PschLib/Debug/Event Bus Debug Viewer")]
    public sealed class EventBusDebugViewer : MonoBehaviour
    {
        public event Action DebugStateChanged
        {
            add => EventBus.DebugListenersChanged += value;
            remove => EventBus.DebugListenersChanged -= value;
        }

        public void GetDebugInfo(List<EventBus.DebugInfo> results)
        {
            EventBus.GetDebugInfo(results);
        }
    }
}
#endif
