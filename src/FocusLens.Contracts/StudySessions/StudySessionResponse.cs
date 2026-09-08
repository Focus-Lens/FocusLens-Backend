namespace FocusLens.Contracts.StudySessions;

public sealed record StudySessionResponse(
    Guid Id,
    Guid StudentId,
    StudySessionMode Mode,
    string Status,
    DateTimeOffset? StartedAtUtc,
    Guid? SelectedSubjectId,
    int? FocusDurationMinutes,
    int EstimatedStudyTimeMinutes,
    StudyMaterialResponse? Material,
    StudySessionPageRangeResponse? PageRange,
    IReadOnlyCollection<Guid> SelectedSectionIds);

public sealed record StudyMaterialResponse(
    Guid Id,
    string FileName,
    long FileSizeBytes,
    int PageCount,
    string StorageReference,
    string? DerivedStorageReference,
    StudyMaterialSource Source,
    IReadOnlyCollection<StudyMaterialSectionResponse> Sections);

public sealed record StudyMaterialSectionResponse(
    Guid Id,
    string Name,
    int EstimatedDurationMinutes);

public sealed record StudySessionPageRangeResponse(int FromPage, int ToPage);
