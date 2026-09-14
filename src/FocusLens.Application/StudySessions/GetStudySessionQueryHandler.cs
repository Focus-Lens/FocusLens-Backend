using FocusLens.Contracts.StudySessions;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.StudySessions;
using MediatR;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;

namespace FocusLens.Application.StudySessions;

public sealed class GetStudySessionQueryHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudySession> studySessionRepository,
    IBaseRepository<StudySessionSelectedSection> selectedSectionRepository,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<GetStudySessionQuery, StudySessionResponse?>
{
    public async Task<StudySessionResponse?> Handle(GetStudySessionQuery request,
        CancellationToken cancellationToken)
    {
        if (request.SessionId == Guid.Empty || currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return null;
        }

        Student? student = await studentRepository.FirstOrDefaultAsync(item => item.UserId == userId);
        if (student is null)
        {
            return null;
        }

        StudySession? session = await studySessionRepository.FirstOrDefaultAsync(
            item => item.Id == request.SessionId && item.StudentId == student.Id,
            item => item.Selection!.SelectedSections,
            item => item.CompletedSections);
        if (session is null)
        {
            return null;
        }

        DateTimeOffset utcNow = timeProvider.GetUtcNow();

        IEnumerable<StudySessionSelectedSection> selectedSections =
            await selectedSectionRepository.GetAllAsync(
                section => section.StudySessionSelectionId == session.Selection!.Id);

        int estimatedStudyTimeMinutes =
            selectedSections.Sum(section => section.EstimatedDurationMinutes);

        return session.ToResponse(utcNow) with
        {
            EstimatedStudyTimeMinutes = estimatedStudyTimeMinutes
        };
    }
}
