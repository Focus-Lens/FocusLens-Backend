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
    ICurrentUser currentUser)
    : IRequestHandler<GetStudyOverviewQuery, StudyOverviewResponse?>,
        IRequestHandler<GetStudyStreakQuery, StudyStreakResponse?>,
        IRequestHandler<GetSessionsByDayQuery, SessionsByDayResponse?>
{
    public async Task<SessionsByDayResponse?> Handle(GetSessionsByDayQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<DateOnly>? sessionDates = await GetSessionDatesAsync();
        if (sessionDates is null)
        {
            return null;
        }

        return new SessionsByDayResponse(sessionDates
            .GroupBy(date => date)
            .OrderBy(group => group.Key)
            .Select(group => new SessionsByDayItemResponse(group.Key, group.Count()))
            .ToArray());
    }

    public async Task<StudyOverviewResponse?> Handle(GetStudyOverviewQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<DateOnly>? sessionDates = await GetSessionDatesAsync();
        if (sessionDates is null)
        {
            return null;
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new StudyOverviewResponse(
            GetStreakDays(sessionDates, today),
            sessionDates.Count(date => date == today),
            null,
            null);
    }

    public async Task<StudyStreakResponse?> Handle(GetStudyStreakQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<DateOnly>? sessionDates = await GetSessionDatesAsync();
        return sessionDates is null
            ? null
            : new StudyStreakResponse(GetStreakDays(sessionDates, DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    private async Task<IReadOnlyCollection<DateOnly>?> GetSessionDatesAsync()
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
        return sessions
            .Select(item => DateOnly.FromDateTime(item.StartedAtUtc!.Value.UtcDateTime))
            .ToArray();
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