#if UNITY_EDITOR
using System.Reflection;
using UnityEngine;

namespace PschLib.AssetLoading.Debugging
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PschLib/Debug/Asset Loader Debug Viewer")]
    public sealed class AssetLoaderDebugViewer : MonoBehaviour
    {
        private void Start()
        {
            if (!HasAssetLoader())
            {
                Debug.LogWarning($"[{name}] AssetLoaderDebugViewer could not find an asset loader on this GameObject.", this);
            }
        }

        private bool HasAssetLoader()
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
                        if (fields[j].GetValue(component) is IAssetLoaderDebugInfo)
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
