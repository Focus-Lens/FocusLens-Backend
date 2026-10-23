using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.Notifications;
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
    IBaseRepository<StudyMaterialSection> materialSectionRepository,
    ICurrentUser currentUser,
    IFocusLensAiClient aiClient,
    IBaseRepository<StudySessionBehaviorAnalysisJob> behaviorAnalysisJobRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    StudyNotificationService? studyNotificationService = null)
    : IRequestHandler<GetCurrentStudySessionQuestionQuery, StudySessionCurrentQuestionResponse?>,
        IRequestHandler<SubmitStudySessionQuestionAnswerCommand, Result<StudySessionQuestionAnswerResponse>>,
        IRequestHandler<GenerateStudySessionQuestionExplanationCommand,
            Result<StudySessionQuestionExplanationResponse>>,
        IRequestHandler<PostponeStudySessionQuestionCommand, Result<PostponeStudySessionQuestionResponse>>,
        IRequestHandler<MakeStudySessionReassessmentAvailableCommand, Result<Success>>,
        IRequestHandler<GetStudySessionResultQuery, StudySessionResultResponse?>
{
    public async Task<Result<Success>> Handle(
        MakeStudySessionReassessmentAvailableCommand request,
        CancellationToken cancellationToken)
    {
        StudySession? session = await GetOwnedSessionAsync(request.SessionId, includeSelection: true);
        if (session is null)
        {
            return Error.NotFound("StudySessions.NotFound", "The study session was not found.");
        }

        if (session.Status != StudySessionStatus.Active || session.Selection is null)
        {
            return StudySessionErrors.NotActive;
        }

        StudySessionSelectedSection? currentSection = await selectedSectionRepository.FirstOrDefaultAsync(section =>
            section.StudySessionSelectionId == session.Selection.Id &&
            section.StartedAtUtc != null && section.CompletedAtUtc == null);

        if (currentSection is null)
        {
            return Error.Conflict("StudySessionQuestions.ChallengeUnavailable",
                "There is no active section available for reassessment.");
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!currentSection.IsChallengeAvailableAt(now))
        {
            return Error.Conflict(
                "StudySessionQuestions.ChallengeUnavailable",
                "Reassessment is available only after the current section challenge is due.");
        }

        List<StudySessionQuestion> sessionQuestions = (await questionRepository.GetAllAsync(question =>
            question.StudySessionId == session.Id)).ToList();
        if (sessionQuestions.Any(question => question.StudyMaterialSectionId == currentSection.StudyMaterialSectionId &&
                                             question.IsReassessment))
            return Result.Success;

        StudySessionQuestion? source = sessionQuestions.FirstOrDefault(question =>
            question.StudyMaterialSectionId == currentSection.StudyMaterialSectionId && !question.IsReassessment);
        if (source is null || session.Selection?.AiExtractedText is not { Length: > 0 } content)
            return Error.Validation("StudySessionQuestions.ReassessmentUnavailable", "Learning content is unavailable for reassessment.");

        List<StudySessionQuestionAnswer> sourceAnswers = (await answerRepository.GetAllAsync(answer =>
            answer.StudySessionQuestionId == source.Id)).ToList();
        if (sourceAnswers.Any(answer => answer.IsCorrect) ||
            sourceAnswers.Count(answer => !answer.IsCorrect) < 2 ||
            source.ExplanationShownAtUtc is null)
        {
            return Error.Validation(
                "StudySessionQuestions.ReassessmentNotEligible",
                "Reassessment requires two incorrect attempts and the adaptive explanation for the current question.");
        }

        int nextOrder = sessionQuestions.Select(question => question.Order).DefaultIfEmpty().Max() + 1;
        AiMcqGenerationResult generated = await aiClient.GenerateMcqAsync(source.AiSectionId, source.AiSectionTitle,
            source.AiConceptId, source.AiConceptName, content, 1, cancellationToken);
        AiMcqQuestion? item = generated.Questions.FirstOrDefault();
        if (item is null) return Error.Validation("StudySessionQuestions.ReassessmentGenerationFailed", "A reassessment question could not be generated.");
        Result<StudySessionQuestion> created = StudySessionQuestion.Create(session.Id, currentSection.StudyMaterialSectionId,
            source.AiSectionId, source.AiSectionTitle, source.AiConceptId, source.AiConceptName, item.Question,
            item.Options, item.CorrectAnswer, item.Difficulty, item.Explanation, Math.Max(1, item.EstimatedTimeMinutes), nextOrder,
            isReassessment: true);
        if (created.IsError) return created.TopError;
        questionRepository.Add(created.Value);

        await unitOfWork.SaveChangesAsync();
        return Result.Success;
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
            await questionRepository.FirstOrDefaultAsync(item => item.Id == request.QuestionId &&
                                                                 item.StudySessionId == session.Id);

        if (question is null)
        {
            return Error.NotFound(
                "StudySessionQuestions.NotFound",
                "The study session question was not found.");
        }

        List<StudySessionQuestionAnswer> answers =
            (await answerRepository.GetAllAsync(answer => answer.StudySessionQuestionId == question.Id))
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
            (await selectedSectionRepository.GetAllAsync(section =>
                section.StudySessionSelectionId == session.Selection.Id))
            .OrderBy(section => section.Order)
            .ToList();

        StudySessionSelectedSection? currentSection =
            selectedSections.FirstOrDefault(section => section.StartedAtUtc is not null &&
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
            (await answerRepository.GetAllAsync(answer => questionIds.Contains(answer.StudySessionQuestionId)))
            .ToList();

        HashSet<Guid> correctlyAnswered =
            answers
                .Where(answer => answer.IsCorrect)
                .Select(answer => answer.StudySessionQuestionId)
                .ToHashSet();

        StudySessionQuestion? currentQuestion =
            questions
                .Where(question => !correctlyAnswered.Contains(question.Id) &&
                                   question.StudyMaterialSectionId == currentSection.StudyMaterialSectionId)
                .OrderByDescending(question => question.IsReassessment)
                .ThenBy(question => question.Order)
                .FirstOrDefault();

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
                true);
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

    public async Task<StudySessionResultResponse?> Handle(
        GetStudySessionResultQuery request,
        CancellationToken cancellationToken)
    {
        StudySession? session = await GetOwnedSessionAsync(
            request.SessionId,
            true,
            true);

        if (session is null)
        {
            return null;
        }

        List<StudySessionQuestion> questions =
            (await questionRepository.GetAllAsync(question => question.StudySessionId == session.Id))
            .ToList();

        Guid[] questionIds = questions.Select(question => question.Id).ToArray();

        List<StudySessionQuestionAnswer> answers = questionIds.Length == 0
            ? []
            : (await answerRepository.GetAllAsync(answer => questionIds.Contains(answer.StudySessionQuestionId)))
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
            (await studySessionRepository.GetAllAsync(item => item.StudentId == session.StudentId &&
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

        Guid[] completedSectionIds = session.CompletedSections.Select(item => item.StudyMaterialSectionId).ToArray();
        IReadOnlyCollection<string> completedSectionNames = completedSectionIds.Length == 0 ? [] :
            (await materialSectionRepository.GetAllAsync(item => completedSectionIds.Contains(item.Id)))
            .OrderBy(item => item.FromPage).Select(item => item.Name).ToArray();
        IReadOnlyCollection<DateOnly> activeDates = completedDates.OrderByDescending(item => item).Take(7).Order().ToArray();

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
            streakDays,
            completedSectionNames,
            activeDates);
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
        StudySessionQuestion? question = await questionRepository.FirstOrDefaultAsync(item =>
            item.Id == request.QuestionId && item.StudySessionId == session.Id);

        if (question is null)
        {
            return Error.NotFound("StudySessionQuestions.NotFound", "The study session question was not found.");
        }

        StudySessionSelectedSection? currentSection = await selectedSectionRepository.FirstOrDefaultAsync(section =>
            section.StudySessionSelectionId == session.Selection.Id &&
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

        // Answering a question is genuine study activity.  Keep the session's
        // activity watermark current so a later completion is not measured
        // from an unrelated stale heartbeat.
        Result<Success> activityResult = session.UpdateProgress(null, now);
        if (activityResult.IsError)
        {
            return activityResult.TopError;
        }

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
            (await selectedSectionRepository.GetAllAsync(section =>
                section.StudySessionSelectionId == session.Selection!.Id))
            .OrderBy(section => section.Order)
            .ToList();

        StudySessionSelectedSection? currentSection = selectedSections.FirstOrDefault(section =>
            section.StartedAtUtc is not null && section.CompletedAtUtc is null);

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
            (await answerRepository.GetAllAsync(answer => answer.StudySessionQuestionId == question.Id))
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
            (await questionRepository.GetAllAsync(item => item.StudySessionId == session.Id))
            .OrderBy(item => item.Order)
            .ToList();

        Guid[] questionIds = questions.Select(item => item.Id).ToArray();

        List<StudySessionQuestionAnswer> sessionAnswers =
            (await answerRepository.GetAllAsync(answer => questionIds.Contains(answer.StudySessionQuestionId)))
            .ToList();

        if (isCorrect)
        {
            HashSet<Guid> successfulQuestionIds = sessionAnswers
                .Where(answer => answer.IsCorrect)
                .Select(answer => answer.StudySessionQuestionId)
                .ToHashSet();

            successfulQuestionIds.Add(question.Id);

            List<StudySessionQuestion> currentSectionQuestions = questions
                .Where(item => item.StudyMaterialSectionId == currentSection.StudyMaterialSectionId)
                .ToList();

            if (session.IsIncreasedAssessmentFrequencyEnabled &&
                currentSection.CompletedChallengeCount == 0 &&
                currentSectionQuestions.Count == 1)
            {
                StudySessionQuestion sourceQuestion = currentSectionQuestions[0];
                AiMcqGenerationResult generated = await aiClient.GenerateMcqAsync(
                    sourceQuestion.AiSectionId,
                    sourceQuestion.AiSectionTitle,
                    sourceQuestion.AiConceptId,
                    sourceQuestion.AiConceptName,
                    content,
                    1,
                    cancellationToken);
                AiMcqQuestion? generatedQuestion = generated.Questions.FirstOrDefault();

                if (generatedQuestion is null)
                {
                    return Error.Validation(
                        "StudySessionQuestions.FrequencyQuestionGenerationFailed",
                        "An additional assessment question could not be generated.");
                }

                Result<StudySessionQuestion> additionalQuestionResult = StudySessionQuestion.Create(
                    session.Id,
                    currentSection.StudyMaterialSectionId,
                    sourceQuestion.AiSectionId,
                    sourceQuestion.AiSectionTitle,
                    sourceQuestion.AiConceptId,
                    sourceQuestion.AiConceptName,
                    generatedQuestion.Question,
                    generatedQuestion.Options,
                    generatedQuestion.CorrectAnswer,
                    generatedQuestion.Difficulty,
                    generatedQuestion.Explanation,
                    Math.Max(1, generatedQuestion.EstimatedTimeMinutes),
                    questions.Select(item => item.Order).DefaultIfEmpty().Max() + 1);

                if (additionalQuestionResult.IsError)
                {
                    return additionalQuestionResult.TopError;
                }

                questionRepository.Add(additionalQuestionResult.Value);
                questions.Add(additionalQuestionResult.Value);

                Result<Success> resolveChallengeResult = currentSection.ResolveChallenge();
                if (resolveChallengeResult.IsError)
                {
                    return resolveChallengeResult.TopError;
                }

                currentSectionQuestions.Add(additionalQuestionResult.Value);
            }

            // A reassessment is a new measure of the same section's
            // understanding. Once it is answered correctly, it replaces the
            // failed original question for section-completion purposes.
            bool reassessmentSucceeded = currentSectionQuestions.Any(item =>
                item.IsReassessment && successfulQuestionIds.Contains(item.Id));
            bool currentSectionCompleted = reassessmentSucceeded ||
                currentSectionQuestions.All(item => successfulQuestionIds.Contains(item.Id));

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
                nextSection = selectedSections.FirstOrDefault(section => section.Order > currentSection.Order &&
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

                Result<StudySessionBehaviorAnalysisJob> jobResult =
                    StudySessionBehaviorAnalysisJob.Create(
                        session.Id,
                        now);

                if (jobResult.IsError)
                {
                    return jobResult.TopError;
                }

                behaviorAnalysisJobRepository.Add(jobResult.Value);

                if (studyNotificationService is not null)
                {
                    await studyNotificationService.NotifySessionCompletedAsync(session.Id, session.StudentId);
                }
            }

            await unitOfWork.SaveChangesAsync();

            HashSet<Guid> reassessmentCompletedSectionIds = questions
                .Where(item => item.IsReassessment && successfulQuestionIds.Contains(item.Id) &&
                               item.StudyMaterialSectionId is not null)
                .Select(item => item.StudyMaterialSectionId!.Value)
                .ToHashSet();

            StudySessionQuestion? nextQuestion = questions.FirstOrDefault(item =>
                !successfulQuestionIds.Contains(item.Id) &&
                (item.IsReassessment || item.StudyMaterialSectionId is null ||
                 !reassessmentCompletedSectionIds.Contains(item.StudyMaterialSectionId.Value)));

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

        Student? student = await studentRepository.FirstOrDefaultAsync(item => item.UserId == userId);

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
