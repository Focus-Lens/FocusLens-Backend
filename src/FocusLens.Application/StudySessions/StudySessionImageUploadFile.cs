namespace FocusLens.Application.StudySessions;

public sealed record StudySessionImageUploadFile(
    string FileName,
    string ContentType,
    long FileSizeBytes,
    Func<CancellationToken, Task<Stream>> OpenReadAsync);