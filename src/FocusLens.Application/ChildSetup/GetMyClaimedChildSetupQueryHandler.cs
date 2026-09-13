using FocusLens.Application.Common.Mappings;
using FocusLens.Contracts.ChildSetup;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.ChildSetup;

public sealed class GetMyClaimedChildSetupQueryHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<ChildSetupDraft> draftRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyClaimedChildSetupQuery, Result<ChildSetupDraftResponse>>
{
    public async Task<Result<ChildSetupDraftResponse>> Handle(
        GetMyClaimedChildSetupQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Students.CurrentUserUnavailable",
                "The current user could not be identified.");
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(item => item.UserId == userId);

        if (student is null)
        {
            return Error.NotFound(
                "Students.NotFound",
                "The current user does not have a student profile.");
        }

        ChildSetupDraft? draft = await draftRepository.FirstOrDefaultAsync(
            item => item.ClaimedByStudentId == student.Id,
            item => item.Subjects);

        if (draft is null)
        {
            return Error.NotFound(
                "ChildSetup.NotFound",
                "The current student does not have a claimed child setup.");
        }

        return new ChildSetupDraftResponse(
            draft.Id,
            draft.Status.ToString(),
            draft.FirstName,
            draft.LastName,
            draft.DateOfBirth,
            draft.Grade is null
                ? null
                : StudentEnumMapper.ToContract(draft.Grade.Value),
            draft.Subjects
                .Select(subject => new ChildSetupSubjectResponse(
                    subject.Id,
                    subject.Type.ToString(),
                    subject.CustomName))
                .ToList(),
            draft.StudyPriorities
                .Select(priority => Enum.Parse<StudyPriority>(priority.ToString()))
                .ToList(),
            draft.StudyTimeGoal is null
                ? null
                : new StudyTimeGoalResponse(
                    draft.StudyTimeGoal.Period.ToString(),
                    draft.StudyTimeGoal.TargetMinutes,
                    draft.StudyTimeGoal.Days,
                    draft.StudyTimeGoal.StartDate));
    }
}