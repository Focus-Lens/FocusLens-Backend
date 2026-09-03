using FocusLens.Contracts.Students;
using MediatR;

namespace FocusLens.Application.Access;

public sealed record GetMyStudentsQuery : IRequest<IReadOnlyList<StudentResponse>>;
