namespace FocusLens.Contracts.Students;

/// <summary>Sets the authenticated student's device IANA time zone identifier.</summary>
public sealed record UpdateStudentTimeZoneRequest(string TimeZoneId);
