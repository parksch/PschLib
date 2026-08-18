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
            DontDestroyOnLoad(gameObject);
        }
    }
}
