using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using MediatR;

namespace FocusLens.Application.Students;

public sealed class CreateCustomStudentSubjectCommandHandler(
    IBaseRepository<Student> studentRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateCustomStudentSubjectCommand, Result<CustomStudentSubjectResponse>>
{
    public async Task<Result<CustomStudentSubjectResponse>> Handle(
        CreateCustomStudentSubjectCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
            return Error.Unauthorized("Students.CurrentUserUnavailable", "The current user could not be identified.");

        if (string.IsNullOrWhiteSpace(request.Request.Name))
            return Error.Validation("Students.CustomSubjectRequired", "A custom subject name is required.");

        if (request.Request.Name.Trim().Length > 200)
            return Error.Validation("Students.CustomSubjectTooLong", "A custom subject name cannot exceed 200 characters.");

        Student? student = await studentRepository.FirstOrDefaultAsync(
            item => item.UserId == userId,
            item => item.Subjects);
        if (student is null)
            return Error.NotFound("Students.NotFound", "The current user does not have a student profile.");

        StudentSubject subject = StudentSubject.Custom(request.Request.Name);
        student.Subjects.Add(subject);
        await unitOfWork.SaveChangesAsync();
        return new CustomStudentSubjectResponse(subject.Id, subject.CustomName!);
    }
}
