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
            var updatedDefinitions = Clone(project.SharedEnums);

            if (!SheetDataCodeGenerator.TryCreateClassName(sheetName, out var currentClassName, out error))
            {
                return false;
            }

            foreach (var field in fields)
            {
                var localEnumName = SheetDataCodeGenerator.GetLocalEnumName(currentClassName, field.Name);

                if (field.Type.EnumMode == SheetEnumMode.Local && Find(updatedDefinitions, localEnumName) != null)
                {
                    error = $"Local enum '{localEnumName}' conflicts with an existing shared enum.";
                    return false;
                }

                if (field.Type.EnumMode != SheetEnumMode.Shared)
                {
                    continue;
                }

                var enumTypeName = GetEnumTypeName(field);

                foreach (var localField in fields)
                {
                    var otherLocalEnumName = SheetDataCodeGenerator.GetLocalEnumName(currentClassName, localField.Name);

                    if (localField.Type.EnumMode == SheetEnumMode.Local && otherLocalEnumName == enumTypeName)
                    {
                        error = $"Shared enum '{enumTypeName}' conflicts with local enum '{otherLocalEnumName}'.";
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

                var definition = Find(updatedDefinitions, enumTypeName);

                if (definition == null)
                {
                    definition = new SheetSharedEnumDefinition
                    {
                        Name = enumTypeName
                    };
                    updatedDefinitions.Add(definition);
                    changed = true;
                }
                else if (definition.Name != enumTypeName)
                {
                    var generatedDefinitionName = SheetDataCodeGenerator.GetEnumName(definition.Name);

                    if (generatedDefinitionName != enumTypeName)
                    {
                        error = $"Shared enum casing does not match. Use '{generatedDefinitionName}' instead of '{enumTypeName}'.";
                        return false;
                    }

                    definition.Name = enumTypeName;
                    changed = true;
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
                Replace(project.SharedEnums, updatedDefinitions);
                EditorUtility.SetDirty(project);
                AssetDatabase.SaveAssets();
            }

            return true;
        }

        public static List<SheetSharedEnumDefinition> CreateSnapshot(GoogleSheetProject project)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            return Clone(project.SharedEnums);
        }

        public static void RestoreSnapshot(GoogleSheetProject project, List<SheetSharedEnumDefinition> snapshot)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            Replace(project.SharedEnums, Clone(snapshot));
            EditorUtility.SetDirty(project);
            AssetDatabase.SaveAssets();
        }

        private static List<SheetSharedEnumDefinition> Clone(List<SheetSharedEnumDefinition> source)
        {
            var result = new List<SheetSharedEnumDefinition>(source.Count);

            foreach (var definition in source)
            {
                if (definition == null)
                {
                    result.Add(null);
                    continue;
                }

                result.Add(new SheetSharedEnumDefinition
                {
                    Name = definition.Name,
                    Values = definition.Values == null
                        ? new List<string>()
                        : new List<string>(definition.Values)
                });
            }

            return result;
        }

        private static void Replace(List<SheetSharedEnumDefinition> target, List<SheetSharedEnumDefinition> source)
        {
            target.Clear();
            target.AddRange(source);
        }

        private static string GetEnumTypeName(SheetField field)
        {
            var name = string.IsNullOrWhiteSpace(field.Type.EnumTypeName) ? field.Name : field.Type.EnumTypeName;
            return SheetDataCodeGenerator.GetEnumName(name);
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

            var enumValueName = SheetDataCodeGenerator.GetEnumName(value);

            if (!SheetDataCodeGenerator.IsValidIdentifier(enumValueName))
            {
                error = $"Row {rowNumber}, field '{fieldName}': '{value}' is not a valid enum value.";
                return false;
            }

            if (existingValues.Add(value))
            {
                definition.Values.Add(enumValueName);
                changed = true;
            }

            return true;
        }
    }
}
