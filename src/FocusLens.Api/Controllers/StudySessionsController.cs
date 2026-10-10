using FocusLens.Application.StudySessions;
using FocusLens.Contracts.BehavioralIntelligence;
using FocusLens.Contracts.StudySessions;
using FocusLens.Domain.Common.Results;
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
        CancellationToken cancellationToken
    )
    {
        return ToActionResult(
            await sender.Send(new CreateStudySessionCommand(request), cancellationToken)
        );
    }

    [HttpGet("{sessionId:guid}")]
    public async Task<IActionResult> Get(Guid sessionId, CancellationToken cancellationToken)
    {
        StudySessionResponse? session = await sender.Send(
            new GetStudySessionQuery(sessionId),
            cancellationToken
        );
        return session is null ? NotFound() : Ok(session);
    }

    [HttpPut("{sessionId:guid}/mode")]
    public async Task<IActionResult> SetMode(Guid sessionId, [FromBody] SetStudySessionModeRequest request,
        CancellationToken cancellationToken)
    {
        Result<Success> result =
            await sender.Send(new SetStudySessionModeCommand(sessionId, request), cancellationToken);
        return result.Match(_ => NoContent(), Problem);
    }

    [HttpPut("{sessionId:guid}/subject")]
    public async Task<IActionResult> SetSubject(Guid sessionId, [FromBody] SetStudySessionSubjectRequest request,
        CancellationToken cancellationToken)
    {
        Result<Success> result =
            await sender.Send(new SetStudySessionSubjectCommand(sessionId, request), cancellationToken);
        return result.Match(_ => NoContent(), Problem);
    }

    [HttpPut("{sessionId:guid}/duration")]
    public async Task<IActionResult> SetDuration(Guid sessionId, [FromBody] SetStudySessionDurationRequest request,
        CancellationToken cancellationToken)
    {
        Result<Success> result = await sender.Send(new SetStudySessionDurationCommand(sessionId, request),
            cancellationToken);
        return result.Match(_ => NoContent(), Problem);
    }

    [HttpPost("{sessionId:guid}/selection")]
    public async Task<IActionResult> SetSelection(
        Guid sessionId,
        [FromBody] SetStudySessionSelectionRequest request,
        CancellationToken cancellationToken
    )
    {
        return ToActionResult(
            await sender.Send(
                new SetStudySessionSelectionCommand(sessionId, request),
                cancellationToken
            )
        );
    }

    [HttpPut("{sessionId:guid}/selection")]
    public async Task<IActionResult> UpdateSelection(
        Guid sessionId,
        [FromBody] SetStudySessionSelectionRequest request,
        CancellationToken cancellationToken
    )
    {
        return ToActionResult(
            await sender.Send(
                new UpdateStudySessionSelectionCommand(sessionId, request),
                cancellationToken
            )
        );
    }

    [HttpPost("{sessionId:guid}/sections")]
    public async Task<IActionResult> ReceiveSections(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(
            await sender.Send(
                new AnalyzeStudySessionContentCommand(sessionId),
                cancellationToken)
        );
    }

    [HttpPut("{sessionId:guid}/sections")]
    public async Task<IActionResult> SelectSections(
        Guid sessionId,
        [FromBody] SelectStudySessionAiSectionsRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(
            await sender.Send(
                new SelectStudySessionAiSectionsCommand(sessionId, request),
                cancellationToken)
        );
    }

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
            return Problem([Error.Validation("StudyMaterials.EmptyFile", "The uploaded file is empty.")]);
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
    {
        Result<Success> result = await sender.Send(new StartStudySessionCommand(sessionId), cancellationToken);
        return result.Match(_ => NoContent(), Problem);
    }

    [HttpPost("{sessionId:guid}/images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
    public async Task<IActionResult> UploadImages(
        Guid sessionId,
        [FromForm] List<IFormFile>? files,
        CancellationToken cancellationToken)
    {
        if (files is null || files.Count == 0)
        {
            return Problem([
                Error.Validation("StudySessionImages.FilesRequired", "At least one image file is required.")
            ]);
        }

        if (files.Any(file => file.Length == 0))
        {
            return Problem([Error.Validation("StudySessionImages.EmptyFile", "The uploaded image is empty.")]);
        }

        StudySessionImageUploadFile[] uploadFiles = files
            .Select(file => new StudySessionImageUploadFile(
                file.FileName,
                file.ContentType,
                file.Length,
                cancellation =>
                {
                    cancellation.ThrowIfCancellationRequested();
                    return Task.FromResult<Stream>(file.OpenReadStream());
                }))
            .ToArray();

        return ToActionResult(await sender.Send(
            new UploadStudySessionImagesCommand(sessionId, uploadFiles),
            cancellationToken));
    }

    [HttpPost("{sessionId:guid}/pause")]
    public async Task<IActionResult> Pause(Guid sessionId, CancellationToken cancellationToken)
    {
        return ToActionResult(
            await sender.Send(new PauseStudySessionCommand(sessionId), cancellationToken)
        );
    }

    [HttpPost("{sessionId:guid}/resume")]
    public async Task<IActionResult> Resume(Guid sessionId, CancellationToken cancellationToken)
    {
        return ToActionResult(
            await sender.Send(new ResumeStudySessionCommand(sessionId), cancellationToken)
        );
    }

    [HttpPost("{sessionId:guid}/end")]
    public async Task<IActionResult> End(Guid sessionId, CancellationToken cancellationToken) =>
        ToActionResult(await sender.Send(new EndStudySessionCommand(sessionId), cancellationToken));

    [HttpPost("{sessionId:guid}/reuse")]
    public async Task<IActionResult> Reuse(Guid sessionId, CancellationToken cancellationToken) =>
        ToActionResult(await sender.Send(new ReuseStudySessionCommand(sessionId), cancellationToken));

    [HttpPut("{sessionId:guid}/progress")]
    public async Task<IActionResult> UpdateProgress(
        Guid sessionId,
        [FromBody] UpdateStudySessionProgressRequest request,
        CancellationToken cancellationToken
    )
    {
        return ToActionResult(
            await sender.Send(
                new UpdateStudySessionProgressCommand(sessionId, request),
                cancellationToken
            )
        );
    }

    [HttpPost("{sessionId:guid}/behavior-events")]
    public async Task<IActionResult> RecordBehaviorEvents(
        Guid sessionId,
        [FromBody] RecordStudySessionBehaviorEventsRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await sender.Send(
            new RecordStudySessionBehaviorEventsCommand(sessionId, request),
            cancellationToken));
    }

    [HttpPost("{sessionId:guid}/behavior-windows/analyze")]
    public async Task<IActionResult> AnalyzeBehaviorWindow(
        Guid sessionId,
        [FromBody] AnalyzeBehaviorWindowRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await sender.Send(
            new AnalyzeStudySessionBehaviorWindowCommand(
                sessionId,
                request.IsFinal),
            cancellationToken));
    }

    [HttpGet("{sessionId:guid}/behavior-windows")]
    public async Task<IActionResult> GetBehaviorWindows(Guid sessionId, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<BehaviorWindowResultResponse>? windows = await sender.Send(
            new GetStudySessionBehaviorWindowsQuery(sessionId), cancellationToken);
        return windows is null ? NotFound() : Ok(windows);
    }

    [HttpPost("{sessionId:guid}/upload-material")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadMaterial(
        Guid sessionId,
        [FromForm] IFormFile file,
        [FromForm] StudyMaterialSource source,
        CancellationToken cancellationToken
    )
    {
        if (file.Length == 0)
        {
            return Problem(
                new List<Error>
                {
                    Error.Validation(
                        "StudyMaterials.EmptyFile",
                        "The uploaded file is empty."
                    )
                }
            );
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
        using MemoryStream buffer = new();
        await input.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private ActionResult ToActionResult<TResponse>(Result<TResponse> result) =>
        result.IsSuccess ? Ok(result.Value) : Problem(result.Errors);
}