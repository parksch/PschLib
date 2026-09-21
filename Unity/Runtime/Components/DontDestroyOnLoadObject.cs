using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace PschLib.Unity.Lifecycle
{
    [MovedFrom(true, sourceNamespace: "PschLib", sourceAssembly: "PschLib.Unity.Runtime")]
    [DisallowMultipleComponent]
    public sealed class DontDestroyOnLoadObject : MonoBehaviour
    {
        private void Awake()
        {
            if (transform.parent != null)
            {
                Debug.LogError(
                    $"[{nameof(DontDestroyOnLoadObject)}] must be attached to a root GameObject. " +
                    $"'{name}' will not be moved to the DontDestroyOnLoad scene.",
                    this);
                return;
            }

            DontDestroyOnLoad(gameObject);
        }
    }
}
