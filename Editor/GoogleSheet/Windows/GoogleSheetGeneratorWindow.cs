using System;
using UnityEditor;
using UnityEngine;

namespace PschLib
{
    public sealed class GoogleSheetGeneratorWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private string _pendingWebAppUrl;
        private string _statusMessage;
        private MessageType _statusType;
        private bool _isBusy;

        private GoogleSheetSettings Settings => GoogleSheetSettings.instance;

        [MenuItem("Tools/PschLib/Google Sheet Generator")]
        public static void Open()
        {
            GetWindow<GoogleSheetGeneratorWindow>("Google Sheet Generator");
        }

        private void OnEnable()
        {
            _pendingWebAppUrl = Settings.WebAppUrl;
        }

        private void OnGUI()
        {
            DrawConnection();
            EditorGUILayout.Space();
            DrawOutputSettings();
            EditorGUILayout.Space();
            DrawSheetList();
            DrawStatus();
        }

        private void DrawConnection()
        {
            EditorGUILayout.LabelField("Connection", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(Settings.IsConnected || _isBusy))
            {
                _pendingWebAppUrl = EditorGUILayout.TextField("Web App URL", _pendingWebAppUrl);
            }

            EditorGUILayout.BeginHorizontal();

            if (!Settings.IsConnected)
            {
                using (new EditorGUI.DisabledScope(_isBusy || string.IsNullOrWhiteSpace(_pendingWebAppUrl)))
                {
                    if (GUILayout.Button(_isBusy ? "Connecting..." : "Connect"))
                    {
                        Connect();
                    }
                }
            }
            else
            {
                using (new EditorGUI.DisabledScope(_isBusy))
                {
                    if (GUILayout.Button(_isBusy ? "Refreshing..." : "Refresh"))
                    {
                        RefreshSheets();
                    }

                    if (GUILayout.Button("Disconnect"))
                    {
                        Disconnect();
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawOutputSettings()
        {
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            Settings.TargetNamespace = EditorGUILayout.TextField("Namespace", Settings.TargetNamespace);
            Settings.ScriptOutputPath = EditorGUILayout.TextField("Script Path", Settings.ScriptOutputPath);
            Settings.AssetOutputPath = EditorGUILayout.TextField("Asset Path", Settings.AssetOutputPath);

            if (EditorGUI.EndChangeCheck())
            {
                Settings.SaveSettings();
            }
        }

        private void DrawSheetList()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Registered Sheets ({Settings.Sheets.Count})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(_isBusy || Settings.Sheets.Count == 0))
            {
                if (GUILayout.Button("Select All", GUILayout.Width(80)))
                {
                    SetAllSelected(true);
                }

                if (GUILayout.Button("Deselect All", GUILayout.Width(90)))
                {
                    SetAllSelected(false);
                }
            }

            EditorGUILayout.EndHorizontal();

            if (!Settings.IsConnected)
            {
                EditorGUILayout.HelpBox("Connect an Apps Script web app to load sheets.", MessageType.Info);
                return;
            }

            if (Settings.Sheets.Count == 0)
            {
                EditorGUILayout.HelpBox("No sheets are registered. Click Refresh to try again.", MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            foreach (var sheet in Settings.Sheets)
            {
                DrawSheet(sheet);
            }

            EditorGUILayout.EndScrollView();

            var selectedCount = GetSelectedCount();

            using (new EditorGUI.DisabledScope(_isBusy || GoogleSheetPendingImportProcessor.IsImporting || selectedCount == 0))
            {
                if (GUILayout.Button($"Generate Selected ({selectedCount})", GUILayout.Height(28)))
                {
                    GenerateSelected();
                }
            }
        }

        private void DrawSheet(GoogleSheetEntry sheet)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUI.BeginChangeCheck();
            sheet.Selected = EditorGUILayout.Toggle(sheet.Selected, GUILayout.Width(20));

            if (EditorGUI.EndChangeCheck())
            {
                Settings.SaveSettings();
            }

            EditorGUILayout.LabelField(sheet.Name);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"ID: {sheet.SheetId}", GUILayout.Width(100));

            using (new EditorGUI.DisabledScope(_isBusy || GoogleSheetPendingImportProcessor.IsImporting))
            {
                if (GUILayout.Button("Generate", GUILayout.Width(80)))
                {
                    GenerateSheet(sheet);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatus()
        {
            if (string.IsNullOrWhiteSpace(_statusMessage))
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(_statusMessage, _statusType);
        }

        private async void Connect()
        {
            SetBusy(true);

            try
            {
                await GoogleSheetRegistryService.ConnectAsync(Settings, _pendingWebAppUrl);
                _pendingWebAppUrl = Settings.WebAppUrl;
                SetStatus($"Connected. {Settings.Sheets.Count} sheet(s) registered.", MessageType.Info);
            }
            catch (Exception exception)
            {
                SetStatus(exception.Message, MessageType.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void RefreshSheets()
        {
            SetBusy(true);

            try
            {
                await GoogleSheetRegistryService.RefreshAsync(Settings);
                SetStatus($"Refreshed. {Settings.Sheets.Count} sheet(s) registered.", MessageType.Info);
            }
            catch (Exception exception)
            {
                SetStatus(exception.Message, MessageType.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void Disconnect()
        {
            var confirmed = EditorUtility.DisplayDialog("Disconnect Google Sheet", "The saved URL and registered sheet list will be removed. Generated files will not be deleted.", "Disconnect", "Cancel");

            if (!confirmed)
            {
                return;
            }

            GoogleSheetRegistryService.Disconnect(Settings);
            _pendingWebAppUrl = string.Empty;
            SetStatus("Disconnected.", MessageType.Info);
            Repaint();
        }

        private void SetAllSelected(bool selected)
        {
            foreach (var sheet in Settings.Sheets)
            {
                sheet.Selected = selected;
            }

            Settings.SaveSettings();
            Repaint();
        }

        private int GetSelectedCount()
        {
            var count = 0;

            foreach (var sheet in Settings.Sheets)
            {
                if (sheet.Selected)
                {
                    count++;
                }
            }

            return count;
        }

        private async void GenerateSheet(GoogleSheetEntry sheet)
        {
            SetBusy(true);
            GoogleSheetPendingImportProcessor.BeginCodeGeneration(1);

            try
            {
                var result = await GoogleSheetImportService.PrepareAsync(Settings, sheet);
                var generatedPath = SheetCodeFileWriter.Write(Settings, result);
                GoogleSheetPendingImportProcessor.ReportCodeGenerated(1, 1);
                GoogleSheetPendingImportProcessor.Queue(new[] { sheet });
                SetStatus($"Generated code for {sheet.Name}: {generatedPath}. Asset generation is queued.", MessageType.Info);
                AssetDatabase.Refresh();
            }
            catch (Exception exception)
            {
                GoogleSheetPendingImportProcessor.ReportFailure(exception);
                SetStatus(exception.Message, MessageType.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void GenerateSelected()
        {
            SetBusy(true);
            var selectedCount = GetSelectedCount();
            GoogleSheetPendingImportProcessor.BeginCodeGeneration(selectedCount);

            try
            {
                var generatedCount = 0;
                var generatedSheets = new System.Collections.Generic.List<GoogleSheetEntry>();

                foreach (var sheet in Settings.Sheets)
                {
                    if (!sheet.Selected)
                    {
                        continue;
                    }

                    var result = await GoogleSheetImportService.PrepareAsync(Settings, sheet);
                    SheetCodeFileWriter.Write(Settings, result);
                    generatedSheets.Add(sheet);
                    generatedCount++;
                    GoogleSheetPendingImportProcessor.ReportCodeGenerated(generatedCount, selectedCount);
                }

                GoogleSheetPendingImportProcessor.Queue(generatedSheets);
                SetStatus($"Generated code for {generatedCount} selected sheet(s). Asset generation is queued.", MessageType.Info);
                AssetDatabase.Refresh();
            }
            catch (Exception exception)
            {
                GoogleSheetPendingImportProcessor.ReportFailure(exception);
                SetStatus(exception.Message, MessageType.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool isBusy)
        {
            _isBusy = isBusy;
            Repaint();
        }

        private void SetStatus(string message, MessageType type)
        {
            _statusMessage = message;
            _statusType = type;
            Repaint();
        }
    }
}
