using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PschLib
{
    internal enum GoogleSheetImportState
    {
        None,
        GeneratingCode,
        WaitingForCompilation,
        CreatingAssets,
        Completed,
        Failed
    }

    [InitializeOnLoad]
    internal static class GoogleSheetPendingImportProcessor
    {
        private const string SessionKey = "PschLib.GoogleSheet.PendingImports";
        private const string StateKey = "PschLib.GoogleSheet.ImportState";
        private const string StatusKey = "PschLib.GoogleSheet.ImportStatus";
        private static bool _scheduled;
        private static bool _processing;

        public static GoogleSheetImportState State => (GoogleSheetImportState)SessionState.GetInt(StateKey, (int)GoogleSheetImportState.None);
        public static string StatusMessage => SessionState.GetString(StatusKey, "Preparing Google Sheet import...");
        public static bool IsFinished => State == GoogleSheetImportState.Completed || State == GoogleSheetImportState.Failed;
        public static bool IsImporting => State == GoogleSheetImportState.GeneratingCode || State == GoogleSheetImportState.WaitingForCompilation || State == GoogleSheetImportState.CreatingAssets;

        [Serializable]
        private sealed class PendingImports
        {
            public List<int> SheetIds = new List<int>();
        }

        static GoogleSheetPendingImportProcessor()
        {
            if (HasPendingImports())
            {
                EditorApplication.delayCall += GoogleSheetImportProgressWindow.Open;
                Schedule();
            }
        }

        public static void BeginCodeGeneration(int sheetCount)
        {
            SetState(GoogleSheetImportState.GeneratingCode, $"Generating code... (0/{sheetCount})");
            GoogleSheetImportProgressWindow.Open();
        }

        public static void ReportCodeGenerated(int completedCount, int sheetCount)
        {
            SetState(GoogleSheetImportState.GeneratingCode, $"Generating code... ({completedCount}/{sheetCount})");
        }

        public static void ReportFailure(Exception exception)
        {
            SetState(GoogleSheetImportState.Failed, $"Import failed.\n{exception.Message}");
        }

        public static void Queue(IEnumerable<GoogleSheetEntry> sheets)
        {
            var pending = ReadPending();

            foreach (var sheet in sheets)
            {
                if (sheet != null && !pending.SheetIds.Contains(sheet.SheetId))
                {
                    pending.SheetIds.Add(sheet.SheetId);
                }
            }

            SessionState.SetString(SessionKey, JsonUtility.ToJson(pending));
            SetState(GoogleSheetImportState.WaitingForCompilation, $"Generated code for {pending.SheetIds.Count} sheet(s). Waiting for Unity compilation...");
            GoogleSheetImportProgressWindow.Open();
            Schedule();
        }

        public static void Schedule()
        {
            if (_scheduled)
            {
                return;
            }

            _scheduled = true;
            EditorApplication.update += TryProcess;
        }

        private static void TryProcess()
        {
            if (_processing || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            EditorApplication.update -= TryProcess;
            _scheduled = false;
            ProcessPending();
        }

        private static async void ProcessPending()
        {
            _processing = true;

            try
            {
                var settings = GoogleSheetSettings.instance;
                var pending = ReadPending();
                var completedCount = 0;
                SetState(GoogleSheetImportState.CreatingAssets, $"Creating ScriptableObject assets... (0/{pending.SheetIds.Count})");

                foreach (var sheetId in pending.SheetIds)
                {
                    var entry = FindEntry(settings, sheetId);

                    if (entry == null)
                    {
                        throw new InvalidOperationException($"Pending Google Sheet was not found in settings: {sheetId}");
                    }

                    var result = await GoogleSheetImportService.PrepareAsync(settings, entry);
                    var assetPath = SheetAssetWriter.Write(settings, result);
                    completedCount++;
                    SetState(GoogleSheetImportState.CreatingAssets, $"Creating ScriptableObject assets... ({completedCount}/{pending.SheetIds.Count})");
                    Debug.Log($"Google Sheet asset generated: {assetPath}");
                }

                SessionState.EraseString(SessionKey);
                SetState(GoogleSheetImportState.Completed, $"Import completed. {completedCount} ScriptableObject asset(s) were created or updated.");
            }
            catch (Exception exception)
            {
                SessionState.EraseString(SessionKey);
                SetState(GoogleSheetImportState.Failed, $"Import failed.\n{exception.Message}");
                Debug.LogError($"Google Sheet asset generation failed: {exception}");
            }
            finally
            {
                _processing = false;
            }
        }

        private static GoogleSheetEntry FindEntry(GoogleSheetSettings settings, int sheetId)
        {
            foreach (var sheet in settings.Sheets)
            {
                if (sheet.SheetId == sheetId)
                {
                    return sheet;
                }
            }

            return null;
        }

        private static bool HasPendingImports()
        {
            return !string.IsNullOrWhiteSpace(SessionState.GetString(SessionKey, string.Empty));
        }

        private static PendingImports ReadPending()
        {
            var json = SessionState.GetString(SessionKey, string.Empty);

            if (string.IsNullOrWhiteSpace(json))
            {
                return new PendingImports();
            }

            return JsonUtility.FromJson<PendingImports>(json) ?? new PendingImports();
        }

        private static void SetState(GoogleSheetImportState state, string message)
        {
            SessionState.SetInt(StateKey, (int)state);
            SessionState.SetString(StatusKey, message);
        }
    }
}
