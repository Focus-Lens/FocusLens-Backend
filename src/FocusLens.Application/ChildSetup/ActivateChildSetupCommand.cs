using FocusLens.Contracts.Students;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.ChildSetup;

public sealed record ActivateChildSetupCommand
    : IRequest<Result<StudentDetailsResponse>>;