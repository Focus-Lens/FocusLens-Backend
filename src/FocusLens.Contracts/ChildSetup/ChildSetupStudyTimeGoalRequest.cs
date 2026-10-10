namespace FocusLens.Contracts.ChildSetup;

/// <summary>
///     The simplified study-time input collected while a parent sets up a child profile.
///     The application derives the weekly schedule from the parent's calendar settings.
/// </summary>
public sealed record ChildSetupStudyTimeGoalRequest(decimal TargetHours);