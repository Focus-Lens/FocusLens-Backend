namespace FocusLens.Contracts;

public sealed record ParentStudyGoalsResponse(
    Guid StudentId,
    ParentStudyGoalResponse? CurrentStudyGoal,
    ParentStudyGoalProgressResponse? CurrentStudyGoalProgress,
    ParentStudyGoalProposalResponse? PendingStudyGoalProposal,
    ParentStudyGoalAcceptedProposalResponse? CurrentStudyGoalAcceptedProposal,
    ParentStudyGoalWeekResponse? ThisWeek);

public sealed record ParentStudyGoalResponse(string Frequency, string Target, DateOnly StartsOn, DateOnly EndsOn);
public sealed record ParentStudyGoalProgressResponse(string Completed, decimal CompletionPercentage, int DaysRemaining);
public sealed record ParentStudyGoalProposalResponse(Guid Id, string Frequency, string Target, DateOnly StartsOn, DateOnly EndsOn, string? SuggestedBy);
public sealed record ParentStudyGoalAcceptedProposalResponse(string? SuggestedBy, string? AcceptedBy);
public sealed record ParentStudyGoalWeekResponse(IReadOnlyCollection<ParentStudyGoalDayResponse> Days);
public sealed record ParentStudyGoalDayResponse(DateOnly Date, string Day, string StudyTime);
