namespace FocusLens.Application.Features.Users.Dtos;

public sealed record UserProfileDto(
    Guid Id,
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    bool EmailVerified,
    DateTimeOffset MemberSince,
    DayOfWeek? WeekStartsOn,
    bool WeekStartSelectionRequired);
