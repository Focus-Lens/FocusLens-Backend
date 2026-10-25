using FocusLens.Application.Common.Mappings;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Students;

public sealed class ReplaceStudentSubjectsCommandHandler(
    IBaseRepository<Student> studentRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ReplaceStudentSubjectsCommand, Result<IReadOnlyCollection<StudentSubjectResponse>>>
{
    public async Task<Result<IReadOnlyCollection<StudentSubjectResponse>>> Handle(
        ReplaceStudentSubjectsCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized("Students.CurrentUserUnavailable",
                "The current user could not be identified.");
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(
            item => item.UserId == userId,
            item => item.Subjects);
        if (student is null)
        {
            return Error.NotFound("Students.NotFound", "The student profile was not found.");
        }

        HashSet<Guid> subjectIds = request.Request.SubjectIds.ToHashSet();
        Dictionary<Guid, FocusLens.Domain.Students.StudentSubject> currentSubjects =
            student.Subjects.ToDictionary(subject => subject.Id);
        if (!subjectIds.All(currentSubjects.ContainsKey))
        {
            return Error.Validation("Students.InvalidSubject",
                "Every subject must belong to the current student.");
        }

        student.ReplaceSubjects(subjectIds.Select(subjectId => currentSubjects[subjectId]));

        await unitOfWork.BeginTransactionAsync();
        try
        {
            await unitOfWork.SaveChangesAsync();
            await unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync();
            throw;
        }

        return student.Subjects.Select(subject => new StudentSubjectResponse(
            subject.Id,
            StudentEnumMapper.ToContract(subject.Type),
            subject.CustomName)).ToArray();
    }
}
