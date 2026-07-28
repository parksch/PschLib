using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PschLib
{
    internal static class SheetAssetWriter
    {
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

            var dataType = FindType($"{settings.TargetNamespace}.{className}");
            var tableType = FindType($"{settings.TargetNamespace}.{className}Table");

            if (dataType == null)
            {
                throw new InvalidOperationException($"Generated data type was not found: {settings.TargetNamespace}.{className}");
            }

            if (tableType == null || !typeof(ScriptableObject).IsAssignableFrom(tableType))
            {
                throw new InvalidOperationException($"Generated table type was not found: {settings.TargetNamespace}.{className}Table");
            }

            var rowsField = tableType.GetField("rows", BindingFlags.Instance | BindingFlags.NonPublic);

            if (rowsField == null)
            {
                throw new InvalidOperationException($"The generated table type does not contain a rows field: {tableType.FullName}");
            }

            var rows = CreateRows(dataType, result);
            var assetFolder = GoogleSheetPathUtility.NormalizeAssetFolder(settings.AssetOutputPath, "asset output");
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

                    field.SetValue(data, pair.Value);
                }

                rows.Add(data);
            }

            return rows;
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
