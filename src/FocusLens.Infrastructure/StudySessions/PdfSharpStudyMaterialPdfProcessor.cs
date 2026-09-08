using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.StudySessions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace FocusLens.Infrastructure.StudySessions;

public sealed class PdfSharpStudyMaterialPdfProcessor : IStudyMaterialPdfProcessor
{
    public Task<int> GetPageCountAsync(Stream content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using PdfDocument document = PdfReader.Open(content, PdfDocumentOpenMode.Import);
        return Task.FromResult(document.PageCount);
    }

    public Task<Stream> ExtractPagesAsync(
        Stream content,
        StudySessionPageRange pageRange,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using PdfDocument source = PdfReader.Open(content, PdfDocumentOpenMode.Import);
        if (pageRange.ToPage > source.PageCount)
        {
            throw new InvalidOperationException("The selected page range exceeds the source PDF.");
        }

        using PdfDocument derived = new();
        for (int pageIndex = pageRange.FromPage - 1; pageIndex < pageRange.ToPage; pageIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            derived.AddPage(source.Pages[pageIndex]);
        }

        var output = new MemoryStream();
        derived.Save(output, closeStream: false);
        output.Position = 0;
        return Task.FromResult<Stream>(output);
    }
}
