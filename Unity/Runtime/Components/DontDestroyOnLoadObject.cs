using UnityEngine;

namespace PschLib
{
    [DisallowMultipleComponent]
    public sealed class DontDestroyOnLoadObject : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
