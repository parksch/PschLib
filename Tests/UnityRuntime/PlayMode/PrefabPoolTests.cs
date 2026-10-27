using System;
using System.Collections;
using NUnit.Framework;
using PschLib.Unity.Pooling;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PschLib.Tests.UnityRuntime
{
    [Category("Pooling")]
    public sealed class PrefabPoolTests
    {
        private GameObject root;
        private GameObject prefab;
        private Transform storage;
        private PrefabPool pool;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("PrefabPool test");
            prefab = new GameObject("Test prefab");
            prefab.transform.SetParent(root.transform, false);
            prefab.transform.localScale = Vector3.one * 2f;
            prefab.SetActive(false);
            var storageObject = new GameObject("Inactive storage");
            storage = storageObject.transform;
            storage.SetParent(root.transform, false);
            storage.position = new Vector3(3f, 4f, 5f);
            storageObject.SetActive(false);
            pool = new PrefabPool(prefab, storage, 2);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Constructor_InvalidArguments_AreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new PrefabPool(null, storage));
            Assert.Throws<ArgumentNullException>(() => new PrefabPool(prefab, null));
            Assert.Throws<ArgumentException>(() => new PrefabPool(prefab, root.transform));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PrefabPool(prefab, storage, 0));
        }

        [Test]
        public void Prewarm_TargetCount_DoesNotCreateDuplicates()
        {
            pool.Prewarm(2);
            pool.Prewarm(2);
            pool.Prewarm(1);
            Assert.That(pool.InactiveCount, Is.EqualTo(2));
            Assert.That(pool.InUseCount, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.Prewarm(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.Prewarm(3));
        }

        [Test]
        public void ReturnThenGet_ReusesInstanceAndResetsTransform()
        {
            var first = pool.Get(root.transform);
            first.transform.localScale = Vector3.one * 7f;
            first.transform.position = Vector3.one * 20f;
            Assert.That(pool.Return(first), Is.True);
            Assert.That(first.transform.parent, Is.SameAs(storage));

            var reused = pool.Get(root.transform);
            Assert.That(reused, Is.SameAs(first));
            Assert.That(reused.transform.localScale, Is.EqualTo(prefab.transform.localScale));
            Assert.That(reused.transform.position, Is.EqualTo(storage.position));
            Assert.That(pool.InUseCount, Is.EqualTo(1));
            Assert.That(pool.InactiveCount, Is.Zero);
        }

        [Test]
        public void Return_ForeignAndAlreadyReturnedInstances_AreRejected()
        {
            LogAssert.Expect(LogType.Warning, "Object was already returned or does not belong to this pool.");
            Assert.That(pool.Return(prefab), Is.False);
            var instance = pool.Get(root.transform);
            Assert.That(pool.Return(instance), Is.True);
            LogAssert.Expect(LogType.Warning, "Object was already returned or does not belong to this pool.");
            Assert.That(pool.Return(instance), Is.False);
            Assert.That(pool.InactiveCount, Is.EqualTo(1));
        }

        [Test]
        public void Get_ActivationIsExplicit()
        {
            var inactive = pool.Get(root.transform);
            var active = pool.Get(root.transform, activate: true);
            Assert.That(inactive.activeSelf, Is.False);
            Assert.That(active.activeInHierarchy, Is.True);
            Assert.That(pool.InUseCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator Clear_DestroysOnlyReturnedInstances()
        {
            var returned = pool.Get(root.transform);
            var inUse = pool.Get(root.transform);
            pool.Return(returned);
            pool.Clear();
            yield return null;

            Assert.That(returned == null, Is.True);
            Assert.That(inUse != null, Is.True);
            Assert.That(pool.InUseCount, Is.EqualTo(1));
            Assert.That(pool.InactiveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Return_OverInactiveLimit_DestroysOverflowInstance()
        {
            pool = new PrefabPool(prefab, storage, 1);
            var first = pool.Get(root.transform);
            var overflow = pool.Get(root.transform);
            pool.Return(first);
            pool.Return(overflow);
            yield return null;

            Assert.That(first != null, Is.True);
            Assert.That(overflow == null, Is.True);
            Assert.That(pool.InactiveCount, Is.EqualTo(1));
            Assert.That(pool.InUseCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Get_DestroyedInactiveInstance_IsReplaced()
        {
            var old = pool.Get(root.transform);
            pool.Return(old);
            Object.Destroy(old);
            yield return null;

            var replacement = pool.Get(root.transform);
            Assert.That(old == null, Is.True);
            Assert.That(replacement != null, Is.True);
            Assert.That(replacement, Is.Not.SameAs(old));
            Assert.That(pool.TotalCount, Is.EqualTo(1));
        }
    }
}
