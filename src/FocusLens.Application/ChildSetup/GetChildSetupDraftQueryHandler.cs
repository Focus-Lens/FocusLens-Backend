using FocusLens.Application.Common.Mappings;
using FocusLens.Application.Parents;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.ChildSetup;

public sealed class GetChildSetupDraftQueryHandler(
    IBaseRepository<Parent> parentRepository,
    IBaseRepository<ChildSetupDraft> childSetupDraftRepository,
    ICurrentUser currentUser
) : IRequestHandler<GetChildSetupDraftQuery, Result<ChildSetupDraftResponse>>
{
    public async Task<Result<ChildSetupDraftResponse>> Handle(
        GetChildSetupDraftQuery request,
        CancellationToken cancellationToken
    )
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Parents.CurrentUserUnavailable",
                "The current user could not be identified."
            );
        }

        Parent? parent = await parentRepository.FirstOrDefaultAsync(parent =>
            parent.UserId == userId
        );

        if (parent is null)
        {
            return Error.NotFound(
                "Parents.NotFound",
                "The current user does not have a parent profile."
            );
        }

        if (request.DraftId == Guid.Empty)
        {
            return Error.Validation(
                "ChildSetup.InvalidDraftId",
                "The child setup draft ID is invalid."
            );
        }

        ChildSetupDraft? draft = await childSetupDraftRepository.GetByIdAsync(
            request.DraftId,
            draft => draft.Subjects
        );

        if (draft is null || draft.ParentId != parent.Id)
        {
            return Error.NotFound(
                "ChildSetup.NotFound",
                "The child setup draft could not be found."
            );
        }

        return new ChildSetupDraftResponse(
            draft.Id,
            draft.Status.ToString(),
            draft.FirstName,
            draft.LastName,
            draft.DateOfBirth,
            draft.Grade is null ? null : StudentEnumMapper.ToContract(draft.Grade.Value),
            draft
                .Subjects.Select(subject => new ChildSetupSubjectResponse(
                    subject.Id,
                    subject.Type.ToString(),
                    subject.CustomName
                ))
                .ToList(),
            draft
                .StudyPriorities.Select(priority => Enum.Parse<StudyPriority>(priority.ToString()))
                .ToList(),
            draft.StudyTimeGoal is null
                ? null
                : new StudyTimeGoalResponse(
                    draft.StudyTimeGoal.Period.ToString(),
                    draft.StudyTimeGoal.TargetMinutes,
                    parent.WeekStartsOn is DayOfWeek value
                        ? ParentWeekdayOrder.OrderDays(draft.StudyTimeGoal.Days, value)
                        : draft.StudyTimeGoal.Days,
                    draft.StudyTimeGoal.StartDate
                )
        );
    }
}
