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
    IBaseRepository<StudySessionImage> imageRepository,
    IStudyMaterialFileStore materialFileStore,
    IStudySessionImageFileStore imageFileStore,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IRequestHandler<GetStudySessionQuery, StudySessionResponse?>,
        IRequestHandler<GetStudySessionDerivedMaterialQuery, StudySessionPrivateFileResponse?>,
        IRequestHandler<GetStudySessionImageQuery, StudySessionPrivateFileResponse?>
{
    public async Task<StudySessionPrivateFileResponse?> Handle(GetStudySessionDerivedMaterialQuery request, CancellationToken cancellationToken)
    {
        StudySession? session = await GetOwnedSessionAsync(request.SessionId);
        if (session?.Selection?.DerivedStorageReference is not { Length: > 0 } reference || session.Material is null) return null;
        try { await using Stream input = await materialFileStore.OpenReadAsync(reference, cancellationToken); using MemoryStream buffer = new(); await input.CopyToAsync(buffer, cancellationToken); return new(buffer.ToArray(), "application/pdf", session.Material.FileName); } catch (IOException) { return null; }
    }

    public async Task<StudySessionPrivateFileResponse?> Handle(GetStudySessionImageQuery request, CancellationToken cancellationToken)
    {
        StudySession? session = await GetOwnedSessionAsync(request.SessionId);
        if (session is null) return null;
        StudySessionImage? image = await imageRepository.FirstOrDefaultAsync(item => item.Id == request.ImageId && item.StudySessionId == session.Id);
        if (image is null) return null;
        try { await using Stream input = await imageFileStore.OpenReadAsync(image.StorageReference, cancellationToken); using MemoryStream buffer = new(); await input.CopyToAsync(buffer, cancellationToken); return new(buffer.ToArray(), image.ContentType, image.OriginalFileName); } catch (IOException) { return null; }
    }

    private async Task<StudySession?> GetOwnedSessionAsync(Guid sessionId)
    {
        if (sessionId == Guid.Empty || currentUser.UserId is not Guid userId || userId == Guid.Empty) return null;
        Student? student = await studentRepository.FirstOrDefaultAsync(item => item.UserId == userId);
        return student is null ? null : await studySessionRepository.FirstOrDefaultAsync(item => item.Id == sessionId && item.StudentId == student.Id, item => item.Material!, item => item.Selection!);
    }
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
            await selectedSectionRepository.GetAllAsync(section =>
                section.StudySessionSelectionId == session.Selection!.Id);

        int estimatedStudyTimeMinutes =
            selectedSections.Sum(section => section.EstimatedDurationMinutes);

        return session.ToResponse(utcNow) with { EstimatedStudyTimeMinutes = estimatedStudyTimeMinutes };
    }
}
