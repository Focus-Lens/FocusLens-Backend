using FocusLens.Application.Terms;
using FocusLens.Contracts.Terms;
using FocusLens.Domain.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[Route("api/terms")]
[AllowAnonymous]
public sealed class TermsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> GetTerms(CancellationToken cancellationToken)
    {
        Result<TermsResponse> result = await sender.Send(new GetTermsQuery(), cancellationToken);

        return result.Match(
            Ok,
            Problem);
    }

    [HttpPost("decision")]
    public async Task<IActionResult> SubmitDecision(
        [FromBody] TermsDecisionRequest request,
        CancellationToken cancellationToken)
    {
        Result<TermsDecisionResponse> result = await sender.Send(
            new SubmitTermsDecisionCommand(request),
            cancellationToken);

        return result.Match(
            Ok,
            Problem);
    }
}