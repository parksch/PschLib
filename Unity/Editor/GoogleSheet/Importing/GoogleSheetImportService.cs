using System;
using System.Threading.Tasks;

namespace PschLib
{
    internal static class GoogleSheetImportService
    {
        public static async Task<GoogleSheetImportResult> PrepareAsync(GoogleSheetSettings settings, GoogleSheetEntry entry)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            var document = await GoogleSheetDocumentLoader.LoadAsync(settings, entry);

            if (!SheetHeaderParser.TryParse(document, out var fields, out var headerError))
            {
                throw new InvalidOperationException(headerError);
            }

            if (!SheetDataParser.TryParse(document, fields, out var rows, out var dataError))
            {
                throw new InvalidOperationException(dataError);
            }

            if (!SheetDataCodeGenerator.TryGenerate(document.Name, fields, settings.TargetNamespace, out var generatedCode, out var generationError))
            {
                throw new InvalidOperationException(generationError);
            }

            return new GoogleSheetImportResult(document, fields, rows, generatedCode);
        }
    }
}
