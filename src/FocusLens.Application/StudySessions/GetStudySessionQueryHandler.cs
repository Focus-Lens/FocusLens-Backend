using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using FocusLens.Contracts.StudySessions;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.StudySessions;
using MediatR;

namespace FocusLens.Application.StudySessions;

public sealed class GetStudySessionQueryHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudySession> studySessionRepository,
    IBaseRepository<StudyMaterialSection> sectionRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetStudySessionQuery, StudySessionResponse?>
{
    public async Task<StudySessionResponse?> Handle(GetStudySessionQuery request, CancellationToken cancellationToken)
    {
        if (request.SessionId == Guid.Empty || currentUser.UserId is not Guid userId || userId == Guid.Empty)
            return null;

        Student? student = await studentRepository.FirstOrDefaultAsync(item => item.UserId == userId);
        if (student is null) return null;

        StudySession? session = await studySessionRepository.FirstOrDefaultAsync(
            item => item.Id == request.SessionId && item.StudentId == student.Id,
            item => item.Material!,
            item => item.SelectedSections);
        if (session is null) return null;

        IReadOnlyCollection<StudyMaterialSection> sections = session.StudyMaterialId is Guid materialId
            ? (await sectionRepository.GetAllAsync(section => section.StudyMaterialId == materialId)).ToArray()
            : [];
        return session.ToResponse(sections);
    }
}
