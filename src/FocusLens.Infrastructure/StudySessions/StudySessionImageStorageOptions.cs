namespace FocusLens.Infrastructure.StudySessions;

public sealed class StudySessionImageStorageOptions
{
    public const string SectionName = "StudySessionImageStorage";

    public string RootPath { get; init; } = "../../storage/study-session-images";
}