namespace FocusLens.Contracts.Students;

public sealed record StudyGoalProposalResponse(
    Guid Id,
    Guid StudentId,
    Guid ParentId,
    string Status,
    StudyTimeGoalResponse Goal,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RespondedAtUtc,
    string? SuggestedByParentName
);
