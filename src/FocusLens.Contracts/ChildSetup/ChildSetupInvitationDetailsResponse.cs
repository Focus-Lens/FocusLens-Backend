using FocusLens.Contracts.Students;

namespace FocusLens.Contracts.ChildSetup;

public sealed record ChildSetupInvitationDetailsResponse(
    string Status,
    string? FirstName,
    string? LastName,
    StudentGrade? Grade,
    IReadOnlyCollection<ChildSetupInvitationSubjectResponse> Subjects,
    IReadOnlyCollection<StudyPriority> StudyPriorities,
    StudyTimeGoalResponse? StudyTimeGoal,
    DateTimeOffset ExpiresAtUtc
);

public sealed record ChildSetupInvitationSubjectResponse(
    string Type,
    string? CustomName
);