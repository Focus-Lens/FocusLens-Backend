using FocusLens.Application.ChildSetup;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Application.UnitTests.ChildSetup;

public sealed class UpdateChildSetupDraftProfileImageCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithValidImage_UpdatesDraftProfileImage()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = new(parent.Id);
        RecordingImageFileStore fileStore = new();
        FakeUnitOfWork unitOfWork = new();

        UpdateChildSetupDraftProfileImageCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            fileStore,
            new FakeCurrentUser(parentUserId),
            unitOfWork);

        Result<string> result = await handler.Handle(
            new UpdateChildSetupDraftProfileImageCommand(
                draft.Id,
                "profile.JPG",
                1024,
                _ => Task.FromResult<Stream>(new MemoryStream([1, 2, 3]))),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("images/1.jpg", result.Value);
        Assert.Equal("images/1.jpg", draft.ProfileImageStorageReference);
        Assert.Single(fileStore.SavedFiles);
        Assert.Equal(draft.Id, fileStore.SavedFiles[0].OwnerId);
        Assert.Equal(".jpg", fileStore.SavedFiles[0].Extension);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WithInvalidExtension_ReturnsValidationError()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = new(parent.Id);
        RecordingImageFileStore fileStore = new();

        UpdateChildSetupDraftProfileImageCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            fileStore,
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork());

        Result<string> result = await handler.Handle(
            new UpdateChildSetupDraftProfileImageCommand(
                draft.Id,
                "profile.gif",
                1024,
                _ => Task.FromResult<Stream>(new MemoryStream([1]))),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.TopError.Type);
        Assert.Equal("ChildSetup.InvalidProfileImage", result.TopError.Code);
        Assert.Empty(fileStore.SavedFiles);
    }

    [Fact]
    public async Task Handle_WithOversizedImage_ReturnsValidationError()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = new(parent.Id);
        RecordingImageFileStore fileStore = new();

        UpdateChildSetupDraftProfileImageCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            fileStore,
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork());

        Result<string> result = await handler.Handle(
            new UpdateChildSetupDraftProfileImageCommand(
                draft.Id,
                "profile.png",
                10 * 1024 * 1024 + 1,
                _ => Task.FromResult<Stream>(new MemoryStream([1]))),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.TopError.Type);
        Assert.Equal("ChildSetup.ProfileImageSizeInvalid", result.TopError.Code);
        Assert.Empty(fileStore.SavedFiles);
    }

    [Fact]
    public async Task Handle_WithDraftOwnedByAnotherParent_ReturnsNotFound()
    {
        Parent owner = new(Guid.NewGuid());
        Parent currentParent = new(Guid.NewGuid());
        ChildSetupDraft draft = new(owner.Id);
        RecordingImageFileStore fileStore = new();

        UpdateChildSetupDraftProfileImageCommandHandler handler = new(
            new InMemoryRepository<Parent>(owner, currentParent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            fileStore,
            new FakeCurrentUser(currentParent.UserId),
            new FakeUnitOfWork());

        Result<string> result = await handler.Handle(
            new UpdateChildSetupDraftProfileImageCommand(
                draft.Id,
                "profile.png",
                1024,
                _ => Task.FromResult<Stream>(new MemoryStream([1]))),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.TopError.Type);
        Assert.Equal("ChildSetup.NotFound", result.TopError.Code);
        Assert.Empty(fileStore.SavedFiles);
    }

    [Fact]
    public async Task Handle_WhenDraftIsNotEditable_ReturnsValidationError()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = new(parent.Id);
        draft.MarkInvited();
        RecordingImageFileStore fileStore = new();

        UpdateChildSetupDraftProfileImageCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            fileStore,
            new FakeCurrentUser(parentUserId),
            new FakeUnitOfWork());

        Result<string> result = await handler.Handle(
            new UpdateChildSetupDraftProfileImageCommand(
                draft.Id,
                "profile.png",
                1024,
                _ => Task.FromResult<Stream>(new MemoryStream([1]))),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.TopError.Type);
        Assert.Equal("ChildSetup.DraftNotEditable", result.TopError.Code);
        Assert.Empty(fileStore.SavedFiles);
    }

    [Fact]
    public async Task Handle_WithExistingImage_ReplacesOldReference()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = new(parent.Id);
        draft.SetProfileImageStorageReference("images/old.png");

        RecordingImageFileStore fileStore = new();
        FakeUnitOfWork unitOfWork = new();

        UpdateChildSetupDraftProfileImageCommandHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            fileStore,
            new FakeCurrentUser(parentUserId),
            unitOfWork);

        Result<string> result = await handler.Handle(
            new UpdateChildSetupDraftProfileImageCommand(
                draft.Id,
                "profile.webp",
                1024,
                _ => Task.FromResult<Stream>(new MemoryStream([1]))),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("images/old.png", Assert.Single(fileStore.DeletedReferences));
        Assert.Equal("images/1.webp", draft.ProfileImageStorageReference);
    }

    private sealed class RecordingImageFileStore : IProfileImageFileStore
    {
        private int _saveCount;

        public List<SavedFile> SavedFiles { get; } = [];
        public List<string> DeletedReferences { get; } = [];

        public Task<string> SaveAsync(
            Guid studentId,
            string extension,
            Stream content,
            CancellationToken cancellationToken)
        {
            string reference = $"images/{++_saveCount}{extension}";
            SavedFiles.Add(new SavedFile(studentId, extension, reference));
            return Task.FromResult(reference);
        }

        public Task<Stream> OpenReadAsync(
            string storageReference,
            CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream());

        public Task DeleteAsync(
            string storageReference,
            CancellationToken cancellationToken)
        {
            DeletedReferences.Add(storageReference);
            return Task.CompletedTask;
        }
    }

    private sealed record SavedFile(
        Guid OwnerId,
        string Extension,
        string Reference);
}
