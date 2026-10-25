using FocusLens.Application.Common.Mappings;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Common.Errors;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Interfaces;
using FocusLens.Domain.Students;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Students;

public sealed class UpdateStudentProfileCommandHandler(
    IBaseRepository<Student> studentRepository,
    IIdentityService identityService,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateStudentProfileCommand, Result<StudentDetailsResponse>>
{
    public async Task<Result<StudentDetailsResponse>> Handle(
        UpdateStudentProfileCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized("Students.CurrentUserUnavailable",
                "The current user could not be identified.");
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(
            item => item.UserId == userId,
            item => item.User,
            item => item.Subjects);
        if (student is null)
        {
            return Error.NotFound("Students.NotFound", "The student profile was not found.");
        }

        ApplicationUser user = student.User;
        user.FirstName = request.Request.FirstName.Trim();
        user.LastName = request.Request.LastName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.Request.PhoneNumber)
            ? null
            : request.Request.PhoneNumber.Trim();
        student.SetDateOfBirth(request.Request.DateOfBirth);
        student.SetGrade(StudentEnumMapper.ToDomain(request.Request.Grade));
        student.SetCustomGrade(request.Request.CustomGrade);

        await unitOfWork.BeginTransactionAsync();
        try
        {
            IdentityResultSummary userResult = await identityService.UpdateAsync(user);
            if (!userResult.Succeeded)
            {
                await unitOfWork.RollbackTransactionAsync();
                return userResult.ToApplicationErrors();
            }

            await unitOfWork.SaveChangesAsync();
            await unitOfWork.CommitTransactionAsync();
            return student.ToDetailsResponse();
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }
}
