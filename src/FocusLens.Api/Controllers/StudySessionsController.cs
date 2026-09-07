using System.Text.Json;
using FocusLens.Application.StudySessions;
using FocusLens.Contracts.StudySessions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[ApiController]
[Route("api/study-sessions")]
[Authorize(Roles = "Student")]
public sealed class StudySessionsController(ISender sender) : ApiController
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateStudySessionRequest request,
        CancellationToken cancellationToken)
        => ToActionResult(await sender.Send(new CreateStudySessionCommand(request), cancellationToken));

    [HttpGet("{sessionId:guid}")]
    public async Task<IActionResult> Get(Guid sessionId, CancellationToken cancellationToken)
    {
        StudySessionResponse? session = await sender.Send(new GetStudySessionQuery(sessionId), cancellationToken);
        return session is null ? NotFound() : Ok(session);
    }

    [HttpPatch("{sessionId:guid}/mode")]
    public async Task<IActionResult> SetMode(Guid sessionId, [FromBody] SetStudySessionModeRequest request, CancellationToken cancellationToken)
        => ToActionResult(await sender.Send(new SetStudySessionModeCommand(sessionId, request), cancellationToken));

    [HttpPatch("{sessionId:guid}/subject")]
    public async Task<IActionResult> SetSubject(Guid sessionId, [FromBody] SetStudySessionSubjectRequest request, CancellationToken cancellationToken)
        => ToActionResult(await sender.Send(new SetStudySessionSubjectCommand(sessionId, request), cancellationToken));

    [HttpPatch("{sessionId:guid}/duration")]
    public async Task<IActionResult> SetDuration(Guid sessionId, [FromBody] SetStudySessionDurationRequest request, CancellationToken cancellationToken)
        => ToActionResult(await sender.Send(new SetStudySessionDurationCommand(sessionId, request), cancellationToken));

    [HttpPatch("{sessionId:guid}/page-range")]
    public async Task<IActionResult> SetPageRange(Guid sessionId, [FromBody] SetStudySessionPageRangeRequest request, CancellationToken cancellationToken)
        => ToActionResult(await sender.Send(new SetStudySessionPageRangeCommand(sessionId, request), cancellationToken));

    [HttpPatch("{sessionId:guid}/sections")]
    public async Task<IActionResult> SetSections(Guid sessionId, [FromBody] SetStudySessionSectionsRequest request, CancellationToken cancellationToken)
        => ToActionResult(await sender.Send(new SetStudySessionSectionsCommand(sessionId, request), cancellationToken));

    [HttpPatch("{sessionId:guid}/settings")]
    public async Task<IActionResult> ChangeSettings(Guid sessionId, [FromBody] ChangeStudySessionSettingsRequest request, CancellationToken cancellationToken)
        => ToActionResult(await sender.Send(new ChangeStudySessionSettingsCommand(sessionId, request), cancellationToken));

    [HttpPost("{sessionId:guid}/ready")]
    public async Task<IActionResult> MarkReady(Guid sessionId, CancellationToken cancellationToken)
        => ToActionResult(await sender.Send(new MarkStudySessionReadyCommand(sessionId), cancellationToken));

    [HttpPost("{sessionId:guid}/start")]
    public async Task<IActionResult> Start(Guid sessionId, CancellationToken cancellationToken)
        => ToActionResult(await sender.Send(new StartStudySessionCommand(sessionId), cancellationToken));

    [HttpPost("{sessionId:guid}/material")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadMaterial(
        Guid sessionId,
        [FromForm] IFormFile file,
        [FromForm] StudyMaterialSource source,
        [FromForm] string? sections,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return Problem(new List<FocusLens.Domain.Common.Results.Error>
            {
                FocusLens.Domain.Common.Results.Error.Validation("StudyMaterials.EmptyFile", "The uploaded file is empty.")
            });
        }

        IReadOnlyCollection<StudyMaterialSectionRequest>? requestedSections;
        try
        {
            requestedSections = string.IsNullOrWhiteSpace(sections)
                ? []
                : JsonSerializer.Deserialize<IReadOnlyCollection<StudyMaterialSectionRequest>>(sections);
        }
        catch (JsonException)
        {
            return Problem(new List<FocusLens.Domain.Common.Results.Error>
            {
                FocusLens.Domain.Common.Results.Error.Validation("StudyMaterials.InvalidSections", "Sections must be valid JSON.")
            });
        }

        if (requestedSections is null)
        {
            return Problem(new List<FocusLens.Domain.Common.Results.Error>
            {
                FocusLens.Domain.Common.Results.Error.Validation("StudyMaterials.InvalidSections", "Sections must be a JSON array.")
            });
        }

        await using Stream input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, cancellationToken);
        return ToActionResult(await sender.Send(new UploadStudyMaterialCommand(
            sessionId,
            file.FileName,
            file.Length,
            buffer.ToArray(),
            source,
            requestedSections), cancellationToken));
    }

    private ActionResult ToActionResult(FocusLens.Domain.Common.Results.Result<StudySessionResponse> result)
        => result.IsSuccess ? Ok(result.Value) : Problem(result.Errors);
}
