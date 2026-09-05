using ContractStudentSubjectType = FocusLens.Contracts.Students.StudentSubjectType;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Common.Mappings;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using MediatR;

namespace FocusLens.Application.Students;

public sealed class CompleteStudentOnboardingCommandHandler(
    IBaseRepository<Student> studentRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CompleteStudentOnboardingCommand, Result<Success>>
{
    public async Task<Result<Success>> Handle(
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

        List<StudentSubject> subjects = request.Request.Subjects
            .Select(MapSubject)
            .ToList();

        student.CompleteOnboarding(
            StudentEnumMapper.ToDomain(request.Request.Goal),
            StudentEnumMapper.ToDomain(request.Request.Grade),
            subjects);

        await unitOfWork.SaveChangesAsync();

        return Result.Success;
    }

    private static StudentSubject MapSubject(StudentSubjectRequest subject)
    {
        return subject.Type == ContractStudentSubjectType.Other
            ? StudentSubject.Custom(subject.CustomName!)
            : StudentSubject.Predefined(StudentEnumMapper.ToDomain(subject.Type));
    }
}
