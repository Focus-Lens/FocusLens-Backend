using FocusLens.Application.Terms;
using FocusLens.Contracts.Terms;
using FocusLens.Domain.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[Route("api/privacy")]
[AllowAnonymous]
public sealed class PrivacyController(ISender sender) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> GetPrivacy(
        [FromQuery] string audience,
        CancellationToken cancellationToken)
    {
        Result<TermsResponse> result = await sender.Send(new GetPrivacyQuery(audience), cancellationToken);
        return result.Match(Ok, Problem);
    }
}