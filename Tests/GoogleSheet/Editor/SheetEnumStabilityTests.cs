using System.Collections.Generic;
using NUnit.Framework;
using PschLib.GoogleSheets;

namespace PschLib.Tests.GoogleSheets
{
    public sealed class SheetEnumStabilityTests
    {
        private static string Generate(string previousCode, params string[] names)
        {
            var field = new SheetField(0, "kind", new SheetTypeInfo(SheetTypeKind.Enum, SheetEnumMode.Local, null, null), false);
            var rows = new List<SheetDataRow>();
            for (var i = 0; i < names.Length; i++)
            {
                rows.Add(new SheetDataRow(i + 3, i.ToString(), new Dictionary<SheetField, object> { [field] = names[i] }));
            }

            Assert.That(SheetDataCodeGenerator.TryGenerate("Item", new[] { field }, rows, "Test.Data",
                previousCode, out var code, out var error), Is.True, error);
            return code;
        }

        [Test]
        public void ReorderRows_PreservesExistingNumericValuesAndOutput()
        {
            var original = Generate(null, "Sword", "Shield");
            Assert.That(Generate(original, "Shield", "Sword"), Is.EqualTo(original));
        }

        [Test]
        public void RemovedValues_AreRetainedAndNewValuesAppend()
        {
            var original = Generate(null, "Sword", "Shield");
            var updated = Generate(original, "Potion", "Shield");
            StringAssert.Contains("Sword = 0,", updated);
            StringAssert.Contains("Shield = 1,", updated);
            StringAssert.Contains("Potion = 2,", updated);
            Assert.That(Generate(updated, "Sword", "Potion", "Shield"), Is.EqualTo(updated));
        }

        [Test]
        public void ExistingExplicitValues_ArePreservedIncludingGaps()
        {
            var previous = "public enum ItemKind { Sword = 4, Shield = 9, }";
            var updated = Generate(previous, "Shield", "Potion");
            StringAssert.Contains("Sword = 4,", updated);
            StringAssert.Contains("Shield = 9,", updated);
            StringAssert.Contains("Potion = 10,", updated);
        }

        [TestCase("public enum ItemKind { Sword = invalid, }")]
        [TestCase("public enum ItemKind { Sword = 0, Shield = 0, }")]
        [TestCase("public enum ItemKind : int { Sword = 4, }")]
        public void InvalidPreviousValue_FailsInsteadOfRenumbering(string previousCode)
        {
            var field = new SheetField(0, "kind", new SheetTypeInfo(SheetTypeKind.Enum, SheetEnumMode.Local, null, null), false);
            Assert.That(SheetDataCodeGenerator.TryGenerate("Item", new[] { field }, new SheetDataRow[0], "Test.Data",
                previousCode, out var code, out var error), Is.False);
            Assert.That(code, Is.Null);
            StringAssert.Contains("Cannot preserve enum", error);
        }
    }
}
