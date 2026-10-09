using FocusLens.Application.Common.Interfaces;
using FocusLens.Contracts.BehavioralIntelligence;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.StudySessions;
using MediatR;

namespace FocusLens.Application.StudySessions;

public sealed class GetStudySessionBehaviorWindowsQueryHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudySession> sessionRepository,
    IBaseRepository<StudySessionBehaviorWindow> windowRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetStudySessionBehaviorWindowsQuery, IReadOnlyCollection<BehaviorWindowResultResponse>?>
{
    public async Task<IReadOnlyCollection<BehaviorWindowResultResponse>?> Handle(
        GetStudySessionBehaviorWindowsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
            return null;

        Student? student = await studentRepository.FirstOrDefaultAsync(item => item.UserId == userId);
        if (student is null || await sessionRepository.FirstOrDefaultAsync(
                item => item.Id == request.SessionId && item.StudentId == student.Id) is null)
            return null;

        return (await windowRepository.GetAllAsync(item => item.StudySessionId == request.SessionId))
            .OrderBy(item => item.WindowIndex)
            .Select(item => new BehaviorWindowResultResponse(
                item.WindowIndex, item.WindowStartUtc, item.WindowEndUtc, item.IsFinal,
                item.FocusScore, item.FocusState, item.FocusTrend, item.UnderstandingScore,
                item.UnderstandingTrend, item.RawAction, item.RecommendedAction, item.ActionEmitted)
            {
                WindowActiveTimeSeconds = item.WindowActiveTimeSeconds
            })
            .ToArray();
    }
}
