using FocusLens.Application.Common.Errors;
using FocusLens.Application.Common.Mappings;
using FocusLens.Application.Features.Identity.Dtos;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Common.Constants;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using FocusLens.Domain.Students;
using MediatR;
using ContractStudentSubjectType = FocusLens.Contracts.Students.StudentSubjectType;
using ContractStudentGrade = FocusLens.Contracts.Students.StudentGrade;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using DomainStudentGoal = FocusLens.Domain.Students.StudentGoal;
using DomainStudyPriority = FocusLens.Domain.Students.StudyPriority;
using DomainStudyTimeGoal = FocusLens.Domain.Students.StudyTimeGoal;
using DomainStudyTimeGoalPeriod = FocusLens.Domain.Students.StudyTimeGoalPeriod;

namespace FocusLens.Application.Students;

public sealed class CompleteStudentOnboardingCommandHandler(
    IBaseRepository<Student> studentRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IIdentityService identityService,
    ITokenProvider tokenProvider)
    : IRequestHandler<CompleteStudentOnboardingCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(
        CompleteStudentOnboardingCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                "Students.CurrentUserUnavailable",
                "The current user could not be identified.");
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(
            student => student.UserId == userId,
            student => student.Subjects);

        if (student is null)
        {
            return Error.NotFound(
                "Students.NotFound",
                "The current user does not have a student profile.");
        }

        List<StudentSubject> subjects = (request.Request.Subjects ?? [])
            .Select(MapSubject)
            .ToList();

        DomainStudentGoal[] goals = (request.Request.Goals ?? [])
            .Select(StudentEnumMapper.ToDomain)
            .ToArray();

        student.CompleteOnboarding(
            goals,
            request.Request.Grade is null
                ? null
                : StudentEnumMapper.ToDomain(request.Request.Grade.Value),
            subjects);

        student.SetCustomGrade(
            request.Request.Grade == ContractStudentGrade.Other
                ? request.Request.CustomGrade
                : null);

        student.SetDateOfBirth(request.Request.DateOfBirth);

        DomainStudyPriority[] priorities =
            (request.Request.StudyPriorities ?? [])
            .Select(priority =>
                Enum.Parse<DomainStudyPriority>(priority.ToString()))
            .ToArray();

        student.ReplaceStudyPriorities(priorities);

        DomainStudyTimeGoal? studyTimeGoal = null;

        if (request.Request.StudyTimeGoal is not null)
        {
            Result<DomainStudyTimeGoal> studyTimeGoalResult =
                DomainStudyTimeGoal.Create(
                    Enum.Parse<DomainStudyTimeGoalPeriod>(
                        request.Request.StudyTimeGoal.Period.ToString()),
                    request.Request.StudyTimeGoal.TargetMinutes,
                    request.Request.StudyTimeGoal.Days ?? [],
                    request.Request.StudyTimeGoal.StartDate);

            if (studyTimeGoalResult.IsError)
            {
                return studyTimeGoalResult.TopError;
            }

            studyTimeGoal = studyTimeGoalResult.Value;
        }

        student.SetStudyTimeGoal(studyTimeGoal);

        await unitOfWork.SaveChangesAsync();

        ApplicationUser? user = await identityService.FindByIdAsync(userId);

        if (user is null)
        {
            return Error.NotFound(
                "Users.NotFound",
                "The current user was not found.");
        }

        if (user.IsDisabled)
        {
            return ApplicationErrors.Identity.UserDisabled;
        }

        if (await identityService.IsLockedOutAsync(user))
        {
            return ApplicationErrors.Identity.UserLockedOut;
        }

        IReadOnlyCollection<string> roles = await identityService.GetRolesAsync(user);

        if (!roles.Contains(ApplicationRoles.Student, StringComparer.Ordinal))
        {
            return Error.Forbidden(
                "Students.CurrentUserIsNotStudent",
                "The current user is not a student.");
        }

        TokenPair tokenPair = await tokenProvider.CreateTokenPairAsync(
            user,
            cancellationToken);

        return user.ToAuthResponse(roles, tokenPair, student.IsOnboardingCompleted);
    }

    private static StudentSubject MapSubject(StudentSubjectRequest subject)
    {
        return subject.Type == ContractStudentSubjectType.Other
            ? StudentSubject.Custom(subject.CustomName!)
            : StudentSubject.Predefined(StudentEnumMapper.ToDomain(subject.Type));
    }
}