using FocusLens.Application.Common.Mappings;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Students;

public sealed class GetMyStudentQueryHandler(
    IBaseRepository<Student> studentRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyStudentQuery, StudentDetailsResponse?>
{
    public async Task<StudentDetailsResponse?> Handle(
        GetMyStudentQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return null;
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(
            student => student.UserId == userId,
            student => student.User,
            student => student.Subjects);

        return student?.ToDetailsResponse();
    }
}