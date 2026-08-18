using System;
using System.Collections.Generic;
using UnityEditor;

namespace PschLib.GoogleSheets
{
    internal static class SheetSharedEnumCatalog
    {
        public static bool TryUpdate(GoogleSheetProject project, string sheetName, IReadOnlyList<SheetField> fields, IReadOnlyList<SheetDataRow> rows, out string error)
        {
            error = null;
            var changed = false;

            if (!SheetDataCodeGenerator.TryCreateClassName(sheetName, out var currentClassName, out error))
            {
                return false;
            }

            foreach (var field in fields)
            {
                if (field.Type.EnumMode == SheetEnumMode.Local && Find(project.SharedEnums, $"{currentClassName}{field.Name}") != null)
                {
                    error = $"Local enum '{currentClassName}{field.Name}' conflicts with an existing shared enum.";
                    return false;
                }

                if (field.Type.EnumMode != SheetEnumMode.Shared)
                {
                    continue;
                }

                var enumTypeName = GetEnumTypeName(field);

                foreach (var localField in fields)
                {
                    if (localField.Type.EnumMode == SheetEnumMode.Local && $"{currentClassName}{localField.Name}" == enumTypeName)
                    {
                        error = $"Shared enum '{enumTypeName}' conflicts with local enum '{currentClassName}{localField.Name}'.";
                        return false;
                    }
                }

                if (!SheetDataCodeGenerator.IsValidIdentifier(enumTypeName))
                {
                    error = $"'{enumTypeName}' is not a valid shared enum name.";
                    return false;
                }

                foreach (var sheet in project.Sheets)
                {
                    if (SheetDataCodeGenerator.TryCreateClassName(sheet.Name, out var className, out _) && className == enumTypeName)
                    {
                        error = $"Shared enum '{enumTypeName}' conflicts with Sheet class '{className}'.";
                        return false;
                    }
                }

                var definition = Find(project.SharedEnums, enumTypeName);

                if (definition == null)
                {
                    definition = new SheetSharedEnumDefinition
                    {
                        Name = enumTypeName
                    };
                    project.SharedEnums.Add(definition);
                    changed = true;
                }
                else if (definition.Name != enumTypeName)
                {
                    error = $"Shared enum casing does not match. Use '{definition.Name}' instead of '{enumTypeName}'.";
                    return false;
                }

                var existingValues = new HashSet<string>(definition.Values, StringComparer.OrdinalIgnoreCase);

                foreach (var row in rows)
                {
                    if (!row.Values.TryGetValue(field, out var rawValue))
                    {
                        continue;
                    }

                    if (field.Type.Kind == SheetTypeKind.Enum)
                    {
                        if (!TryAppend(definition, existingValues, (string)rawValue, row.RowNumber, field.Name, ref changed, out error))
                        {
                            return false;
                        }

                        continue;
                    }

                    foreach (var value in (string[])rawValue)
                    {
                        if (!TryAppend(definition, existingValues, value, row.RowNumber, field.Name, ref changed, out error))
                        {
                            return false;
                        }
                    }
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(project);
                AssetDatabase.SaveAssets();
            }

            return true;
        }

        private static string GetEnumTypeName(SheetField field)
        {
            return string.IsNullOrWhiteSpace(field.Type.EnumTypeName) ? field.Name : field.Type.EnumTypeName;
        }

        private static SheetSharedEnumDefinition Find(List<SheetSharedEnumDefinition> definitions, string name)
        {
            foreach (var definition in definitions)
            {
                if (definition != null && string.Equals(definition.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return definition;
                }
            }

            return null;
        }

        private static bool TryAppend(SheetSharedEnumDefinition definition, HashSet<string> existingValues, string value, int rowNumber, string fieldName, ref bool changed, out string error)
        {
            error = null;

            if (!SheetDataCodeGenerator.IsValidIdentifier(value))
            {
                error = $"Row {rowNumber}, field '{fieldName}': '{value}' is not a valid enum value.";
                return false;
            }

            if (existingValues.Add(value))
            {
                definition.Values.Add(value);
                changed = true;
            }

            return true;
        }
    }
}
