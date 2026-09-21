using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PschLib.GoogleSheets
{
    internal enum GoogleSheetImportState
    {
        None,
        GeneratingCode,
        WaitingForCompilation,
        CreatingAssets,
        Completed,
        Failed,
        PreparingData,
        CompilationFailed,
        PartialFailure
    }

    [InitializeOnLoad]
    internal static class GoogleSheetPendingImportProcessor
    {
        private const string sessionKey = "PschLib.GoogleSheet.PendingImports";
        private const string stateKey = "PschLib.GoogleSheet.ImportState";
        private const string statusKey = "PschLib.GoogleSheet.ImportStatus";
        private static bool scheduled;
        private static bool processing;

        public static GoogleSheetImportState State => (GoogleSheetImportState)SessionState.GetInt(stateKey, (int)GoogleSheetImportState.None);
        public static string StatusMessage => SessionState.GetString(statusKey, "Preparing Google Sheet import...");
        public static bool IsFinished => State == GoogleSheetImportState.Completed || State == GoogleSheetImportState.Failed || State == GoogleSheetImportState.CompilationFailed || State == GoogleSheetImportState.PartialFailure;
        public static bool IsImporting => State == GoogleSheetImportState.PreparingData || State == GoogleSheetImportState.GeneratingCode || State == GoogleSheetImportState.WaitingForCompilation || State == GoogleSheetImportState.CreatingAssets;
        public static bool HasPendingImport => HasPendingImports();

        [Serializable]
        private sealed class PendingImports
        {
            public string ProjectAssetPath;
            public string SnapshotFilePath;
        }

        [Serializable]
        private sealed class ImportSnapshot
        {
            public string ProjectAssetPath;
            public string ProjectKey;
            public string RootNamespace;
            public string ScriptOutputPath;
            public string AssetOutputPath;
            public bool GenerateScriptableObject;
            public int State;
            public List<SheetSnapshot> Sheets = new List<SheetSnapshot>();
        }

        [Serializable]
        private sealed class SheetSnapshot
        {
            public string Name;
            public List<RowSnapshot> Rows = new List<RowSnapshot>();
        }

        [Serializable]
        private sealed class RowSnapshot
        {
            public List<string> Cells = new List<string>();
        }

        static GoogleSheetPendingImportProcessor()
        {
            if (!HasPendingImports())
            {
                TryRestorePendingImport();
            }

            if (HasPendingImports())
            {
                EditorApplication.delayCall += GoogleSheetImportProgressWindow.Open;

                if (State == GoogleSheetImportState.WaitingForCompilation || State == GoogleSheetImportState.CreatingAssets)
                {
                    Schedule();
                }
            }
        }

        public static void BeginCodeGeneration(int sheetCount)
        {
            CleanupPending();
            SetState(GoogleSheetImportState.PreparingData, $"Downloading and validating sheet data... (0/{sheetCount})");
            GoogleSheetImportProgressWindow.Open();
        }

        public static void ReportCodeGenerated(int completedCount, int sheetCount)
        {
            SetState(GoogleSheetImportState.GeneratingCode, $"Generating code... ({completedCount}/{sheetCount})");
        }

        public static void CompleteWithoutAssets(int sheetCount)
        {
            SetState(GoogleSheetImportState.Completed, $"Import completed. Generated code for {sheetCount} sheet(s).");
        }

        public static void CompleteAssets(int sheetCount)
        {
            SetState(GoogleSheetImportState.Completed, $"Import completed. Updated {sheetCount} ScriptableObject asset(s) without recompiling unchanged code.");
        }

        public static void ReportFailure(Exception exception)
        {
            SetState(GoogleSheetImportState.Failed, $"Import failed.\n{exception.Message}");
        }

        public static void Queue(GoogleSheetProject project, IReadOnlyList<GoogleSheetImportResult> results)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (results == null || results.Count == 0)
            {
                throw new ArgumentException("At least one prepared sheet is required.", nameof(results));
            }

            var projectAssetPath = AssetDatabase.GetAssetPath(project);

            if (string.IsNullOrWhiteSpace(projectAssetPath))
            {
                throw new InvalidOperationException("Google Sheet Project must be saved as an asset.");
            }

            StorePendingSnapshot(project, projectAssetPath, results, GoogleSheetImportState.WaitingForCompilation);
            SetState(GoogleSheetImportState.WaitingForCompilation, $"Generated code for {results.Count} sheet(s). Waiting for Unity compilation...");
            GoogleSheetImportProgressWindow.Open();
            Schedule();
        }

        public static string RetainAssetFailures(GoogleSheetProject project, SheetAssetBatchWriteResult batchResult, int totalCount)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (batchResult == null || !batchResult.HasFailures)
            {
                throw new ArgumentException("At least one failed asset result is required.", nameof(batchResult));
            }

            var failedResults = new List<GoogleSheetImportResult>(batchResult.Failures.Count);
            var failedNames = new List<string>(batchResult.Failures.Count);

            foreach (var failure in batchResult.Failures)
            {
                failedResults.Add(failure.Result);
                failedNames.Add(failure.Result?.Document?.Name ?? "Unknown");
            }

            var projectAssetPath = AssetDatabase.GetAssetPath(project);

            if (string.IsNullOrWhiteSpace(projectAssetPath))
            {
                throw new InvalidOperationException("Google Sheet Project must be saved as an asset.");
            }

            StorePendingSnapshot(project, projectAssetPath, failedResults, GoogleSheetImportState.PartialFailure);
            var message = $"SO generation completed with errors. Success: {batchResult.SuccessCount}/{totalCount}, Failed: {batchResult.Failures.Count}/{totalCount}.\nFailed sheets: {string.Join(", ", failedNames)}\nFix the errors, then continue to retry only the failed sheets.";
            SetState(GoogleSheetImportState.PartialFailure, message);
            GoogleSheetImportProgressWindow.Open();
            return message;
        }

        public static void ContinuePending()
        {
            GoogleSheetImportProgressWindow.Open();

            if (!HasPendingImports())
            {
                SetState(GoogleSheetImportState.Failed, "No pending Google Sheet snapshot was found.");
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                SetState(GoogleSheetImportState.WaitingForCompilation, "Waiting for Unity compilation...");
                Schedule();
                return;
            }

            if (EditorUtility.scriptCompilationFailed)
            {
                SetState(GoogleSheetImportState.CompilationFailed, "Compilation still has errors. Fix them before continuing SO generation.");
                return;
            }

            SetState(GoogleSheetImportState.WaitingForCompilation, "Compilation succeeded. Preparing ScriptableObject assets...");
            Schedule();
        }

        public static void CancelPending()
        {
            CleanupPending();
            SetState(GoogleSheetImportState.Failed, "Pending Google Sheet import and its snapshot were deleted.");
        }

        public static void Schedule()
        {
            if (scheduled)
            {
                return;
            }

            scheduled = true;
            EditorApplication.update += TryProcess;
        }

        private static void TryProcess()
        {
            if (processing || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            EditorApplication.update -= TryProcess;
            scheduled = false;

            if (EditorUtility.scriptCompilationFailed)
            {
                SetState(GoogleSheetImportState.CompilationFailed, "Generated code did not compile. Fix the errors, then continue SO generation.");
                GoogleSheetImportProgressWindow.Open();
                return;
            }

            ProcessPending();
        }

        private static void ProcessPending()
        {
            processing = true;

            try
            {
                var pending = ReadPending();
                var project = AssetDatabase.LoadAssetAtPath<GoogleSheetProject>(pending.ProjectAssetPath);

                if (project == null)
                {
                    throw new InvalidOperationException($"Pending Google Sheet Project was not found: {pending.ProjectAssetPath}");
                }

                var snapshot = ReadSnapshot(pending.SnapshotFilePath);
                ValidateSnapshotSettings(project, pending.ProjectAssetPath, snapshot);
                var results = new List<GoogleSheetImportResult>(snapshot.Sheets.Count);

                foreach (var sheet in snapshot.Sheets)
                {
                    results.Add(GoogleSheetImportService.Prepare(project, CreateDocument(sheet)));
                }

                SetState(GoogleSheetImportState.CreatingAssets, $"Creating ScriptableObject assets... (0/{results.Count})");

                var batchResult = SheetAssetWriter.WriteAll(
                    project,
                    results,
                    (processed, total) =>
                    {
                        SetState(GoogleSheetImportState.CreatingAssets, $"Creating ScriptableObject assets... ({processed}/{total})");
                    });

                if (batchResult.HasFailures)
                {
                    RetainAssetFailures(project, batchResult, results.Count);
                    return;
                }

                CleanupPending();
                SetState(GoogleSheetImportState.Completed, $"Import completed. {batchResult.SuccessCount} ScriptableObject asset(s) were created or updated.");
            }
            catch (Exception exception)
            {
                SetState(GoogleSheetImportState.Failed, $"SO generation failed. The snapshot was kept for retry.\n{exception.Message}");
                Debug.LogError($"Google Sheet asset generation failed: {exception}");
            }
            finally
            {
                processing = false;
            }
        }

        private static void StorePendingSnapshot(
            GoogleSheetProject project,
            string projectAssetPath,
            IReadOnlyList<GoogleSheetImportResult> results,
            GoogleSheetImportState state)
        {
            var snapshotFilePath = WriteSnapshot(project, projectAssetPath, results, state);
            var pending = new PendingImports
            {
                ProjectAssetPath = projectAssetPath,
                SnapshotFilePath = snapshotFilePath
            };

            SessionState.SetString(sessionKey, JsonUtility.ToJson(pending));
        }

        private static string WriteSnapshot(
            GoogleSheetProject project,
            string projectAssetPath,
            IReadOnlyList<GoogleSheetImportResult> results,
            GoogleSheetImportState state)
        {
            var snapshot = new ImportSnapshot
            {
                ProjectAssetPath = projectAssetPath,
                ProjectKey = project.ProjectKey,
                RootNamespace = project.RootNamespace,
                ScriptOutputPath = project.ScriptOutputPath,
                AssetOutputPath = project.AssetOutputPath,
                GenerateScriptableObject = project.GenerateScriptableObject,
                State = (int)state
            };

            foreach (var result in results)
            {
                if (result?.Document == null)
                {
                    throw new InvalidOperationException("A prepared Google Sheet result is missing its document.");
                }

                var sheet = new SheetSnapshot { Name = result.Document.Name };

                foreach (var sourceRow in result.Document.Rows)
                {
                    var row = new RowSnapshot();

                    if (sourceRow != null)
                    {
                        row.Cells.AddRange(sourceRow);
                    }

                    sheet.Rows.Add(row);
                }

                snapshot.Sheets.Add(sheet);
            }

            var directory = GetSnapshotDirectory();
            Directory.CreateDirectory(directory);
            var filePath = Path.Combine(directory, "pending.json");
            File.WriteAllText(filePath, JsonUtility.ToJson(snapshot, true), new UTF8Encoding(false));
            return filePath;
        }

        private static ImportSnapshot ReadSnapshot(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                throw new InvalidOperationException($"Pending Google Sheet snapshot was not found: {filePath}");
            }

            var snapshot = JsonUtility.FromJson<ImportSnapshot>(File.ReadAllText(filePath));

            if (snapshot?.Sheets == null || snapshot.Sheets.Count == 0)
            {
                throw new InvalidOperationException("Pending Google Sheet snapshot does not contain any sheets.");
            }

            return snapshot;
        }

        private static void ValidateSnapshotSettings(GoogleSheetProject project, string projectAssetPath, ImportSnapshot snapshot)
        {
            if (!string.Equals(snapshot.ProjectAssetPath, projectAssetPath, StringComparison.Ordinal) ||
                !string.Equals(snapshot.ProjectKey, project.ProjectKey, StringComparison.Ordinal) ||
                !string.Equals(snapshot.RootNamespace, project.RootNamespace, StringComparison.Ordinal) ||
                !string.Equals(snapshot.ScriptOutputPath, project.ScriptOutputPath, StringComparison.Ordinal) ||
                !string.Equals(snapshot.AssetOutputPath, project.AssetOutputPath, StringComparison.Ordinal) ||
                snapshot.GenerateScriptableObject != project.GenerateScriptableObject)
            {
                throw new InvalidOperationException("Google Sheet generation settings changed after code generation. Restore the previous settings or cancel this import and generate again.");
            }
        }

        private static SheetDocument CreateDocument(SheetSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.Name) || snapshot.Rows == null)
            {
                throw new InvalidOperationException("Pending Google Sheet snapshot contains invalid sheet data.");
            }

            var rows = new List<List<string>>(snapshot.Rows.Count);

            foreach (var row in snapshot.Rows)
            {
                rows.Add(row?.Cells == null ? new List<string>() : new List<string>(row.Cells));
            }

            return new SheetDocument(snapshot.Name, rows);
        }

        private static bool HasPendingImports()
        {
            return !string.IsNullOrWhiteSpace(SessionState.GetString(sessionKey, string.Empty));
        }

        private static PendingImports ReadPending()
        {
            var json = SessionState.GetString(sessionKey, string.Empty);

            if (string.IsNullOrWhiteSpace(json))
            {
                return new PendingImports();
            }

            return JsonUtility.FromJson<PendingImports>(json) ?? new PendingImports();
        }

        private static void TryRestorePendingImport()
        {
            try
            {
                var snapshotFilePath = Path.Combine(GetSnapshotDirectory(), "pending.json");

                if (!File.Exists(snapshotFilePath))
                {
                    return;
                }

                var snapshot = ReadSnapshot(snapshotFilePath);

                if (string.IsNullOrWhiteSpace(snapshot.ProjectAssetPath))
                {
                    throw new InvalidOperationException("The pending snapshot does not contain a project asset path.");
                }

                var pending = new PendingImports
                {
                    ProjectAssetPath = snapshot.ProjectAssetPath,
                    SnapshotFilePath = snapshotFilePath
                };

                var restoredState = Enum.IsDefined(typeof(GoogleSheetImportState), snapshot.State)
                    ? (GoogleSheetImportState)snapshot.State
                    : GoogleSheetImportState.Failed;

                SessionState.SetString(sessionKey, JsonUtility.ToJson(pending));
                SessionState.SetInt(stateKey, (int)restoredState);
                SessionState.SetString(
                    statusKey,
                    restoredState == GoogleSheetImportState.WaitingForCompilation || restoredState == GoogleSheetImportState.CreatingAssets
                        ? "Recovered a pending Google Sheet import. Waiting to continue..."
                        : "Recovered a paused Google Sheet import. Continue or cancel it.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Pending Google Sheet snapshot could not be restored: {exception.Message}");
            }
        }

        private static void CleanupPending()
        {
            var pending = ReadPending();
            DeleteSnapshot(pending.SnapshotFilePath);
            SessionState.EraseString(sessionKey);
        }

        private static void DeleteSnapshot(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return;
            }

            var snapshotDirectory = Path.GetFullPath(GetSnapshotDirectory()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(filePath);

            if (!fullPath.StartsWith(snapshotDirectory, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"Pending Google Sheet snapshot is outside the expected directory and was not deleted: {fullPath}");
                return;
            }

            File.Delete(fullPath);
        }

        private static string GetSnapshotDirectory()
        {
            var projectPath = Directory.GetParent(Application.dataPath)?.FullName;

            if (string.IsNullOrWhiteSpace(projectPath))
            {
                throw new InvalidOperationException("The Unity project path could not be resolved.");
            }

            return Path.Combine(projectPath, "Library", "PschLib", "GoogleSheetImports");
        }

        private static void SetState(GoogleSheetImportState state, string message)
        {
            var stateChanged = State != state;
            SessionState.SetInt(stateKey, (int)state);
            SessionState.SetString(statusKey, message);

            if (stateChanged)
            {
                PersistSnapshotState(state);
            }
        }

        private static void PersistSnapshotState(GoogleSheetImportState state)
        {
            if (!HasPendingImports())
            {
                return;
            }

            try
            {
                var pending = ReadPending();

                if (string.IsNullOrWhiteSpace(pending.SnapshotFilePath) || !File.Exists(pending.SnapshotFilePath))
                {
                    return;
                }

                var snapshot = ReadSnapshot(pending.SnapshotFilePath);
                snapshot.State = (int)state;
                File.WriteAllText(pending.SnapshotFilePath, JsonUtility.ToJson(snapshot, true), new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Pending Google Sheet snapshot state could not be saved: {exception.Message}");
            }
        }
    }
}
