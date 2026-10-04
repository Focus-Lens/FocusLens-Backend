using FocusLens.Contracts;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using FocusLens.Domain.StudySessions;
using FocusLens.Application.Common.Utilities;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Parents;

public sealed class GetParentStudyGoalsQueryHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudyGoalProposal> proposalRepository,
    IBaseRepository<StudySession> sessionRepository,
    IBaseRepository<StudentWeek> weekRepository,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IStudentLocalTime? studentLocalTime = null) : IRequestHandler<GetParentStudyGoalsQuery, Result<ParentStudyGoalsResponse>>
{
    public async Task<Result<ParentStudyGoalsResponse>> Handle(GetParentStudyGoalsQuery request, CancellationToken cancellationToken)
    {
        Result<ParentDashboardContext> context = await ParentDashboardHelpers.ResolveContextAsync(request.StudentId, parentRepository, relationshipRepository, studentRepository, currentUser);
        if (context.IsError) return context.Errors;
        Student student = context.Value.Student;
        DateTimeOffset now = timeProvider.GetUtcNow();
        studentLocalTime ??= new FocusLens.Application.Common.Services.StudentLocalTime(timeProvider);
        DateOnly today = studentLocalTime.GetToday(student);
        StudyTimeGoal? goal = student.StudyTimeGoal?.IsActiveOn(today) == true ? student.StudyTimeGoal : null;
        StudyGoalProposal? pending = goal is null
            ? (await proposalRepository.GetAllAsync(x => x.StudentId == student.Id && x.Status == StudyGoalProposalStatus.Pending))
                .Where(x => x.Goal.IsActiveOn(today))
                .OrderByDescending(x => x.CreatedAtUtc)
                .FirstOrDefault()
            : null;
        StudyGoalProposal? accepted = goal is null
            ? null
            : (await proposalRepository.GetAllAsync(x => x.StudentId == student.Id && x.Status == StudyGoalProposalStatus.Accepted))
                .Where(x => IsForGoal(x.Goal, goal))
                .OrderByDescending(x => x.RespondedAtUtc)
                .ThenByDescending(x => x.CreatedAtUtc)
                .FirstOrDefault();
        Parent? proposalParent = accepted is null && pending is null ? null : await parentRepository.FirstOrDefaultAsync(x => x.Id == (accepted ?? pending)!.ParentId, x => x.User);
        StudySession[] sessions = (await sessionRepository.GetAllAsync(x => x.StudentId == student.Id && x.StartedAtUtc != null, x => x.PauseIntervals)).ToArray();
        ParentStudyGoalResponse? current = goal is null ? null : ToGoal(goal);
        ParentStudyGoalProgressResponse? progress = goal is null ? null : ToProgress(goal, sessions, now, today, student, studentLocalTime);
        Result<ParentOverviewWeekResponse>? resolvedWeek = goal is null ? null : await OverviewWeekResolver.ResolveAsync(context.Value, studentLocalTime, weekRepository);
        if (resolvedWeek?.IsError == true) return resolvedWeek.Errors;
        ParentStudyGoalWeekResponse? thisWeek = resolvedWeek is null ? null : CreateWeek(sessions, resolvedWeek.Value.StartsOn, now, student, studentLocalTime);
        return new ParentStudyGoalsResponse(student.Id, current, progress,
            pending is null ? null : new(pending.Id, pending.Goal.Period.ToString(), FormatMinutes(pending.Goal.TargetMinutes), pending.Goal.StartDate!.Value, EndsOn(pending.Goal), Name(proposalParent)),
            accepted is null ? null : new(Name(proposalParent), student.User is null ? null : Name(student.User.FirstName, student.User.LastName)), thisWeek);
    }
    private static ParentStudyGoalResponse ToGoal(StudyTimeGoal goal) => new(goal.Period.ToString(), FormatMinutes(goal.TargetMinutes), goal.StartDate!.Value, EndsOn(goal));
    private static DateOnly EndsOn(StudyTimeGoal goal) => goal.EndDate ?? goal.StartDate!.Value;
    private static ParentStudyGoalProgressResponse ToProgress(StudyTimeGoal goal, IEnumerable<StudySession> sessions, DateTimeOffset now, DateOnly today, Student student, IStudentLocalTime studentLocalTime)
    { DateOnly start = goal.Period == StudyTimeGoalPeriod.Weekly ? goal.StartDate!.Value : today; DateOnly end = goal.Period == StudyTimeGoalPeriod.Weekly ? goal.EndDate!.Value : today; int completed = ParentDashboardHelpers.GetActualStudyMinutesForDates(sessions, start, end, now, student, studentLocalTime); return new(FormatMinutes(completed), Math.Round(completed * 100m / goal.TargetMinutes, 2), end.DayNumber - today.DayNumber + 1); }
    private static ParentStudyGoalWeekResponse CreateWeek(IEnumerable<StudySession> sessions, DateOnly start, DateTimeOffset now, Student student, IStudentLocalTime studentLocalTime) => new(Enumerable.Range(0, 7).Select(i => { DateOnly date = start.AddDays(i); return new ParentStudyGoalDayResponse(date, date.DayOfWeek.ToString(), FormatMinutes(ParentDashboardHelpers.GetActualStudyMinutesForDates(sessions, date, date, now, student, studentLocalTime))); }).ToArray());
    private static bool IsForGoal(StudyTimeGoal proposalGoal, StudyTimeGoal currentGoal) =>
        proposalGoal.Period == currentGoal.Period &&
        proposalGoal.StartDate == currentGoal.StartDate &&
        proposalGoal.EndDate == currentGoal.EndDate;
    private static string FormatMinutes(int minutes) => DurationDisplayFormatter.FormatMinutes(minutes);
    private static string? Name(Parent? parent) => parent?.User is null ? null : Name(parent.User.FirstName, parent.User.LastName);
    private static string? Name(string? first, string? last) { string name = $"{first} {last}".Trim(); return string.IsNullOrEmpty(name) ? null : name; }
}
