using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PschLib
{
    public sealed class GoogleSheetGeneratorWindow : EditorWindow
    {
        private readonly List<GoogleSheetProjectItem> _remoteProjects = new List<GoogleSheetProjectItem>();
        private Vector2 _scrollPosition;
        private string _statusMessage;
        private MessageType _statusType;
        private int _selectedRemoteProject;
        private bool _isBusy;

        private GoogleSheetSettings Settings => GoogleSheetSettings.instance;
        private GoogleSheetProject Project => Settings.Project;
        private bool IsLocked => _isBusy || GoogleSheetPendingImportProcessor.IsImporting || EditorApplication.isCompiling;

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
            DrawServer();
            EditorGUILayout.Space();
            DrawRegistryProjects();
            EditorGUILayout.Space();
            DrawProjectSettings();
            EditorGUILayout.Space();
            DrawSheetList();
            DrawStatus();
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
                    _remoteProjects.Clear();
                    Settings.SaveSettings();
                }
            }

            using (new EditorGUI.DisabledScope(IsLocked || Settings.Server == null || !Settings.Server.IsConfigured))
            {
                if (GUILayout.Button(_isBusy ? "Loading..." : "Load Projects"))
                {
                    LoadProjects();
                }
            }
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
                            _remoteProjects.Clear();
                            EditorApplication.delayCall += LoadProjectsIfAvailable;
                        }
                    }

                    Settings.SaveSettings();
                }
            }

            if (_remoteProjects.Count == 0)
            {
                EditorGUILayout.HelpBox("Select a server and click Load Projects.", MessageType.Info);
                return;
            }

            var names = new string[_remoteProjects.Count];

            for (var index = 0; index < _remoteProjects.Count; index++)
            {
                var remoteProject = _remoteProjects[index];
                names[index] = $"{remoteProject.Key} ({remoteProject.Name})";
            }

            using (new EditorGUI.DisabledScope(IsLocked))
            {
                _selectedRemoteProject = EditorGUILayout.Popup("Project", _selectedRemoteProject, names);

                if (GUILayout.Button("Use Selected Project"))
                {
                    UseSelectedProject();
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

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

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
            if (string.IsNullOrWhiteSpace(_statusMessage))
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(_statusMessage, _statusType);
        }

        private async void LoadProjects()
        {
            SetBusy(true);

            try
            {
                var projects = await GoogleSheetRegistryService.GetProjectsAsync(Settings.Server);
                _remoteProjects.Clear();
                _remoteProjects.AddRange(projects);
                _selectedRemoteProject = FindSelectedRemoteProject();
                SetStatus($"Loaded {_remoteProjects.Count} project(s).", MessageType.Info);
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
            if (_selectedRemoteProject < 0 || _selectedRemoteProject >= _remoteProjects.Count)
            {
                return;
            }

            SetBusy(true);

            try
            {
                Settings.Project = await GoogleSheetRegistryService.GetOrCreateProjectAsync(Settings.Server, _remoteProjects[_selectedRemoteProject]);
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

            for (var index = 0; index < _remoteProjects.Count; index++)
            {
                if (_remoteProjects[index].SpreadsheetId == Project.SpreadsheetId)
                {
                    return index;
                }
            }

            return 0;
        }

        private void SaveProject()
        {
            EditorUtility.SetDirty(Project);
            AssetDatabase.SaveAssets();
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
