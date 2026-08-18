using System;
using System.IO;
using System.Text;

namespace PschLib.GoogleSheets
{
    internal static class SheetCodeFileWriter
    {
        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

        public static string Write(GoogleSheetProject project, GoogleSheetImportResult result)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (!SheetDataCodeGenerator.TryCreateClassName(result.Document.Name, out var className, out var classNameError))
            {
                throw new InvalidOperationException(classNameError);
            }

            var targetNamespace = GoogleSheetPathUtility.GetTargetNamespace(project);
            var rootAssetPath = GoogleSheetPathUtility.GetScriptOutputPath(project);
            var sheetAssetPath = $"{rootAssetPath}/{className}";
            var sheetDirectory = GoogleSheetPathUtility.GetAbsolutePath(sheetAssetPath);
            Directory.CreateDirectory(sheetDirectory);
            WriteSharedEnums(project, targetNamespace, rootAssetPath);

            var dataFileName = $"{className}.Data.g.cs";
            var functionsFileName = $"{className}.Functions.cs";
            var tableFileName = $"{className}Table.g.cs";
            var keyField = result.Fields.Find(field => field.IsKey);

            if (keyField == null)
            {
                throw new InvalidOperationException($"The generated data does not contain an id field: {className}");
            }

            File.WriteAllText(Path.Combine(sheetDirectory, dataFileName), result.GeneratedCode, Utf8WithoutBom);

            if (project.GenerateScriptableObject)
            {
                File.WriteAllText(Path.Combine(sheetDirectory, tableFileName), CreateTableCode(targetNamespace, className, keyField.Name), Utf8WithoutBom);
            }

            var functionsPath = Path.Combine(sheetDirectory, functionsFileName);

            if (!File.Exists(functionsPath))
            {
                File.WriteAllText(functionsPath, CreateFunctionsCode(targetNamespace, className), Utf8WithoutBom);
            }

            return $"{sheetAssetPath}/{dataFileName}";
        }

        private static void WriteSharedEnums(GoogleSheetProject project, string targetNamespace, string rootAssetPath)
        {
            if (project.SharedEnums.Count == 0)
            {
                return;
            }

            var outputDirectory = GoogleSheetPathUtility.GetAbsolutePath(rootAssetPath);
            var legacyFilePath = Path.Combine(outputDirectory, $"{GoogleSheetPathUtility.GetProjectName(project)}.SharedEnums.g.cs");
            var filePath = Path.Combine(outputDirectory, "SharedEnums.g.cs");

            DeleteLegacyGeneratedFile(legacyFilePath);

            var builder = new StringBuilder();
            builder.AppendLine($"namespace {targetNamespace}");
            builder.AppendLine("{");
            builder.AppendLine("    // Generated shared enums. Do not edit.");

            foreach (var definition in project.SharedEnums)
            {
                if (definition == null || !SheetDataCodeGenerator.IsValidIdentifier(definition.Name))
                {
                    throw new InvalidOperationException($"Shared enum name is invalid: '{definition?.Name}'");
                }

                builder.AppendLine($"    public enum {definition.Name}");
                builder.AppendLine("    {");

                for (var index = 0; index < definition.Values.Count; index++)
                {
                    if (!SheetDataCodeGenerator.IsValidIdentifier(definition.Values[index]))
                    {
                        throw new InvalidOperationException($"Shared enum '{definition.Name}' contains an invalid value: '{definition.Values[index]}'");
                    }

                    builder.AppendLine($"        {definition.Values[index]} = {index},");
                }

                builder.AppendLine("    }");
                builder.AppendLine();
            }

            builder.AppendLine("}");
            File.WriteAllText(filePath, builder.ToString(), Utf8WithoutBom);
        }

        private static void DeleteLegacyGeneratedFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            var metaPath = $"{filePath}.meta";

            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
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

        private static string CreateTableCode(string targetNamespace, string className, string keyFieldName)
        {
            var builder = new StringBuilder();
            builder.AppendLine("using System;");
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using UnityEngine;");
            builder.AppendLine();
            builder.AppendLine($"namespace {targetNamespace}");
            builder.AppendLine("{");
            builder.AppendLine($"    public sealed partial class {className}Table : PschLib.GoogleSheets.SheetTableBase, ISerializationCallbackReceiver");
            builder.AppendLine("    {");
            builder.AppendLine($"        [SerializeField] private List<{className}> rows = new List<{className}>();");
            builder.AppendLine($"        [NonSerialized] private Dictionary<string, {className}> byId;");
            builder.AppendLine();
            builder.AppendLine($"        public IReadOnlyList<{className}> Rows => rows;");
            builder.AppendLine($"        public IReadOnlyDictionary<string, {className}> ById");
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
            builder.AppendLine($"            throw new KeyNotFoundException($\"{className} ID '{{id}}' was not found.\");");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        private void EnsureLookup()");
            builder.AppendLine("        {");
            builder.AppendLine("            if (byId != null)");
            builder.AppendLine("            {");
            builder.AppendLine("                return;");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine($"            byId = new Dictionary<string, {className}>(rows.Count, StringComparer.OrdinalIgnoreCase);");
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
            builder.AppendLine("        void ISerializationCallbackReceiver.OnBeforeSerialize()");
            builder.AppendLine("        {");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        void ISerializationCallbackReceiver.OnAfterDeserialize()");
            builder.AppendLine("        {");
            builder.AppendLine("            byId = null;");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }
    }
}
