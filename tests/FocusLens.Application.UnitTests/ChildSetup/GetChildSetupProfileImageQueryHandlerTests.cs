using FocusLens.Application.ChildSetup;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Domain;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Application.UnitTests.ChildSetup;

public sealed class GetChildSetupProfileImageQueryHandlerTests
{
    [Fact]
    public async Task Handle_WithExistingImage_ReturnsImageStreamAndContentType()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = new(parent.Id);
        draft.SetProfileImageStorageReference("images/profile.png");
        RecordingImageFileStore fileStore = new(new MemoryStream([1, 2, 3]));

        GetChildSetupProfileImageQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            fileStore,
            new FakeCurrentUser(parentUserId));

        Result<ChildSetupProfileImageFile> result = await handler.Handle(
            new GetChildSetupProfileImageQuery(draft.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("image/png", result.Value.ContentType);
        Assert.Same(fileStore.OpenedStream, result.Value.Content);
        Assert.Equal("images/profile.png", fileStore.OpenedReference);
    }

    [Fact]
    public async Task Handle_WhenImageReferenceIsMissing_ReturnsNotFound()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = new(parent.Id);
        RecordingImageFileStore fileStore = new();

        GetChildSetupProfileImageQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            fileStore,
            new FakeCurrentUser(parentUserId));

        Result<ChildSetupProfileImageFile> result = await handler.Handle(
            new GetChildSetupProfileImageQuery(draft.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.TopError.Type);
        Assert.Equal("ChildSetup.ProfileImageNotFound", result.TopError.Code);
        Assert.Null(fileStore.OpenedReference);
    }

    [Fact]
    public async Task Handle_WhenDraftBelongsToAnotherParent_ReturnsNotFound()
    {
        Parent owner = new(Guid.NewGuid());
        Parent currentParent = new(Guid.NewGuid());
        ChildSetupDraft draft = new(owner.Id);
        draft.SetProfileImageStorageReference("images/profile.jpg");
        RecordingImageFileStore fileStore = new(new MemoryStream([1]));

        GetChildSetupProfileImageQueryHandler handler = new(
            new InMemoryRepository<Parent>(owner, currentParent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            fileStore,
            new FakeCurrentUser(currentParent.UserId));

        Result<ChildSetupProfileImageFile> result = await handler.Handle(
            new GetChildSetupProfileImageQuery(draft.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.TopError.Type);
        Assert.Equal("ChildSetup.NotFound", result.TopError.Code);
        Assert.Null(fileStore.OpenedReference);
    }

    [Fact]
    public async Task Handle_WhenStoredFileDoesNotExist_ReturnsNotFound()
    {
        Guid parentUserId = Guid.NewGuid();
        Parent parent = new(parentUserId);
        ChildSetupDraft draft = new(parent.Id);
        draft.SetProfileImageStorageReference("images/profile.jpg");
        RecordingImageFileStore fileStore = new(fileMissing: true);

        GetChildSetupProfileImageQueryHandler handler = new(
            new InMemoryRepository<Parent>(parent),
            new InMemoryRepository<ChildSetupDraft>(draft),
            fileStore,
            new FakeCurrentUser(parentUserId));

        Result<ChildSetupProfileImageFile> result = await handler.Handle(
            new GetChildSetupProfileImageQuery(draft.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.TopError.Type);
        Assert.Equal("ChildSetup.ProfileImageNotFound", result.TopError.Code);
    }

    private sealed class RecordingImageFileStore(Stream? openedStream = null, bool fileMissing = false)
        : IProfileImageFileStore
    {
        public Stream? OpenedStream { get; } = openedStream;
        public string? OpenedReference { get; private set; }
        private bool FileMissing { get; } = fileMissing;

        public Task<string> SaveAsync(
            Guid ownerId,
            string extension,
            Stream content,
            CancellationToken cancellationToken) =>
            Task.FromResult("images/new" + extension);

        public Task<Stream> OpenReadAsync(
            string storageReference,
            CancellationToken cancellationToken)
        {
            OpenedReference = storageReference;

            if (FileMissing)
            {
                throw new FileNotFoundException();
            }

            return Task.FromResult(OpenedStream ?? new MemoryStream());
        }

        public Task DeleteAsync(
            string storageReference,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}