using FocusLens.Contracts.Students;
using MediatR;

namespace FocusLens.Application.Access;

public sealed record GetStudentForParentQuery(
    Guid StudentId
) : IRequest<StudentDetailsResponse?>;