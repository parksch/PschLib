using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;

namespace PschLib.GoogleSheets
{
    internal static class GoogleSheetRegistryService
    {
        public static bool TryValidateRegistrationInput(string rawKey, string rawSpreadsheetAddress, IReadOnlyList<GoogleSheetProjectItem> projects, out string key, out string spreadsheetId, out string error)
        {
            key = rawKey?.Trim();
            spreadsheetId = null;
            error = null;

            if (string.IsNullOrWhiteSpace(key))
            {
                error = "Project name is required.";
                return false;
            }

            if (!SheetDataCodeGenerator.IsValidIdentifier(key))
            {
                error = $"Project name '{key}' is not a valid C# identifier.";
                return false;
            }

            if (!TryExtractSpreadsheetId(rawSpreadsheetAddress, out spreadsheetId, out error))
            {
                return false;
            }

            if (projects == null)
            {
                return true;
            }

            foreach (var project in projects)
            {
                if (project == null)
                {
                    continue;
                }

                if (string.Equals(project.Key?.Trim(), key, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"Project name is already registered: {key}";
                    return false;
                }

                if (string.Equals(project.SpreadsheetId?.Trim(), spreadsheetId, StringComparison.Ordinal))
                {
                    error = $"Spreadsheet ID is already registered: {spreadsheetId}";
                    return false;
                }
            }

            return true;
        }

        public static bool TryExtractSpreadsheetId(string rawAddress, out string spreadsheetId, out string error)
        {
            spreadsheetId = null;
            error = null;
            var address = rawAddress?.Trim();

            if (string.IsNullOrWhiteSpace(address))
            {
                error = "Google Sheet URL or Spreadsheet ID is required.";
                return false;
            }

            const string marker = "/spreadsheets/d/";
            var markerIndex = address.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

            if (markerIndex >= 0)
            {
                var idStartIndex = markerIndex + marker.Length;
                var idEndIndex = address.IndexOfAny(new[] { '/', '?', '#' }, idStartIndex);
                spreadsheetId = idEndIndex < 0 ? address.Substring(idStartIndex) : address.Substring(idStartIndex, idEndIndex - idStartIndex);
            }
            else
            {
                spreadsheetId = address;
            }

            spreadsheetId = spreadsheetId.Trim();

            if (!IsValidSpreadsheetId(spreadsheetId))
            {
                spreadsheetId = null;
                error = "Google Sheet URL or Spreadsheet ID is invalid.";
                return false;
            }

            return true;
        }

        public static async Task<List<GoogleSheetProjectItem>> GetProjectsAsync(GoogleSheetServer server)
        {
            ValidateServer(server);

            var client = new GoogleSheetWebClient(server.WebAppUrl);
            var response = await client.GetProjectsAsync();

            if (response == null)
            {
                throw new InvalidOperationException("Google Sheet returned no response.");
            }

            if (!response.Success)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Error) ? "Google Sheet project request failed." : response.Error);
            }

            if (response.Projects == null)
            {
                throw new InvalidOperationException("Google Sheet response does not contain a project list.");
            }

            if (!string.IsNullOrWhiteSpace(response.RegistrySpreadsheetId))
            {
                server.RegistrySpreadsheetId = response.RegistrySpreadsheetId.Trim();
                server.RegistrySheetId = response.RegistrySheetId;
                EditorUtility.SetDirty(server);
                AssetDatabase.SaveAssets();
            }

            return response.Projects;
        }

        public static async Task<GoogleSheetProjectItem> RegisterProjectAsync(GoogleSheetServer server, string rawKey, string rawSpreadsheetAddress, IReadOnlyList<GoogleSheetProjectItem> projects)
        {
            ValidateServer(server);

            if (!TryValidateRegistrationInput(rawKey, rawSpreadsheetAddress, projects, out var key, out var spreadsheetId, out var error))
            {
                throw new InvalidOperationException(error);
            }

            var client = new GoogleSheetWebClient(server.WebAppUrl);
            var response = await client.RegisterProjectAsync(key, spreadsheetId);

            if (response == null)
            {
                throw new InvalidOperationException("Google Sheet returned no response.");
            }

            if (!response.Success)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Error) ? "Google Sheet project registration failed." : response.Error);
            }

            if (response.Project == null)
            {
                throw new InvalidOperationException("Google Sheet response does not contain the registered project.");
            }

            if (!string.Equals(response.Project.Key, key, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Google Sheet returned project key '{response.Project.Key}' instead of '{key}'.");
            }

            if (!string.Equals(response.Project.SpreadsheetId, spreadsheetId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Google Sheet returned SpreadsheetId '{response.Project.SpreadsheetId}' instead of '{spreadsheetId}'.");
            }

            return response.Project;
        }

        public static async Task SynchronizeAsync(GoogleSheetProject project, GoogleSheetProjectItem remoteProject)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (remoteProject == null)
            {
                throw new ArgumentNullException(nameof(remoteProject));
            }

            ValidateServer(project.Server);

            var client = new GoogleSheetWebClient(project.Server.WebAppUrl);
            var response = await client.GetSheetListAsync(remoteProject.SpreadsheetId);
            ValidateResponse(response);

            if (response.SpreadsheetId != remoteProject.SpreadsheetId)
            {
                throw new InvalidOperationException($"Google Sheet returned SpreadsheetId '{response.SpreadsheetId}' instead of '{remoteProject.SpreadsheetId}'.");
            }

            project.ProjectKey = remoteProject.Key;
            project.SpreadsheetId = remoteProject.SpreadsheetId;
            project.SpreadsheetName = response.Name;
            Synchronize(project.Sheets, response.Sheets);
            EditorUtility.SetDirty(project);
            AssetDatabase.SaveAssets();
        }

        public static async Task<GoogleSheetProject> GetOrCreateProjectAsync(GoogleSheetServer server, GoogleSheetProjectItem remoteProject)
        {
            ValidateServer(server);

            if (remoteProject == null)
            {
                throw new ArgumentNullException(nameof(remoteProject));
            }

            var project = FindProject(server, remoteProject.SpreadsheetId);

            if (project == null)
            {
                project = CreateProject(server, remoteProject);
            }

            await SynchronizeAsync(project, remoteProject);
            return project;
        }

        public static async Task RefreshAsync(GoogleSheetProject project)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (!project.IsConnected)
            {
                throw new InvalidOperationException("Google Sheet project is not connected.");
            }

            ValidateServer(project.Server);

            var client = new GoogleSheetWebClient(project.Server.WebAppUrl);
            var response = await client.GetSheetListAsync(project.SpreadsheetId);
            ValidateResponse(response);

            if (response.SpreadsheetId != project.SpreadsheetId)
            {
                throw new InvalidOperationException($"Google Sheet returned SpreadsheetId '{response.SpreadsheetId}' instead of '{project.SpreadsheetId}'.");
            }

            project.SpreadsheetName = response.Name;
            Synchronize(project.Sheets, response.Sheets);
            EditorUtility.SetDirty(project);
            AssetDatabase.SaveAssets();
        }

        private static void ValidateResponse(GoogleSheetListResponse response)
        {
            if (response == null)
            {
                throw new InvalidOperationException("Google Sheet returned no response.");
            }

            if (!response.Success)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Error) ? "Google Sheet list request failed." : response.Error);
            }

            if (response.Sheets == null)
            {
                throw new InvalidOperationException("Google Sheet response does not contain a sheet list.");
            }
        }

        private static void ValidateServer(GoogleSheetServer server)
        {
            if (server == null)
            {
                throw new ArgumentNullException(nameof(server));
            }

            if (!server.IsConfigured)
            {
                throw new InvalidOperationException("Google Sheet Server does not contain a Web App URL.");
            }
        }

        private static GoogleSheetProject FindProject(GoogleSheetServer server, string spreadsheetId)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:GoogleSheetProject"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var project = AssetDatabase.LoadAssetAtPath<GoogleSheetProject>(path);

                if (project != null && project.Server == server && project.SpreadsheetId == spreadsheetId)
                {
                    return project;
                }
            }

            return null;
        }

        private static bool IsValidSpreadsheetId(string spreadsheetId)
        {
            if (string.IsNullOrWhiteSpace(spreadsheetId))
            {
                return false;
            }

            foreach (var character in spreadsheetId)
            {
                if (!char.IsLetterOrDigit(character) && character != '_' && character != '-')
                {
                    return false;
                }
            }

            return true;
        }

        private static GoogleSheetProject CreateProject(GoogleSheetServer server, GoogleSheetProjectItem remoteProject)
        {
            if (!SheetDataCodeGenerator.TryCreateClassName(remoteProject.Key, out var projectName, out var error))
            {
                throw new InvalidOperationException(error);
            }

            var serverPath = AssetDatabase.GetAssetPath(server);

            if (string.IsNullOrWhiteSpace(serverPath))
            {
                throw new InvalidOperationException("Google Sheet Server must be saved as an asset.");
            }

            var separatorIndex = serverPath.LastIndexOf('/');
            var parentFolder = separatorIndex >= 0 ? serverPath.Substring(0, separatorIndex) : "Assets";
            var projectFolder = $"{parentFolder}/{server.name}Projects";
            GoogleSheetPathUtility.EnsureAssetFolder(projectFolder);

            var assetPath = $"{projectFolder}/{projectName}.asset";
            var existingAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);

            if (existingAsset != null)
            {
                throw new InvalidOperationException($"An asset already exists at '{assetPath}'.");
            }

            var project = UnityEngine.ScriptableObject.CreateInstance<GoogleSheetProject>();
            project.Server = server;
            project.ProjectKey = remoteProject.Key;
            project.SpreadsheetId = remoteProject.SpreadsheetId;
            project.SpreadsheetName = remoteProject.Name;
            AssetDatabase.CreateAsset(project, assetPath);
            AssetDatabase.SaveAssets();
            return project;
        }

        private static void Synchronize(List<GoogleSheetEntry> sheets, List<GoogleSheetListItem> remoteSheets)
        {
            var selectedById = new Dictionary<int, bool>();

            foreach (var sheet in sheets)
            {
                selectedById[sheet.SheetId] = sheet.Selected;
            }

            sheets.Clear();

            foreach (var remoteSheet in remoteSheets)
            {
                if (remoteSheet == null)
                {
                    continue;
                }

                var selected = !selectedById.TryGetValue(remoteSheet.SheetId, out var previousSelected) || previousSelected;
                sheets.Add(new GoogleSheetEntry
                {
                    Name = remoteSheet.Name,
                    SheetId = remoteSheet.SheetId,
                    Selected = selected
                });
            }
        }
    }
}
