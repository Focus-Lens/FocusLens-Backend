namespace FocusLens.Contracts.Students;

/// <summary>
///     The simple study-time input collected during student onboarding.
///     The application derives the weekly goal schedule from the student's calendar.
/// </summary>
public sealed record OnboardingStudyTimeGoalRequest(decimal TargetHours);