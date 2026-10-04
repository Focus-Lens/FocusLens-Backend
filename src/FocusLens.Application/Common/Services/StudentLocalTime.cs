using FocusLens.Application.Common.Interfaces;
using FocusLens.Domain;

namespace FocusLens.Application.Common.Services;

/// <summary>
/// The sole conversion boundary for student calendar calculations. Timestamps stay UTC;
/// legacy students without a configured zone intentionally use UTC until their device updates it.
/// </summary>
public sealed class StudentLocalTime(TimeProvider timeProvider) : IStudentLocalTime
{
    public DateTimeOffset GetLocalNow(Student student) => ConvertFromUtc(timeProvider.GetUtcNow(), student);

    public DateOnly GetToday(Student student) => GetLocalDate(timeProvider.GetUtcNow(), student);

    public DateTimeOffset ConvertFromUtc(DateTimeOffset utcInstant, Student student) =>
        TimeZoneInfo.ConvertTime(utcInstant, ResolveTimeZone(student.TimeZoneId));

    public DateOnly GetLocalDate(DateTimeOffset utcInstant, Student student) =>
        DateOnly.FromDateTime(ConvertFromUtc(utcInstant, student).DateTime);

    public bool IsValidTimeZoneId(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return false;
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim()); return true; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return TimeZoneInfo.Utc;
        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }
}
