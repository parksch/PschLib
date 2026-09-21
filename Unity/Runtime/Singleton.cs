using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace PschLib.Unity.Lifecycle
{
    [MovedFrom(true, sourceNamespace: "PschLib", sourceAssembly: "PschLib.Unity.Runtime")]
    public class Singleton<T> : MonoBehaviour where T : Singleton<T>
    {
        private static T instance;

        public static T Instance
        {
            get
            {
                // When Domain Reload is disabled, a destroyed Unity object can remain in
                // the static field between Play Mode sessions. Normalize Unity's fake-null
                // reference without discarding a live instance when Scene Reload is also off.
                if (!object.ReferenceEquals(instance, null) && instance == null)
                {
                    instance = null;
                }

                return instance;
            }
            private set => instance = value;
        }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = (T)this;
        }

        protected virtual void OnDestroy()
        {
            if (object.ReferenceEquals(instance, this))
            {
                instance = null;
            }
        }
    }
}

