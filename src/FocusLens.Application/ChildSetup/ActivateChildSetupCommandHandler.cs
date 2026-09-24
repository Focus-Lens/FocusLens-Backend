using FocusLens.Application.Common.Errors;
using FocusLens.Application.Common.Mappings;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using FocusLens.Domain.Students;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using StudentSubjectType = FocusLens.Domain.Students.StudentSubjectType;

namespace FocusLens.Application.ChildSetup;

public sealed class ActivateChildSetupCommandHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<ChildSetupDraft> childSetupDraftRepository,
    IBaseRepository<ParentStudentRelationship> relationshipRepository,
    ICurrentUser currentUser,
    IIdentityService identityService,
    IUnitOfWork unitOfWork
) : IRequestHandler<ActivateChildSetupCommand, Result<StudentDetailsResponse>>
{
    public async Task<Result<StudentDetailsResponse>> Handle(
        ActivateChildSetupCommand request,
        CancellationToken cancellationToken
    )
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Students.CurrentUserUnavailable",
                "The current user could not be identified."
            );
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(
            item => item.UserId == userId,
            item => item.User,
            item => item.Subjects
        );

        if (student is null)
        {
            return Error.NotFound(
                "Students.NotFound",
                "The current user does not have a student profile."
            );
        }

        ChildSetupDraft? draft = await childSetupDraftRepository.FirstOrDefaultAsync(
            item => item.ClaimedByStudentId == student.Id,
            item => item.Subjects
        );

        if (draft is null)
        {
            return Error.NotFound(
                "ChildSetup.NotFound",
                "The current student does not have a claimed child setup."
            );
        }

        if (draft.Status != ChildSetupStatus.Claimed)
        {
            return Error.Conflict(
                "ChildSetup.NotReadyForActivation",
                "Only a claimed child setup can be activated."
            );
        }

        ApplicationUser? user = await identityService.FindByIdAsync(userId);

        if (user is null)
        {
            return ApplicationErrors.Users.NotFound;
        }

        bool hasParentSetupData =
            !string.IsNullOrWhiteSpace(draft.FirstName)
            || !string.IsNullOrWhiteSpace(draft.LastName)
            || draft.DateOfBirth is not null
            || draft.Grade is not null
            || draft.Subjects.Count > 0
            || draft.StudyPriorities.Count > 0
            || draft.StudyTimeGoal is not null;

        if (hasParentSetupData)
        {
            if (
                string.IsNullOrWhiteSpace(draft.FirstName)
                || string.IsNullOrWhiteSpace(draft.LastName)
            )
            {
                return Error.Conflict(
                    "ChildSetup.Incomplete",
                    "The child setup is missing the child's name."
                );
            }

            student.SetDateOfBirth(draft.DateOfBirth);
            student.SetGrade(draft.Grade);
            student.ReplaceSubjects(draft.Subjects.Select(MapSubject));
            student.ReplaceStudyPriorities(draft.StudyPriorities);

            if (draft.StudyTimeGoal is not null)
            {
                Result<StudyTimeGoal> studyTimeGoalResult = StudyTimeGoal.Create(
                    draft.StudyTimeGoal.Period,
                    draft.StudyTimeGoal.TargetMinutes,
                    draft.StudyTimeGoal.Days,
                    draft.StudyTimeGoal.StartDate
                );

                if (studyTimeGoalResult.IsError)
                {
                    return studyTimeGoalResult.TopError;
                }

                student.SetStudyTimeGoal(studyTimeGoalResult.Value);
            }

            user.FirstName = draft.FirstName.Trim();
            user.LastName = draft.LastName.Trim();

            IdentityResultSummary updateResult = await identityService.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                return updateResult.ToApplicationErrors();
            }
        }

        ParentStudentRelationship? relationship = await relationshipRepository.FirstOrDefaultAsync(
            item => item.ParentId == draft.ParentId && item.StudentId == student.Id
        );

        if (relationship is null)
        {
            relationship = new ParentStudentRelationship(
                draft.ParentId,
                student.Id,
                InvitationInitiator.Parent,
                null
            );

            relationship.Accept();
            relationshipRepository.Add(relationship);
        }
        else if (relationship.Status == RelationshipStatus.Revoked)
        {
            relationship.Reinvite();
            relationship.Accept();
            relationshipRepository.Update(relationship);
        }
        else if (relationship.Status == RelationshipStatus.Pending)
        {
            if (relationship.IsExpired(DateTimeOffset.UtcNow))
            {
                relationship.Reinvite();
            }

            relationship.Accept();
            relationshipRepository.Update(relationship);
        }

        if (!string.IsNullOrWhiteSpace(draft.ProfileImageStorageReference))
        {
            student.SetProfileImageStorageReference(
                draft.ProfileImageStorageReference
            );
        }

        draft.MarkActivated();

        await unitOfWork.SaveChangesAsync();

        return student.ToDetailsResponse();
    }

    private static StudentSubject MapSubject(ChildSetupSubject subject)
    {
        return subject.Type == StudentSubjectType.Other
            ? StudentSubject.Custom(subject.CustomName!)
            : StudentSubject.Predefined(subject.Type);
    }
}
