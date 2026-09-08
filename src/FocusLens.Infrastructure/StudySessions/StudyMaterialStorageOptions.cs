namespace FocusLens.Infrastructure.StudySessions;

public sealed class StudyMaterialStorageOptions
{
    public const string SectionName = "StudyMaterialStorage";

    public string RootPath { get; init; } = "../../storage/study-materials";
}
