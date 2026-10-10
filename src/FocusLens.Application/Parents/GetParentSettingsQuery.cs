using FocusLens.Contracts;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Parents;

public sealed record GetParentSettingsQuery : IRequest<Result<ParentSettingsResponse>>;