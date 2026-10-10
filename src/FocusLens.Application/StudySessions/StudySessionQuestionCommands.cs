using FocusLens.Contracts.StudySessions;
using FocusLens.Domain.Common.Results;
using MediatR;

namespace FocusLens.Application.StudySessions;

public sealed record SubmitStudySessionQuestionAnswerCommand(
    Guid SessionId,
    Guid QuestionId,
    SubmitStudySessionQuestionAnswerRequest Request)
    : IRequest<Result<StudySessionQuestionAnswerResponse>>;

public sealed record GenerateStudySessionQuestionExplanationCommand(
    Guid SessionId,
    Guid QuestionId)
    : IRequest<Result<StudySessionQuestionExplanationResponse>>;

public sealed record PostponeStudySessionQuestionCommand(
    Guid SessionId,
    Guid QuestionId)
    : IRequest<Result<PostponeStudySessionQuestionResponse>>;