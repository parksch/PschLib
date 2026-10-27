using System.Collections.Generic;
using NUnit.Framework;
using PschLib.AssetLoading.Internal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PschLib.Tests.AssetLoading
{
    public sealed class AssetCacheTests
    {
        private Texture2D texture;
        private AssetCache cache;

        [SetUp]
        public void SetUp()
        {
            texture = new Texture2D(1, 1);
            cache = new AssetCache();
            cache.Add<Texture2D>("texture", texture);
            cache.Add<Object>("alias", texture);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(texture);

        [Test]
        public void UnloadUnusedAlias_WhileAnotherTypeIsActive_IsBlocked()
        {
            cache.Release<Texture2D>("texture");
            Assert.That(cache.Unload<Texture2D>("texture", out _), Is.EqualTo(AssetUnloadResult.InUse));
            Assert.That(cache.TryGet<Object>("alias", out var asset), Is.True);
            Assert.That(asset, Is.SameAs(texture));
        }

        [Test]
        public void ClearUnused_KeepsAllAliasesUntilTheirReferencesAreReleased()
        {
            cache.Release<Texture2D>("texture");
            var unloaded = new List<Object>();
            Assert.That(cache.ClearUnused(_ => true, unloaded.Add), Is.Zero);
            Assert.That(cache.Count, Is.EqualTo(2));

            cache.Release<Object>("alias");
            Assert.That(cache.ClearUnused(_ => true, unloaded.Add), Is.EqualTo(1));
            Assert.That(unloaded, Is.EqualTo(new Object[] { texture }));
            Assert.That(cache.Count, Is.Zero);
        }

        [Test]
        public void Unload_WhenAllAliasesAreUnused_RemovesEveryAlias()
        {
            cache.Release<Texture2D>("texture");
            cache.Release<Object>("alias");
            Assert.That(cache.Unload<Object>("alias", out var asset), Is.EqualTo(AssetUnloadResult.Unloaded));
            Assert.That(asset, Is.SameAs(texture));
            Assert.That(cache.Count, Is.Zero);
        }

        [Test]
        public void ClearUnused_PendingAliasProtectsWholeAsset()
        {
            cache.Release<Texture2D>("texture");
            cache.Release<Object>("alias");
            Assert.That(cache.ClearUnused(key => key.Address != "alias", _ => Assert.Fail("Pending alias unloaded")), Is.Zero);
            Assert.That(cache.Count, Is.EqualTo(2));
        }

        [Test]
        public void Unload_PendingAliasProtectsWholeAsset()
        {
            cache.Release<Texture2D>("texture");
            cache.Release<Object>("alias");
            Assert.That(cache.Unload<Texture2D>("texture", out _, key => key.Address != "alias"),
                Is.EqualTo(AssetUnloadResult.InUse));
            Assert.That(cache.Count, Is.EqualTo(2));
        }

        [Test]
        public void Clear_UnloadsEachActualAssetOnlyOnce()
        {
            var unloaded = new List<Object>();
            cache.Clear(unloaded.Add);
            Assert.That(unloaded, Is.EqualTo(new Object[] { texture }));
            Assert.That(cache.Count, Is.Zero);
        }

        [Test]
        public void CompletionPin_ProtectsAllAliasesBetweenWaitersAndReleasesIdempotently()
        {
            cache.Release<Texture2D>("texture");
            cache.Release<Object>("alias");
            var pin = cache.Pin<Texture2D>("texture");
            Assert.That(cache.TryAcquire<Texture2D>("texture", out _), Is.True);
            cache.Release<Texture2D>("texture");

            Assert.That(cache.Unload<Object>("alias", out _), Is.EqualTo(AssetUnloadResult.InUse));
            Assert.That(cache.ClearUnused(_ => true, _ => Assert.Fail("Remaining waiter lost its asset")), Is.Zero);
            Assert.That(cache.TryAcquire<Texture2D>("texture", out var second), Is.True);
            Assert.That(second, Is.SameAs(texture));
            pin.Dispose();
            pin.Dispose();

            cache.Release<Texture2D>("texture");
            var unloaded = new List<Object>();
            Assert.That(cache.ClearUnused(_ => true, unloaded.Add), Is.EqualTo(1));
            Assert.That(unloaded.Count, Is.EqualTo(1));
        }

        [Test]
        public void CompletionPin_AllWaitersCancelled_DoesNotLeavePermanentOwnership()
        {
            cache.Release<Texture2D>("texture");
            cache.Release<Object>("alias");
            using (cache.Pin<Texture2D>("texture"))
            {
                Assert.That(cache.ClearUnused(_ => true, _ => Assert.Fail("Pinned asset unloaded")), Is.Zero);
            }
            Assert.That(cache.ClearUnused(_ => true, _ => { }), Is.EqualTo(1));
        }

        [Test]
        public void CompletionPin_DisposedAfterClear_DoesNotAffectReplacementEntry()
        {
            var pin = cache.Pin<Texture2D>("texture");
            cache.Clear(_ => { });
            cache.Add<Texture2D>("texture", texture);
            pin.Dispose();

            Assert.That(cache.Unload<Texture2D>("texture", out _), Is.EqualTo(AssetUnloadResult.InUse));
            cache.Release<Texture2D>("texture");
            Assert.That(cache.Unload<Texture2D>("texture", out _), Is.EqualTo(AssetUnloadResult.Unloaded));
        }
    }
}
