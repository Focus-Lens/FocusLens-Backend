using FocusLens.Contracts.Students;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Students;

public sealed record UpdateStudentTimeZoneCommand(UpdateStudentTimeZoneRequest Request) : IRequest<Result<Success>>;