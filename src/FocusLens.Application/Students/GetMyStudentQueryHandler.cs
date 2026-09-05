using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using MediatR;

namespace FocusLens.Application.Students;

public sealed class GetMyStudentQueryHandler(
    IBaseRepository<Student> studentRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyStudentQuery, StudentResponse?>
{
    public async Task<StudentResponse?> Handle(
        GetMyStudentQuery request,
        CancellationToken cancellationToken)
    {
        Student? student = await studentRepository.FirstOrDefaultAsync(
            student => student.UserId == currentUser.UserId);

        return student is null
            ? null
            : new StudentResponse(
                student.Id,
                student.UserId,
                student.IsOnboardingCompleted);
    }
}
