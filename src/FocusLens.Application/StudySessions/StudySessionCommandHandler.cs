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
    IBaseRepository<StudySessionSelection> selectionRepository,
    IBaseRepository<StudyMaterialSection> sectionRepository,
    IBaseRepository<StudySessionSelectedSection> selectedSectionRepository,
    IStudyMaterialFileStore fileStore,
    IStudyMaterialPdfProcessor pdfProcessor,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider = null!)
    : IRequestHandler<CreateStudySessionCommand, Result<StudySessionResponse>>,
        IRequestHandler<SetStudySessionModeCommand, Result<Success>>,
        IRequestHandler<SetStudySessionSubjectCommand, Result<Success>>,
        IRequestHandler<SetStudySessionDurationCommand, Result<Success>>,
        IRequestHandler<SetStudySessionSelectionCommand, Result<StudySessionSelectionResponse>>,
        IRequestHandler<UpdateStudySessionSelectionCommand, Result<StudySessionSelectionResponse>>,
        IRequestHandler<ReceiveStudySessionSectionsCommand, Result<StudySessionResponse>>,
        IRequestHandler<ChangeStudySessionMaterialCommand, Result<StudyMaterialResponse>>,
        IRequestHandler<StartStudySessionCommand, Result<Success>>,
        IRequestHandler<UploadStudyMaterialCommand, Result<StudyMaterialResponse>>,
        IRequestHandler<PauseStudySessionCommand, Result<StudySessionResponse>>,
        IRequestHandler<ResumeStudySessionCommand, Result<StudySessionResponse>>,
        IRequestHandler<EndStudySessionCommand, Result<StudySessionResponse>>,
        IRequestHandler<UpdateStudySessionProgressCommand, Result<StudySessionResponse>>
{
    public async Task<Result<StudySessionResponse>> Handle(CreateStudySessionCommand request, CancellationToken cancellationToken)
    {
        Result<Student> studentResult = await GetCurrentStudentAsync(includeSubjects: true);
        if (studentResult.IsError) return studentResult.TopError;
        if (!Enum.IsDefined(request.Request.Mode)) return StudySessionErrors.InvalidMode;
        Result<StudySession> sessionResult = StudySession.Create(studentResult.Value.Id, (DomainStudySessionMode)request.Request.Mode);
        if (sessionResult.IsError) return sessionResult.TopError;
        StudentSubject? subject = studentResult.Value.Subjects.SingleOrDefault(item => item.Id == request.Request.SubjectId);
        if (subject is null) return Error.Validation("StudySessions.InvalidSubject", "The selected subject does not belong to the student.");
        Result<Success> subjectResult = sessionResult.Value.SetSubject(subject);
        if (subjectResult.IsError) return subjectResult.TopError;
        Result<Success> durationResult = sessionResult.Value.SetDuration(request.Request.FocusDurationMinutes);
        if (durationResult.IsError) return durationResult.TopError;
        studySessionRepository.Add(sessionResult.Value);
        await unitOfWork.SaveChangesAsync();
        return sessionResult.Value.ToResponse(timeProvider.GetUtcNow());
    }

    public async Task<Result<Success>> Handle(SetStudySessionModeCommand request, CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId);
        if (sessionResult.IsError) return sessionResult.TopError;
        if (!Enum.IsDefined(request.Request.Mode)) return StudySessionErrors.InvalidMode;
        return await SaveSessionAsync(sessionResult.Value.SetMode((DomainStudySessionMode)request.Request.Mode));
    }

    public async Task<Result<Success>> Handle(SetStudySessionSubjectCommand request, CancellationToken cancellationToken)
    {
        Result<Student> studentResult = await GetCurrentStudentAsync(includeSubjects: true);
        if (studentResult.IsError) return studentResult.TopError;
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId, studentResult.Value);
        if (sessionResult.IsError) return sessionResult.TopError;
        StudentSubject? subject = studentResult.Value.Subjects.SingleOrDefault(item => item.Id == request.Request.SubjectId);
        if (subject is null) return Error.Validation("StudySessions.InvalidSubject", "The selected subject does not belong to the student.");
        return await SaveSessionAsync(sessionResult.Value.SetSubject(subject));
    }

    public async Task<Result<Success>> Handle(SetStudySessionDurationCommand request, CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId);
        return sessionResult.IsError ? sessionResult.TopError : await SaveSessionAsync(sessionResult.Value.SetDuration(request.Request.FocusDurationMinutes));
    }

    public async Task<Result<StudySessionSelectionResponse>> Handle(SetStudySessionSelectionCommand request, CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId, includeMaterial: true);
        if (sessionResult.IsError) return sessionResult.TopError;
        StudySessionSelection? existing = await selectionRepository.FirstOrDefaultAsync(selection => selection.StudySessionId == request.SessionId);
        if (existing is not null) return Error.Conflict("StudySessions.SelectionAlreadySet", "A selection has already been set for this study session.");
        return await CreateSelectionAsync(sessionResult.Value, request.Request, cancellationToken);
    }

    public async Task<Result<StudySessionSelectionResponse>> Handle(UpdateStudySessionSelectionCommand request, CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId, includeMaterial: true);
        if (sessionResult.IsError) return sessionResult.TopError;
        StudySessionSelection? existing = await selectionRepository.FirstOrDefaultAsync(selection => selection.StudySessionId == request.SessionId, selection => selection.SelectedSections);
        if (existing is null) return Error.Validation("StudySessions.SelectionNotSet", "Set a selection before updating it.");
        if (existing.StudyMaterialId != sessionResult.Value.StudyMaterialId) return StudySessionErrors.SelectionMaterialMismatch;
        string? oldDerivedStorageReference = existing.DerivedStorageReference;
        selectionRepository.Delete(existing);
        Result<StudySessionSelectionResponse> result = await CreateSelectionAsync(sessionResult.Value, request.Request, cancellationToken);
        if (result.IsSuccess && oldDerivedStorageReference is not null)
        {
            await fileStore.DeleteAsync(oldDerivedStorageReference, cancellationToken);
        }

        return result;
    }

    private async Task<Result<StudySessionSelectionResponse>> CreateSelectionAsync(StudySession session, SetStudySessionSelectionRequest request, CancellationToken cancellationToken)
    {
        if (session.Material is not { } material) return StudySessionErrors.PageRangeRequiresMaterial;
        Result<StudySessionSelection> selectionResult = StudySessionSelection.Create(session, material, request.FromPage, request.ToPage);
        if (selectionResult.IsError) return selectionResult.TopError;
        string? derivedStorageReference = null;
        try
        {
            await using Stream original = await fileStore.OpenReadAsync(material.StorageReference, cancellationToken);
            await using Stream derived = await pdfProcessor.ExtractPagesAsync(original, StudySessionPageRange.Create(request.FromPage, request.ToPage).Value, cancellationToken);
            derivedStorageReference = await fileStore.SaveDerivedAsync(session.StudentId, material.FileName, derived, cancellationToken);
            Result<Success> derivedResult = selectionResult.Value.SetDerivedStorageReference(derivedStorageReference);
            if (derivedResult.IsError)
            {
                await fileStore.DeleteAsync(derivedStorageReference, cancellationToken);
                return derivedResult.TopError;
            }
        }
        catch
        {
            if (derivedStorageReference is not null)
            {
                await fileStore.DeleteAsync(derivedStorageReference, cancellationToken);
            }

            throw;
        }

        Result<Success> setSelectionResult = session.SetSelection(selectionResult.Value);
        if (setSelectionResult.IsError)
        {
            await fileStore.DeleteAsync(derivedStorageReference!, cancellationToken);
            return setSelectionResult.TopError;
        }

        selectionRepository.Add(selectionResult.Value);
        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch
        {
            await fileStore.DeleteAsync(derivedStorageReference!, cancellationToken);
            throw;
        }

        return selectionResult.Value.ToResponse();
    }

    public async Task<Result<StudySessionResponse>> Handle(ReceiveStudySessionSectionsCommand request, CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId, includeMaterial: true, includeSelection: true);
        if (sessionResult.IsError) return sessionResult.TopError;
        if (sessionResult.Value.Material is not { } material || sessionResult.Value.Selection is null) return StudySessionErrors.SectionsRequireMaterial;
        List<StudyMaterialSection> materialSections = [];
        foreach (StudyMaterialSectionRequest requestedSection in request.Request.Sections ?? [])
        {
            Result<StudyMaterialSection> sectionResult = StudyMaterialSection.Create(material.Id, requestedSection.Name, requestedSection.EstimatedDurationMinutes);
            if (sectionResult.IsError) return sectionResult.TopError;
            materialSections.Add(sectionResult.Value);
        }
        Result<Success> setSectionsResult = sessionResult.Value.SetSelectedSections(materialSections);
        if (setSectionsResult.IsError) return setSectionsResult.TopError;
        IEnumerable<StudySessionSelectedSection> oldSelectedSections = await selectedSectionRepository.GetAllAsync(
            section => section.StudySessionSelectionId == sessionResult.Value.Selection.Id);
        selectedSectionRepository.DeleteRange(oldSelectedSections);
        await sectionRepository.AddRangeAsync(materialSections);
        await selectedSectionRepository.AddRangeAsync(sessionResult.Value.Selection.SelectedSections);
        return await SaveSessionAsync(Result.Success, sessionResult.Value);
    }

    public async Task<Result<StudyMaterialResponse>> Handle(ChangeStudySessionMaterialCommand request, CancellationToken cancellationToken)
        => await UploadMaterialAsync(request.SessionId, request.FileName, request.FileSizeBytes, request.Content, request.Source, true, cancellationToken);

    public async Task<Result<Success>> Handle(StartStudySessionCommand request, CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId, includeMaterial: true);
        if (sessionResult.IsError) return sessionResult.TopError;
        if (sessionResult.Value.Status == StudySessionStatus.Draft)
        {
            Result<Success> readyResult = sessionResult.Value.MarkReady();
            if (readyResult.IsError) return readyResult.TopError;
        }
        return await SaveSessionAsync(sessionResult.Value.Start(timeProvider.GetUtcNow()));
    }

    public Task<Result<StudySessionResponse>> Handle(PauseStudySessionCommand request, CancellationToken cancellationToken)
        => HandleRuntimeAsync(request.SessionId, session => session.Pause(timeProvider.GetUtcNow()));

    public Task<Result<StudySessionResponse>> Handle(ResumeStudySessionCommand request, CancellationToken cancellationToken)
        => HandleRuntimeAsync(request.SessionId, session => session.Resume(timeProvider.GetUtcNow()));

    public Task<Result<StudySessionResponse>> Handle(EndStudySessionCommand request, CancellationToken cancellationToken)
        => HandleRuntimeAsync(request.SessionId, session => session.End(timeProvider.GetUtcNow()));

    public Task<Result<StudySessionResponse>> Handle(
        UpdateStudySessionProgressCommand request,
        CancellationToken cancellationToken)
        => HandleRuntimeAsync(
            request.SessionId,
            session => session.UpdateProgress(
                request.Request.CurrentPage,
                request.Request.CompletedSectionIds,
                timeProvider.GetUtcNow()));

    public async Task<Result<StudyMaterialResponse>> Handle(UploadStudyMaterialCommand request, CancellationToken cancellationToken)
        => await UploadMaterialAsync(request.SessionId, request.FileName, request.FileSizeBytes, request.Content, request.Source, false, cancellationToken);

    private async Task<Result<StudyMaterialResponse>> UploadMaterialAsync(Guid sessionId, string fileName, long fileSizeBytes, byte[] contentBytes, FocusLens.Contracts.StudySessions.StudyMaterialSource source, bool replaceExisting, CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(sessionId, includeMaterial: true, includeSelection: true);
        if (sessionResult.IsError) return sessionResult.TopError;
        if (contentBytes.Length == 0) return Error.Validation("StudyMaterials.EmptyFile", "The uploaded file is empty.");
        if (!Enum.IsDefined(source)) return Error.Validation("StudyMaterials.SourceInvalid", "Material source is invalid.");
        if (!replaceExisting && sessionResult.Value.Material is not null) return Error.Conflict("StudySessions.MaterialAlreadySet", "Use the material update endpoint to replace the study material.");
        if (replaceExisting && sessionResult.Value.Material is null) return StudySessionErrors.MaterialRequired;
        await using var content = new MemoryStream(contentBytes, writable: false);
        int pageCount;
        try { pageCount = await pdfProcessor.GetPageCountAsync(content, cancellationToken); }
        catch (Exception) { return Error.Validation("StudyMaterials.InvalidPdf", "The uploaded file is not a readable PDF."); }
        content.Position = 0;
        string storageReference = await fileStore.SaveOriginalAsync(sessionResult.Value.StudentId, fileName, content, cancellationToken);
        Result<StudyMaterial> materialResult = StudyMaterial.Create(sessionResult.Value.StudentId, fileName, fileSizeBytes, pageCount, storageReference, (DomainStudyMaterialSource)source);
        if (materialResult.IsError)
        {
            await fileStore.DeleteAsync(storageReference, cancellationToken);
            return materialResult.TopError;
        }

        StudyMaterial? oldMaterial = sessionResult.Value.Material;
        StudySessionSelection? oldSelection = sessionResult.Value.Selection;
        if (oldSelection is not null) selectionRepository.Delete(oldSelection);

        Result<Success> attachResult = sessionResult.Value.SetStudyMaterial(materialResult.Value);
        if (attachResult.IsError)
        {
            await fileStore.DeleteAsync(storageReference, cancellationToken);
            return attachResult.TopError;
        }

        materialRepository.Add(materialResult.Value);
        bool oldMaterialIsShared = oldMaterial is not null
            && studySessionRepository.GetAll().Any(session => session.Id != sessionId && session.StudyMaterialId == oldMaterial.Id);
        if (oldMaterial is not null && !oldMaterialIsShared) materialRepository.Delete(oldMaterial);

        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch
        {
            await fileStore.DeleteAsync(storageReference, cancellationToken);
            throw;
        }

        if (oldSelection?.DerivedStorageReference is { } derivedReference) await fileStore.DeleteAsync(derivedReference, cancellationToken);
        if (oldMaterial is not null && !oldMaterialIsShared) await fileStore.DeleteAsync(oldMaterial.StorageReference, cancellationToken);
        return materialResult.Value.ToResponse();
    }

    private async Task<Result<Student>> GetCurrentStudentAsync(bool includeSubjects = false)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty) return Error.Unauthorized("StudySessions.CurrentUserUnavailable", "The current user could not be identified.");
        Student? student = includeSubjects ? await studentRepository.FirstOrDefaultAsync(item => item.UserId == userId, item => item.Subjects) : await studentRepository.FirstOrDefaultAsync(item => item.UserId == userId);
        return student is null ? Error.NotFound("StudySessions.StudentNotFound", "The student profile was not found.") : student;
    }

    private async Task<Result<StudySession>> GetSessionAsync(Guid sessionId, Student? knownStudent = null, bool includeMaterial = false, bool includeSelection = false)
    {
        if (sessionId == Guid.Empty) return Error.Validation("StudySessions.IdRequired", "Study session id is required.");
        Result<Student> studentResult = knownStudent is null ? await GetCurrentStudentAsync() : knownStudent;
        if (studentResult.IsError) return studentResult.TopError;
        StudySession? session = includeMaterial && includeSelection
            ? await studySessionRepository.FirstOrDefaultAsync(item => item.Id == sessionId && item.StudentId == studentResult.Value.Id, item => item.Material!, item => item.Selection!, item => item.CompletedSections)
            : includeMaterial
                ? await studySessionRepository.FirstOrDefaultAsync(item => item.Id == sessionId && item.StudentId == studentResult.Value.Id, item => item.Material!, item => item.Selection!, item => item.CompletedSections)
                : await studySessionRepository.FirstOrDefaultAsync(item => item.Id == sessionId && item.StudentId == studentResult.Value.Id, item => item.Selection!, item => item.CompletedSections);
        return session is null ? Error.NotFound("StudySessions.NotFound", "The study session was not found.") : session;
    }

    private async Task<Result<StudySessionResponse>> SaveSessionAsync(Result<Success> result, StudySession session)
    {
        if (result.IsError) return result.TopError;
        await unitOfWork.SaveChangesAsync();
        return session.ToResponse(timeProvider.GetUtcNow());
    }

    private async Task<Result<StudySessionResponse>> HandleRuntimeAsync(
        Guid sessionId,
        Func<StudySession, Result<Success>> operation)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(sessionId, includeMaterial: true);
        if (sessionResult.IsError) return sessionResult.TopError;

        StudySession session = sessionResult.Value;
        bool completedNaturally = session.CompleteIfElapsed(timeProvider.GetUtcNow());
        Result<Success> result = operation(session);
        if (result.IsError)
        {
            if (completedNaturally)
            {
                await unitOfWork.SaveChangesAsync();
            }

            return result.TopError;
        }

        await unitOfWork.SaveChangesAsync();
        return session.ToResponse(timeProvider.GetUtcNow());
    }

    private async Task<Result<Success>> SaveSessionAsync(Result<Success> result)
    {
        if (result.IsError) return result.TopError;
        await unitOfWork.SaveChangesAsync();
        return Result.Success;
    }
}
