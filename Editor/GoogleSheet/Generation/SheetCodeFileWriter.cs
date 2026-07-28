using System;
using System.IO;
using System.Text;

namespace PschLib
{
    internal static class SheetCodeFileWriter
    {
        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

        public static string Write(GoogleSheetSettings settings, GoogleSheetImportResult result)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (!SheetDataCodeGenerator.TryCreateClassName(result.Document.Name, out var className, out var classNameError))
            {
                throw new InvalidOperationException(classNameError);
            }

            var rootAssetPath = GoogleSheetPathUtility.NormalizeAssetFolder(settings.ScriptOutputPath, "script output");
            var sheetAssetPath = $"{rootAssetPath}/{className}";
            var sheetDirectory = GoogleSheetPathUtility.GetAbsolutePath(sheetAssetPath);
            Directory.CreateDirectory(sheetDirectory);

            var dataFileName = $"{className}.Data.g.cs";
            var functionsFileName = $"{className}.Functions.cs";
            var tableFileName = $"{className}Table.g.cs";
            File.WriteAllText(Path.Combine(sheetDirectory, dataFileName), result.GeneratedCode, Utf8WithoutBom);
            File.WriteAllText(Path.Combine(sheetDirectory, tableFileName), CreateTableCode(settings.TargetNamespace, className), Utf8WithoutBom);

            var functionsPath = Path.Combine(sheetDirectory, functionsFileName);

            if (!File.Exists(functionsPath))
            {
                File.WriteAllText(functionsPath, CreateFunctionsCode(settings.TargetNamespace, className), Utf8WithoutBom);
            }

            return $"{sheetAssetPath}/{dataFileName}";
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

        private static string CreateTableCode(string targetNamespace, string className)
        {
            var builder = new StringBuilder();
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using UnityEngine;");
            builder.AppendLine();
            builder.AppendLine($"namespace {targetNamespace}");
            builder.AppendLine("{");
            builder.AppendLine($"    public sealed partial class {className}Table : ScriptableObject");
            builder.AppendLine("    {");
            builder.AppendLine($"        [SerializeField] private List<{className}> rows = new List<{className}>();");
            builder.AppendLine($"        public IReadOnlyList<{className}> Rows => rows;");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }
    }
}
