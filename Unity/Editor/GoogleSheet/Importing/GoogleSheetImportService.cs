using System;
using System.Threading.Tasks;

namespace PschLib.GoogleSheets
{
    internal static class GoogleSheetImportService
    {
        public static async Task<GoogleSheetImportResult> PrepareAsync(GoogleSheetProject project, GoogleSheetEntry entry)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (!SheetDataCodeGenerator.TryValidateGeneratedTypeNames(project.Sheets, out var typeNameError))
            {
                throw new InvalidOperationException(typeNameError);
            }

            var document = await GoogleSheetDocumentLoader.LoadAsync(project, entry);

            return Prepare(project, document);
        }

        public static GoogleSheetImportResult Prepare(GoogleSheetProject project, SheetDocument document)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (!SheetHeaderParser.TryParse(document, out var fields, out var headerError))
            {
                throw new InvalidOperationException(headerError);
            }

            if (!SheetDataParser.TryParse(document, fields, out var rows, out var dataError))
            {
                throw new InvalidOperationException(dataError);
            }

            if (!SheetSharedEnumCatalog.TryUpdate(project, document.Name, fields, rows, out var sharedEnumError))
            {
                throw new InvalidOperationException(sharedEnumError);
            }

            var targetNamespace = GoogleSheetPathUtility.GetTargetNamespace(project);

            if (!SheetDataCodeGenerator.TryGenerate(document.Name, fields, rows, targetNamespace, out var generatedCode, out var generationError))
            {
                throw new InvalidOperationException(generationError);
            }

            return new GoogleSheetImportResult(document, fields, rows, generatedCode);
        }
    }
}
