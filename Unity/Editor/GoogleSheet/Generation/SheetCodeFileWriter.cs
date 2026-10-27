using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PschLib.GoogleSheets
{
    internal static class SheetCodeFileWriter
    {
        private static readonly Encoding utf8WithoutBom = new UTF8Encoding(false);

        public static string Write(GoogleSheetProject project, GoogleSheetImportResult result)
        {
            return Write(project, result, out _);
        }

        public static string Write(GoogleSheetProject project, GoogleSheetImportResult result, out bool changed)
        {
            ValidateWrite(project, result);
            ValidateSharedEnums(project);
            ValidateGeneratedTypeNames(project, new[] { result });
            var transaction = new GeneratedFileTransaction();
            changed = false;
            try
            {
                return WriteCore(project, result, transaction, out changed);
            }
            catch (Exception exception)
            {
                changed = false;
                transaction.Rollback(exception);
                throw;
            }
        }

        private static string WriteCore(GoogleSheetProject project, GoogleSheetImportResult result,
            GeneratedFileTransaction transaction, out bool changed)
        {
            SheetDataCodeGenerator.TryCreateClassName(result.Document.Name, out var className, out _);

            var targetNamespace = GoogleSheetPathUtility.GetTargetNamespace(project);
            var rootAssetPath = GoogleSheetPathUtility.GetScriptOutputPath(project);
            var sheetAssetPath = $"{rootAssetPath}/{className}";
            var sheetDirectory = GoogleSheetPathUtility.GetAbsolutePath(sheetAssetPath);
            transaction.CreateDirectory(sheetDirectory);
            changed = WriteSharedEnums(project, targetNamespace, rootAssetPath, transaction);

            var dataFileName = $"{className}.Data.g.cs";
            var functionsFileName = $"{className}.Functions.cs";
            var tableFileName = $"{className}Table.g.cs";
            var keyField = result.Fields.Find(field => field.IsKey);

            changed |= transaction.WriteIfChanged(Path.Combine(sheetDirectory, dataFileName), result.GeneratedCode, utf8WithoutBom);

            if (project.GenerateScriptableObject)
            {
                changed |= transaction.WriteIfChanged(Path.Combine(sheetDirectory, tableFileName), CreateTableCode(targetNamespace, className, SheetDataCodeGenerator.GetMemberName(keyField.Name)), utf8WithoutBom);
            }

            var functionsPath = Path.Combine(sheetDirectory, functionsFileName);

            if (!File.Exists(functionsPath))
            {
                transaction.WriteIfChanged(functionsPath, CreateFunctionsCode(targetNamespace, className), utf8WithoutBom);
                changed = true;
            }

            return $"{sheetAssetPath}/{dataFileName}";
        }

        public static void WriteAll(
            GoogleSheetProject project,
            IReadOnlyList<GoogleSheetImportResult> results,
            Action<int, int> reportProgress,
            out bool changed)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            ValidateSharedEnums(project);

            for (var index = 0; index < results.Count; index++)
            {
                ValidateWrite(project, results[index]);
            }

            ValidateGeneratedTypeNames(project, results);

            changed = false;
            var transaction = new GeneratedFileTransaction();
            try
            {
                for (var index = 0; index < results.Count; index++)
                {
                    WriteCore(project, results[index], transaction, out var sheetChanged);
                    changed |= sheetChanged;
                    reportProgress?.Invoke(index + 1, results.Count);
                }
            }
            catch (Exception exception)
            {
                changed = false;
                transaction.Rollback(exception);
                throw;
            }
        }

        private static void ValidateWrite(GoogleSheetProject project, GoogleSheetImportResult result)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (result.Document == null)
            {
                throw new InvalidOperationException("A prepared Google Sheet result is missing its document.");
            }

            if (!SheetDataCodeGenerator.TryCreateClassName(result.Document.Name, out var className, out var error))
            {
                throw new InvalidOperationException(error);
            }

            if (result.Fields.Find(field => field.IsKey) == null)
            {
                throw new InvalidOperationException($"The generated data does not contain an id field: {className}");
            }
        }

        private static void ValidateSharedEnums(GoogleSheetProject project)
        {
            foreach (var definition in project.SharedEnums)
            {
                if (definition == null || !SheetDataCodeGenerator.IsValidIdentifier(SheetDataCodeGenerator.GetEnumName(definition.Name)))
                {
                    throw new InvalidOperationException($"Shared enum name is invalid: '{definition?.Name}'");
                }

                if (definition.Values == null)
                {
                    throw new InvalidOperationException($"Shared enum '{definition.Name}' has no value list.");
                }

                var generatedValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var value in definition.Values)
                {
                    var generatedValue = SheetDataCodeGenerator.GetEnumName(value);

                    if (!SheetDataCodeGenerator.IsValidIdentifier(generatedValue))
                    {
                        throw new InvalidOperationException($"Shared enum '{definition.Name}' contains an invalid value: '{value}'");
                    }

                    if (!generatedValues.Add(generatedValue))
                    {
                        throw new InvalidOperationException($"Shared enum '{definition.Name}' generates a duplicate value: '{generatedValue}'");
                    }
                }
            }
        }

        private static void ValidateGeneratedTypeNames(
            GoogleSheetProject project,
            IReadOnlyList<GoogleSheetImportResult> results)
        {
            var existingTypes = ReadExistingGeneratedTypes(project, results);

            if (!SheetDataCodeGenerator.TryValidatePreparedTypeNames(
                    project.Sheets,
                    project.SharedEnums,
                    results,
                    existingTypes,
                    out var error))
            {
                throw new InvalidOperationException(error);
            }
        }

        private static List<KeyValuePair<string, string>> ReadExistingGeneratedTypes(
            GoogleSheetProject project,
            IReadOnlyList<GoogleSheetImportResult> results)
        {
            var existingTypes = new List<KeyValuePair<string, string>>();
            var rootPath = GoogleSheetPathUtility.GetAbsolutePath(GoogleSheetPathUtility.GetScriptOutputPath(project));

            if (!Directory.Exists(rootPath))
            {
                return existingTypes;
            }

            var expectedDataFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var expectedTableFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var overwrittenDataFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var sheet in project.Sheets)
            {
                if (sheet == null || !SheetDataCodeGenerator.TryCreateClassName(sheet.Name, out var className, out _))
                {
                    continue;
                }

                var sheetPath = Path.Combine(rootPath, className);
                expectedDataFiles.Add(Path.Combine(sheetPath, $"{className}.Data.g.cs"));
                expectedTableFiles.Add(Path.Combine(sheetPath, $"{className}Table.g.cs"));
            }

            foreach (var result in results)
            {
                SheetDataCodeGenerator.TryCreateClassName(result.Document.Name, out var className, out _);
                overwrittenDataFiles.Add(Path.Combine(rootPath, className, $"{className}.Data.g.cs"));
            }

            var replacesSharedEnums = project.SharedEnums.Count > 0;
            var legacySharedEnumFile = $"{GoogleSheetPathUtility.GetProjectName(project)}.SharedEnums.g.cs";
            var sharedEnumPath = Path.Combine(rootPath, "SharedEnums.g.cs");
            var legacySharedEnumPath = Path.Combine(rootPath, legacySharedEnumFile);
            var namespaceDeclaration = $"namespace {GoogleSheetPathUtility.GetTargetNamespace(project)}";

            var generatedFiles = Directory.GetFiles(rootPath, "*.g.cs", SearchOption.AllDirectories);
            Array.Sort(generatedFiles, StringComparer.OrdinalIgnoreCase);

            foreach (var filePath in generatedFiles)
            {
                var fileName = Path.GetFileName(filePath);
                var isDataFile = fileName.EndsWith(".Data.g.cs", StringComparison.OrdinalIgnoreCase);
                var isTableFile = fileName.EndsWith("Table.g.cs", StringComparison.OrdinalIgnoreCase);
                var isSharedEnumFile = string.Equals(fileName, "SharedEnums.g.cs", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, legacySharedEnumFile, StringComparison.OrdinalIgnoreCase);

                if ((!isDataFile && !isTableFile && !isSharedEnumFile) ||
                    overwrittenDataFiles.Contains(filePath) ||
                    (isTableFile && expectedTableFiles.Contains(filePath)) ||
                    (replacesSharedEnums &&
                        (string.Equals(filePath, sharedEnumPath, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(filePath, legacySharedEnumPath, StringComparison.OrdinalIgnoreCase))))
                {
                    continue;
                }

                var includeClasses = !expectedDataFiles.Contains(filePath);
                var isTargetNamespace = false;

                foreach (var line in File.ReadLines(filePath))
                {
                    var declaration = line.TrimStart();

                    if (declaration.StartsWith("namespace ", StringComparison.Ordinal))
                    {
                        isTargetNamespace = string.Equals(declaration.TrimEnd(), namespaceDeclaration, StringComparison.Ordinal);
                        continue;
                    }

                    if (!isTargetNamespace)
                    {
                        continue;
                    }

                    var prefix = declaration.StartsWith("public enum ", StringComparison.Ordinal)
                        ? "public enum "
                        : includeClasses && declaration.StartsWith("public partial class ", StringComparison.Ordinal)
                            ? "public partial class "
                            : includeClasses && declaration.StartsWith("public sealed partial class ", StringComparison.Ordinal)
                                ? "public sealed partial class "
                                : null;

                    if (prefix == null)
                    {
                        continue;
                    }

                    var nameStart = prefix.Length;
                    var nameEnd = nameStart;

                    while (nameEnd < declaration.Length &&
                        (char.IsLetterOrDigit(declaration[nameEnd]) || declaration[nameEnd] == '_'))
                    {
                        nameEnd++;
                    }

                    if (nameEnd > nameStart)
                    {
                        var typeName = declaration.Substring(nameStart, nameEnd - nameStart);
                        existingTypes.Add(new KeyValuePair<string, string>(typeName, $"generated file '{filePath}'"));
                    }
                }
            }

            return existingTypes;
        }

        private static bool WriteSharedEnums(GoogleSheetProject project, string targetNamespace, string rootAssetPath,
            GeneratedFileTransaction transaction)
        {
            if (project.SharedEnums.Count == 0)
            {
                return false;
            }

            var outputDirectory = GoogleSheetPathUtility.GetAbsolutePath(rootAssetPath);
            var legacyFilePath = Path.Combine(outputDirectory, $"{GoogleSheetPathUtility.GetProjectName(project)}.SharedEnums.g.cs");
            var filePath = Path.Combine(outputDirectory, "SharedEnums.g.cs");

            var builder = new StringBuilder();
            builder.AppendLine($"namespace {targetNamespace}");
            builder.AppendLine("{");
            builder.AppendLine("    // Generated shared enums. Do not edit.");

            foreach (var definition in project.SharedEnums)
            {
                builder.AppendLine($"    public enum {SheetDataCodeGenerator.GetEnumName(definition.Name)}");
                builder.AppendLine("    {");

                for (var index = 0; index < definition.Values.Count; index++)
                {
                    builder.AppendLine($"        {SheetDataCodeGenerator.GetEnumName(definition.Values[index])} = {index},");
                }

                builder.AppendLine("    }");
                builder.AppendLine();
            }

            builder.AppendLine("}");
            var changed = transaction.Delete(legacyFilePath);
            changed |= transaction.Delete($"{legacyFilePath}.meta");
            return transaction.WriteIfChanged(filePath, builder.ToString(), utf8WithoutBom) || changed;
        }

        private static string CreateFunctionsCode(string targetNamespace, string className)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"namespace {targetNamespace}");
            builder.AppendLine("{");
            builder.AppendLine($"    public partial class {className} // User functions.");
            builder.AppendLine("    {");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        internal static string CreateTableCode(string targetNamespace, string className, string keyFieldName)
        {
            var builder = new StringBuilder();
            builder.AppendLine("using System;");
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using UnityEngine;");
            builder.AppendLine();
            builder.AppendLine($"namespace {targetNamespace}");
            builder.AppendLine("{");
            builder.AppendLine($"    public sealed partial class {className}Table : global::PschLib.GoogleSheets.SheetTableBase, global::UnityEngine.ISerializationCallbackReceiver");
            builder.AppendLine("    {");
            builder.AppendLine($"        [global::UnityEngine.SerializeField] private global::System.Collections.Generic.List<{className}> rows = new global::System.Collections.Generic.List<{className}>();");
            builder.AppendLine($"        [global::System.NonSerialized] private global::System.Collections.Generic.Dictionary<string, {className}> byId;");
            builder.AppendLine();
            builder.AppendLine($"        public global::System.Collections.Generic.IReadOnlyList<{className}> Rows => rows;");
            builder.AppendLine($"        public global::System.Collections.Generic.IReadOnlyDictionary<string, {className}> ById");
            builder.AppendLine("        {");
            builder.AppendLine("            get");
            builder.AppendLine("            {");
            builder.AppendLine("                EnsureLookup();");
            builder.AppendLine("                return byId;");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine($"        public bool TryGet(string id, out {className} value)");
            builder.AppendLine("        {");
            builder.AppendLine("            if (string.IsNullOrWhiteSpace(id))");
            builder.AppendLine("            {");
            builder.AppendLine("                value = null;");
            builder.AppendLine("                return false;");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            EnsureLookup();");
            builder.AppendLine("            return byId.TryGetValue(id.Trim(), out value);");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine($"        public {className} Get(string id)");
            builder.AppendLine("        {");
            builder.AppendLine("            if (TryGet(id, out var value))");
            builder.AppendLine("            {");
            builder.AppendLine("                return value;");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine($"            throw new global::System.Collections.Generic.KeyNotFoundException($\"{className} ID '{{id}}' was not found.\");");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        private void EnsureLookup()");
            builder.AppendLine("        {");
            builder.AppendLine("            if (byId != null)");
            builder.AppendLine("            {");
            builder.AppendLine("                return;");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine($"            byId = new global::System.Collections.Generic.Dictionary<string, {className}>(rows.Count, global::System.StringComparer.OrdinalIgnoreCase);");
            builder.AppendLine();
            builder.AppendLine("            foreach (var row in rows)");
            builder.AppendLine("            {");
            builder.AppendLine($"                if (row == null || string.IsNullOrWhiteSpace(row.{keyFieldName}))");
            builder.AppendLine("                {");
            builder.AppendLine("                    continue;");
            builder.AppendLine("                }");
            builder.AppendLine();
            builder.AppendLine($"                byId.Add(row.{keyFieldName}.Trim(), row);");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        void global::UnityEngine.ISerializationCallbackReceiver.OnBeforeSerialize()");
            builder.AppendLine("        {");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        void global::UnityEngine.ISerializationCallbackReceiver.OnAfterDeserialize()");
            builder.AppendLine("        {");
            builder.AppendLine("            byId = null;");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }
    }
}
