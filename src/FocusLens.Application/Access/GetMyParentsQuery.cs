using FocusLens.Contracts.Access;
using MediatR;

namespace FocusLens.Application.Access;

public sealed record GetMyParentsQuery : IRequest<IReadOnlyList<StudentParentSummaryResponse>>;