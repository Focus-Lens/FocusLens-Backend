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

    [HttpPut("{sessionId:guid}/mode")]
    public async Task<IActionResult> SetMode(Guid sessionId, [FromBody] SetStudySessionModeRequest request, CancellationToken cancellationToken)
        => ToActionResult(await sender.Send(new SetStudySessionModeCommand(sessionId, request), cancellationToken));

    [HttpPut("{sessionId:guid}/subject")]
    public async Task<IActionResult> SetSubject(Guid sessionId, [FromBody] SetStudySessionSubjectRequest request, CancellationToken cancellationToken)
        => ToActionResult(await sender.Send(new SetStudySessionSubjectCommand(sessionId, request), cancellationToken));

    [HttpPut("{sessionId:guid}/duration")]
    public async Task<IActionResult> SetDuration(Guid sessionId, [FromBody] SetStudySessionDurationRequest request, CancellationToken cancellationToken)
        => ToActionResult(await sender.Send(new SetStudySessionDurationCommand(sessionId, request), cancellationToken));

    [HttpPost("{sessionId:guid}/selection")]
    public async Task<IActionResult> SetSelection(Guid sessionId, [FromBody] SetStudySessionSelectionRequest request, CancellationToken cancellationToken)
        => ToActionResult(await sender.Send(new SetStudySessionSelectionCommand(sessionId, request), cancellationToken));

    [HttpPut("{sessionId:guid}/selection")]
    public async Task<IActionResult> UpdateSelection(Guid sessionId, [FromBody] SetStudySessionSelectionRequest request, CancellationToken cancellationToken)
        => ToActionResult(await sender.Send(new UpdateStudySessionSelectionCommand(sessionId, request), cancellationToken));

    [HttpPost("{sessionId:guid}/sections")]
    public async Task<IActionResult> ReceiveSections(Guid sessionId, [FromBody] ReceiveStudySessionSectionsRequest request, CancellationToken cancellationToken)
        => ToActionResult(await sender.Send(new ReceiveStudySessionSectionsCommand(sessionId, request), cancellationToken));

    [HttpPut("{sessionId:guid}/material")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ChangeMaterial(
        Guid sessionId,
        [FromForm] IFormFile file,
        [FromForm] StudyMaterialSource source,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return Problem([FocusLens.Domain.Common.Results.Error.Validation("StudyMaterials.EmptyFile", "The uploaded file is empty.")]);
        }

        return ToActionResult(await sender.Send(new ChangeStudySessionMaterialCommand(
            sessionId,
            file.FileName,
            file.Length,
            await ReadFileAsync(file, cancellationToken),
            source), cancellationToken));
    }

    [HttpPost("{sessionId:guid}/start")]
    public async Task<IActionResult> Start(Guid sessionId, CancellationToken cancellationToken)
        => ToActionResult(await sender.Send(new StartStudySessionCommand(sessionId), cancellationToken));

    [HttpPost("{sessionId:guid}/upload-material")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadMaterial(
        Guid sessionId,
        [FromForm] IFormFile file,
        [FromForm] StudyMaterialSource source,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return Problem(new List<FocusLens.Domain.Common.Results.Error>
            {
                FocusLens.Domain.Common.Results.Error.Validation("StudyMaterials.EmptyFile", "The uploaded file is empty.")
            });
        }

        return ToActionResult(await sender.Send(new UploadStudyMaterialCommand(
            sessionId,
            file.FileName,
            file.Length,
            await ReadFileAsync(file, cancellationToken),
            source), cancellationToken));
    }

    private static async Task<byte[]> ReadFileAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using Stream input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private ActionResult ToActionResult<TResponse>(FocusLens.Domain.Common.Results.Result<TResponse> result)
        => result.IsSuccess ? Ok(result.Value) : Problem(result.Errors);
}
