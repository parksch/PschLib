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
        private bool IsLocked => isBusy || GoogleSheetPendingImportProcessor.IsImporting || EditorApplication.isCompiling;

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
                if (GUILayout.Button($"Generate Selected ({selectedCount})", GUILayout.Height(28)))
                {
                    GenerateSelected();
                }
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
                if (GUILayout.Button("Generate", GUILayout.Width(80)))
                {
                    GenerateSheet(sheet);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatus()
        {
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

        private async void GenerateSheet(GoogleSheetEntry sheet)
        {
            SetBusy(true);
            GoogleSheetPendingImportProcessor.BeginCodeGeneration(1);

            try
            {
                var result = await GoogleSheetImportService.PrepareAsync(Project, sheet);
                var generatedPath = SheetCodeFileWriter.Write(Project, result);
                GoogleSheetPendingImportProcessor.ReportCodeGenerated(1, 1);
                CompleteGeneration(new[] { sheet }, 1);
                SetStatus(Project.GenerateScriptableObject ? $"Generated code for {sheet.Name}: {generatedPath}. Asset generation is queued." : $"Generated code for {sheet.Name}: {generatedPath}.", MessageType.Info);
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
                var generatedSheets = new List<GoogleSheetEntry>();

                foreach (var sheet in Project.Sheets)
                {
                    if (!sheet.Selected)
                    {
                        continue;
                    }

                    var result = await GoogleSheetImportService.PrepareAsync(Project, sheet);
                    SheetCodeFileWriter.Write(Project, result);
                    generatedSheets.Add(sheet);
                    generatedCount++;
                    GoogleSheetPendingImportProcessor.ReportCodeGenerated(generatedCount, selectedCount);
                }

                CompleteGeneration(generatedSheets, generatedCount);
                SetStatus(Project.GenerateScriptableObject ? $"Generated code for {generatedCount} selected sheet(s). Asset generation is queued." : $"Generated code for {generatedCount} selected sheet(s).", MessageType.Info);
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

        private void CompleteGeneration(IEnumerable<GoogleSheetEntry> sheets, int generatedCount)
        {
            if (Project.GenerateScriptableObject)
            {
                GoogleSheetPendingImportProcessor.Queue(Project, sheets);
                return;
            }

            GoogleSheetPendingImportProcessor.CompleteWithoutAssets(generatedCount);
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
