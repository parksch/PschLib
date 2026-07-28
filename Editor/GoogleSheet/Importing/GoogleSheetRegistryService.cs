using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PschLib
{
    internal static class GoogleSheetRegistryService
    {
        public static async Task ConnectAsync(GoogleSheetSettings settings, string webAppUrl)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var client = new GoogleSheetWebClient(webAppUrl);
            var response = await client.GetSheetListAsync();
            ValidateResponse(response);

            Synchronize(settings, response.Sheets);
            settings.WebAppUrl = webAppUrl.Trim();
            settings.SaveSettings();
        }

        public static async Task RefreshAsync(GoogleSheetSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!settings.IsConnected)
            {
                throw new InvalidOperationException("Google Sheet is not connected.");
            }

            var client = new GoogleSheetWebClient(settings.WebAppUrl);
            var response = await client.GetSheetListAsync();
            ValidateResponse(response);

            Synchronize(settings, response.Sheets);
            settings.SaveSettings();
        }

        public static void Disconnect(GoogleSheetSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.WebAppUrl = string.Empty;
            settings.Sheets.Clear();
            settings.SaveSettings();
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

        private static void Synchronize(GoogleSheetSettings settings, List<GoogleSheetListItem> remoteSheets)
        {
            var selectedById = new Dictionary<int, bool>();

            foreach (var sheet in settings.Sheets)
            {
                selectedById[sheet.SheetId] = sheet.Selected;
            }

            settings.Sheets.Clear();

            foreach (var remoteSheet in remoteSheets)
            {
                if (remoteSheet == null)
                {
                    continue;
                }

                var selected = !selectedById.TryGetValue(remoteSheet.SheetId, out var previousSelected) || previousSelected;
                settings.Sheets.Add(new GoogleSheetEntry
                {
                    Name = remoteSheet.Name,
                    SheetId = remoteSheet.SheetId,
                    Selected = selected
                });
            }
        }
    }
}
