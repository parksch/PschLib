using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PschLib
{
    internal sealed class GoogleSheetProjectRegistrationWindow : EditorWindow
    {
        private GoogleSheetServer _server;
        private List<GoogleSheetProjectItem> _projects;
        private Action<GoogleSheetProjectItem> _onRegistered;
        private string _projectName;
        private string _spreadsheetAddress;
        private string _error;
        private bool _isRegistering;
        private bool _showHelp;

        public static void Open(GoogleSheetServer server, IReadOnlyList<GoogleSheetProjectItem> projects, Action<GoogleSheetProjectItem> onRegistered)
        {
            var window = CreateInstance<GoogleSheetProjectRegistrationWindow>();
            window.titleContent = new GUIContent("Register Project");
            window.minSize = new Vector2(420f, 155f);
            window.maxSize = new Vector2(600f, 340f);
            window._server = server;
            window._projects = projects == null ? new List<GoogleSheetProjectItem>() : new List<GoogleSheetProjectItem>(projects);
            window._onRegistered = onRegistered;
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Register Google Sheet Project", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("!", GUILayout.Width(26)))
            {
                ToggleHelp();
            }

            EditorGUILayout.EndHorizontal();

            if (_showHelp)
            {
                const string message =
                    "Project Name must be a valid C# identifier.\n" +
                    "Use letters, numbers, or underscores, and do not start with a number.\n\n" +
                    "Paste the full Google Sheet URL or enter its Spreadsheet ID.\n" +
                    "Project names and Spreadsheets cannot be registered twice.";

                EditorGUILayout.HelpBox(message, MessageType.Info);
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Server", _server, typeof(GoogleSheetServer), false);
            }

            using (new EditorGUI.DisabledScope(_isRegistering))
            {
                _projectName = EditorGUILayout.TextField("Project Name", _projectName);
                _spreadsheetAddress = EditorGUILayout.TextField("Sheet URL or ID", _spreadsheetAddress);

                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(_isRegistering ? "Registering..." : "Register"))
                {
                    Register();
                }

                if (GUILayout.Button("Cancel"))
                {
                    Close();
                }

                EditorGUILayout.EndHorizontal();
            }

            if (!string.IsNullOrWhiteSpace(_error))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_error, MessageType.Error);
            }
        }

        private void ToggleHelp()
        {
            _showHelp = !_showHelp;
            var windowPosition = position;
            windowPosition.height = _showHelp ? 285f : 155f;
            position = windowPosition;
        }

        private async void Register()
        {
            _isRegistering = true;
            _error = null;
            Repaint();

            try
            {
                var project = await GoogleSheetRegistryService.RegisterProjectAsync(_server, _projectName, _spreadsheetAddress, _projects);
                _onRegistered?.Invoke(project);
                Close();
            }
            catch (Exception exception)
            {
                _error = exception.Message;
            }
            finally
            {
                _isRegistering = false;
                Repaint();
            }
        }
    }
}
