#if UNITY_EDITOR
using System.Reflection;
using PschLib.StateMachines;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace PschLib.Unity.Debugging
{
    [MovedFrom(true, sourceNamespace: "PschLib", sourceAssembly: "PschLib.Unity.Runtime")]
    [DisallowMultipleComponent]
    [AddComponentMenu("PschLib/Debug/State Machine Debug Viewer")]
    public sealed class StateMachineDebugViewer : MonoBehaviour
    {
        private void Start()
        {
            if (!HasStateMachine())
            {
                Debug.LogWarning($"[{name}] StateMachineDebugViewer could not find a StateMachine on this GameObject.", this);
            }
        }

        private bool HasStateMachine()
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
                    var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                    for (var j = 0; j < fields.Length; j++)
                    {
                        if (fields[j].GetValue(component) is IStateMachineDebugInfo)
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
