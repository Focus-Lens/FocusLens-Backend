using FocusLens.Application.Terms;
using FocusLens.Contracts.Terms;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[Route("api/terms")]
[AllowAnonymous]
public sealed class TermsController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> GetTerms(
        [FromQuery] string audience,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTermsQuery(audience), cancellationToken);

        return result.Match(
            Ok,
            Problem);
    }

    [HttpPost("decision")]
    public async Task<IActionResult> SubmitDecision(
        [FromBody] TermsDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SubmitTermsDecisionCommand(request),
            cancellationToken);

        return result.Match(
            Ok,
            Problem);
    }
}
