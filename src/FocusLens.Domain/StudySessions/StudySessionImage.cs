using FocusLens.Domain.Common;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Domain.StudySessions;

public sealed class StudySessionImage : AuditableEntity
{
    private StudySessionImage()
    {
    }

    private StudySessionImage(
        Guid id,
        Guid studySessionId,
        string originalFileName,
        string contentType,
        long fileSizeBytes,
        string storageReference)
        : base(id)
    {
        StudySessionId = studySessionId;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        FileSizeBytes = fileSizeBytes;
        StorageReference = storageReference;
    }

    public Guid StudySessionId { get; private set; }

    public string OriginalFileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long FileSizeBytes { get; private set; }

    public string StorageReference { get; private set; } = string.Empty;

    public static Result<StudySessionImage> Create(
        Guid studySessionId,
        string? originalFileName,
        string? contentType,
        long fileSizeBytes,
        string? storageReference)
    {
        if (studySessionId == Guid.Empty)
        {
            return Error.Validation("StudySessionImages.SessionIdRequired", "Study session id is required.");
        }

        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            return Error.Validation("StudySessionImages.FileNameRequired", "File name is required.");
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            return Error.Validation("StudySessionImages.ContentTypeRequired", "Image content type is required.");
        }

        if (fileSizeBytes <= 0)
        {
            return Error.Validation("StudySessionImages.FileSizeInvalid", "File size must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(storageReference))
        {
            return Error.Validation("StudySessionImages.StorageReferenceRequired", "Storage reference is required.");
        }

        return new StudySessionImage(
            Guid.CreateVersion7(),
            studySessionId,
            originalFileName.Trim(),
            contentType.Trim(),
            fileSizeBytes,
            storageReference.Trim());
    }
}