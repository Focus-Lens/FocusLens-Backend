using FocusLens.Application.Common.Mappings;
using FocusLens.Contracts.Students;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.Students;

public sealed class GetMyStudentSubjectsQueryHandler(
    IBaseRepository<Student> studentRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetMyStudentSubjectsQuery, IReadOnlyCollection<StudentSubjectResponse>>
{
    public async Task<IReadOnlyCollection<StudentSubjectResponse>> Handle(
        GetMyStudentSubjectsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return [];
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(
            item => item.UserId == userId,
            item => item.Subjects);

        return student?.Subjects
                   .Select(subject => new StudentSubjectResponse(
                       subject.Id,
                       StudentEnumMapper.ToContract(subject.Type),
                       subject.CustomName))
                   .ToArray()
               ?? [];
    }
}