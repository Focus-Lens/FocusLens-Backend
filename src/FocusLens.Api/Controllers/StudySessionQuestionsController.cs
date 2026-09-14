using FocusLens.Application.StudySessions;
using FocusLens.Contracts.StudySessions;
using FocusLens.Domain.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[ApiController]
[Route("api/study-sessions/{sessionId:guid}/questions")]
[Authorize(Roles = "Student")]
public sealed class StudySessionQuestionsController(ISender sender) : ApiController
{
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        StudySessionCurrentQuestionResponse? response =
            await sender.Send(
                new GetCurrentStudySessionQuestionQuery(sessionId),
                cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("{questionId:guid}/answer")]
    public async Task<IActionResult> SubmitAnswer(
        Guid sessionId,
        Guid questionId,
        [FromBody] SubmitStudySessionQuestionAnswerRequest request,
        CancellationToken cancellationToken)
    {
        Result<StudySessionQuestionAnswerResponse> result =
            await sender.Send(
                new SubmitStudySessionQuestionAnswerCommand(
                    sessionId,
                    questionId,
                    request),
                cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : Problem(result.Errors);
    }

    [HttpPost("{questionId:guid}/explanation")]
    public async Task<IActionResult> GetExplanation(
        Guid sessionId,
        Guid questionId,
        CancellationToken cancellationToken)
    {
        Result<StudySessionQuestionExplanationResponse> result =
            await sender.Send(
                new GenerateStudySessionQuestionExplanationCommand(
                    sessionId,
                    questionId),
                cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : Problem(result.Errors);
    }

    [HttpPost("{questionId:guid}/postpone")]
    public async Task<IActionResult> Postpone(
        Guid sessionId,
        Guid questionId,
        CancellationToken cancellationToken)
    {
        Result<PostponeStudySessionQuestionResponse> result =
            await sender.Send(
                new PostponeStudySessionQuestionCommand(sessionId, questionId),
                cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : Problem(result.Errors);
    }

    [HttpGet("~/api/study-sessions/{sessionId:guid}/result")]
    public async Task<IActionResult> GetResult(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        StudySessionResultResponse? response =
            await sender.Send(
                new GetStudySessionResultQuery(sessionId),
                cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }
}
