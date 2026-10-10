using FocusLens.Domain;

namespace FocusLens.Application.Common.Interfaces;

public interface IStudentLocalTime
{
    DateTimeOffset GetLocalNow(Student student);
    DateOnly GetToday(Student student);
    DateTimeOffset ConvertFromUtc(DateTimeOffset utcInstant, Student student);
    DateOnly GetLocalDate(DateTimeOffset utcInstant, Student student);
    bool IsValidTimeZoneId(string? timeZoneId);
}