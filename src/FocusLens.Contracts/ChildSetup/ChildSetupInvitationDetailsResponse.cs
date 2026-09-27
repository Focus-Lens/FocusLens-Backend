using FocusLens.Contracts.Students;

namespace FocusLens.Contracts.ChildSetup;

public sealed record ChildSetupInvitationDetailsResponse(
    string Status,
    string Type,
    string? TargetEmail,
    string? FirstName,
    string? LastName,
    StudentGrade? Grade,
    string? CustomGrade,
    IReadOnlyCollection<ChildSetupInvitationSubjectResponse> Subjects,
    StudentGoal? Goal,
    StudyTimeGoalResponse? StudyTimeGoal,
    DateTimeOffset ExpiresAtUtc
);

public sealed record ChildSetupInvitationSubjectResponse(
    string Type,
    string? CustomName
);