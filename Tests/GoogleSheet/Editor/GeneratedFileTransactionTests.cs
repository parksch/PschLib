using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using PschLib.GoogleSheets;

namespace PschLib.Tests.GoogleSheets
{
    public sealed class GeneratedFileTransactionTests
    {
        private string root;
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PschLibTransactionTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Path.GetDirectoryName(root) == Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) &&
                Path.GetFileName(root).StartsWith("PschLibTransactionTests_", StringComparison.Ordinal))
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void Rollback_RestoresOriginalBytesAndDeletedMetaAndRemovesNewFiles()
        {
            var existing = Path.Combine(root, "Existing.cs");
            var legacy = Path.Combine(root, "Legacy.cs");
            var meta = legacy + ".meta";
            var original = new byte[] { 239, 187, 191, 65, 13, 10 };
            File.WriteAllBytes(existing, original);
            File.WriteAllText(legacy, "legacy enum", Utf8);
            File.WriteAllText(meta, "original guid", Utf8);
            var transaction = new GeneratedFileTransaction();
            transaction.WriteIfChanged(existing, "changed", Utf8);
            transaction.WriteIfChanged(existing, "changed again", Utf8);
            transaction.Delete(legacy);
            transaction.Delete(meta);
            var nested = Path.Combine(root, "New", "Nested");
            transaction.CreateDirectory(nested);
            transaction.WriteIfChanged(Path.Combine(nested, "Created.cs"), "new", Utf8);

            transaction.Rollback(new IOException("Injected failure"));

            Assert.That(File.ReadAllBytes(existing), Is.EqualTo(original));
            Assert.That(File.ReadAllText(legacy), Is.EqualTo("legacy enum"));
            Assert.That(File.ReadAllText(meta), Is.EqualTo("original guid"));
            Assert.That(Directory.Exists(Path.Combine(root, "New")), Is.False);
        }

        [Test]
        public void UnchangedFile_IsNotRewritten()
        {
            var path = Path.Combine(root, "Existing.cs");
            File.WriteAllText(path, "same", Utf8);
            var before = File.GetLastWriteTimeUtc(path);
            var transaction = new GeneratedFileTransaction();
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.That(transaction.WriteIfChanged(path, "same", Utf8), Is.False);
            }
            Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(before));
        }

        [Test]
        public void RollbackFailure_ReportsOriginalFailureAndStillRestoresOtherFiles()
        {
            var first = Path.Combine(root, "First.cs");
            var second = Path.Combine(root, "Second.cs");
            File.WriteAllText(first, "first original", Utf8);
            File.WriteAllText(second, "second original", Utf8);
            var transaction = new GeneratedFileTransaction();
            transaction.WriteIfChanged(first, "changed first", Utf8);
            transaction.WriteIfChanged(second, "changed second", Utf8);
            var originalFailure = new IOException("Original generation failure");

            using (File.Open(second, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var failure = Assert.Throws<AggregateException>(() => transaction.Rollback(originalFailure));
                Assert.That(failure.InnerExceptions[0], Is.SameAs(originalFailure));
                Assert.That(failure.InnerExceptions.Count, Is.EqualTo(2));
            }
            Assert.That(File.ReadAllText(first), Is.EqualTo("first original"));
        }
    }
}
