using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using PschLib.Unity.Pooling;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PschLib.Tests.UnityRuntime
{
    [Category("Pooling")]
    public sealed class PrefabPoolManagerTests
    {
        private GameObject root;
        private GameObject prefab;
        private PrefabPoolManager manager;
        private readonly List<GameObject> rentals = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("PrefabPoolManager test");
            manager = root.AddComponent<PrefabPoolManager>();
            prefab = new GameObject("Test prefab");
            prefab.transform.SetParent(root.transform, false);
            prefab.SetActive(false);
            manager.Register("enemy", prefab, initialCapacity: 1, maxInactiveCount: 2);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var instance in rentals)
            {
                if (instance != null && manager != null)
                {
                    manager.Return(instance);
                }
            }
            rentals.Clear();
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        private GameObject Rent()
        {
            var instance = manager.Get("enemy", root.transform);
            rentals.Add(instance);
            return instance;
        }

        private void Return(GameObject instance)
        {
            Assert.That(manager.Return(instance), Is.True);
            rentals.Remove(instance);
        }

        [Test]
        public void Register_DuplicateKey_DoesNotReplaceExistingPool()
        {
            LogAssert.Expect(LogType.Warning, "Pool is already registered: enemy");
            Assert.That(manager.Register("enemy", prefab), Is.False);
            Assert.That(manager.Count, Is.EqualTo(1));
            Assert.That(Rent(), Is.Not.Null);
        }

        [Test]
        public void Return_ForeignObject_IsRejected()
        {
            LogAssert.Expect(LogType.Warning, "Object was not spawned by this pool manager.");
            Assert.That(manager.Return(prefab), Is.False);
            Assert.That(Rent(), Is.Not.Null);
        }

        [Test]
        public void Remove_WhileInUse_IsRejectedUntilReturned()
        {
            var instance = Rent();
            LogAssert.Expect(LogType.Warning, "Pool cannot be removed while 1 object(s) are still in use: enemy");
            Assert.That(manager.Remove("enemy"), Is.False);
            Return(instance);
            Assert.That(manager.Remove("enemy"), Is.True);
            Assert.That(manager.Count, Is.Zero);
            LogAssert.Expect(LogType.Warning, "Pool is not registered: enemy");
            Assert.That(manager.Get("enemy"), Is.Null);
        }

        [UnityTest]
        public IEnumerator Clear_KeepsRegistrationAndInUseObjects()
        {
            var returned = Rent();
            var inUse = Rent();
            Return(returned);
            Assert.That(manager.Clear("enemy"), Is.True);
            yield return null;

            Assert.That(returned == null, Is.True);
            Assert.That(inUse != null, Is.True);
            Assert.That(manager.Count, Is.EqualTo(1));
            Return(inUse);
            Assert.That(Rent(), Is.Not.Null);
        }
    }
}
