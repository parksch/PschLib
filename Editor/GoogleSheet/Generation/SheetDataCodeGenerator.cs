using System;
using System.Collections.Generic;
using System.Text;

namespace PschLib
{
    public static class SheetDataCodeGenerator
    {
        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
            "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
            "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
            "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
            "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof",
            "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
        };

        public static bool TryGenerate(string sheetName, IReadOnlyList<SheetField> fields, string targetNamespace, out string code, out string error)
        {
            code = null;
            error = null;

            if (!TryCreateClassName(sheetName, out var className, out error))
            {
                return false;
            }

            if (!IsValidNamespace(targetNamespace))
            {
                error = $"'{targetNamespace}' is not a valid namespace.";
                return false;
            }

            if (fields == null || fields.Count == 0)
            {
                error = $"[{sheetName}] No fields are available for code generation.";
                return false;
            }

            var fieldNames = new HashSet<string>(StringComparer.Ordinal);
            var builder = new StringBuilder();
            builder.AppendLine("using System;");
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine();
            builder.AppendLine($"namespace {targetNamespace}");
            builder.AppendLine("{");
            builder.AppendLine("    [Serializable]");
            builder.AppendLine($"    public partial class {className} // Generated data. Do not edit.");
            builder.AppendLine("    {");

            foreach (var field in fields)
            {
                if (field == null || !IsValidIdentifier(field.Name))
                {
                    error = $"'{field?.Name}' is not a valid C# field name.";
                    return false;
                }

                if (!fieldNames.Add(field.Name))
                {
                    error = $"Field '{field.Name}' is duplicated.";
                    return false;
                }

                if (!TryGetTypeName(field.Type, out var typeName, out var typeError))
                {
                    error = $"[{sheetName}] Field '{field.Name}': {typeError}";
                    return false;
                }

                builder.AppendLine($"        public {typeName} {field.Name};");
            }

            builder.AppendLine("    }");
            builder.AppendLine("}");
            code = builder.ToString();
            return true;
        }

        private static bool TryGetTypeName(SheetTypeInfo typeInfo, out string typeName, out string error)
        {
            typeName = null;
            error = null;

            if (typeInfo == null)
            {
                error = "Type information is missing.";
                return false;
            }

            if (typeInfo.Kind == SheetTypeKind.Enum || typeInfo.Kind == SheetTypeKind.EnumList)
            {
                error = "Enum code generation is not implemented yet.";
                return false;
            }

            if (typeInfo.ElementType == null)
            {
                error = "Element type information is missing.";
                return false;
            }

            typeName = typeInfo.Kind == SheetTypeKind.List ? $"List<{typeInfo.ElementType.CSharpName}>" : typeInfo.ElementType.CSharpName;
            return true;
        }

        public static bool TryCreateClassName(string sheetName, out string className, out string error)
        {
            className = null;
            error = null;

            if (string.IsNullOrWhiteSpace(sheetName))
            {
                error = "The sheet name is empty.";
                return false;
            }

            var builder = new StringBuilder();
            var makeUpper = true;

            foreach (var character in sheetName.Trim())
            {
                if (!char.IsLetterOrDigit(character))
                {
                    makeUpper = true;
                    continue;
                }

                builder.Append(makeUpper ? char.ToUpperInvariant(character) : character);
                makeUpper = false;
            }

            if (builder.Length == 0)
            {
                error = $"A class name cannot be created from '{sheetName}'.";
                return false;
            }

            if (char.IsDigit(builder[0]))
            {
                builder.Insert(0, "Sheet");
            }

            className = builder.ToString();

            if (!IsValidIdentifier(className))
            {
                error = $"'{className}' is not a valid C# class name.";
                className = null;
                return false;
            }

            return true;
        }

        private static bool IsValidNamespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (var section in value.Split('.'))
            {
                if (!IsValidIdentifier(section))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || (!char.IsLetter(value[0]) && value[0] != '_'))
            {
                return false;
            }

            for (var index = 1; index < value.Length; index++)
            {
                if (!char.IsLetterOrDigit(value[index]) && value[index] != '_')
                {
                    return false;
                }
            }

            return !CSharpKeywords.Contains(value);
        }
    }
}
