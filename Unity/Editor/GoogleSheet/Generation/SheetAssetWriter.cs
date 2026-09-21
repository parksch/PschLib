using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PschLib.GoogleSheets
{
    internal sealed class SheetAssetWriteFailure
    {
        public SheetAssetWriteFailure(GoogleSheetImportResult result, Exception exception)
        {
            Result = result;
            Exception = exception;
        }

        public GoogleSheetImportResult Result { get; }
        public Exception Exception { get; }
    }

    internal sealed class SheetAssetBatchWriteResult
    {
        public int SuccessCount { get; set; }
        public List<SheetAssetWriteFailure> Failures { get; } = new List<SheetAssetWriteFailure>();
        public bool HasFailures => Failures.Count > 0;
    }

    internal static class SheetAssetWriter
    {
        public static SheetAssetBatchWriteResult WriteAll(
            GoogleSheetProject project,
            IReadOnlyList<GoogleSheetImportResult> results,
            Action<int, int> reportProgress = null)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            var batchResult = new SheetAssetBatchWriteResult();

            for (var index = 0; index < results.Count; index++)
            {
                var result = results[index];

                try
                {
                    var assetPath = Write(project, result);
                    batchResult.SuccessCount++;
                    Debug.Log($"Google Sheet asset generated: {assetPath}");
                }
                catch (Exception exception)
                {
                    batchResult.Failures.Add(new SheetAssetWriteFailure(result, exception));
                    Debug.LogError($"Google Sheet asset generation failed for '{GetSheetName(result)}': {exception}");
                }

                reportProgress?.Invoke(index + 1, results.Count);
            }

            return batchResult;
        }

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
            var createdAsset = false;
            object previousRows = null;

            if (tableAsset == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                {
                    throw new InvalidOperationException($"An incompatible asset already exists at '{assetPath}'.");
                }

                tableAsset = ScriptableObject.CreateInstance(tableType);
                AssetDatabase.CreateAsset(tableAsset, assetPath);
                createdAsset = true;
            }

            if (tableAsset.GetType() != tableType)
            {
                throw new InvalidOperationException($"The existing asset type does not match {tableType.FullName}: '{assetPath}'.");
            }

            if (!createdAsset)
            {
                previousRows = rowsField.GetValue(tableAsset);
            }

            try
            {
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
            catch
            {
                RollbackAsset(tableAsset, rowsField, assetPath, createdAsset, previousRows);
                throw;
            }
        }

        private static void RollbackAsset(
            ScriptableObject tableAsset,
            FieldInfo rowsField,
            string assetPath,
            bool createdAsset,
            object previousRows)
        {
            try
            {
                if (createdAsset)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                    return;
                }

                if (tableAsset == null || rowsField == null)
                {
                    return;
                }

                rowsField.SetValue(tableAsset, previousRows);
                EditorUtility.SetDirty(tableAsset);
                AssetDatabase.SaveAssets();
            }
            catch (Exception rollbackException)
            {
                Debug.LogError($"Failed to roll back Google Sheet asset '{assetPath}': {rollbackException}");
            }
        }

        private static string GetSheetName(GoogleSheetImportResult result)
        {
            return result?.Document == null ? "Unknown" : result.Document.Name;
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
