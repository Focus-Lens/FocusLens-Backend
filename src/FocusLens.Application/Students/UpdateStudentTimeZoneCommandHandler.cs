using FocusLens.Application.Common.Interfaces;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Students;

public sealed class UpdateStudentTimeZoneCommandHandler(
    IBaseRepository<Student> studentRepository,
    ICurrentUser currentUser,
    IStudentLocalTime studentLocalTime,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateStudentTimeZoneCommand, Result<Success>>
{
    public async Task<Result<Success>> Handle(UpdateStudentTimeZoneCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized("Students.CurrentUserUnavailable", "The current user could not be identified.");
        }

        string timeZoneId = request.Request.TimeZoneId?.Trim() ?? string.Empty;
        if (!studentLocalTime.IsValidTimeZoneId(timeZoneId))
        {
            return Error.Validation("Students.InvalidTimeZoneId",
                "TimeZoneId must be a supported IANA time zone identifier.");
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(student => student.UserId == userId);
        if (student is null)
        {
            return Error.NotFound("Students.NotFound", "The student profile was not found.");
        }

        student.SetTimeZoneId(timeZoneId);
        await unitOfWork.SaveChangesAsync();
        return Result.Success;
    }
}