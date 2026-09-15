using FocusLens.Application.Students;
using FocusLens.Contracts;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using FocusLens.Domain.StudySessions;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using DomainStudyTimeGoalPeriod = FocusLens.Domain.Students.StudyTimeGoalPeriod;

namespace FocusLens.Application.Parents;

public sealed class GetParentDashboardQueryHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudyGoalProposal> studyGoalProposalRepository,
    IBaseRepository<StudySession> studySessionRepository,
    IBaseRepository<StudySessionBehaviorWindow> behaviorWindowRepository,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<GetParentDashboardQuery, Result<ParentDashboardResponse>>
{
    private const int RecentSessionCount = 5;

    public async Task<Result<ParentDashboardResponse>> Handle(
        GetParentDashboardQuery request,
        CancellationToken cancellationToken)
    {
        Result<ParentDashboardContext> contextResult = await ParentDashboardHelpers.ResolveContextAsync(
            request.StudentId,
            parentRepository,
            relationshipRepository,
            studentRepository,
            currentUser);

        if (contextResult.IsError)
        {
            return contextResult.Errors;
        }

        Student student = contextResult.Value.Student;
        Parent parent = contextResult.Value.Parent;
        bool shareSummaries = student.ShareSessionSummariesWithParents;
        bool shareTrends = student.ShareSubjectTrendsWithParents;

        if (parent.WeekStartsOn is not DayOfWeek weekStartsOn)
        {
            return Error.Validation(
                "ParentDashboard.WeekStartsOnRequired",
                "Choose a week start day before viewing weekly dashboard data.");
        }

        StudyGoalProposal? pendingProposal =
            await studyGoalProposalRepository.FirstOrDefaultAsync(proposal =>
                proposal.StudentId == student.Id &&
                proposal.Status == StudyGoalProposalStatus.Pending);

        StudyGoalProposal? acceptedProposal = (await studyGoalProposalRepository.GetAllAsync(proposal =>
                proposal.StudentId == student.Id &&
                proposal.Status == StudyGoalProposalStatus.Accepted))
            .OrderByDescending(proposal => proposal.RespondedAtUtc)
            .ThenByDescending(proposal => proposal.CreatedAtUtc)
            .FirstOrDefault();

        Parent? acceptedProposalParent = acceptedProposal is null
            ? null
            : await parentRepository.FirstOrDefaultAsync(
                parent => parent.Id == acceptedProposal.ParentId,
                parent => parent.User);

        StudySession[] startedSessions = (await studySessionRepository.GetAllAsync(session =>
                session.StudentId == student.Id &&
                session.StartedAtUtc != null))
            .ToArray();

        int? focusQuality = null;

        if (shareTrends && startedSessions.Length > 0)
        {
            Guid[] sessionIds = startedSessions
                .Select(session => session.Id)
                .ToArray();

            int[] focusScores = (await behaviorWindowRepository.GetAllAsync(window =>
                    sessionIds.Contains(window.StudySessionId) &&
                    window.FocusScore != null))
                .Select(window => window.FocusScore!.Value)
                .ToArray();

            if (focusScores.Length > 0)
            {
                focusQuality = (int)Math.Round(
                    focusScores.Average(),
                    MidpointRounding.AwayFromZero);
            }
        }

        DateTimeOffset utcNow = timeProvider.GetUtcNow();
        IReadOnlyDictionary<Guid, string?> subjectNamesById = ParentDashboardHelpers.GetSubjectNamesById(student);
        ParentDashboardWeeklyStudyPulseResponse weeklyPulse = CreateWeeklyPulse(startedSessions, utcNow, weekStartsOn);

        return new ParentDashboardResponse(
            student.Id,
            shareTrends ? ToCurrentGoalResponse(student.StudyTimeGoal, weekStartsOn) : null,
            shareTrends ? CreateCurrentGoalProgress(student.StudyTimeGoal, startedSessions, utcNow) : null,
            pendingProposal?.ToResponse(weekStartsOn, parent),
            acceptedProposal?.ToResponse(acceptedProposalParent?.WeekStartsOn ?? weekStartsOn, acceptedProposalParent),
            shareSummaries
                ? ParentDashboardHelpers.OrderSessions(startedSessions)
                    .Take(RecentSessionCount)
                    .Select(session => ParentDashboardHelpers.ToSessionResponse(session, subjectNamesById, utcNow))
                    .ToArray()
                : [],
            shareSummaries
                ? startedSessions.Count(session =>
                {
                    DateOnly sessionDate = DateOnly.FromDateTime(session.StartedAtUtc!.Value.UtcDateTime);
                    return session.Status == StudySessionStatus.Completed &&
                           sessionDate >= weeklyPulse.StartsOn &&
                           sessionDate <= weeklyPulse.EndsOn;
                })
                : 0,
            shareTrends ? weeklyPulse.Days.Count(day => day.ActualStudyMinutes > 0) : 0,
            focusQuality,
            shareTrends
                ? weeklyPulse
                : new ParentDashboardWeeklyStudyPulseResponse(
                    weekStartsOn,
                    weeklyPulse.StartsOn,
                    weeklyPulse.EndsOn,
                    0,
                    0,
                    0,
                    weeklyPulse.Days
                        .Select(day => new ParentDashboardWeeklyStudyPulseDayResponse(day.Date, 0))
                        .ToArray()));
    }

    private static StudyTimeGoalResponse? ToCurrentGoalResponse(StudyTimeGoal? goal, DayOfWeek weekStartsOn)
    {
        return goal is null
            ? null
            : StudyGoalProposalMappings.ToResponse(goal, weekStartsOn);
    }

    private static ParentDashboardWeeklyStudyPulseResponse CreateWeeklyPulse(
        IReadOnlyCollection<StudySession> sessions,
        DateTimeOffset utcNow,
        DayOfWeek weekStartsOn)
    {
        DateOnly today = DateOnly.FromDateTime(utcNow.UtcDateTime);
        DateOnly startsOn = ParentWeekdayOrder.GetWeekStart(today, weekStartsOn);
        DateOnly previousStartsOn = startsOn.AddDays(-ParentDashboardHelpers.PulseDays);
        DateOnly endsOn = startsOn.AddDays(ParentDashboardHelpers.PulseDays - 1);
        DateOnly previousEndsOn = previousStartsOn.AddDays(ParentDashboardHelpers.PulseDays - 1);

        ParentDashboardWeeklyStudyPulseDayResponse[] days = Enumerable
            .Range(0, ParentDashboardHelpers.PulseDays)
            .Select(offset =>
            {
                DateOnly date = startsOn.AddDays(offset);
                return new ParentDashboardWeeklyStudyPulseDayResponse(
                    date,
                    ParentDashboardHelpers.GetActualStudyMinutesForDates(sessions, date, date, utcNow));
            })
            .ToArray();

        int actualStudyMinutes = days.Sum(day => day.ActualStudyMinutes);
        int previousPeriodActualStudyMinutes = ParentDashboardHelpers.GetActualStudyMinutesForDates(
            sessions,
            previousStartsOn,
            previousEndsOn,
            utcNow);

        return new ParentDashboardWeeklyStudyPulseResponse(
            weekStartsOn,
            startsOn,
            endsOn,
            actualStudyMinutes,
            previousPeriodActualStudyMinutes,
            actualStudyMinutes - previousPeriodActualStudyMinutes,
            days);
    }

    private static ParentDashboardStudyGoalProgressResponse? CreateCurrentGoalProgress(
        StudyTimeGoal? goal,
        IReadOnlyCollection<StudySession> sessions,
        DateTimeOffset utcNow)
    {
        if (goal is null || goal.StartDate is null)
        {
            return null;
        }

        DateOnly today = DateOnly.FromDateTime(utcNow.UtcDateTime);
        if (today < goal.StartDate.Value)
        {
            DateOnly futureStartsOn = goal.StartDate.Value;
            DateOnly futureEndsOn = goal.Period == DomainStudyTimeGoalPeriod.Weekly
                ? futureStartsOn.AddDays(ParentDashboardHelpers.PulseDays - 1)
                : futureStartsOn;

            return new ParentDashboardStudyGoalProgressResponse(
                0,
                0,
                futureEndsOn.DayNumber - futureStartsOn.DayNumber + 1,
                futureStartsOn,
                futureEndsOn,
                CreateGoalProgressDays(sessions, futureStartsOn, futureEndsOn, utcNow, goal.StartDate.Value));
        }

        DateOnly startsOn;
        DateOnly endsOn;
        int daysRemaining;

        if (goal.Period == DomainStudyTimeGoalPeriod.Daily)
        {
            if (!goal.Days.Contains(today.DayOfWeek))
            {
                return new ParentDashboardStudyGoalProgressResponse(
                    0,
                    0,
                    0,
                    today,
                    today,
                    CreateGoalProgressDays(sessions, today, today, utcNow, today));
            }

            startsOn = today;
            endsOn = today;
            daysRemaining = 1;
        }
        else
        {
            DayOfWeek goalWeekStartsOn = goal.Days.Single();
            startsOn = ParentWeekdayOrder.GetWeekStart(today, goalWeekStartsOn);
            endsOn = startsOn.AddDays(6);
            daysRemaining = endsOn.DayNumber - today.DayNumber + 1;
        }

        ParentDashboardWeeklyStudyPulseDayResponse[] days = CreateGoalProgressDays(
            sessions,
            startsOn,
            endsOn,
            utcNow,
            goal.StartDate.Value);
        int completedMinutes = days.Sum(day => day.ActualStudyMinutes);

        decimal completionPercentage = goal.TargetMinutes <= 0
            ? 0
            : Math.Round(completedMinutes * 100m / goal.TargetMinutes, 2);

        return new ParentDashboardStudyGoalProgressResponse(
            completedMinutes,
            completionPercentage,
            daysRemaining,
            startsOn,
            endsOn,
            days);
    }

    private static ParentDashboardWeeklyStudyPulseDayResponse[] CreateGoalProgressDays(
        IReadOnlyCollection<StudySession> sessions,
        DateOnly startsOn,
        DateOnly endsOn,
        DateTimeOffset utcNow,
        DateOnly countFromDate)
    {
        return Enumerable
            .Range(0, endsOn.DayNumber - startsOn.DayNumber + 1)
            .Select(offset =>
            {
                DateOnly date = startsOn.AddDays(offset);
                int actualStudyMinutes = date < countFromDate
                    ? 0
                    : ParentDashboardHelpers.GetActualStudyMinutesForDates(sessions, date, date, utcNow);

                return new ParentDashboardWeeklyStudyPulseDayResponse(date, actualStudyMinutes);
            })
            .ToArray();
    }
}
