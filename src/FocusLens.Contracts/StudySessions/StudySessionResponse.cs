namespace FocusLens.Contracts.StudySessions;

public sealed record StudySessionResponse(
    Guid Id,
    Guid StudentId,
    StudySessionMode Mode,
    string Status,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? PausedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    int RemainingSeconds,
    int? CurrentPage,
    DateTimeOffset? LastActivityAtUtc,
    IReadOnlyCollection<Guid> CompletedSectionIds,
    Guid? SelectedSubjectId,
    int? FocusDurationMinutes,
    int EstimatedStudyTimeMinutes);

public sealed record StudyMaterialResponse(
    Guid Id,
    string FileName,
    long FileSizeBytes,
    int PageCount,
    string StorageReference,
    StudyMaterialSource Source);

public sealed record StudyMaterialSectionResponse(
    Guid Id,
    string Name,
    int EstimatedDurationMinutes);

public sealed record StudySessionSelectionResponse(
    Guid Id,
    Guid StudySessionId,
    Guid StudyMaterialId,
    int FromPage,
    int ToPage,
    IReadOnlyCollection<Guid> SelectedSectionIds,
    int EstimatedStudyTimeMinutes);

public sealed record StudySessionImageResponse(
    Guid Id,
    Guid StudySessionId,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    string StorageReference,
    DateTimeOffset UploadedAtUtc);

public sealed record StudySessionImagesUploadResponse(
    IReadOnlyCollection<StudySessionImageResponse> Images);
