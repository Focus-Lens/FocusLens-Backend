using FocusLens.Contracts.StudySessions;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.Students;
using FocusLens.Domain.StudySessions;
using MediatR;
using Microsoft.Extensions.Options;
using ICurrentUser = FocusLens.Application.Common.Interfaces.ICurrentUser;
using DomainStudyMaterialSource = FocusLens.Domain.StudySessions.StudyMaterialSource;
using DomainStudySessionMode = FocusLens.Domain.StudySessions.StudySessionMode;
using StudyMaterialSource = FocusLens.Contracts.StudySessions.StudyMaterialSource;

namespace FocusLens.Application.StudySessions;

public sealed class StudySessionCommandHandler(
    IBaseRepository<Student> studentRepository,
    IBaseRepository<StudySession> studySessionRepository,
    IBaseRepository<StudyMaterial> materialRepository,
    IBaseRepository<StudySessionSelection> selectionRepository,
    IBaseRepository<StudySessionSelectedSection> selectedSectionRepository,
    IBaseRepository<StudyMaterialSection> materialSectionRepository,
    IBaseRepository<StudySessionImage> imageRepository,
    IStudyMaterialFileStore fileStore,
    IStudySessionImageFileStore imageFileStore,
    IStudyMaterialPdfProcessor pdfProcessor,
    ICurrentUser currentUser,
    IBaseRepository<StudySessionBehaviorAnalysisJob> behaviorAnalysisJobRepository,
    IUnitOfWork unitOfWork,
    IOptions<StudySessionImageUploadOptions> imageUploadOptions,
    TimeProvider timeProvider = null!)
    : IRequestHandler<CreateStudySessionCommand, Result<StudySessionResponse>>,
        IRequestHandler<SetStudySessionModeCommand, Result<Success>>,
        IRequestHandler<SetStudySessionSubjectCommand, Result<Success>>,
        IRequestHandler<SetStudySessionDurationCommand, Result<Success>>,
        IRequestHandler<SetStudySessionSelectionCommand, Result<StudySessionSelectionResponse>>,
        IRequestHandler<UpdateStudySessionSelectionCommand, Result<StudySessionSelectionResponse>>,
        IRequestHandler<ChangeStudySessionMaterialCommand, Result<StudyMaterialResponse>>,
        IRequestHandler<StartStudySessionCommand, Result<Success>>,
        IRequestHandler<UploadStudyMaterialCommand, Result<StudyMaterialResponse>>,
        IRequestHandler<UploadStudySessionImagesCommand, Result<StudySessionImagesUploadResponse>>,
        IRequestHandler<PauseStudySessionCommand, Result<StudySessionResponse>>,
        IRequestHandler<ResumeStudySessionCommand, Result<StudySessionResponse>>,
        IRequestHandler<EndStudySessionCommand, Result<StudySessionResponse>>,
        IRequestHandler<ReuseStudySessionCommand, Result<StudySessionResponse>>,
        IRequestHandler<UpdateStudySessionProgressCommand, Result<StudySessionResponse>>
{
    public async Task<Result<StudyMaterialResponse>> Handle(ChangeStudySessionMaterialCommand request,
        CancellationToken cancellationToken)
    {
        return await UploadMaterialAsync(request.SessionId, request.FileName, request.FileSizeBytes,
            request.Content, request.Source, true, cancellationToken);
    }

    public async Task<Result<StudySessionResponse>> Handle(CreateStudySessionCommand request,
        CancellationToken cancellationToken)
    {
        Result<Student> studentResult = await GetCurrentStudentAsync(true);
        if (studentResult.IsError)
        {
            return studentResult.TopError;
        }

        if (!Enum.IsDefined(request.Request.Mode))
        {
            return StudySessionErrors.InvalidMode;
        }

        Result<StudySession> sessionResult =
            StudySession.Create(studentResult.Value.Id, (DomainStudySessionMode)request.Request.Mode);
        if (sessionResult.IsError)
        {
            return sessionResult.TopError;
        }

        StudentSubject? subject =
            studentResult.Value.Subjects.SingleOrDefault(item => item.Id == request.Request.SubjectId);
        if (subject is null)
        {
            return Error.Validation("StudySessions.InvalidSubject",
                "The selected subject does not belong to the student.");
        }

        Result<Success> subjectResult = sessionResult.Value.SetSubject(subject);
        if (subjectResult.IsError)
        {
            return subjectResult.TopError;
        }

        Result<Success> durationResult = sessionResult.Value.SetDuration(request.Request.FocusDurationMinutes);
        if (durationResult.IsError)
        {
            return durationResult.TopError;
        }

        studySessionRepository.Add(sessionResult.Value);
        await unitOfWork.SaveChangesAsync();
        return sessionResult.Value.ToResponse(timeProvider.GetUtcNow());
    }

    public async Task<Result<StudySessionResponse>> Handle(
        EndStudySessionCommand request,
        CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult =
            await GetSessionAsync(
                request.SessionId,
                includeMaterial: true);

        if (sessionResult.IsError)
        {
            return sessionResult.TopError;
        }

        StudySession session = sessionResult.Value;
        DateTimeOffset now = timeProvider.GetUtcNow();

        Result<Success> endResult = session.End(now);

        if (endResult.IsError)
        {
            return endResult.TopError;
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

        await unitOfWork.SaveChangesAsync();

        return session.ToResponse(now);
    }

    public async Task<Result<StudySessionResponse>> Handle(
        PauseStudySessionCommand request,
        CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult =
            await GetSessionAsync(
                request.SessionId,
                includeSelection: true);

        if (sessionResult.IsError)
        {
            return sessionResult.TopError;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        Result<Success> pauseResult =
            sessionResult.Value.Pause(now);

        if (pauseResult.IsError)
        {
            return pauseResult.TopError;
        }

        return await SaveSessionAsync(
            pauseResult,
            sessionResult.Value);
    }


    public async Task<Result<StudySessionResponse>> Handle(
        ResumeStudySessionCommand request,
        CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult =
            await GetSessionAsync(
                request.SessionId,
                includeSelection: true);

        if (sessionResult.IsError)
        {
            return sessionResult.TopError;
        }

        StudySession session = sessionResult.Value;
        DateTimeOffset now = timeProvider.GetUtcNow();
        Result<Success> resumeResult =
            session.Resume(now);

        if (resumeResult.IsError)
        {
            return resumeResult.TopError;
        }

        return await SaveSessionAsync(
            resumeResult,
            session);
    }

    public async Task<Result<StudySessionResponse>> Handle(
        ReuseStudySessionCommand request,
        CancellationToken cancellationToken)
    {
        Result<Student> studentResult = await GetCurrentStudentAsync();
        if (studentResult.IsError)
        {
            return studentResult.TopError;
        }

        StudySession? source = await studySessionRepository.FirstOrDefaultAsync(
            item => item.Id == request.SessionId && item.StudentId == studentResult.Value.Id &&
                    (item.Status == StudySessionStatus.Paused ||
                     item.Status == StudySessionStatus.Completed ||
                     item.Status == StudySessionStatus.Cancelled),
            item => item.Material!, item => item.Selection!.SelectedSections);
        if (source?.Material is null || source.Selection is null || source.SelectedSubjectId is null ||
            source.FocusDurationMinutes is null)
        {
            return Error.NotFound("StudySessions.NotFound", "Study session was not found.");
        }

        Guid[] materialSectionIds = source.Selection.SelectedSections
            .OrderBy(item => item.Order)
            .Select(item => item.StudyMaterialSectionId)
            .ToArray();
        if (materialSectionIds.Length == 0)
        {
            return Error.Validation("StudySessionReuse.SectionsRequired",
                "A session with no selected sections cannot be reused.");
        }

        StudyMaterialSection[] materialSections =
            (await materialSectionRepository.GetAllAsync(item => materialSectionIds.Contains(item.Id)))
            .OrderBy(item => Array.IndexOf(materialSectionIds, item.Id))
            .ToArray();
        if (materialSections.Length != materialSectionIds.Length)
        {
            return Error.Validation("StudySessionReuse.SectionsUnavailable",
                "One or more selected study sections are no longer available.");
        }

        Result<StudySession> newSessionResult = StudySession.Create(source.StudentId, source.Mode);
        if (newSessionResult.IsError)
        {
            return newSessionResult.TopError;
        }

        StudySession reused = newSessionResult.Value;
        Result<Success> subjectResult = reused.SetSubjectId(source.SelectedSubjectId.Value);
        if (subjectResult.IsError)
        {
            return subjectResult.TopError;
        }

        Result<Success> durationResult = reused.SetDuration(source.FocusDurationMinutes.Value);
        if (durationResult.IsError)
        {
            return durationResult.TopError;
        }

        Result<Success> materialResult = reused.SetStudyMaterial(source.Material);
        if (materialResult.IsError)
        {
            return materialResult.TopError;
        }

        Result<StudySessionSelection> selectionResult = StudySessionSelection.Create(
            reused, source.Material, source.Selection.FromPage, source.Selection.ToPage);
        if (selectionResult.IsError)
        {
            return selectionResult.TopError;
        }

        StudySessionSelection selection = selectionResult.Value;
        if (source.Selection.DerivedStorageReference is not null)
        {
            Result<Success> derivedResult =
                selection.SetDerivedStorageReference(source.Selection.DerivedStorageReference);
            if (derivedResult.IsError)
            {
                return derivedResult.TopError;
            }
        }

        if (source.Selection.AiExtractedText is not null && source.Selection.AiAnalysisJson is not null)
        {
            Result<Success> analysisResult = selection.SetAiAnalysis(
                source.Selection.AiExtractedText, source.Selection.AiAnalysisJson);
            if (analysisResult.IsError)
            {
                return analysisResult.TopError;
            }
        }

        Result<Success> setSelection = reused.SetSelection(selection);
        if (setSelection.IsError)
        {
            return setSelection.TopError;
        }

        Result<Success> setSections = reused.SetSelectedSections(materialSections);
        if (setSections.IsError)
        {
            return setSections.TopError;
        }

        studySessionRepository.Add(reused);
        selectionRepository.Add(selection);
        await unitOfWork.SaveChangesAsync();
        return reused.ToResponse(timeProvider.GetUtcNow());
    }

    public async Task<Result<Success>> Handle(SetStudySessionDurationCommand request,
        CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId);
        return sessionResult.IsError
            ? sessionResult.TopError
            : await SaveSessionAsync(sessionResult.Value.SetDuration(request.Request.FocusDurationMinutes));
    }

    public async Task<Result<Success>> Handle(SetStudySessionModeCommand request,
        CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId);
        if (sessionResult.IsError)
        {
            return sessionResult.TopError;
        }

        if (!Enum.IsDefined(request.Request.Mode))
        {
            return StudySessionErrors.InvalidMode;
        }

        return await SaveSessionAsync(sessionResult.Value.SetMode((DomainStudySessionMode)request.Request.Mode));
    }

    public async Task<Result<StudySessionSelectionResponse>> Handle(SetStudySessionSelectionCommand request,
        CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId, includeMaterial: true);
        if (sessionResult.IsError)
        {
            return sessionResult.TopError;
        }

        StudySessionSelection? existing =
            await selectionRepository.FirstOrDefaultAsync(selection =>
                selection.StudySessionId == request.SessionId);
        if (existing is not null)
        {
            return Error.Conflict("StudySessions.SelectionAlreadySet",
                "A selection has already been set for this study session.");
        }

        return await CreateSelectionAsync(sessionResult.Value, request.Request, cancellationToken);
    }

    public async Task<Result<Success>> Handle(SetStudySessionSubjectCommand request,
        CancellationToken cancellationToken)
    {
        Result<Student> studentResult = await GetCurrentStudentAsync(true);
        if (studentResult.IsError)
        {
            return studentResult.TopError;
        }

        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId, studentResult.Value);
        if (sessionResult.IsError)
        {
            return sessionResult.TopError;
        }

        StudentSubject? subject =
            studentResult.Value.Subjects.SingleOrDefault(item => item.Id == request.Request.SubjectId);
        if (subject is null)
        {
            return Error.Validation("StudySessions.InvalidSubject",
                "The selected subject does not belong to the student.");
        }

        return await SaveSessionAsync(sessionResult.Value.SetSubject(subject));
    }

    public async Task<Result<Success>> Handle(
        StartStudySessionCommand request,
        CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult =
            await GetSessionAsync(
                request.SessionId,
                includeMaterial: true,
                includeSelection: true);

        if (sessionResult.IsError)
        {
            return sessionResult.TopError;
        }

        StudySession session = sessionResult.Value;

        if (session.Selection is null)
        {
            return StudySessionErrors.SectionsRequireMaterial;
        }

        List<StudySessionSelectedSection> selectedSections =
            (await selectedSectionRepository.GetAllAsync(section =>
                section.StudySessionSelectionId == session.Selection.Id))
            .OrderBy(section => section.Order)
            .ToList();

        if (selectedSections.Count == 0)
        {
            return Error.Validation(
                "StudySessionSections.NoneSelected",
                "At least one study section must be selected before starting the session.");
        }

        if (session.Status == StudySessionStatus.Draft)
        {
            Result<Success> readyResult = session.MarkReady();

            if (readyResult.IsError)
            {
                return readyResult.TopError;
            }
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        Result<Success> startResult = session.Start(now);

        if (startResult.IsError)
        {
            return startResult;
        }

        Result<Success> sectionStartResult =
            selectedSections[0].Start(now);

        if (sectionStartResult.IsError)
        {
            return sectionStartResult;
        }

        return await SaveSessionAsync(Result.Success);
    }

    public async Task<Result<StudySessionResponse>> Handle(
        UpdateStudySessionProgressCommand request,
        CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(
            request.SessionId,
            includeMaterial: true,
            includeSelection: true);
        if (sessionResult.IsError)
        {
            return sessionResult.TopError;
        }

        StudySession session = sessionResult.Value;
        DateTimeOffset now = timeProvider.GetUtcNow();
        StudySessionSelectedSection? currentSection = null;
        StudyMaterialSection? activeMaterialSection = null;

        if (session.Status == StudySessionStatus.Active && session.Selection is not null)
        {
            currentSection = await selectedSectionRepository.FirstOrDefaultAsync(section =>
                section.StudySessionSelectionId == session.Selection.Id &&
                section.StartedAtUtc != null &&
                section.CompletedAtUtc == null);

            if (currentSection is not null)
            {
                activeMaterialSection = await materialSectionRepository.FirstOrDefaultAsync(section =>
                    section.Id == currentSection.StudyMaterialSectionId);

                if (activeMaterialSection is null)
                {
                    return Error.Validation(
                        "StudySessionQuestions.SectionUnavailable",
                        "The active material section could not be found.");
                }

                if (currentSection.IsChallengeAvailableAt(now) &&
                    request.Request.CurrentPage is int requestedPage &&
                    requestedPage > activeMaterialSection.ToPage &&
                    requestedPage != session.CurrentPage)
                {
                    return Error.Conflict(
                        "StudySessionQuestions.ChallengeMustBeResolved",
                        "Resolve the current section challenge before continuing.");
                }
            }
        }

        Result<Success> progressResult = session.UpdateProgress(request.Request.CurrentPage, now);
        if (progressResult.IsError)
        {
            return progressResult.TopError;
        }

        if (session.Status == StudySessionStatus.Active &&
            request.Request.CurrentPage is int currentPage &&
            session.Selection is not null)
        {
            if (currentSection is not null && !currentSection.IsChallengeAvailable)
            {
                if (currentPage > activeMaterialSection!.ToPage ||
                    (currentPage == activeMaterialSection.ToPage &&
                     currentPage == session.Selection.ToPage))
                {
                    Result<Success> challengeResult = currentSection.MakeChallengeAvailable(now);
                    if (challengeResult.IsError)
                    {
                        return challengeResult.TopError;
                    }
                }
            }
        }

        await unitOfWork.SaveChangesAsync();
        return session.ToResponse(now);
    }

    public async Task<Result<StudySessionSelectionResponse>> Handle(UpdateStudySessionSelectionCommand request,
        CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId, includeMaterial: true);
        if (sessionResult.IsError)
        {
            return sessionResult.TopError;
        }

        StudySessionSelection? existing = await selectionRepository.FirstOrDefaultAsync(
            selection => selection.StudySessionId == request.SessionId, selection => selection.SelectedSections);
        if (existing is null)
        {
            return Error.Validation("StudySessions.SelectionNotSet", "Set a selection before updating it.");
        }

        if (existing.StudyMaterialId != sessionResult.Value.StudyMaterialId)
        {
            return StudySessionErrors.SelectionMaterialMismatch;
        }

        string? oldDerivedStorageReference = existing.DerivedStorageReference;
        selectionRepository.Delete(existing);
        Result<StudySessionSelectionResponse> result =
            await CreateSelectionAsync(sessionResult.Value, request.Request, cancellationToken);
        if (result.IsSuccess && oldDerivedStorageReference is not null)
        {
            await fileStore.DeleteAsync(oldDerivedStorageReference, cancellationToken);
        }

        return result;
    }

    public async Task<Result<StudyMaterialResponse>> Handle(UploadStudyMaterialCommand request,
        CancellationToken cancellationToken)
    {
        return await UploadMaterialAsync(request.SessionId, request.FileName, request.FileSizeBytes,
            request.Content, request.Source, false, cancellationToken);
    }

    public async Task<Result<StudySessionImagesUploadResponse>> Handle(UploadStudySessionImagesCommand request,
        CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId);
        if (sessionResult.IsError)
        {
            return sessionResult.TopError;
        }

        if (request.Files.Count == 0)
        {
            return Error.Validation("StudySessionImages.FilesRequired", "At least one image file is required.");
        }

        StudySessionImageUploadOptions options = imageUploadOptions.Value;
        if (request.Files.Count > options.MaxImagesPerRequest)
        {
            return Error.Validation("StudySessionImages.TooManyFiles",
                $"Upload no more than {options.MaxImagesPerRequest} images in one request.");
        }

        List<PendingStudySessionImage> pendingImages = [];
        List<string> createdStorageReferences = [];
        try
        {
            foreach (StudySessionImageUploadFile file in request.Files)
            {
                Result<ImageFormat> formatResult = await ValidateImageAsync(file, options, cancellationToken);
                if (formatResult.IsError)
                {
                    await DeleteCreatedImagesAsync(createdStorageReferences);
                    return formatResult.TopError;
                }

                await using Stream content = await file.OpenReadAsync(cancellationToken);
                string storageReference = await imageFileStore.SaveAsync(
                    sessionResult.Value.StudentId,
                    formatResult.Value.Extension,
                    content,
                    cancellationToken);
                createdStorageReferences.Add(storageReference);
                pendingImages.Add(new PendingStudySessionImage(file, formatResult.Value, storageReference));
            }

            List<StudySessionImage> images = [];
            foreach (PendingStudySessionImage pendingImage in pendingImages)
            {
                Result<StudySessionImage> imageResult = StudySessionImage.Create(
                    sessionResult.Value.Id,
                    pendingImage.File.FileName,
                    pendingImage.Format.ContentType,
                    pendingImage.File.FileSizeBytes,
                    pendingImage.StorageReference);
                if (imageResult.IsError)
                {
                    await DeleteCreatedImagesAsync(createdStorageReferences);
                    return imageResult.TopError;
                }

                images.Add(imageResult.Value);
            }

            await imageRepository.AddRangeAsync(images);
            await unitOfWork.SaveChangesAsync();
            return new StudySessionImagesUploadResponse(images.Select(image => image.ToResponse()).ToArray());
        }
        catch
        {
            await DeleteCreatedImagesAsync(createdStorageReferences);
            throw;
        }
    }

    private async Task<Result<StudySessionSelectionResponse>> CreateSelectionAsync(StudySession session,
        SetStudySessionSelectionRequest request, CancellationToken cancellationToken)
    {
        if (session.Material is not { } material)
        {
            return StudySessionErrors.PageRangeRequiresMaterial;
        }

        Result<StudySessionSelection> selectionResult =
            StudySessionSelection.Create(session, material, request.FromPage, request.ToPage);
        if (selectionResult.IsError)
        {
            return selectionResult.TopError;
        }

        string? derivedStorageReference = null;
        try
        {
            await using Stream original =
                await fileStore.OpenReadAsync(material.StorageReference, cancellationToken);
            await using Stream derived = await pdfProcessor.ExtractPagesAsync(original,
                StudySessionPageRange.Create(request.FromPage, request.ToPage).Value, cancellationToken);
            derivedStorageReference =
                await fileStore.SaveDerivedAsync(session.StudentId, material.FileName, derived, cancellationToken);
            Result<Success> derivedResult =
                selectionResult.Value.SetDerivedStorageReference(derivedStorageReference);
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

    private async Task<Result<StudyMaterialResponse>> UploadMaterialAsync(Guid sessionId, string fileName,
        long fileSizeBytes, byte[] contentBytes, StudyMaterialSource source, bool replaceExisting,
        CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult =
            await GetSessionAsync(sessionId, includeMaterial: true, includeSelection: true);
        if (sessionResult.IsError)
        {
            return sessionResult.TopError;
        }

        if (contentBytes.Length == 0)
        {
            return Error.Validation("StudyMaterials.EmptyFile", "The uploaded file is empty.");
        }

        if (!Enum.IsDefined(source))
        {
            return Error.Validation("StudyMaterials.SourceInvalid", "Material source is invalid.");
        }

        if (!replaceExisting && sessionResult.Value.Material is not null)
        {
            return Error.Conflict("StudySessions.MaterialAlreadySet",
                "Use the material update endpoint to replace the study material.");
        }

        if (replaceExisting && sessionResult.Value.Material is null)
        {
            return StudySessionErrors.MaterialRequired;
        }

        await using MemoryStream content = new(contentBytes, false);
        int pageCount;
        try { pageCount = await pdfProcessor.GetPageCountAsync(content, cancellationToken); }
        catch (Exception)
        {
            return Error.Validation("StudyMaterials.InvalidPdf", "The uploaded file is not a readable PDF.");
        }

        content.Position = 0;
        string storageReference =
            await fileStore.SaveOriginalAsync(sessionResult.Value.StudentId, fileName, content, cancellationToken);
        Result<StudyMaterial> materialResult = StudyMaterial.Create(sessionResult.Value.StudentId, fileName,
            fileSizeBytes, pageCount, storageReference, (DomainStudyMaterialSource)source);
        if (materialResult.IsError)
        {
            await fileStore.DeleteAsync(storageReference, cancellationToken);
            return materialResult.TopError;
        }

        StudyMaterial? oldMaterial = sessionResult.Value.Material;
        StudySessionSelection? oldSelection = sessionResult.Value.Selection;
        if (oldSelection is not null)
        {
            selectionRepository.Delete(oldSelection);
        }

        Result<Success> attachResult = sessionResult.Value.SetStudyMaterial(materialResult.Value);
        if (attachResult.IsError)
        {
            await fileStore.DeleteAsync(storageReference, cancellationToken);
            return attachResult.TopError;
        }

        materialRepository.Add(materialResult.Value);
        bool oldMaterialIsShared = oldMaterial is not null
                                   && await studySessionRepository.AnyAsync(session =>
                                       session.Id != sessionId && session.StudyMaterialId == oldMaterial.Id);
        if (oldMaterial is not null && !oldMaterialIsShared)
        {
            materialRepository.Delete(oldMaterial);
        }

        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch
        {
            await fileStore.DeleteAsync(storageReference, cancellationToken);
            throw;
        }

        if (oldSelection?.DerivedStorageReference is { } derivedReference)
        {
            await fileStore.DeleteAsync(derivedReference, cancellationToken);
        }

        if (oldMaterial is not null && !oldMaterialIsShared)
        {
            await fileStore.DeleteAsync(oldMaterial.StorageReference, cancellationToken);
        }

        return materialResult.Value.ToResponse();
    }

    private async Task<Result<Student>> GetCurrentStudentAsync(bool includeSubjects = false)
    {
        if (currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Error.Unauthorized("StudySessions.CurrentUserUnavailable",
                "The current user could not be identified.");
        }

        Student? student = includeSubjects
            ? await studentRepository.FirstOrDefaultAsync(item => item.UserId == userId, item => item.Subjects)
            : await studentRepository.FirstOrDefaultAsync(item => item.UserId == userId);
        return student is null
            ? Error.NotFound("StudySessions.StudentNotFound", "The student profile was not found.")
            : student;
    }

    private async Task<Result<StudySession>> GetSessionAsync(Guid sessionId, Student? knownStudent = null,
        bool includeMaterial = false, bool includeSelection = false)
    {
        if (sessionId == Guid.Empty)
        {
            return Error.Validation("StudySessions.IdRequired", "Study session id is required.");
        }

        Result<Student> studentResult = knownStudent is null ? await GetCurrentStudentAsync() : knownStudent;
        if (studentResult.IsError)
        {
            return studentResult.TopError;
        }

        StudySession? session = includeMaterial && includeSelection
            ? await studySessionRepository.FirstOrDefaultAsync(
                item => item.Id == sessionId && item.StudentId == studentResult.Value.Id, item => item.Material!,
                item => item.Selection!.SelectedSections, item => item.CompletedSections)
            : includeMaterial
                ? await studySessionRepository.FirstOrDefaultAsync(
                    item => item.Id == sessionId && item.StudentId == studentResult.Value.Id,
                    item => item.Material!, item => item.Selection!.SelectedSections, item => item.CompletedSections)
                : await studySessionRepository.FirstOrDefaultAsync(
                    item => item.Id == sessionId && item.StudentId == studentResult.Value.Id,
                    item => item.Selection!.SelectedSections, item => item.CompletedSections);
        return session is null
            ? Error.NotFound("StudySessions.NotFound", "The study session was not found.")
            : session;
    }

    private async Task<Result<StudySessionResponse>> SaveSessionAsync(Result<Success> result, StudySession session)
    {
        if (result.IsError)
        {
            return result.TopError;
        }

        await unitOfWork.SaveChangesAsync();
        return session.ToResponse(timeProvider.GetUtcNow());
    }

    private async Task<Result<StudySessionResponse>> HandleRuntimeAsync(
        Guid sessionId,
        Func<StudySession, Result<Success>> operation)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(sessionId, includeMaterial: true);
        if (sessionResult.IsError)
        {
            return sessionResult.TopError;
        }

        StudySession session = sessionResult.Value;
        Result<Success> result = operation(session);
        if (result.IsError)
        {
            return result.TopError;
        }

        await unitOfWork.SaveChangesAsync();
        return session.ToResponse(timeProvider.GetUtcNow());
    }

    private async Task<Result<Success>> SaveSessionAsync(Result<Success> result)
    {
        if (result.IsError)
        {
            return result.TopError;
        }

        await unitOfWork.SaveChangesAsync();
        return Result.Success;
    }

    private static async Task<Result<ImageFormat>> ValidateImageAsync(
        StudySessionImageUploadFile file,
        StudySessionImageUploadOptions options,
        CancellationToken cancellationToken)
    {
        if (file.FileSizeBytes <= 0)
        {
            return Error.Validation("StudySessionImages.EmptyFile", "The uploaded image is empty.");
        }

        if (file.FileSizeBytes > options.MaxImageSizeBytes)
        {
            return Error.Validation("StudySessionImages.FileTooLarge",
                $"Each image must be {options.MaxImageSizeBytes} bytes or smaller.");
        }

        await using Stream content = await file.OpenReadAsync(cancellationToken);
        byte[] header = new byte[12];
        int bytesRead = await content.ReadAsync(header, cancellationToken);
        if (bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return ImageFormat.Jpeg;
        }

        if (bytesRead >= 8
            && header[0] == 0x89
            && header[1] == 0x50
            && header[2] == 0x4E
            && header[3] == 0x47
            && header[4] == 0x0D
            && header[5] == 0x0A
            && header[6] == 0x1A
            && header[7] == 0x0A)
        {
            return ImageFormat.Png;
        }

        if (bytesRead >= 12
            && header[0] == 0x52
            && header[1] == 0x49
            && header[2] == 0x46
            && header[3] == 0x46
            && header[8] == 0x57
            && header[9] == 0x45
            && header[10] == 0x42
            && header[11] == 0x50)
        {
            return ImageFormat.WebP;
        }

        return Error.Validation("StudySessionImages.UnsupportedFormat",
            "Only JPEG, PNG, and WebP images are supported.");
    }

    private async Task DeleteCreatedImagesAsync(IEnumerable<string> storageReferences)
    {
        foreach (string storageReference in storageReferences)
        {
            await imageFileStore.DeleteAsync(storageReference, CancellationToken.None);
        }
    }

    private sealed record PendingStudySessionImage(
        StudySessionImageUploadFile File,
        ImageFormat Format,
        string StorageReference);

    private sealed record ImageFormat(string ContentType, string Extension)
    {
        public static readonly ImageFormat Jpeg = new("image/jpeg", ".jpg");

        public static readonly ImageFormat Png = new("image/png", ".png");

        public static readonly ImageFormat WebP = new("image/webp", ".webp");
    }
}