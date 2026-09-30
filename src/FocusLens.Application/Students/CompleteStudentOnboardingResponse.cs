namespace FocusLens.Application.Students;

public sealed record CompleteStudentOnboardingResponse(
    Guid UserId,
    string FirstName,
    string LastName,
    string OnboardingStatus);
