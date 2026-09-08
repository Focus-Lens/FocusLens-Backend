using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using FocusLens.Contracts.StudySessions;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.StudySessions;
using FocusLens.Domain.Students;
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
        IRequestHandler<SetStudySessionSelectionCommand, Result<StudySessionResponse>>,
        IRequestHandler<UpdateStudySessionSelectionCommand, Result<StudySessionResponse>>,
        IRequestHandler<ReceiveStudySessionSectionsCommand, Result<StudySessionResponse>>,
        IRequestHandler<ChangeStudySessionMaterialCommand, Result<StudySessionResponse>>,
        IRequestHandler<StartStudySessionCommand, Result<StudySessionResponse>>,
        IRequestHandler<UploadStudyMaterialCommand, Result<StudySessionResponse>>
{
    public async Task<Result<StudySessionResponse>> Handle(CreateStudySessionCommand request, CancellationToken cancellationToken)
    {
        Result<Student> studentResult = await GetCurrentStudentAsync(includeSubjects: true);
        if (studentResult.IsError) return studentResult.TopError;
        if (!Enum.IsDefined(request.Request.Mode)) return StudySessionErrors.InvalidMode;

        Result<StudySession> sessionResult = StudySession.Create(
            studentResult.Value.Id,
            (DomainStudySessionMode)request.Request.Mode);
        if (sessionResult.IsError) return sessionResult.TopError;

        StudentSubject? subject = studentResult.Value.Subjects.SingleOrDefault(item => item.Id == request.Request.SubjectId);
        if (subject is null) return Error.Validation("StudySessions.InvalidSubject", "The selected subject does not belong to the student.");

        Result<Success> subjectResult = sessionResult.Value.SetSubject(subject);
        if (subjectResult.IsError) return subjectResult.TopError;

        Result<Success> durationResult = sessionResult.Value.SetDuration(request.Request.FocusDurationMinutes);
        if (durationResult.IsError) return durationResult.TopError;

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

    public async Task<Result<StudySessionResponse>> Handle(SetStudySessionSelectionCommand request, CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId, includeMaterial: true);
        if (sessionResult.IsError) return sessionResult.TopError;

        if (sessionResult.Value.PageRange is not null)
            return Error.Conflict("StudySessions.SelectionAlreadySet", "A selection has already been set for this study session.");

        return await SetSelectionAsync(sessionResult.Value, request.Request, cancellationToken);
    }

    public async Task<Result<StudySessionResponse>> Handle(UpdateStudySessionSelectionCommand request, CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId, includeMaterial: true);
        if (sessionResult.IsError) return sessionResult.TopError;

        if (sessionResult.Value.PageRange is null)
            return Error.Validation("StudySessions.SelectionNotSet", "Set a selection before updating it.");

        return await SetSelectionAsync(sessionResult.Value, request.Request, cancellationToken);
    }

    private async Task<Result<StudySessionResponse>> SetSelectionAsync(
        StudySession session,
        SetStudySessionSelectionRequest request,
        CancellationToken cancellationToken)
    {
        Result<StudySessionPageRange> rangeResult = StudySessionPageRange.Create(request.FromPage, request.ToPage);
        if (rangeResult.IsError) return rangeResult.TopError;

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

    public async Task<Result<StudySessionResponse>> Handle(ReceiveStudySessionSectionsCommand request, CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId, includeMaterial: true);
        if (sessionResult.IsError) return sessionResult.TopError;

        if (sessionResult.Value.Material is not { } material) return StudySessionErrors.SectionsRequireMaterial;

        List<StudyMaterialSection> selectedSections = [];
        foreach (StudyMaterialSectionRequest requestedSection in request.Request.Sections ?? [])
        {
            Result<StudyMaterialSection> sectionResult = StudyMaterialSection.Create(
                material.Id,
                requestedSection.Name,
                requestedSection.EstimatedDurationMinutes);
            if (sectionResult.IsError) return sectionResult.TopError;
            selectedSections.Add(sectionResult.Value);
        }

        Result<Success> setSectionsResult = sessionResult.Value.SetSelectedSections(selectedSections);
        if (setSectionsResult.IsError) return setSectionsResult.TopError;

        await sectionRepository.AddRangeAsync(selectedSections);
        return await SaveAsync(Result.Success, sessionResult.Value, selectedSections);
    }

    public async Task<Result<StudySessionResponse>> Handle(ChangeStudySessionMaterialCommand request, CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId);
        if (sessionResult.IsError) return sessionResult.TopError;
        StudyMaterial? material = await materialRepository.FirstOrDefaultAsync(
            item => item.Id == request.Request.StudyMaterialId
                && item.StudentId == sessionResult.Value.StudentId);
        if (material is null) return Error.NotFound("StudyMaterials.NotFound", "The study material was not found.");
        return await SaveAsync(sessionResult.Value.SetStudyMaterial(material), sessionResult.Value);
    }

    public async Task<Result<StudySessionResponse>> Handle(StartStudySessionCommand request, CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId, includeMaterial: true);
        if (sessionResult.IsError) return sessionResult.TopError;
        if (sessionResult.Value.Status == StudySessionStatus.Draft)
        {
            Result<Success> readyResult = sessionResult.Value.MarkReady();
            if (readyResult.IsError) return readyResult.TopError;
        }

        return await SaveAsync(sessionResult.Value.Start(DateTimeOffset.UtcNow), sessionResult.Value);
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

        materialRepository.Add(materialResult.Value);
        return await SaveAsync(Result.Success, sessionResult.Value);
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
