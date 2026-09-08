#if UNITY_EDITOR
using System.Reflection;
using PschLib.Scheduling;
using UnityEngine;

namespace PschLib.Unity.Scheduling
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PschLib/Debug/Timer Scheduler Debug Viewer")]
    public sealed class TimerSchedulerDebugViewer : MonoBehaviour
    {
        private void Start()
        {
            if (!HasTimerScheduler())
            {
                Debug.LogWarning(
                    $"[{name}] TimerSchedulerDebugViewer could not find a TimerScheduler on this GameObject.",
                    this);
            }
        }

        private bool HasTimerScheduler()
        {
            var components = GetComponents<MonoBehaviour>();

            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];

                if (component == null || component == this)
                {
                    continue;
                }

                var type = component.GetType();

                while (type != null && type != typeof(MonoBehaviour))
                {
                    var fields = type.GetFields(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly);

                    for (var j = 0; j < fields.Length; j++)
                    {
                        if (fields[j].GetValue(component) is ITimerSchedulerDebugInfo)
                        {
                            return true;
                        }
                    }

                    type = type.BaseType;
                }
            }

            return false;
        }
    }
}
#endif
