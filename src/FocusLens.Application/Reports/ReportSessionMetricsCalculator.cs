using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.StudySessions;

namespace FocusLens.Application.Reports;

public sealed class ReportSessionMetricsCalculator(
    IBaseRepository<StudySessionQuestion> questionRepository,
    IBaseRepository<StudySessionQuestionAnswer> answerRepository,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyDictionary<Guid, ReportSessionMetrics>> CalculateAsync(
        IReadOnlyCollection<StudySession> sessions)
    {
        Guid[] sessionIds = sessions.Select(session => session.Id).ToArray();
        if (sessionIds.Length == 0)
        {
            return new Dictionary<Guid, ReportSessionMetrics>();
        }

        StudySessionQuestion[] questions =
            (await questionRepository.GetAllAsync(question => sessionIds.Contains(question.StudySessionId)))
            .ToArray();
        Guid[] questionIds = questions.Select(question => question.Id).ToArray();
        StudySessionQuestionAnswer[] answers = questionIds.Length == 0
            ? []
            : (await answerRepository.GetAllAsync(answer => questionIds.Contains(answer.StudySessionQuestionId)))
            .ToArray();

        ILookup<Guid, StudySessionQuestion>
            questionsBySession = questions.ToLookup(question => question.StudySessionId);
        Dictionary<Guid, Guid> sessionByQuestion =
            questions.ToDictionary(question => question.Id, question => question.StudySessionId);
        ILookup<Guid, StudySessionQuestionAnswer> answersBySession = answers
            .Where(answer => sessionByQuestion.ContainsKey(answer.StudySessionQuestionId))
            .ToLookup(answer => sessionByQuestion[answer.StudySessionQuestionId]);

        DateTimeOffset utcNow = timeProvider.GetUtcNow();
        return sessions.ToDictionary(
            session => session.Id,
            session => Create(session, questionsBySession[session.Id], answersBySession[session.Id], utcNow));
    }

    private static ReportSessionMetrics Create(
        StudySession session,
        IEnumerable<StudySessionQuestion> sessionQuestions,
        IEnumerable<StudySessionQuestionAnswer> sessionAnswers,
        DateTimeOffset utcNow)
    {
        StudySessionQuestion[] questions = sessionQuestions.ToArray();
        StudySessionQuestionAnswer[] answers = sessionAnswers.ToArray();
        int correctQuestions = answers.Where(answer => answer.IsCorrect)
            .Select(answer => answer.StudySessionQuestionId)
            .Distinct()
            .Count();
        int totalSelectedSections = session.Selection?.SelectedSections.Count ?? 0;
        int completedSections = session.CompletedSections.Count;
        int completionPercentage = totalSelectedSections == 0
            ? 0
            : (int)Math.Round(completedSections * 100d / totalSelectedSections, MidpointRounding.AwayFromZero);
        int learningPercentage = questions.Length == 0
            ? 0
            : (int)Math.Round(correctQuestions * 100d / questions.Length, MidpointRounding.AwayFromZero);

        DateTimeOffset? end = session.Status switch
        {
            StudySessionStatus.Paused => session.PausedAtUtc,
            StudySessionStatus.Completed => session.CompletedAtUtc,
            StudySessionStatus.Cancelled => session.CancelledAtUtc,
            _ => utcNow
        };
        int duration = session.StartedAtUtc is null || end is null
            ? 0
            : Math.Max(0, (int)Math.Floor((end.Value - session.StartedAtUtc.Value -
                                           TimeSpan.FromSeconds(session.AccumulatedPausedSeconds)).TotalMinutes));

        return new ReportSessionMetrics(duration, completionPercentage, questions.Length, correctQuestions,
            answers.Length, learningPercentage, completedSections, totalSelectedSections);
    }
}

public sealed record ReportSessionMetrics(
    int DurationMinutes,
    int CompletionPercentage,
    int QuestionCount,
    int CorrectQuestions,
    int Attempts,
    int LearningPercentage,
    int CompletedSections,
    int TotalSelectedSections);