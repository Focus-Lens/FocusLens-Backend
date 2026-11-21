using FocusLens.Application.Students;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Access;

public sealed record GetStudentProfileImageForParentQuery(Guid StudentId)
    : IRequest<Result<StudentProfileImageResult>>;
