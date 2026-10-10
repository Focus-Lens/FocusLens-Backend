using FocusLens.Application.Common.Services;
using FocusLens.Domain;

namespace FocusLens.Application.UnitTests.Students;

public sealed class StudentLocalTimeTests
{
    [Theory]
    [InlineData("Africa/Cairo", 2026, 10, 3)]
    [InlineData("America/Los_Angeles", 2026, 10, 2)]
    public void GetToday_UsesTheStudentsIanaCalendar(
        string timeZoneId,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        Student student = new(Guid.NewGuid());
        student.SetTimeZoneId(timeZoneId);
        StudentLocalTime service = new(new FixedTimeProvider(
            new DateTimeOffset(2026, 10, 2, 22, 30, 0, TimeSpan.Zero)));

        DateOnly today = service.GetToday(student);

        Assert.Equal(new DateOnly(expectedYear, expectedMonth, expectedDay), today);
    }

    [Fact]
    public void GetToday_UsesUtcUntilALegacyStudentSetsTheirTimeZone()
    {
        Student student = new(Guid.NewGuid());
        StudentLocalTime service = new(new FixedTimeProvider(
            new DateTimeOffset(2026, 10, 2, 22, 30, 0, TimeSpan.Zero)));

        Assert.Equal(new DateOnly(2026, 10, 2), service.GetToday(student));

        student.SetTimeZoneId("Africa/Cairo");
        Assert.Equal(new DateOnly(2026, 10, 3), service.GetToday(student));
    }

    [Fact]
    public void IsValidTimeZoneId_RejectsUnknownIds()
    {
        StudentLocalTime service = new(TimeProvider.System);

        Assert.False(service.IsValidTimeZoneId("Not/A_TimeZone"));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}