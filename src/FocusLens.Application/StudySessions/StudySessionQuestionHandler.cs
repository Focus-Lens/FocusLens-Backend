using FocusLens.Application.Common.Interfaces;
using FocusLens.Contracts.StudySessions;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.StudySessions;
using MediatR;

namespace FocusLens.Application.StudySessions;

public sealed class StudySessionQuestionHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudySession> studySessionRepository,
    IBaseRepository<StudySessionQuestion> questionRepository,
    IBaseRepository<StudySessionQuestionAnswer> answerRepository,
    IBaseRepository<StudySessionSelectedSection> selectedSectionRepository,
    ICurrentUser currentUser,
    IFocusLensAiClient aiClient,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<GetCurrentStudySessionQuestionQuery, StudySessionCurrentQuestionResponse?>,
      IRequestHandler<SubmitStudySessionQuestionAnswerCommand, Result<StudySessionQuestionAnswerResponse>>,
      IRequestHandler<GenerateStudySessionQuestionExplanationCommand, Result<StudySessionQuestionExplanationResponse>>,
      IRequestHandler<PostponeStudySessionQuestionCommand, Result<PostponeStudySessionQuestionResponse>>,
      IRequestHandler<GetStudySessionResultQuery, StudySessionResultResponse?>
{
    public async Task<StudySessionCurrentQuestionResponse?> Handle(
        GetCurrentStudySessionQuestionQuery request,
        CancellationToken cancellationToken)
    {
        StudySession? session = await GetOwnedSessionAsync(
            request.SessionId,
            includeSelection: true);

        if (session is null ||
            session.Status is not (StudySessionStatus.Active or StudySessionStatus.Paused))
        {
            return null;
        }

        if (session.Status == StudySessionStatus.Paused)
        {
            return new StudySessionCurrentQuestionResponse(
                false,
                null);
        }

        if (session.Selection is null)
        {
            return null;
        }

        List<StudySessionSelectedSection> selectedSections =
            (await selectedSectionRepository.GetAllAsync(
                section => section.StudySessionSelectionId == session.Selection.Id))
            .OrderBy(section => section.Order)
            .ToList();

        StudySessionSelectedSection? currentSection =
            selectedSections.FirstOrDefault(
                section => section.StartedAtUtc is not null &&
                           section.CompletedAtUtc is null);

        if (currentSection is null)
        {
            return new StudySessionCurrentQuestionResponse(
                false,
                null);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        if (!currentSection.IsChallengeAvailableAt(now))
        {
            return new StudySessionCurrentQuestionResponse(
                false,
                null);
        }

        List<StudySessionQuestion> questions =
            (await questionRepository.GetAllAsync(
                question => question.StudySessionId == session.Id,
                question => question.Options))
            .OrderBy(question => question.Order)
            .ToList();

        if (questions.Count == 0)
        {
            return new StudySessionCurrentQuestionResponse(
                false,
                null);
        }

        Guid[] questionIds =
            questions.Select(question => question.Id).ToArray();

        List<StudySessionQuestionAnswer> answers =
            (await answerRepository.GetAllAsync(
                answer => questionIds.Contains(answer.StudySessionQuestionId)))
            .ToList();

        HashSet<Guid> correctlyAnswered =
            answers
                .Where(answer => answer.IsCorrect)
                .Select(answer => answer.StudySessionQuestionId)
                .ToHashSet();

        StudySessionQuestion? currentQuestion =
            questions.FirstOrDefault(question =>
                !correctlyAnswered.Contains(question.Id) &&
                question.StudyMaterialSectionId == currentSection.StudyMaterialSectionId);

        if (currentQuestion is null)
        {
            return new StudySessionCurrentQuestionResponse(
                false,
                null);
        }

        int incorrectAttempts = answers.Count(answer =>
            answer.StudySessionQuestionId == currentQuestion.Id && !answer.IsCorrect);

        if (incorrectAttempts >= 2 && currentQuestion.ExplanationShownAtUtc is null)
        {
            return new StudySessionCurrentQuestionResponse(
                true,
                null,
                RequiresExplanation: true);
        }

        int number =
            questions.FindIndex(question => question.Id == currentQuestion.Id) + 1;

        return new StudySessionCurrentQuestionResponse(
            true,
            new StudySessionCurrentQuestionDetails(
                currentQuestion.Id,
                number,
                questions.Count,
                currentQuestion.AiSectionTitle,
                currentQuestion.AiConceptName,
                currentQuestion.Question,
                currentQuestion.Options
                    .OrderBy(option => option.Order)
                    .Select(option => option.Text)
                    .ToArray(),
                currentQuestion.EstimatedTimeMinutes));
    }

    public async Task<Result<StudySessionQuestionAnswerResponse>> Handle(
        SubmitStudySessionQuestionAnswerCommand request,
        CancellationToken cancellationToken)
    {
        StudySession? session = await GetOwnedSessionAsync(
            request.SessionId,
            includeSelection: true);

        if (session is null)
        {
            return Error.NotFound(
                "StudySessions.NotFound",
                "The study session was not found.");
        }

        if (session.Status != StudySessionStatus.Active)
        {
            return StudySessionErrors.NotActive;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        if (session.Selection?.AiExtractedText is not { Length: > 0 } content)
        {
            return Error.Validation(
                "StudySessionQuestions.ContentUnavailable",
                "AI learning content is not available for this session.");
        }

        StudySessionQuestion? question = await questionRepository.FirstOrDefaultAsync(
            item => item.Id == request.QuestionId && item.StudySessionId == session.Id,
            item => item.Options);

        if (question is null)
        {
            return Error.NotFound(
                "StudySessionQuestions.NotFound",
                "The study session question was not found.");
        }

        List<StudySessionSelectedSection> selectedSections =
            (await selectedSectionRepository.GetAllAsync(
                section => section.StudySessionSelectionId == session.Selection!.Id))
            .OrderBy(section => section.Order)
            .ToList();

        StudySessionSelectedSection? currentSection = selectedSections.FirstOrDefault(
            section => section.StartedAtUtc is not null && section.CompletedAtUtc is null);

        if (currentSection is null ||
            !currentSection.IsChallengeAvailableAt(now) ||
            currentSection.StudyMaterialSectionId != question.StudyMaterialSectionId)
        {
            return Error.Conflict(
                "StudySessionQuestions.ChallengeUnavailable",
                "This challenge is not currently available.");
        }

        string selectedAnswer = request.Request.SelectedAnswer?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(selectedAnswer))
        {
            return Error.Validation(
                "StudySessionQuestionAnswers.SelectedAnswerRequired",
                "Selected answer is required.");
        }

        bool validOption = question.Options.Any(option =>
            string.Equals(option.Text, selectedAnswer, StringComparison.OrdinalIgnoreCase));

        if (!validOption)
        {
            return Error.Validation(
                "StudySessionQuestionAnswers.InvalidOption",
                "The selected answer is not one of the available options.");
        }

        List<StudySessionQuestionAnswer> existingAnswers =
            (await answerRepository.GetAllAsync(
                answer => answer.StudySessionQuestionId == question.Id))
            .ToList();

        if (existingAnswers.Any(answer => answer.IsCorrect))
        {
            return Error.Conflict(
                "StudySessionQuestions.AlreadyAnswered",
                "This question has already been answered correctly.");
        }

        bool isCorrect = string.Equals(
            selectedAnswer,
            question.CorrectAnswer,
            StringComparison.OrdinalIgnoreCase);

        AiAnswerEvaluationResult evaluation = await aiClient.EvaluateAnswerAsync(
            question.Question,
            selectedAnswer,
            question.CorrectAnswer,
            question.AiSectionId,
            question.AiConceptId,
            content,
            cancellationToken);

        int attemptNumber = existingAnswers.Count + 1;

        Result<StudySessionQuestionAnswer> answerResult =
            StudySessionQuestionAnswer.Create(
                question.Id,
                selectedAnswer,
                isCorrect,
                evaluation.LearningSignal,
                attemptNumber,
                now);

        if (answerResult.IsError)
        {
            return answerResult.TopError;
        }

        answerRepository.Add(answerResult.Value);

        List<StudySessionQuestion> questions =
            (await questionRepository.GetAllAsync(
                item => item.StudySessionId == session.Id))
            .OrderBy(item => item.Order)
            .ToList();

        Guid[] questionIds = questions.Select(item => item.Id).ToArray();

        List<StudySessionQuestionAnswer> sessionAnswers =
            (await answerRepository.GetAllAsync(
                answer => questionIds.Contains(answer.StudySessionQuestionId)))
            .ToList();

        if (isCorrect)
        {
            HashSet<Guid> successfulQuestionIds = sessionAnswers
                .Where(answer => answer.IsCorrect)
                .Select(answer => answer.StudySessionQuestionId)
                .ToHashSet();

            successfulQuestionIds.Add(question.Id);

            bool currentSectionCompleted =
                questions
                    .Where(item =>
                        item.StudyMaterialSectionId == currentSection.StudyMaterialSectionId)
                    .All(item => successfulQuestionIds.Contains(item.Id));

            if (currentSectionCompleted)
            {
                Result<Success> completeSectionResult =
                    currentSection.Complete(now);

                if (completeSectionResult.IsError)
                {
                    return completeSectionResult.TopError;
                }
            }

            if (currentSectionCompleted)
            {
                Result<Success> completedSectionResult =
                    session.MarkSectionCompleted(currentSection.StudyMaterialSectionId);

                if (completedSectionResult.IsError)
                {
                    return completedSectionResult.TopError;
                }
            }

            bool completed = selectedSections.All(section => section.CompletedAtUtc is not null);

            StudySessionSelectedSection? nextSection = null;

            if (!completed && currentSectionCompleted)
            {
                nextSection = selectedSections.FirstOrDefault(
                    section => section.Order > currentSection.Order &&
                               section.StartedAtUtc is null &&
                               section.CompletedAtUtc is null);

                if (nextSection is not null)
                {
                    Result<Success> nextSectionStartResult =
                        nextSection.Start(now);

                    if (nextSectionStartResult.IsError)
                    {
                        return nextSectionStartResult.TopError;
                    }
                }
            }

            if (completed)
            {
                Result<Success> completeResult =
                    session.CompleteSuccessfully(now);

                if (completeResult.IsError)
                {
                    return completeResult.TopError;
                }
            }

            await unitOfWork.SaveChangesAsync();

            StudySessionQuestion? nextQuestion =
                questions.FirstOrDefault(
                    item => !successfulQuestionIds.Contains(item.Id));

            return new StudySessionQuestionAnswerResponse(
                question.Id,
                true,
                evaluation.LearningSignal,
                question.CorrectAnswer,
                question.Explanation,
                false,
                false,
                attemptNumber,
                completed,
                nextQuestion is null
                    ? null
                    : questions.FindIndex(item => item.Id == nextQuestion.Id) + 1,
                questions.Count);
        }

        await unitOfWork.SaveChangesAsync();

        int currentNumber =
            questions.FindIndex(item => item.Id == question.Id) + 1;

        bool requiresExplanation = attemptNumber >= 2 &&
                                   question.ExplanationShownAtUtc is null;

        return new StudySessionQuestionAnswerResponse(
            question.Id,
            false,
            evaluation.LearningSignal,
            question.CorrectAnswer,
            null,
            !requiresExplanation,
            requiresExplanation,
            attemptNumber,
            false,
            currentNumber,
            questions.Count);
    }

    public async Task<Result<StudySessionQuestionExplanationResponse>> Handle(
        GenerateStudySessionQuestionExplanationCommand request,
        CancellationToken cancellationToken)
    {
        StudySession? session = await GetOwnedSessionAsync(
            request.SessionId,
            includeSelection: true);

        if (session is null)
        {
            return Error.NotFound(
                "StudySessions.NotFound",
                "The study session was not found.");
        }

        if (session.Selection?.AiExtractedText is not { Length: > 0 } content)
        {
            return Error.Validation(
                "StudySessionQuestions.ContentUnavailable",
                "AI learning content is not available for this session.");
        }

        StudySessionQuestion? question =
            await questionRepository.FirstOrDefaultAsync(
                item => item.Id == request.QuestionId &&
                        item.StudySessionId == session.Id);

        if (question is null)
        {
            return Error.NotFound(
                "StudySessionQuestions.NotFound",
                "The study session question was not found.");
        }

        List<StudySessionQuestionAnswer> answers =
            (await answerRepository.GetAllAsync(
                answer => answer.StudySessionQuestionId == question.Id))
            .OrderBy(answer => answer.AttemptNumber)
            .ToList();

        if (answers.Count < 2 || answers[^1].IsCorrect ||
            question.ExplanationShownAtUtc is not null)
        {
            return Error.Validation(
                "StudySessionQuestions.ExplanationNotRequired",
                "An adaptive explanation is available after a second incorrect answer.");
        }

        Result<Success> explanationShownResult = question.MarkExplanationShown(
            timeProvider.GetUtcNow());

        if (explanationShownResult.IsError)
        {
            return explanationShownResult.TopError;
        }

        AiExplanationResult explanation =
            await aiClient.GenerateExplanationAsync(
                question.AiSectionId,
                question.AiSectionTitle,
                question.AiConceptId,
                question.AiConceptName,
                content,
                cancellationToken);

        await unitOfWork.SaveChangesAsync();

        return new StudySessionQuestionExplanationResponse(
            question.Id,
            explanation.SectionTitle,
            explanation.ConceptName,
            explanation.Explanation);
    }

    public async Task<Result<PostponeStudySessionQuestionResponse>> Handle(
        PostponeStudySessionQuestionCommand request,
        CancellationToken cancellationToken)
    {
        StudySession? session = await GetOwnedSessionAsync(
            request.SessionId,
            includeSelection: true);

        if (session is null)
        {
            return Error.NotFound("StudySessions.NotFound", "The study session was not found.");
        }

        if (session.Status != StudySessionStatus.Active || session.Selection is null)
        {
            return StudySessionErrors.NotActive;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        StudySessionQuestion? question = await questionRepository.FirstOrDefaultAsync(
            item => item.Id == request.QuestionId && item.StudySessionId == session.Id);

        if (question is null)
        {
            return Error.NotFound("StudySessionQuestions.NotFound", "The study session question was not found.");
        }

        StudySessionSelectedSection? currentSection = await selectedSectionRepository.FirstOrDefaultAsync(
            section => section.StudySessionSelectionId == session.Selection.Id &&
                       section.StartedAtUtc != null &&
                       section.CompletedAtUtc == null);

        if (currentSection is null ||
            currentSection.StudyMaterialSectionId != question.StudyMaterialSectionId)
        {
            return Error.Conflict(
                "StudySessionQuestions.ChallengeUnavailable",
                "This challenge is not currently available.");
        }

        Result<Success> postponeResult = currentSection.PostponeChallenge(
            now,
            TimeSpan.FromMinutes(5));

        if (postponeResult.IsError)
        {
            return postponeResult.TopError;
        }

        await unitOfWork.SaveChangesAsync();

        return new PostponeStudySessionQuestionResponse(
            question.Id,
            currentSection.ChallengeDeferredUntilUtc!.Value);
    }

    public async Task<StudySessionResultResponse?> Handle(
        GetStudySessionResultQuery request,
        CancellationToken cancellationToken)
    {
        StudySession? session = await GetOwnedSessionAsync(
            request.SessionId,
            includeMaterial: true,
            includeSelection: true);

        if (session is null)
        {
            return null;
        }

        List<StudySessionQuestion> questions =
            (await questionRepository.GetAllAsync(
                question => question.StudySessionId == session.Id))
            .ToList();

        Guid[] questionIds = questions.Select(question => question.Id).ToArray();

        List<StudySessionQuestionAnswer> answers = questionIds.Length == 0
            ? []
            : (await answerRepository.GetAllAsync(
                answer => questionIds.Contains(answer.StudySessionQuestionId)))
                .ToList();

        DateTimeOffset end = session.CompletedAtUtc
            ?? session.CancelledAtUtc
            ?? timeProvider.GetUtcNow();

        int focusedMinutes = session.StartedAtUtc is null
            ? 0
            : Math.Max(
                0,
                (int)Math.Floor(
                    (end - session.StartedAtUtc.Value -
                     TimeSpan.FromSeconds(session.AccumulatedPausedSeconds))
                    .TotalMinutes));

        List<StudySession> completedSessions =
            (await studySessionRepository.GetAllAsync(
                item => item.StudentId == session.StudentId &&
                        item.Status == StudySessionStatus.Completed))
            .ToList();

        HashSet<DateOnly> completedDates = completedSessions
            .Where(item => item.CompletedAtUtc.HasValue)
            .Select(item => DateOnly.FromDateTime(item.CompletedAtUtc!.Value.UtcDateTime))
            .ToHashSet();

        int streakDays = 0;
        DateOnly streakDate = session.CompletedAtUtc.HasValue
            ? DateOnly.FromDateTime(session.CompletedAtUtc.Value.UtcDateTime)
            : DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        while (completedDates.Contains(streakDate))
        {
            streakDays++;
            streakDate = streakDate.AddDays(-1);
        }

        int correctAnswers = answers.Count(answer => answer.IsCorrect);

        return new StudySessionResultResponse(
            session.Id,
            session.Status.ToString(),
            focusedMinutes,
            session.CompletedSections.Count,
            questions.Select(question => question.StudyMaterialSectionId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .Count(),
            session.Material?.FileName ?? string.Empty,
            questions.Count,
            correctAnswers,
            answers.Count,
            streakDays);
    }

    private async Task<StudySession?> GetOwnedSessionAsync(
        Guid sessionId,
        bool includeMaterial = false,
        bool includeSelection = false)
    {
        if (sessionId == Guid.Empty ||
            currentUser.UserId is not Guid userId ||
            userId == Guid.Empty)
        {
            return null;
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(
            item => item.UserId == userId);

        if (student is null)
        {
            return null;
        }

        return includeMaterial && includeSelection
            ? await studySessionRepository.FirstOrDefaultAsync(
                item => item.Id == sessionId && item.StudentId == student.Id,
                item => item.Material!,
                item => item.Selection!,
                item => item.CompletedSections)
            : includeSelection
                ? await studySessionRepository.FirstOrDefaultAsync(
                    item => item.Id == sessionId && item.StudentId == student.Id,
                    item => item.Selection!,
                    item => item.CompletedSections)
                : includeMaterial
                    ? await studySessionRepository.FirstOrDefaultAsync(
                        item => item.Id == sessionId && item.StudentId == student.Id,
                        item => item.Material!,
                        item => item.CompletedSections)
                    : await studySessionRepository.FirstOrDefaultAsync(
                        item => item.Id == sessionId && item.StudentId == student.Id,
                        item => item.CompletedSections);
    }
}
