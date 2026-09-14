namespace FocusLens.Domain.StudySessions;

public sealed class StudySessionImageUploadOptions
{
    public const string SectionName = "StudySessionImageUpload";

    public int MaxImagesPerRequest { get; init; } = 10;

    public long MaxImageSizeBytes { get; init; } = 5 * 1024 * 1024;
}