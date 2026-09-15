using System.Linq.Expressions;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.StudySessions;
using FocusLens.Contracts.StudySessions;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;
using FocusLens.Domain.StudySessions;
using Microsoft.Extensions.Options;
using ContractStudyMaterialSource = FocusLens.Contracts.StudySessions.StudyMaterialSource;
using DomainStudyMaterialSource = FocusLens.Domain.StudySessions.StudyMaterialSource;
using StudySessionMode = FocusLens.Domain.StudySessions.StudySessionMode;

namespace FocusLens.Application.UnitTests.StudySessions;

public sealed class StudySessionMaterialReplacementTests
{
    [Fact]
    public async Task ReplaceMaterial_ReplacesMaterialAndSelectionInOneSave_ThenCleansRetiredFiles()
    {
        Fixture fixture = Fixture.Create(true);

        Result<StudyMaterialResponse> result = await fixture.Handler.Handle(
            new ChangeStudySessionMaterialCommand(fixture.Session.Id, "replacement.pdf", 12, [1, 2, 3],
                ContractStudyMaterialSource.Upload),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCalls);
        Assert.Equal(result.Value.Id, fixture.Session.StudyMaterialId);
        Assert.Null(await fixture.Selections.FirstOrDefaultAsync(item => item.Id == fixture.OldSelection!.Id));
        Assert.Null(await fixture.Materials.FirstOrDefaultAsync(item => item.Id == fixture.OldMaterial.Id));
        Assert.Contains(fixture.OldMaterial.StorageReference, fixture.FileStore.DeletedReferences);
        Assert.Contains(fixture.OldSelection!.DerivedStorageReference!, fixture.FileStore.DeletedReferences);
        Assert.DoesNotContain(result.Value.StorageReference, fixture.FileStore.DeletedReferences);
    }

    [Fact]
    public async Task ReplaceMaterial_WhenPersistenceFails_DeletesNewFileAndLeavesOldFilesUntouched()
    {
        Fixture fixture = Fixture.Create(true, true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Handler.Handle(
            new ChangeStudySessionMaterialCommand(fixture.Session.Id, "replacement.pdf", 12, [1, 2, 3],
                ContractStudyMaterialSource.Upload),
            CancellationToken.None));

        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCalls);
        Assert.Contains("original/new.pdf", fixture.FileStore.DeletedReferences);
        Assert.DoesNotContain(fixture.OldMaterial.StorageReference, fixture.FileStore.DeletedReferences);
        Assert.DoesNotContain(fixture.OldSelection!.DerivedStorageReference!, fixture.FileStore.DeletedReferences);
    }

    [Fact]
    public async Task UploadImages_WithMultipleValidImages_PersistsMetadataAndReturnsResponse()
    {
        Fixture fixture = Fixture.Create(false);

        Result<StudySessionImagesUploadResponse> result = await fixture.Handler.Handle(
            new UploadStudySessionImagesCommand(fixture.Session.Id,
            [
                UploadFile("front.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
                UploadFile("back.jpg", [0xFF, 0xD8, 0xFF, 0xE0])
            ]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Images.Count);
        Assert.Equal(2, fixture.Images.GetAll().Count());
        Assert.All(result.Value.Images, image => Assert.Equal(fixture.Session.Id, image.StudySessionId));
        Assert.Contains(result.Value.Images, image => image.ContentType == "image/png");
        Assert.Contains(result.Value.Images, image => image.ContentType == "image/jpeg");
    }

    [Fact]
    public async Task UploadImages_WhenSecondFileHasInvalidContent_DeletesPreviouslySavedImage()
    {
        Fixture fixture = Fixture.Create(false);

        Result<StudySessionImagesUploadResponse> result = await fixture.Handler.Handle(
            new UploadStudySessionImagesCommand(fixture.Session.Id,
            [
                UploadFile("front.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
                UploadFile("notes.txt", [0x25, 0x50, 0x44, 0x46])
            ]),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("StudySessionImages.UnsupportedFormat", result.TopError.Code);
        Assert.Equal(["images/1.png"], fixture.ImageFileStore.DeletedReferences);
        Assert.Empty(fixture.Images.GetAll());
    }

    [Fact]
    public async Task UploadImages_WhenPersistenceFails_DeletesImagesCreatedByRequest()
    {
        Fixture fixture = Fixture.Create(false, true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Handler.Handle(
            new UploadStudySessionImagesCommand(fixture.Session.Id,
            [
                UploadFile("front.webp", [0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50])
            ]),
            CancellationToken.None));

        Assert.Equal(["images/1.webp"], fixture.ImageFileStore.DeletedReferences);
    }

    [Fact]
    public async Task UploadImages_WhenRequestIsCanceledAfterImageIsStored_DeletesImagesCreatedByRequest()
    {
        Fixture fixture = Fixture.Create(false);
        using CancellationTokenSource cancellation = new();
        fixture.ImageFileStore.CancelAfterSaveCount = 1;
        fixture.ImageFileStore.Cancellation = cancellation;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Handler.Handle(
            new UploadStudySessionImagesCommand(fixture.Session.Id,
            [
                UploadFile("front.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
                UploadFile("back.jpg", [0xFF, 0xD8, 0xFF, 0xE0])
            ]),
            cancellation.Token));

        Assert.Equal(["images/1.png"], fixture.ImageFileStore.SavedReferences);
        Assert.Equal(["images/1.png"], fixture.ImageFileStore.DeletedReferences);
        Assert.Empty(fixture.Images.GetAll());
    }

    [Fact]
    public async Task UploadImages_WhenSessionBelongsToAnotherStudent_ReturnsNotFound()
    {
        Fixture fixture = Fixture.Create(false, currentUserOwnsSession: false);

        Result<StudySessionImagesUploadResponse> result = await fixture.Handler.Handle(
            new UploadStudySessionImagesCommand(fixture.Session.Id,
            [
                UploadFile("front.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])
            ]),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("StudySessions.NotFound", result.TopError.Code);
        Assert.Empty(fixture.ImageFileStore.SavedReferences);
    }

    [Fact]
    public async Task UploadImages_WhenSessionDoesNotExist_ReturnsNotFound()
    {
        Fixture fixture = Fixture.Create(false);

        Result<StudySessionImagesUploadResponse> result = await fixture.Handler.Handle(
            new UploadStudySessionImagesCommand(Guid.NewGuid(),
            [
                UploadFile("front.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])
            ]),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("StudySessions.NotFound", result.TopError.Code);
        Assert.Empty(fixture.ImageFileStore.SavedReferences);
    }

    [Fact]
    public async Task UploadImages_WhenCollectionIsEmpty_ReturnsValidationError()
    {
        Fixture fixture = Fixture.Create(false);

        Result<StudySessionImagesUploadResponse> result = await fixture.Handler.Handle(
            new UploadStudySessionImagesCommand(fixture.Session.Id, []),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("StudySessionImages.FilesRequired", result.TopError.Code);
    }

    [Fact]
    public async Task UploadImages_WhenIndividualFileIsEmpty_ReturnsValidationError()
    {
        Fixture fixture = Fixture.Create(false);

        Result<StudySessionImagesUploadResponse> result = await fixture.Handler.Handle(
            new UploadStudySessionImagesCommand(fixture.Session.Id, [UploadFile("empty.png", [])]),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("StudySessionImages.EmptyFile", result.TopError.Code);
    }

    [Fact]
    public async Task UploadImages_WhenTooManyFiles_ReturnsValidationError()
    {
        Fixture fixture = Fixture.Create(false);

        Result<StudySessionImagesUploadResponse> result = await fixture.Handler.Handle(
            new UploadStudySessionImagesCommand(fixture.Session.Id,
                Enumerable.Range(0, 11)
                    .Select(index => UploadFile($"{index}.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
                    .ToArray()),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("StudySessionImages.TooManyFiles", result.TopError.Code);
        Assert.Empty(fixture.ImageFileStore.SavedReferences);
    }

    private static StudySessionImageUploadFile UploadFile(string fileName, byte[] content) =>
        new(fileName, "application/octet-stream", content.Length,
            _ => Task.FromResult<Stream>(new MemoryStream(content)));

    private sealed record Fixture(
        StudySessionCommandHandler Handler,
        StudySession Session,
        StudyMaterial OldMaterial,
        StudySessionSelection? OldSelection,
        InMemoryRepository<StudyMaterial> Materials,
        InMemoryRepository<StudySessionSelection> Selections,
        InMemoryRepository<StudySessionImage> Images,
        RecordingFileStore FileStore,
        RecordingImageFileStore ImageFileStore,
        RecordingUnitOfWork UnitOfWork)
    {
        public static Fixture Create(bool withSelection, bool failSave = false, bool currentUserOwnsSession = true)
        {
            Guid userId = Guid.NewGuid();
            Student student = new(userId);
            Guid currentUserId = currentUserOwnsSession ? userId : Guid.NewGuid();
            Student currentStudent = currentUserOwnsSession ? student : new Student(currentUserId);
            StudySession session = StudySession.Create(student.Id, StudySessionMode.Digital).Value;
            StudyMaterial material = StudyMaterial.Create(student.Id, "old.pdf", 10, 5, "original/old.pdf",
                DomainStudyMaterialSource.Upload).Value;
            session.SetStudyMaterial(material);
            StudySessionSelection? selection = null;
            if (withSelection)
            {
                selection = StudySessionSelection.Create(session, material, 1, 2).Value;
                selection.SetDerivedStorageReference("derived/old.pdf");
                session.SetSelection(selection);
            }

            InMemoryRepository<StudyMaterial> materials = new(material);
            InMemoryRepository<StudySessionSelection> selections = selection is null
                ? new InMemoryRepository<StudySessionSelection>()
                : new InMemoryRepository<StudySessionSelection>(selection);
            InMemoryRepository<StudySessionImage> images = new();
            RecordingFileStore fileStore = new();
            RecordingImageFileStore imageFileStore = new();
            RecordingUnitOfWork unitOfWork = new(failSave);
            StudySessionCommandHandler handler = new(
                currentUserOwnsSession
                    ? new InMemoryRepository<Student>(student)
                    : new InMemoryRepository<Student>(student, currentStudent),
                new InMemoryRepository<StudySession>(session),
                materials,
                selections,
                new InMemoryRepository<StudySessionSelectedSection>(),
                new InMemoryRepository<StudyMaterialSection>(),
                images,
                fileStore,
                imageFileStore,
                new StubPdfProcessor(),
                new StubCurrentUser(currentUserId),
                unitOfWork,
                Options.Create(new StudySessionImageUploadOptions()));

            return new Fixture(handler, session, material, selection, materials, selections, images, fileStore,
                imageFileStore, unitOfWork);
        }
    }

    private sealed class RecordingFileStore : IStudyMaterialFileStore
    {
        public List<string> DeletedReferences { get; } = [];

        public Task<string> SaveOriginalAsync(Guid studentId, string fileName, Stream content,
            CancellationToken cancellationToken) =>
            Task.FromResult("original/new.pdf");

        public Task<Stream> OpenReadAsync(string storageReference, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream([1]));

        public Task<string> SaveDerivedAsync(Guid studentId, string fileName, Stream content,
            CancellationToken cancellationToken) =>
            Task.FromResult("derived/new.pdf");

       public Task DeleteAsync(string storageReference, CancellationToken cancellationToken)
{
    // Cleanup must still work when the request token has been canceled.
    DeletedReferences.Add(storageReference);
    return Task.CompletedTask;
}
    }

    private sealed class RecordingImageFileStore : IStudySessionImageFileStore
    {
        private int _saveCount;

        public List<string> SavedReferences { get; } = [];

        public List<string> DeletedReferences { get; } = [];

        public int? CancelAfterSaveCount { get; set; }

        public CancellationTokenSource? Cancellation { get; set; }

        public Task<string> SaveAsync(Guid studentId, string extension, Stream content,
            CancellationToken cancellationToken)
        {
            string reference = $"images/{++_saveCount}{extension}";
            SavedReferences.Add(reference);
            if (SavedReferences.Count == CancelAfterSaveCount)
            {
                Cancellation?.Cancel();
            }

            return Task.FromResult(reference);
        }

        public Task DeleteAsync(string storageReference, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeletedReferences.Add(storageReference);
            return Task.CompletedTask;
        }
    }

    private sealed class StubPdfProcessor : IStudyMaterialPdfProcessor
    {
        public Task<int> GetPageCountAsync(Stream content, CancellationToken cancellationToken) => Task.FromResult(5);

        public Task<Stream> ExtractPagesAsync(Stream content, StudySessionPageRange pageRange,
            CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream([1]));
    }

    private sealed class StubCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public string? Email => null;
    }

    private sealed class RecordingUnitOfWork(bool failSave) : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }

        public Task<int> SaveChangesAsync()
        {
            SaveChangesCalls++;
            return failSave
                ? Task.FromException<int>(new InvalidOperationException("Persistence failed."))
                : Task.FromResult(1);
        }

        public Task BeginTransactionAsync() => Task.CompletedTask;

        public Task CommitTransactionAsync() => Task.CompletedTask;

        public Task RollbackTransactionAsync() => Task.CompletedTask;

        public void Dispose() { }
    }

    private sealed class InMemoryRepository<T>(params T[] entities) : IBaseRepository<T> where T : class
    {
        private readonly List<T> _entities = [.. entities];

        public Task<T?> GetByIdAsync(Guid id, params Expression<Func<T, object>>[] includes)
        {
            return Task.FromResult(_entities.SingleOrDefault(item =>
                (Guid)typeof(T).GetProperty("Id")!.GetValue(item)! == id));
        }

        public Task<IEnumerable<T>> GetAllAsync(params Expression<Func<T, object>>[] includes) =>
            Task.FromResult<IEnumerable<T>>(_entities.ToArray());

        public Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>> criteria,
            params Expression<Func<T, object>>[] includes) =>
            Task.FromResult<IEnumerable<T>>(_entities.AsQueryable().Where(criteria).ToArray());

        public IQueryable<T> GetAll() => _entities.AsQueryable();

        public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> criteria,
            params Expression<Func<T, object>>[] includes) =>
            Task.FromResult(_entities.AsQueryable().FirstOrDefault(criteria));

        public void Add(T entity) => _entities.Add(entity);

        public Task AddRangeAsync(IEnumerable<T> entities)
        {
            _entities.AddRange(entities);
            return Task.CompletedTask;
        }

        public void Update(T entity) { }

        public Task UpdateAsync(T entity) => Task.CompletedTask;

        public Task DeleteAsync(T entity)
        {
            _entities.Remove(entity);
            return Task.CompletedTask;
        }

        public void DeleteRange(IEnumerable<T> entities) => _entities.RemoveAll(entities.Contains);

        public void Delete(T entity) => _entities.Remove(entity);
    }
}
