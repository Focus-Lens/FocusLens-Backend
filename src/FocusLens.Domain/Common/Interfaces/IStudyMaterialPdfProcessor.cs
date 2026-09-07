using FocusLens.Domain.StudySessions;

namespace FocusLens.Domain.Common.Interfaces;

public interface IStudyMaterialPdfProcessor
{
    Task<int> GetPageCountAsync(Stream content, CancellationToken cancellationToken);

    Task<Stream> ExtractPagesAsync(
        Stream content,
        StudySessionPageRange pageRange,
        CancellationToken cancellationToken);
}
