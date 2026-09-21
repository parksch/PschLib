using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PschLib.GoogleSheets
{
    public sealed class GoogleSheetGeneratorWindow : EditorWindow
    {
        private readonly List<GoogleSheetProjectItem> remoteProjects = new List<GoogleSheetProjectItem>();
        private Vector2 scrollPosition;
        private string statusMessage;
        private MessageType statusType;
        private int selectedRemoteProject;
        private bool isBusy;

        private GoogleSheetSettings Settings => GoogleSheetSettings.instance;
        private GoogleSheetProject Project => Settings.Project;
        private bool IsLocked => isBusy || GoogleSheetPendingImportProcessor.IsImporting ||
            GoogleSheetPendingImportProcessor.HasPendingImport || EditorApplication.isCompiling;

        [MenuItem("Tools/PschLib/Google Sheet Generator")]
        public static void Open()
        {
            GetWindow<GoogleSheetGeneratorWindow>("Google Sheet Generator");
        }

        private void OnEnable()
        {
            EditorApplication.delayCall += LoadProjectsIfAvailable;
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= LoadProjectsIfAvailable;
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space();
            DrawServer();
            EditorGUILayout.Space();
            DrawRegistryProjects();
            EditorGUILayout.Space();
            DrawProjectSettings();
            EditorGUILayout.Space();
            DrawSheetList();
            DrawStatus();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Google Sheet Generator", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("!", GUILayout.Width(26)))
            {
                GoogleSheetDocumentation.Open();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawServer()
        {
            EditorGUILayout.LabelField("Server", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(IsLocked))
            {
                EditorGUI.BeginChangeCheck();
                var server = (GoogleSheetServer)EditorGUILayout.ObjectField("Google Sheet Server", Settings.Server, typeof(GoogleSheetServer), false);

                if (EditorGUI.EndChangeCheck())
                {
                    Settings.Server = server;
                    Settings.Project = null;
                    remoteProjects.Clear();
                    Settings.SaveSettings();
                }
            }

            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(IsLocked || Settings.Server == null || !Settings.Server.IsConfigured))
            {
                if (GUILayout.Button(isBusy ? "Loading..." : "Load Projects"))
                {
                    LoadProjects();
                }
            }

            using (new EditorGUI.DisabledScope(IsLocked || Settings.Server == null || !Settings.Server.HasRegistrySpreadsheet))
            {
                if (GUILayout.Button("Open Registry"))
                {
                    OpenRegistry();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawRegistryProjects()
        {
            EditorGUILayout.LabelField("Registry Projects", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(IsLocked))
            {
                EditorGUI.BeginChangeCheck();
                var activeProject = (GoogleSheetProject)EditorGUILayout.ObjectField("Active Project", Project, typeof(GoogleSheetProject), false);

                if (EditorGUI.EndChangeCheck())
                {
                    Settings.Project = activeProject;

                    if (activeProject != null)
                    {
                        var serverChanged = Settings.Server != activeProject.Server;
                        Settings.Server = activeProject.Server;

                        if (serverChanged)
                        {
                            remoteProjects.Clear();
                            EditorApplication.delayCall += LoadProjectsIfAvailable;
                        }
                    }

                    Settings.SaveSettings();
                }
            }

            DrawProjectRegistration();

            if (remoteProjects.Count == 0)
            {
                EditorGUILayout.HelpBox("Select a server and click Load Projects.", MessageType.Info);
                return;
            }

            var names = new string[remoteProjects.Count];

            for (var index = 0; index < remoteProjects.Count; index++)
            {
                var remoteProject = remoteProjects[index];
                names[index] = $"{remoteProject.Key} ({remoteProject.Name})";
            }

            using (new EditorGUI.DisabledScope(IsLocked))
            {
                selectedRemoteProject = EditorGUILayout.Popup("Project", selectedRemoteProject, names);

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Use Selected Project"))
                {
                    UseSelectedProject();
                }

                if (GUILayout.Button("Open Sheet"))
                {
                    OpenSelectedSheet();
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawProjectRegistration()
        {
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(IsLocked || Settings.Server == null || !Settings.Server.IsConfigured))
            {
                if (GUILayout.Button("Register New Project..."))
                {
                    var server = Settings.Server;
                    GoogleSheetProjectRegistrationWindow.Open(server, remoteProjects, project => HandleProjectRegistered(server, project));
                }
            }
        }

        private void DrawProjectSettings()
        {
            if (Project == null)
            {
                return;
            }

            EditorGUILayout.LabelField("Project Settings", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Project Key", Project.ProjectKey);
                EditorGUILayout.TextField("Spreadsheet", Project.SpreadsheetName);
            }

            using (new EditorGUI.DisabledScope(IsLocked))
            {
                EditorGUI.BeginChangeCheck();
                Project.RootNamespace = EditorGUILayout.TextField("Root Namespace", Project.RootNamespace);
                Project.ScriptOutputPath = EditorGUILayout.TextField("Script Path", Project.ScriptOutputPath);
                Project.GenerateScriptableObject = EditorGUILayout.Toggle("Generate SO", Project.GenerateScriptableObject);

                using (new EditorGUI.DisabledScope(!Project.GenerateScriptableObject))
                {
                    Project.AssetOutputPath = EditorGUILayout.TextField("Asset Path", Project.AssetOutputPath);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(Project);
                    AssetDatabase.SaveAssets();
                }
            }

            using (new EditorGUI.DisabledScope(IsLocked))
            {
                if (GUILayout.Button("Refresh Sheets"))
                {
                    RefreshSheets();
                }
            }
        }

        private void DrawSheetList()
        {
            if (Project == null)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Sheets ({Project.Sheets.Count})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(IsLocked || Project.Sheets.Count == 0))
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

            if (Project.Sheets.Count == 0)
            {
                EditorGUILayout.HelpBox("No sheets are registered. Click Refresh Sheets.", MessageType.Info);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var sheet in Project.Sheets)
            {
                DrawSheet(sheet);
            }

            EditorGUILayout.EndScrollView();

            var selectedCount = GetSelectedCount();

            using (new EditorGUI.DisabledScope(IsLocked || selectedCount == 0))
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button($"Generate Selected ({selectedCount})", GUILayout.Height(28)))
                {
                    GenerateSelected(false);
                }

                if (GUILayout.Button("Code Only", GUILayout.Width(100), GUILayout.Height(28)))
                {
                    GenerateSelected(true);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawSheet(GoogleSheetEntry sheet)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            using (new EditorGUI.DisabledScope(IsLocked))
            {
                EditorGUI.BeginChangeCheck();
                sheet.Selected = EditorGUILayout.Toggle(sheet.Selected, GUILayout.Width(20));

                if (EditorGUI.EndChangeCheck())
                {
                    SaveProject();
                }
            }

            EditorGUILayout.LabelField(sheet.Name);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"ID: {sheet.SheetId}", GUILayout.Width(100));

            using (new EditorGUI.DisabledScope(IsLocked))
            {
                if (GUILayout.Button("Generate", GUILayout.Width(75)))
                {
                    GenerateSheet(sheet, false);
                }

                if (GUILayout.Button("Code Only", GUILayout.Width(75)))
                {
                    GenerateSheet(sheet, true);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatus()
        {
            if (GoogleSheetPendingImportProcessor.IsFinished && GoogleSheetPendingImportProcessor.HasPendingImport)
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Continue SO Generation"))
                {
                    GoogleSheetPendingImportProcessor.ContinuePending();
                }

                if (GUILayout.Button("Cancel Pending Import"))
                {
                    GoogleSheetPendingImportProcessor.CancelPending();
                }

                EditorGUILayout.EndHorizontal();
            }

            if (string.IsNullOrWhiteSpace(statusMessage))
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(statusMessage, statusType);
        }

        private async void LoadProjects()
        {
            SetBusy(true);

            try
            {
                var projects = await GoogleSheetRegistryService.GetProjectsAsync(Settings.Server);
                remoteProjects.Clear();
                remoteProjects.AddRange(projects);
                selectedRemoteProject = FindSelectedRemoteProject();
                SetStatus($"Loaded {remoteProjects.Count} project(s).", MessageType.Info);
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

        private void LoadProjectsIfAvailable()
        {
            if (!IsLocked && Settings.Server != null && Settings.Server.IsConfigured)
            {
                LoadProjects();
            }
        }

        private async void UseSelectedProject()
        {
            if (selectedRemoteProject < 0 || selectedRemoteProject >= remoteProjects.Count)
            {
                return;
            }

            SetBusy(true);

            try
            {
                Settings.Project = await GoogleSheetRegistryService.GetOrCreateProjectAsync(Settings.Server, remoteProjects[selectedRemoteProject]);
                Settings.SaveSettings();
                SetStatus($"Using project '{Settings.Project.ProjectKey}'. {Settings.Project.Sheets.Count} sheet(s) loaded.", MessageType.Info);
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

        private void OpenSelectedSheet()
        {
            if (selectedRemoteProject < 0 || selectedRemoteProject >= remoteProjects.Count)
            {
                SetStatus("Select a Registry project first.", MessageType.Warning);
                return;
            }

            var spreadsheetId = remoteProjects[selectedRemoteProject].SpreadsheetId;

            if (!GoogleSheetRegistryService.TryExtractSpreadsheetId(spreadsheetId, out var normalizedSpreadsheetId, out var error))
            {
                SetStatus(error, MessageType.Error);
                return;
            }

            Application.OpenURL($"https://docs.google.com/spreadsheets/d/{normalizedSpreadsheetId}/edit");
        }

        private void OpenRegistry()
        {
            if (!GoogleSheetRegistryService.TryExtractSpreadsheetId(Settings.Server.RegistrySpreadsheetId, out var spreadsheetId, out var error))
            {
                SetStatus(error, MessageType.Error);
                return;
            }

            Application.OpenURL($"https://docs.google.com/spreadsheets/d/{spreadsheetId}/edit#gid={Settings.Server.RegistrySheetId}");
        }

        private async void HandleProjectRegistered(GoogleSheetServer server, GoogleSheetProjectItem registeredProject)
        {
            SetBusy(true);

            try
            {
                var projects = await GoogleSheetRegistryService.GetProjectsAsync(server);
                remoteProjects.Clear();
                remoteProjects.AddRange(projects);
                selectedRemoteProject = FindRemoteProjectIndex(registeredProject.SpreadsheetId);

                if (selectedRemoteProject < 0)
                {
                    throw new InvalidOperationException($"Registered project '{registeredProject.Key}' was not found after refreshing the Registry.");
                }

                Settings.Server = server;
                Settings.Project = await GoogleSheetRegistryService.GetOrCreateProjectAsync(server, remoteProjects[selectedRemoteProject]);
                Settings.SaveSettings();
                SetStatus($"Registered and selected project '{Settings.Project.ProjectKey}'.", MessageType.Info);
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
                await GoogleSheetRegistryService.RefreshAsync(Project);
                SetStatus($"Refreshed. {Project.Sheets.Count} sheet(s) loaded.", MessageType.Info);
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

        private void SetAllSelected(bool selected)
        {
            foreach (var sheet in Project.Sheets)
            {
                sheet.Selected = selected;
            }

            SaveProject();
            Repaint();
        }

        private int GetSelectedCount()
        {
            var count = 0;

            foreach (var sheet in Project.Sheets)
            {
                if (sheet.Selected)
                {
                    count++;
                }
            }

            return count;
        }

        private async void GenerateSheet(GoogleSheetEntry sheet, bool codeOnly)
        {
            SetBusy(true);
            var project = Project;
            List<SheetSharedEnumDefinition> sharedEnumSnapshot = null;
            var codeWritten = false;

            try
            {
                GoogleSheetPendingImportProcessor.BeginCodeGeneration(1);
                sharedEnumSnapshot = SheetSharedEnumCatalog.CreateSnapshot(project);
                var result = await GoogleSheetImportService.PrepareAsync(project, sheet);
                var generatedPath = SheetCodeFileWriter.Write(project, result, out var codeChanged);
                codeWritten = true;
                GoogleSheetPendingImportProcessor.ReportCodeGenerated(1, 1);
                var generationError = CompleteGeneration(project, new[] { result }, codeChanged, codeOnly);
                SetStatus(
                    generationError ?? CreateGenerationStatus(project, sheet.Name, generatedPath, codeChanged, codeOnly),
                    generationError == null ? MessageType.Info : MessageType.Error);
            }
            catch (Exception exception)
            {
                RestoreSharedEnumsAfterFailedCodeGeneration(project, sharedEnumSnapshot, codeWritten, exception);
                GoogleSheetPendingImportProcessor.ReportFailure(exception);
                SetStatus(exception.Message, MessageType.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void GenerateSelected(bool codeOnly)
        {
            SetBusy(true);
            var project = Project;
            var selectedCount = GetSelectedCount();
            List<SheetSharedEnumDefinition> sharedEnumSnapshot = null;
            var codeWritten = false;

            try
            {
                GoogleSheetPendingImportProcessor.BeginCodeGeneration(selectedCount);
                sharedEnumSnapshot = SheetSharedEnumCatalog.CreateSnapshot(project);
                var results = new List<GoogleSheetImportResult>(selectedCount);

                foreach (var sheet in project.Sheets)
                {
                    if (!sheet.Selected)
                    {
                        continue;
                    }

                    var result = await GoogleSheetImportService.PrepareAsync(project, sheet);
                    results.Add(result);
                }

                var generatedCount = 0;
                var codeChanged = false;

                SheetCodeFileWriter.WriteAll(
                    project,
                    results,
                    (processed, total) =>
                    {
                        generatedCount = processed;
                        GoogleSheetPendingImportProcessor.ReportCodeGenerated(processed, total);
                    },
                    out codeChanged);
                codeWritten = true;

                var generationError = CompleteGeneration(project, results, codeChanged, codeOnly);
                SetStatus(
                    generationError ?? CreateGenerationStatus(project, generatedCount, codeChanged, codeOnly),
                    generationError == null ? MessageType.Info : MessageType.Error);
            }
            catch (Exception exception)
            {
                RestoreSharedEnumsAfterFailedCodeGeneration(project, sharedEnumSnapshot, codeWritten, exception);
                GoogleSheetPendingImportProcessor.ReportFailure(exception);
                SetStatus(exception.Message, MessageType.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void RestoreSharedEnumsAfterFailedCodeGeneration(
            GoogleSheetProject project,
            List<SheetSharedEnumDefinition> snapshot,
            bool codeWritten,
            Exception generationException)
        {
            if (snapshot == null || codeWritten)
            {
                return;
            }

            try
            {
                SheetSharedEnumCatalog.RestoreSnapshot(project, snapshot);
            }
            catch (Exception rollbackException)
            {
                Debug.LogError($"Google Sheet generation failed and the shared enum catalog could not be restored.\nGeneration error: {generationException}\nRollback error: {rollbackException}");
            }
        }

        private string CompleteGeneration(GoogleSheetProject project, IReadOnlyList<GoogleSheetImportResult> results, bool codeChanged, bool codeOnly)
        {
            var shouldGenerateAssets = project.GenerateScriptableObject && !codeOnly;

            if (!shouldGenerateAssets)
            {
                GoogleSheetPendingImportProcessor.CompleteWithoutAssets(results.Count);

                if (codeChanged)
                {
                    AssetDatabase.Refresh();
                }

                return null;
            }

            if (codeChanged)
            {
                GoogleSheetPendingImportProcessor.Queue(project, results);
                AssetDatabase.Refresh();
                return null;
            }

            if (EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed)
            {
                throw new InvalidOperationException("ScriptableObject assets cannot be updated while the project has compilation errors.");
            }

            var batchResult = SheetAssetWriter.WriteAll(project, results);

            if (batchResult.HasFailures)
            {
                return GoogleSheetPendingImportProcessor.RetainAssetFailures(project, batchResult, results.Count);
            }

            GoogleSheetPendingImportProcessor.CompleteAssets(batchResult.SuccessCount);
            return null;
        }

        private string CreateGenerationStatus(GoogleSheetProject project, string sheetName, string generatedPath, bool codeChanged, bool codeOnly)
        {
            if (codeOnly || !project.GenerateScriptableObject)
            {
                return codeChanged ? $"Generated code for {sheetName}: {generatedPath}." : $"Code is already up to date for {sheetName}: {generatedPath}.";
            }

            return codeChanged
                ? $"Generated code for {sheetName}: {generatedPath}. SO generation will continue after compilation."
                : $"Code was unchanged and the SO was updated for {sheetName}: {generatedPath}.";
        }

        private string CreateGenerationStatus(GoogleSheetProject project, int generatedCount, bool codeChanged, bool codeOnly)
        {
            if (codeOnly || !project.GenerateScriptableObject)
            {
                return codeChanged ? $"Generated code for {generatedCount} selected sheet(s)." : $"Code is already up to date for {generatedCount} selected sheet(s).";
            }

            return codeChanged
                ? $"Generated code for {generatedCount} selected sheet(s). SO generation will continue after compilation."
                : $"Code was unchanged and {generatedCount} SO asset(s) were updated.";
        }

        private int FindSelectedRemoteProject()
        {
            if (Project == null)
            {
                return 0;
            }

            var index = FindRemoteProjectIndex(Project.SpreadsheetId);
            return index < 0 ? 0 : index;
        }

        private int FindRemoteProjectIndex(string spreadsheetId)
        {
            for (var index = 0; index < remoteProjects.Count; index++)
            {
                if (remoteProjects[index].SpreadsheetId == spreadsheetId)
                {
                    return index;
                }
            }

            return -1;
        }

        private void SaveProject()
        {
            EditorUtility.SetDirty(Project);
            AssetDatabase.SaveAssets();
        }

        private void SetBusy(bool isBusy)
        {
            this.isBusy = isBusy;
            Repaint();
        }

        private void SetStatus(string message, MessageType type)
        {
            statusMessage = message;
            statusType = type;
            Repaint();
        }
    }
}
