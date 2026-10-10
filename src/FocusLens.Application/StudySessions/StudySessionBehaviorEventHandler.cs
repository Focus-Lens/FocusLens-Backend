using FocusLens.Contracts.StudySessions;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.StudySessions;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using DomainEventType = FocusLens.Domain.StudySessions.StudySessionBehaviorEventType;

namespace FocusLens.Application.StudySessions;

public sealed class StudySessionBehaviorEventHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudySession> sessionRepository,
    IBaseRepository<StudySessionQuestion> questionRepository,
    IBaseRepository<StudySessionBehaviorEvent> eventRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<RecordStudySessionBehaviorEventsCommand, Result<StudySessionBehaviorEventsResponse>>
{
    private const int MaximumBatchSize = 100;

    public async Task<Result<StudySessionBehaviorEventsResponse>> Handle(
        RecordStudySessionBehaviorEventsCommand request,
        CancellationToken cancellationToken)
    {
        StudySessionBehaviorEventRequest[] events = request.Request.Events?.ToArray() ?? [];
        if (events.Length is 0 or > MaximumBatchSize)
        {
            return Error.Validation(
                "StudySessionBehaviorEvents.BatchInvalid",
                $"A behavior event batch must contain between 1 and {MaximumBatchSize} events.");
        }

        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.NotFound("StudySessions.NotFound", "Study session was not found.");
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(item => item.UserId == userId);
        if (student is null)
        {
            return Error.NotFound("StudySessions.NotFound", "Study session was not found.");
        }

        StudySession? session = await sessionRepository.FirstOrDefaultAsync(
            item => item.Id == request.SessionId && item.StudentId == student.Id,
            item => item.Selection!.SelectedSections);
        if (session is null)
        {
            return Error.NotFound("StudySessions.NotFound", "Study session was not found.");
        }

        if (session.StartedAtUtc is null ||
            session.Status is StudySessionStatus.Completed or StudySessionStatus.Cancelled)
        {
            return Error.Conflict(
                "StudySessionBehaviorEvents.SessionUnavailable",
                "Behavior events can only be recorded for an in-progress study session.");
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid[] sectionIds = session.Selection?.SelectedSections
            .Select(section => section.StudyMaterialSectionId)
            .ToArray() ?? [];
        Guid[] requestedQuestionIds = events
            .Where(item => item.StudySessionQuestionId.HasValue)
            .Select(item => item.StudySessionQuestionId!.Value)
            .Distinct()
            .ToArray();
        Guid[] ownedQuestionIds = requestedQuestionIds.Length == 0
            ? []
            : (await questionRepository.GetAllAsync(question =>
                question.StudySessionId == session.Id &&
                requestedQuestionIds.Contains(question.Id)))
            .Select(question => question.Id)
            .ToArray();

        List<StudySessionBehaviorEvent> entities = [];
        foreach (StudySessionBehaviorEventRequest item in events)
        {
            if (item.OccurredAtUtc < session.StartedAtUtc.Value || item.OccurredAtUtc > now)
            {
                return Error.Validation(
                    "StudySessionBehaviorEvents.TimestampInvalid",
                    "Behavior event time must be within the started session timeline.");
            }

            if (item.StudyMaterialSectionId is Guid sectionId && !sectionIds.Contains(sectionId))
            {
                return Error.Validation(
                    "StudySessionBehaviorEvents.SectionInvalid",
                    "Behavior event material section is not selected for this session.");
            }

            if (item.StudySessionQuestionId is Guid questionId && !ownedQuestionIds.Contains(questionId))
            {
                return Error.Validation(
                    "StudySessionBehaviorEvents.QuestionInvalid",
                    "Behavior event question does not belong to this session.");
            }

            Result<StudySessionBehaviorEvent> eventResult = StudySessionBehaviorEvent.Create(
                session.Id,
                item.StudyMaterialSectionId,
                item.StudySessionQuestionId,
                (DomainEventType)(int)item.EventType,
                item.OccurredAtUtc,
                item.ScrollSpeedAvgPxPerSec,
                item.ScrollDirectionChanges,
                item.ContentProgressionPct,
                item.InteractionCount,
                item.BackgroundCount,
                item.BackgroundDurationSeconds);
            if (eventResult.IsError)
            {
                return eventResult.TopError;
            }

            entities.Add(eventResult.Value);
        }

        await eventRepository.AddRangeAsync(entities);
        await unitOfWork.SaveChangesAsync();
        return new StudySessionBehaviorEventsResponse(entities.Count);
    }
}