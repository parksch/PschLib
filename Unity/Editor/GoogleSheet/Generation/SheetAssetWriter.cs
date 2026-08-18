using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PschLib.GoogleSheets
{
    internal static class SheetAssetWriter
    {
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
            var dataType = FindType($"{targetNamespace}.{className}");
            var tableType = FindType($"{targetNamespace}.{className}Table");

            if (dataType == null)
            {
                throw new InvalidOperationException($"Generated data type was not found: {targetNamespace}.{className}");
            }

            if (tableType == null || !typeof(ScriptableObject).IsAssignableFrom(tableType))
            {
                throw new InvalidOperationException($"Generated table type was not found: {targetNamespace}.{className}Table");
            }

            var rowsField = tableType.GetField("rows", BindingFlags.Instance | BindingFlags.NonPublic);

            if (rowsField == null)
            {
                throw new InvalidOperationException($"The generated table type does not contain a rows field: {tableType.FullName}");
            }

            var rows = CreateRows(dataType, result);
            var assetFolder = GoogleSheetPathUtility.GetAssetOutputPath(project);
            GoogleSheetPathUtility.EnsureAssetFolder(assetFolder);

            var assetPath = $"{assetFolder}/{className}Table.asset";
            var tableAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);

            if (tableAsset == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                {
                    throw new InvalidOperationException($"An incompatible asset already exists at '{assetPath}'.");
                }

                tableAsset = ScriptableObject.CreateInstance(tableType);
                AssetDatabase.CreateAsset(tableAsset, assetPath);
            }

            if (tableAsset.GetType() != tableType)
            {
                throw new InvalidOperationException($"The existing asset type does not match {tableType.FullName}: '{assetPath}'.");
            }

            Undo.RecordObject(tableAsset, $"Import {className} Sheet");
            rowsField.SetValue(tableAsset, rows);

            if (tableAsset is ISerializationCallbackReceiver serializationCallbackReceiver)
            {
                serializationCallbackReceiver.OnAfterDeserialize();
            }

            EditorUtility.SetDirty(tableAsset);
            AssetDatabase.SaveAssets();
            return assetPath;
        }

        private static IList CreateRows(Type dataType, GoogleSheetImportResult result)
        {
            var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(dataType);
            var rows = (IList)Activator.CreateInstance(listType);

            foreach (var sourceRow in result.Rows)
            {
                var data = Activator.CreateInstance(dataType);

                foreach (var pair in sourceRow.Values)
                {
                    var field = dataType.GetField(pair.Key.Name, BindingFlags.Instance | BindingFlags.Public);

                    if (field == null)
                    {
                        throw new InvalidOperationException($"Generated field was not found: {dataType.FullName}.{pair.Key.Name}");
                    }

                    field.SetValue(data, ConvertValue(pair.Value, field.FieldType));
                }

                rows.Add(data);
            }

            return rows;
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, (string)value, true);
            }

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
            {
                var elementType = targetType.GetGenericArguments()[0];

                if (elementType.IsEnum)
                {
                    var result = (IList)Activator.CreateInstance(targetType);

                    foreach (var enumValue in (string[])value)
                    {
                        result.Add(Enum.Parse(elementType, enumValue, true));
                    }

                    return result;
                }
            }

            return value;
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);

                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
