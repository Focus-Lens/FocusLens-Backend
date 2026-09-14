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
    public async Task<IActionResult> GetTerms(
        [FromQuery] string audience,
        CancellationToken cancellationToken)
    {
        Result<TermsResponse> result = await sender.Send(new GetTermsQuery(audience), cancellationToken);

        return result.Match(
            Ok,
            Problem);
    }
}