using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PschLib
{
    internal static class GoogleSheetDocumentLoader
    {
        public static async Task<SheetDocument> LoadAsync(GoogleSheetSettings settings, GoogleSheetEntry entry)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (!settings.IsConnected)
            {
                throw new InvalidOperationException("Google Sheet is not connected.");
            }

            var client = new GoogleSheetWebClient(settings.WebAppUrl);
            var response = await client.GetSheetDataAsync(entry.SheetId);
            ValidateResponse(response, entry.SheetId);

            return CreateDocument(response.Sheet);
        }

        private static void ValidateResponse(GoogleSheetDataResponse response, int requestedSheetId)
        {
            if (response == null)
            {
                throw new InvalidOperationException("Google Sheet returned no response.");
            }

            if (!response.Success)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Error) ? "Google Sheet data request failed." : response.Error);
            }

            if (response.Sheet == null)
            {
                throw new InvalidOperationException("Google Sheet response does not contain sheet data.");
            }

            if (response.Sheet.SheetId != requestedSheetId)
            {
                throw new InvalidOperationException($"Google Sheet returned SheetId {response.Sheet.SheetId} instead of {requestedSheetId}.");
            }

            if (string.IsNullOrWhiteSpace(response.Sheet.Name))
            {
                throw new InvalidOperationException("Google Sheet returned an empty sheet name.");
            }

            if (response.Sheet.Rows == null)
            {
                throw new InvalidOperationException("Google Sheet response does not contain rows.");
            }
        }

        private static SheetDocument CreateDocument(GoogleSheetDataPayload payload)
        {
            var rows = new List<List<string>>(payload.Rows.Count);

            foreach (var rowPayload in payload.Rows)
            {
                var cells = new List<string>();

                if (rowPayload?.Cells != null)
                {
                    foreach (var cell in rowPayload.Cells)
                    {
                        cells.Add(cell ?? string.Empty);
                    }
                }

                rows.Add(cells);
            }

            return new SheetDocument(payload.Name, rows);
        }
    }
}
