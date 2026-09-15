using FocusLens.Contracts.Students;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Students;

public sealed record UpdateStudentProfileImageCommand(
    string FileName,
    long FileSizeBytes,
    Func<CancellationToken, Task<Stream>> OpenReadAsync)
    : IRequest<Result<StudentDetailsResponse>>;
