using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using FocusLens.Contracts.StudySessions;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.StudySessions;
using MediatR;
using DomainStudyMaterialSource = FocusLens.Domain.StudySessions.StudyMaterialSource;
using DomainStudySessionMode = FocusLens.Domain.StudySessions.StudySessionMode;

namespace FocusLens.Application.StudySessions;

public sealed class StudySessionCommandHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudySession> studySessionRepository,
    IBaseRepository<StudyMaterial> materialRepository,
    IBaseRepository<StudyMaterialSection> sectionRepository,
    IStudyMaterialFileStore fileStore,
    IStudyMaterialPdfProcessor pdfProcessor,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateStudySessionCommand, Result<StudySessionResponse>>,
        IRequestHandler<SetStudySessionModeCommand, Result<StudySessionResponse>>,
        IRequestHandler<SetStudySessionSubjectCommand, Result<StudySessionResponse>>,
        IRequestHandler<SetStudySessionDurationCommand, Result<StudySessionResponse>>,
        IRequestHandler<SetStudySessionPageRangeCommand, Result<StudySessionResponse>>,
        IRequestHandler<SetStudySessionSectionsCommand, Result<StudySessionResponse>>,
        IRequestHandler<ChangeStudySessionSettingsCommand, Result<StudySessionResponse>>,
        IRequestHandler<MarkStudySessionReadyCommand, Result<StudySessionResponse>>,
        IRequestHandler<StartStudySessionCommand, Result<StudySessionResponse>>,
        IRequestHandler<UploadStudyMaterialCommand, Result<StudySessionResponse>>
{
    public async Task<Result<StudySessionResponse>> Handle(CreateStudySessionCommand request, CancellationToken cancellationToken)
    {
        Result<Student> studentResult = await GetCurrentStudentAsync();
        if (studentResult.IsError) return studentResult.TopError;
        if (!Enum.IsDefined(request.Request.Mode)) return StudySessionErrors.InvalidMode;

        Result<StudySession> sessionResult = StudySession.Create(
            studentResult.Value.Id,
            (DomainStudySessionMode)request.Request.Mode);
        if (sessionResult.IsError) return sessionResult.TopError;

        studySessionRepository.Add(sessionResult.Value);
        await unitOfWork.SaveChangesAsync();
        return sessionResult.Value.ToResponse([]);
    }

    public async Task<Result<StudySessionResponse>> Handle(SetStudySessionModeCommand request, CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId);
        if (sessionResult.IsError) return sessionResult.TopError;
        if (!Enum.IsDefined(request.Request.Mode)) return StudySessionErrors.InvalidMode;
        return await SaveAsync(sessionResult.Value.SetMode((DomainStudySessionMode)request.Request.Mode), sessionResult.Value);
    }

    public async Task<Result<StudySessionResponse>> Handle(SetStudySessionSubjectCommand request, CancellationToken cancellationToken)
    {
        Result<Student> studentResult = await GetCurrentStudentAsync(includeSubjects: true);
        if (studentResult.IsError) return studentResult.TopError;
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId, studentResult.Value);
        if (sessionResult.IsError) return sessionResult.TopError;

        var subject = studentResult.Value.Subjects.SingleOrDefault(item => item.Id == request.Request.SubjectId);
        if (subject is null) return Error.Validation("StudySessions.InvalidSubject", "The selected subject does not belong to the student.");
        return await SaveAsync(sessionResult.Value.SetSubject(subject), sessionResult.Value);
    }

    public async Task<Result<StudySessionResponse>> Handle(SetStudySessionDurationCommand request, CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId);
        if (sessionResult.IsError) return sessionResult.TopError;
        return await SaveAsync(sessionResult.Value.SetDuration(request.Request.FocusDurationMinutes), sessionResult.Value);
    }

    public async Task<Result<StudySessionResponse>> Handle(SetStudySessionPageRangeCommand request, CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId, includeMaterial: true);
        if (sessionResult.IsError) return sessionResult.TopError;
        Result<StudySessionPageRange> rangeResult = StudySessionPageRange.Create(request.Request.FromPage, request.Request.ToPage);
        if (rangeResult.IsError) return rangeResult.TopError;

        StudySession session = sessionResult.Value;
        if (session.Material is not { } material) return StudySessionErrors.PageRangeRequiresMaterial;

        Result<Success> setRangeResult = session.SetPageRange(rangeResult.Value);
        if (setRangeResult.IsError) return setRangeResult.TopError;

        await using Stream original = await fileStore.OpenReadAsync(material.StorageReference, cancellationToken);
        await using Stream derived = await pdfProcessor.ExtractPagesAsync(original, rangeResult.Value, cancellationToken);
        Result<Success> derivedResult = material.SetDerivedStorageReference(
            await fileStore.SaveDerivedAsync(session.StudentId, material.FileName, derived, cancellationToken));
        if (derivedResult.IsError) return derivedResult.TopError;

        return await SaveAsync(Result.Success, session);
    }

    public async Task<Result<StudySessionResponse>> Handle(SetStudySessionSectionsCommand request, CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId, includeMaterial: true);
        if (sessionResult.IsError) return sessionResult.TopError;
        IReadOnlyCollection<Guid> sectionIds = request.Request.SectionIds ?? [];
        if (sectionIds.Distinct().Count() != sectionIds.Count) return StudySessionErrors.DuplicateSection;

        IEnumerable<StudyMaterialSection> sections = await sectionRepository.GetAllAsync(section => sectionIds.Contains(section.Id));
        List<StudyMaterialSection> selectedSections = sections.ToList();
        if (selectedSections.Count != sectionIds.Count) return StudySessionErrors.SectionMismatch;
        return await SaveAsync(sessionResult.Value.SetSelectedSections(selectedSections), sessionResult.Value, selectedSections);
    }

    public async Task<Result<StudySessionResponse>> Handle(ChangeStudySessionSettingsCommand request, CancellationToken cancellationToken)
    {
        ChangeStudySessionSettingsRequest settings = request.Request;
        if (settings.Mode is null && settings.SubjectId is null && settings.FocusDurationMinutes is null)
            return Error.Validation("StudySessions.NoSettingsProvided", "Provide at least one setting to change.");

        Result<Student> studentResult = await GetCurrentStudentAsync(includeSubjects: settings.SubjectId is not null);
        if (studentResult.IsError) return studentResult.TopError;
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId, studentResult.Value);
        if (sessionResult.IsError) return sessionResult.TopError;
        StudySession session = sessionResult.Value;

        if (settings.Mode is not null)
        {
            if (!Enum.IsDefined(settings.Mode.Value)) return StudySessionErrors.InvalidMode;
            Result<Success> result = session.SetMode((DomainStudySessionMode)settings.Mode.Value);
            if (result.IsError) return result.TopError;
        }

        if (settings.SubjectId is not null)
        {
            var subject = studentResult.Value.Subjects.SingleOrDefault(item => item.Id == settings.SubjectId.Value);
            if (subject is null) return Error.Validation("StudySessions.InvalidSubject", "The selected subject does not belong to the student.");
            Result<Success> result = session.SetSubject(subject);
            if (result.IsError) return result.TopError;
        }

        if (settings.FocusDurationMinutes is not null)
        {
            Result<Success> result = session.SetDuration(settings.FocusDurationMinutes.Value);
            if (result.IsError) return result.TopError;
        }

        return await SaveAsync(Result.Success, session);
    }

    public async Task<Result<StudySessionResponse>> Handle(MarkStudySessionReadyCommand request, CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId, includeMaterial: true);
        if (sessionResult.IsError) return sessionResult.TopError;
        return await SaveAsync(sessionResult.Value.MarkReady(), sessionResult.Value);
    }

    public async Task<Result<StudySessionResponse>> Handle(StartStudySessionCommand request, CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId, includeMaterial: true);
        if (sessionResult.IsError) return sessionResult.TopError;
        return await SaveAsync(sessionResult.Value.Start(), sessionResult.Value);
    }

    public async Task<Result<StudySessionResponse>> Handle(UploadStudyMaterialCommand request, CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId);
        if (sessionResult.IsError) return sessionResult.TopError;
        if (request.Content.Length == 0) return Error.Validation("StudyMaterials.EmptyFile", "The uploaded file is empty.");
        if (!Enum.IsDefined(request.Source)) return Error.Validation("StudyMaterials.SourceInvalid", "Material source is invalid.");

        await using var content = new MemoryStream(request.Content, writable: false);
        int pageCount;
        try
        {
            pageCount = await pdfProcessor.GetPageCountAsync(content, cancellationToken);
        }
        catch (Exception)
        {
            return Error.Validation("StudyMaterials.InvalidPdf", "The uploaded file is not a readable PDF.");
        }

        content.Position = 0;
        string storageReference = await fileStore.SaveOriginalAsync(
            sessionResult.Value.StudentId,
            request.FileName,
            content,
            cancellationToken);
        Result<StudyMaterial> materialResult = StudyMaterial.Create(
            sessionResult.Value.StudentId,
            request.FileName,
            request.FileSizeBytes,
            pageCount,
            storageReference,
            (DomainStudyMaterialSource)request.Source);
        if (materialResult.IsError) return materialResult.TopError;

        Result<Success> attachResult = sessionResult.Value.SetStudyMaterial(materialResult.Value);
        if (attachResult.IsError) return attachResult.TopError;

        List<StudyMaterialSection> sections = [];
        foreach (StudyMaterialSectionRequest requestedSection in request.Sections)
        {
            Result<StudyMaterialSection> sectionResult = StudyMaterialSection.Create(
                materialResult.Value.Id,
                requestedSection.Name,
                requestedSection.EstimatedDurationMinutes);
            if (sectionResult.IsError) return sectionResult.TopError;
            sections.Add(sectionResult.Value);
        }

        materialRepository.Add(materialResult.Value);
        await sectionRepository.AddRangeAsync(sections);
        return await SaveAsync(Result.Success, sessionResult.Value, sections);
    }

    private async Task<Result<Student>> GetCurrentStudentAsync(bool includeSubjects = false)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
            return Error.Unauthorized("StudySessions.CurrentUserUnavailable", "The current user could not be identified.");

        Student? student = includeSubjects
            ? await studentRepository.FirstOrDefaultAsync(item => item.UserId == userId, item => item.Subjects)
            : await studentRepository.FirstOrDefaultAsync(item => item.UserId == userId);
        return student is null
            ? Error.NotFound("StudySessions.StudentNotFound", "The student profile was not found.")
            : student;
    }

    private async Task<Result<StudySession>> GetSessionAsync(Guid sessionId, Student? knownStudent = null, bool includeMaterial = false)
    {
        if (sessionId == Guid.Empty) return Error.Validation("StudySessions.IdRequired", "Study session id is required.");
        Result<Student> studentResult = knownStudent is null ? await GetCurrentStudentAsync() : knownStudent;
        if (studentResult.IsError) return studentResult.TopError;

        StudySession? session = includeMaterial
            ? await studySessionRepository.FirstOrDefaultAsync(item => item.Id == sessionId && item.StudentId == studentResult.Value.Id, item => item.Material!, item => item.SelectedSections)
            : await studySessionRepository.FirstOrDefaultAsync(item => item.Id == sessionId && item.StudentId == studentResult.Value.Id, item => item.SelectedSections);
        return session is null
            ? Error.NotFound("StudySessions.NotFound", "The study session was not found.")
            : session;
    }

    private async Task<Result<StudySessionResponse>> SaveAsync(Result<Success> result, StudySession session, IReadOnlyCollection<StudyMaterialSection>? knownSections = null)
    {
        if (result.IsError) return result.TopError;
        await unitOfWork.SaveChangesAsync();
        IReadOnlyCollection<StudyMaterialSection> sections = knownSections ?? await GetMaterialSectionsAsync(session);
        return session.ToResponse(sections);
    }

    private async Task<IReadOnlyCollection<StudyMaterialSection>> GetMaterialSectionsAsync(StudySession session)
    {
        if (session.StudyMaterialId is not Guid materialId) return [];
        return (await sectionRepository.GetAllAsync(section => section.StudyMaterialId == materialId)).ToArray();
    }
}
