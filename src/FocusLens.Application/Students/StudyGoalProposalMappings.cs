using FocusLens.Contracts.Students;
using FocusLens.Application.Parents;
using FocusLens.Domain;
using FocusLens.Domain.Students;

namespace FocusLens.Application.Students;

internal static class StudyGoalProposalMappings
{
    public static StudyGoalProposalResponse ToResponse(
        this StudyGoalProposal proposal,
        DayOfWeek? weekStartsOn = null,
        Parent? parent = null)
    {
        return new StudyGoalProposalResponse(
            proposal.Id,
            proposal.StudentId,
            proposal.ParentId,
            proposal.Status.ToString(),
            ToResponse(proposal.Goal, weekStartsOn),
            proposal.CreatedAtUtc,
            proposal.RespondedAtUtc,
            GetSuggestedByParentName(parent));
    }

    public static StudyTimeGoalResponse ToResponse(StudyTimeGoal goal, DayOfWeek? weekStartsOn = null)
    {
        return new StudyTimeGoalResponse(
            goal.Period.ToString(),
            goal.TargetMinutes,
            weekStartsOn is DayOfWeek value
                ? ParentWeekdayOrder.OrderDays(goal.Days, value)
                : goal.Days,
            goal.StartDate,
            goal.EndDate);
    }

    private static string? GetSuggestedByParentName(Parent? parent)
    {
        if (parent?.User is not { } user)
        {
            return null;
        }

        string name = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name)
            ? null
            : name;
    }
}
