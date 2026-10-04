using FocusLens.Application.Common.Interfaces;
using FocusLens.Contracts.Home;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.StudySessions;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Home;

public sealed class HomeQueryHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudySession> studySessionRepository,
    ICurrentUser currentUser,
    IStudentLocalTime studentLocalTime)
    : IRequestHandler<GetStudyOverviewQuery, StudyOverviewResponse?>,
        IRequestHandler<GetStudyStreakQuery, StudyStreakResponse?>,
        IRequestHandler<GetSessionsByDayQuery, SessionsByDayResponse?>
{
    public async Task<SessionsByDayResponse?> Handle(GetSessionsByDayQuery request,
        CancellationToken cancellationToken)
    {
        (Student Student, IReadOnlyCollection<DateOnly> SessionDates)? data = await GetSessionDatesAsync();
        if (data is null)
        {
            return null;
        }

        return new SessionsByDayResponse(data.Value.SessionDates
            .GroupBy(date => date)
            .OrderBy(group => group.Key)
            .Select(group => new SessionsByDayItemResponse(group.Key, group.Count()))
            .ToArray());
    }

    public async Task<StudyOverviewResponse?> Handle(GetStudyOverviewQuery request,
        CancellationToken cancellationToken)
    {
        (Student Student, IReadOnlyCollection<DateOnly> SessionDates)? data = await GetSessionDatesAsync();
        if (data is null)
        {
            return null;
        }

        DateOnly today = studentLocalTime.GetToday(data.Value.Student);
        return new StudyOverviewResponse(
            GetStreakDays(data.Value.SessionDates, today),
            data.Value.SessionDates.Count(date => date == today),
            null,
            null);
    }

    public async Task<StudyStreakResponse?> Handle(GetStudyStreakQuery request, CancellationToken cancellationToken)
    {
        (Student Student, IReadOnlyCollection<DateOnly> SessionDates)? data = await GetSessionDatesAsync();
        return data is null
            ? null
            : new StudyStreakResponse(GetStreakDays(data.Value.SessionDates, studentLocalTime.GetToday(data.Value.Student)));
    }

    private async Task<(Student Student, IReadOnlyCollection<DateOnly> SessionDates)?> GetSessionDatesAsync()
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return null;
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(item => item.UserId == userId);
        if (student is null)
        {
            return null;
        }

        IEnumerable<StudySession> sessions =
            await studySessionRepository.GetAllAsync(item =>
                item.StudentId == student.Id && item.StartedAtUtc != null);
        return (student, sessions
            .Select(item => studentLocalTime.GetLocalDate(item.StartedAtUtc!.Value, student))
            .ToArray());
    }

    private static int GetStreakDays(IReadOnlyCollection<DateOnly> sessionDates, DateOnly today)
    {
        HashSet<DateOnly> dates = sessionDates.ToHashSet();
        DateOnly current = today;
        int streak = 0;

        while (dates.Contains(current))
        {
            streak++;
            current = current.AddDays(-1);
        }

        return streak;
    }
}
