using FocusLens.Contracts.Access;
using MediatR;

namespace FocusLens.Application.Access;

public sealed record GetMyStudentsQuery : IRequest<IReadOnlyList<ParentStudentSummaryResponse>>;