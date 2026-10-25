namespace FocusLens.Contracts.Students;

public sealed record UpdateStudentProfileRequest(
    string FirstName,
    string LastName,
    string? PhoneNumber,
    DateOnly? DateOfBirth,
    StudentGrade Grade,
    string? CustomGrade);
