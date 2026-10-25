using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Students;

public sealed record GetStudentProfileImageQuery
    : IRequest<Result<StudentProfileImageResult>>;

public sealed record StudentProfileImageFile(Stream Content, string ContentType);

public sealed record StudentProfileImageResult(StudentProfileImageFile? File);
