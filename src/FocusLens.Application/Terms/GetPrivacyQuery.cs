using FocusLens.Contracts.Terms;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.Terms;

public sealed record GetPrivacyQuery(string Audience) : IRequest<Result<TermsResponse>>;