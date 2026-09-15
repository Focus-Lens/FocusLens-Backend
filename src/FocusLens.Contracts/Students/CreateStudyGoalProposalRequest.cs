namespace FocusLens.Contracts.Students;

public sealed record CreateStudyGoalProposalRequest(
    StudyTimeGoalPeriod Period,
    decimal TargetHours
);
