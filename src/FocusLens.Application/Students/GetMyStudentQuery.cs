using FocusLens.Contracts.Students;

using MediatR;

namespace FocusLens.Application.Students;

public sealed record GetMyStudentQuery : IRequest<StudentResponse?>;
