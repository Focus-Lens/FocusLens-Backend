using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Parents;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Notifications;
using FocusLens.Domain.Students;
using MediatR;
using DomainStudyTimeGoalPeriod = FocusLens.Domain.Students.StudyTimeGoalPeriod;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Students;

public sealed class CreateStudyGoalProposalCommandHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<Student> studentRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    IBaseRepository<StudyGoalProposal> proposalRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    INotificationWriter? notificationWriter = null,
    IStudentLocalTime? studentLocalTime = null
) : IRequestHandler<CreateStudyGoalProposalCommand, Result<StudyGoalProposalResponse>>
{
    public async Task<Result<StudyGoalProposalResponse>> Handle(
        CreateStudyGoalProposalCommand request,
        CancellationToken cancellationToken
    )
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "StudyGoalProposals.CurrentUserUnavailable",
                "The current user could not be identified."
            );
        }

        if (request.StudentId == Guid.Empty)
        {
            return Error.Validation(
                "StudyGoalProposals.InvalidStudentId",
                "Student ID is required."
            );
        }

        Parent? parent = await parentRepository.FirstOrDefaultAsync(
            parent => parent.UserId == userId,
            parent => parent.User
        );

        if (parent is null)
        {
            return Error.NotFound(
                "StudyGoalProposals.ParentNotFound",
                "The parent profile was not found."
            );
        }

        if (parent.WeekStartsOn is not DayOfWeek weekStartsOn)
        {
            return Error.Validation(
                "StudyGoalProposals.WeekStartsOnRequired",
                "Choose a week start day before proposing study goals."
            );
        }

        ParentStudentRelationship? relationship =
            await relationshipRepository.FirstOrDefaultAsync(
                relationship =>
                    relationship.ParentId == parent.Id
                    && relationship.StudentId == request.StudentId
                    && relationship.Status == RelationshipStatus.Active
            );

        if (relationship is null)
        {
            return Error.Forbidden(
                "StudyGoalProposals.RelationshipRequired",
                "You can only propose goals for an active student relationship."
            );
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(
            student => student.Id == request.StudentId,
            student => student.User
        );

        if (student is null
            || student.User.IsDisabled
            || student.User.DeletedAtUtc is not null)
        {
            return Error.NotFound(
                "StudyGoalProposals.StudentNotFound",
                "The student profile was not found."
            );
        }

        StudyGoalProposal? pendingProposal =
            await proposalRepository.FirstOrDefaultAsync(
                proposal =>
                    proposal.StudentId == request.StudentId
                    && proposal.Status == StudyGoalProposalStatus.Pending
            );

        if (pendingProposal is not null)
        {
            return Error.Conflict(
                "StudyGoalProposals.PendingProposalExists",
                "This student already has a pending study goal proposal."
            );
        }

        if (request.Request.TargetHours <= 0)
        {
            return Error.Validation(
                "StudyGoalProposals.TargetHoursInvalid",
                "Target hours must be greater than zero."
            );
        }

        if (request.Request.TargetHours > int.MaxValue / 60m)
        {
            return Error.Validation(
                "StudyGoalProposals.TargetHoursInvalid",
                "Target hours is too large."
            );
        }

        int targetMinutes = (int)
            Math.Round(
                request.Request.TargetHours * 60m,
                MidpointRounding.AwayFromZero
            );

        studentLocalTime ??= new FocusLens.Application.Common.Services.StudentLocalTime(timeProvider);
        DateOnly today = studentLocalTime.GetToday(student);

        DateOnly startsOn =
            ParentWeekdayOrder.GetWeekStart(today, weekStartsOn);

        StudyGoalProposal? acceptedProposal =
            await proposalRepository.FirstOrDefaultAsync(
                proposal =>
                    proposal.StudentId == request.StudentId
                    && proposal.Status == StudyGoalProposalStatus.Accepted
                    && proposal.Goal.StartDate == startsOn
            );

        if (acceptedProposal is not null)
        {
            return Error.Conflict(
                "StudyGoalProposals.AcceptedProposalExistsForCurrentWeek",
                "This student already accepted a study goal proposal for the current week."
            );
        }

        Result<StudyTimeGoal> goal = StudyTimeGoal.Create(
            DomainStudyTimeGoalPeriod.Weekly,
            targetMinutes,
            [weekStartsOn],
            startsOn
        );

        if (goal.IsError)
        {
            return goal.Errors;
        }

        StudyGoalProposal proposal =
            new(parent.Id, request.StudentId, goal.Value);

        proposalRepository.Add(proposal);

        if (notificationWriter is not null)
        {
            await notificationWriter.AddAsync(
                student.UserId,
                NotificationAudience.Student,
                NotificationCategory.StudyGoalProposal,
                "New study goal request",
                $"{parent.User.FirstName} proposed a weekly study goal of {request.Request.TargetHours:0.##} hours.",
                $"/students/me/study-goal-proposals/{proposal.Id}",
                "Review goal",
                $"study-goal-proposal:{proposal.Id}:student"
            );
        }

        await unitOfWork.SaveChangesAsync();

        return proposal.ToResponse(weekStartsOn, parent);
    }
}
