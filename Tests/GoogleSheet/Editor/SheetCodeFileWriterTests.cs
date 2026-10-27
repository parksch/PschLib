using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PschLib.GoogleSheets;
using UnityEngine;

namespace PschLib.Tests.GoogleSheets
{
    public sealed class SheetCodeFileWriterTests
    {
        private GoogleSheetProject project;
        private string root;

        [SetUp]
        public void SetUp()
        {
            project = ScriptableObject.CreateInstance<GoogleSheetProject>();
            project.ProjectKey = "Fixture";
            project.ScriptOutputPath = "Assets/PschLibCodeWriterTests_" + Guid.NewGuid().ToString("N");
            project.Sheets.Add(new GoogleSheetEntry { Name = "First", SheetId = 1 });
            project.Sheets.Add(new GoogleSheetEntry { Name = "Second", SheetId = 2 });
            project.SharedEnums.Add(new SheetSharedEnumDefinition { Name = "Rank", Values = new List<string> { "Common" } });
            root = GoogleSheetPathUtility.GetAbsolutePath(project.ScriptOutputPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (Path.GetDirectoryName(root) == Path.GetFullPath(Application.dataPath) &&
                Path.GetFileName(root).StartsWith("PschLibCodeWriterTests_", StringComparison.Ordinal) && Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
            UnityEngine.Object.DestroyImmediate(project);
        }

        private static GoogleSheetImportResult Result(string name, string suffix = "")
        {
            var id = new SheetField(0, "id", new SheetTypeInfo(SheetTypeKind.Scalar,
                new SheetScalarTypeDefinition("string", typeof(string), "string")), true);
            var fields = new List<SheetField> { id };
            var rows = new List<SheetDataRow>();
            Assert.That(SheetDataCodeGenerator.TryGenerate(name, fields, rows, "GoogleSheetData.Fixture", out var code, out var error), Is.True, error);
            return new GoogleSheetImportResult(new SheetDocument(name, new List<List<string>>()), fields, rows, code + suffix);
        }

        [Test]
        public void BatchWrite_LockedSecondTable_RestoresWholeBatchIncludingLegacyGuid()
        {
            SheetCodeFileWriter.WriteAll(project, new[] { Result("First"), Result("Second") }, null, out _);
            var output = Path.Combine(root, "Fixture");
            var table = Path.Combine(output, "Second", "SecondTable.g.cs");
            File.AppendAllText(table, "\n// Previous generated version");
            var functions = Path.Combine(output, "First", "First.Functions.cs");
            File.WriteAllText(functions, "// User-authored functions");
            var legacy = Path.Combine(output, "Fixture.SharedEnums.g.cs");
            File.WriteAllText(legacy, "// Legacy shared enums");
            File.WriteAllText(legacy + ".meta", "original guid");
            var before = new Dictionary<string, byte[]>();
            foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                before.Add(file, File.ReadAllBytes(file));
            }
            project.SharedEnums[0].Values.Add("Rare");
            bool changed = true;
            bool firstWritten = false;

            using (File.Open(table, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.Throws<IOException>(() => SheetCodeFileWriter.WriteAll(project,
                    new[] { Result("First", "\n// Updated first"), Result("Second", "\n// Updated second") },
                    (done, total) => firstWritten = done == 1, out changed));
            }

            Assert.That(firstWritten, Is.True);
            Assert.That(changed, Is.False);
            Assert.That(Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length, Is.EqualTo(before.Count));
            foreach (var pair in before)
            {
                Assert.That(File.ReadAllBytes(pair.Key), Is.EqualTo(pair.Value), pair.Key);
            }
        }

        [Test]
        public void BatchWrite_ProgressCallbackThrows_RemovesAllNewFilesAndDirectories()
        {
            var expected = new InvalidOperationException("Injected progress failure");
            var failure = Assert.Throws<InvalidOperationException>(() => SheetCodeFileWriter.WriteAll(project,
                new[] { Result("First"), Result("Second") }, (done, total) => throw expected, out _));
            Assert.That(failure, Is.SameAs(expected));
            Assert.That(Directory.Exists(root), Is.False);
        }

        [Test]
        public void SingleWrite_TableCannotBeCreated_RestoresEarlierWrites()
        {
            var output = Path.Combine(root, "Fixture", "First");
            var blockedTable = Path.Combine(output, "FirstTable.g.cs");
            Directory.CreateDirectory(blockedTable);
            Assert.Catch(() => SheetCodeFileWriter.Write(project, Result("First"), out _));
            Assert.That(File.Exists(Path.Combine(output, "First.Data.g.cs")), Is.False);
            Assert.That(File.Exists(Path.Combine(output, "First.Functions.cs")), Is.False);
            Assert.That(File.Exists(Path.Combine(root, "Fixture", "SharedEnums.g.cs")), Is.False);
            Assert.That(Directory.Exists(blockedTable), Is.True);
        }
    }
}
