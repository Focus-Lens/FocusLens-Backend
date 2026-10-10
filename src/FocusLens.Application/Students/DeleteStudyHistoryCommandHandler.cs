using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.StudySessions;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Students;

public sealed class DeleteStudyHistoryCommandHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudySession> sessionRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteStudyHistoryCommand, Result<Success>>
{
    public async Task<Result<Success>> Handle(
        DeleteStudyHistoryCommand request,
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
            return Error.NotFound("Students.NotFound", "The student profile was not found.");
        }

        StudySession[] sessions = (await sessionRepository.GetAllAsync(session => session.StudentId == student.Id &&
                session.Status == StudySessionStatus.Completed))
            .ToArray();

        sessionRepository.DeleteRange(sessions);
        await unitOfWork.SaveChangesAsync();
        return Result.Success;
    }
}