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
    IBaseRepository<StudyMaterialSection> sectionRepository,
    IBaseRepository<StudySessionSelectedSection> selectedSectionRepository,
    IBaseRepository<StudySessionImage> imageRepository,
    IStudyMaterialFileStore fileStore,
    IStudySessionImageFileStore imageFileStore,
    IStudyMaterialPdfProcessor pdfProcessor,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IOptions<StudySessionImageUploadOptions> imageUploadOptions,
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
        IRequestHandler<UploadStudySessionImagesCommand, Result<StudySessionImagesUploadResponse>>,
        IRequestHandler<PauseStudySessionCommand, Result<StudySessionResponse>>,
        IRequestHandler<ResumeStudySessionCommand, Result<StudySessionResponse>>,
        IRequestHandler<EndStudySessionCommand, Result<StudySessionResponse>>,
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

    public Task<Result<StudySessionResponse>> Handle(EndStudySessionCommand request,
        CancellationToken cancellationToken) =>
        HandleRuntimeAsync(request.SessionId, session => session.End(timeProvider.GetUtcNow()));

    public Task<Result<StudySessionResponse>> Handle(PauseStudySessionCommand request,
        CancellationToken cancellationToken) =>
        HandleRuntimeAsync(request.SessionId, session => session.Pause(timeProvider.GetUtcNow()));

    public async Task<Result<StudySessionResponse>> Handle(ReceiveStudySessionSectionsCommand request,
        CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult =
            await GetSessionAsync(request.SessionId, includeMaterial: true, includeSelection: true);
        if (sessionResult.IsError)
        {
            return sessionResult.TopError;
        }

        if (sessionResult.Value.Material is not { } material || sessionResult.Value.Selection is null)
        {
            return StudySessionErrors.SectionsRequireMaterial;
        }

        List<StudyMaterialSection> materialSections = [];
        foreach (StudyMaterialSectionRequest requestedSection in request.Request.Sections ?? [])
        {
            Result<StudyMaterialSection> sectionResult = StudyMaterialSection.Create(material.Id,
                requestedSection.Name, requestedSection.EstimatedDurationMinutes);
            if (sectionResult.IsError)
            {
                return sectionResult.TopError;
            }

            materialSections.Add(sectionResult.Value);
        }

        Result<Success> setSectionsResult = sessionResult.Value.SetSelectedSections(materialSections);
        if (setSectionsResult.IsError)
        {
            return setSectionsResult.TopError;
        }

        IEnumerable<StudySessionSelectedSection> oldSelectedSections =
            await selectedSectionRepository.GetAllAsync(section =>
                section.StudySessionSelectionId == sessionResult.Value.Selection.Id);
        selectedSectionRepository.DeleteRange(oldSelectedSections);
        await sectionRepository.AddRangeAsync(materialSections);
        await selectedSectionRepository.AddRangeAsync(sessionResult.Value.Selection.SelectedSections);
        return await SaveSessionAsync(Result.Success, sessionResult.Value);
    }

    public Task<Result<StudySessionResponse>> Handle(ResumeStudySessionCommand request,
        CancellationToken cancellationToken) =>
        HandleRuntimeAsync(request.SessionId, session => session.Resume(timeProvider.GetUtcNow()));

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

    public async Task<Result<Success>> Handle(StartStudySessionCommand request, CancellationToken cancellationToken)
    {
        Result<StudySession> sessionResult = await GetSessionAsync(request.SessionId, includeMaterial: true);
        if (sessionResult.IsError)
        {
            return sessionResult.TopError;
        }

        if (sessionResult.Value.Status == StudySessionStatus.Draft)
        {
            Result<Success> readyResult = sessionResult.Value.MarkReady();
            if (readyResult.IsError)
            {
                return readyResult.TopError;
            }
        }

        return await SaveSessionAsync(sessionResult.Value.Start(timeProvider.GetUtcNow()));
    }

    public Task<Result<StudySessionResponse>> Handle(
        UpdateStudySessionProgressCommand request,
        CancellationToken cancellationToken)
    {
        return HandleRuntimeAsync(
            request.SessionId,
            session => session.UpdateProgress(
                request.Request.CurrentPage,
                request.Request.CompletedSectionIds,
                timeProvider.GetUtcNow()));
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
                                   && studySessionRepository.GetAll().Any(session =>
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
                item => item.Selection!, item => item.CompletedSections)
            : includeMaterial
                ? await studySessionRepository.FirstOrDefaultAsync(
                    item => item.Id == sessionId && item.StudentId == studentResult.Value.Id,
                    item => item.Material!, item => item.Selection!, item => item.CompletedSections)
                : await studySessionRepository.FirstOrDefaultAsync(
                    item => item.Id == sessionId && item.StudentId == studentResult.Value.Id,
                    item => item.Selection!, item => item.CompletedSections);
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
