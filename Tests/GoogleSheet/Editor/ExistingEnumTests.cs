using System;
using System.Collections.Generic;
using NUnit.Framework;
using PschLib.GoogleSheets;

namespace PschLib.Tests.GoogleSheets
{
    public sealed class ExistingEnumTests
    {
        public class Outer
        {
            public class Inner
            {
                public enum Kind { One }
            }
        }

        public class Generic<T> { public enum Kind { One } }
        private class Hidden { public enum Kind { One } }
        public class @class { public enum @enum { One } }

        [TestCase(false)]
        [TestCase(true)]
        public void NestedEnum_AcceptsCSharpAndClrNamesAndGeneratesCSharp(bool clrSyntax)
        {
            var runtimeType = typeof(Outer.Inner.Kind);
            var name = clrSyntax ? runtimeType.FullName : runtimeType.FullName.Replace('+', '.');
            Assert.That(SheetTypeCatalog.TryParse($"enum<{name}>", out var type), Is.True);
            Assert.That(type.EnumRuntimeType, Is.EqualTo(runtimeType));
            var code = Generate(type, "One");
            StringAssert.Contains($"global::{runtimeType.FullName.Replace('+', '.')} kind;", code);
        }

        [Test]
        public void NestedEnumList_UsesCSharpNames()
        {
            var name = typeof(Outer.Inner.Kind).FullName.Replace('+', '.');
            Assert.That(SheetTypeCatalog.TryParse($"List<enum<{name}>>", out var type), Is.True);
            StringAssert.Contains($"List<global::{name}> kind;", Generate(type, new[] { "One" }));
        }

        [Test]
        public void KeywordIdentifiers_AreEscapedInGeneratedCode()
        {
            var name = typeof(@class.@enum).FullName.Replace('+', '.');
            var escaped = name.Replace(".class.enum", ".@class.@enum");
            Assert.That(SheetTypeCatalog.TryParse($"enum<global::{escaped}>", out var type), Is.True);
            StringAssert.Contains($"global::{escaped} kind;", Generate(type, "One"));
        }

        [Test]
        public void PublicEnumInsidePrivateOuter_IsRejectedBeforeGeneration()
        {
            Assert.That(SheetTypeCatalog.TryParse($"enum<{typeof(Hidden.Kind).FullName}>", out _), Is.False);
            AssertRejected(typeof(Hidden.Kind), "public");
        }

        [Test]
        public void EnumInsideGenericOuter_IsRejectedBeforeGeneration()
        {
            Assert.That(SheetTypeCatalog.TryParse($"enum<{typeof(Generic<>.Kind).FullName}>", out _), Is.False);
            AssertRejected(typeof(Generic<int>.Kind), "generic");
        }

        [Test]
        public void ManuallyConstructedTypeInfo_UsesValidatedRuntimeTypeInsteadOfRawText()
        {
            var runtimeType = typeof(Outer.Inner.Kind);
            var type = new SheetTypeInfo(SheetTypeKind.Enum, SheetEnumMode.Existing, "invalid+raw+text", runtimeType);
            StringAssert.Contains($"global::{runtimeType.FullName.Replace('+', '.')} kind;", Generate(type, "One"));
        }

        private static string Generate(SheetTypeInfo type, object value)
        {
            var field = new SheetField(0, "kind", type, false);
            var row = new SheetDataRow(3, "one", new Dictionary<SheetField, object> { [field] = value });
            Assert.That(SheetDataCodeGenerator.TryGenerate("Item", new[] { field }, new[] { row }, "Generated.Enums",
                out var code, out var error), Is.True, error);
            return code;
        }

        private static void AssertRejected(Type runtimeType, string reason)
        {
            var type = new SheetTypeInfo(SheetTypeKind.Enum, SheetEnumMode.Existing, runtimeType.FullName, runtimeType);
            var field = new SheetField(0, "kind", type, false);
            Assert.That(SheetDataCodeGenerator.TryGenerate("Item", new[] { field }, new SheetDataRow[0], "Generated.Enums",
                out var code, out var error), Is.False);
            Assert.That(code, Is.Null);
            StringAssert.Contains(reason, error);
        }
    }
}
