using UnityEngine;

namespace PschLib.Unity.Pooling
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class PooledObjectTracker : MonoBehaviour
    {
        private PrefabPoolManager owner;
        private PrefabPool pool;
        private PooledObjectState state;
        private bool expectedDestroy;

        internal void Initialize(PrefabPoolManager owner, PrefabPool pool)
        {
            this.owner = owner;
            this.pool = pool;
            MarkInUse();
        }

        internal void MarkInUse()
        {
            state = PooledObjectState.InUse;
            expectedDestroy = false;
        }

        internal void MarkInactive()
        {
            state = PooledObjectState.Inactive;
        }

        internal void MarkExpectedDestroy()
        {
            expectedDestroy = true;
        }

        private void OnDestroy()
        {
            if (owner != null && pool != null)
            {
                owner.NotifyTrackedObjectDestroyed(gameObject, pool, state, expectedDestroy, gameObject.scene);
            }
        }
    }

    internal enum PooledObjectState
    {
        InUse,
        Inactive
    }
}
