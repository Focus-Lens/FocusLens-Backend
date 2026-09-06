using ContractStudentSubjectType = FocusLens.Contracts.Students.StudentSubjectType;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using FocusLens.Application.Common.Errors;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Common.Mappings;
using FocusLens.Application.Features.Identity.Dtos;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Common.Constants;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using MediatR;

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

        student.CompleteOnboarding(
            request.Request.Goal is null
                ? null
                : StudentEnumMapper.ToDomain(request.Request.Goal.Value),
            request.Request.Grade is null
                ? null
                : StudentEnumMapper.ToDomain(request.Request.Grade.Value),
            subjects);

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
