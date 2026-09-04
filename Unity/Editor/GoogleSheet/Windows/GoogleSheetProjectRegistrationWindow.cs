using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PschLib.GoogleSheets
{
    internal sealed class GoogleSheetProjectRegistrationWindow : EditorWindow
    {
        private GoogleSheetServer server;
        private List<GoogleSheetProjectItem> projects;
        private Action<GoogleSheetProjectItem> onRegistered;
        private string projectName;
        private string spreadsheetAddress;
        private string error;
        private bool isRegistering;

        public static void Open(GoogleSheetServer server, IReadOnlyList<GoogleSheetProjectItem> projects, Action<GoogleSheetProjectItem> onRegistered)
        {
            var window = CreateInstance<GoogleSheetProjectRegistrationWindow>();
            window.titleContent = new GUIContent("Register Project");
            window.minSize = new Vector2(420f, 155f);
            window.maxSize = new Vector2(600f, 340f);
            window.server = server;
            window.projects = projects == null ? new List<GoogleSheetProjectItem>() : new List<GoogleSheetProjectItem>(projects);
            window.onRegistered = onRegistered;
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Register Google Sheet Project", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("!", GUILayout.Width(26)))
            {
                GoogleSheetDocumentation.Open("register");
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Server", server, typeof(GoogleSheetServer), false);
            }

            using (new EditorGUI.DisabledScope(isRegistering))
            {
                projectName = EditorGUILayout.TextField("Project Name", projectName);
                spreadsheetAddress = EditorGUILayout.TextField("Sheet URL or ID", spreadsheetAddress);

                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(isRegistering ? "Registering..." : "Register"))
                {
                    Register();
                }

                if (GUILayout.Button("Cancel"))
                {
                    Close();
                }

                EditorGUILayout.EndHorizontal();
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }

        private async void Register()
        {
            isRegistering = true;
            error = null;
            Repaint();

            try
            {
                var project = await GoogleSheetRegistryService.RegisterProjectAsync(server, projectName, spreadsheetAddress, projects);
                onRegistered?.Invoke(project);
                Close();
            }
            catch (Exception exception)
            {
                error = exception.Message;
            }
            finally
            {
                isRegistering = false;
                Repaint();
            }
        }
    }
}
