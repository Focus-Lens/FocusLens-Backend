using FocusLens.Application.Students;
using FocusLens.Application.UnitTests.Access;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.Common.Results;

namespace FocusLens.Application.UnitTests.Students;

public sealed class GetStudentProfileImageQueryHandlerTests
{
    [Fact]
    public async Task Handle_WithExistingImage_ReturnsImageStreamAndContentType()
    {
        Student student = new(Guid.NewGuid());
        student.SetProfileImageStorageReference("images/profile.png");
        RecordingImageFileStore fileStore = new(new MemoryStream([1, 2, 3]));

        GetStudentProfileImageQueryHandler handler = new(
            new InMemoryRepository<Student>(student),
            fileStore,
            new FakeCurrentUser(student.UserId));

        Result<StudentProfileImageFile> result = await handler.Handle(
            new GetStudentProfileImageQuery(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("image/png", result.Value.ContentType);
        Assert.Same(fileStore.OpenedStream, result.Value.Content);
        Assert.Equal("images/profile.png", fileStore.OpenedReference);
    }

    [Fact]
    public async Task Handle_WhenImageReferenceIsMissing_ReturnsNotFound()
    {
        Student student = new(Guid.NewGuid());
        RecordingImageFileStore fileStore = new();

        GetStudentProfileImageQueryHandler handler = new(
            new InMemoryRepository<Student>(student),
            fileStore,
            new FakeCurrentUser(student.UserId));

        Result<StudentProfileImageFile> result = await handler.Handle(
            new GetStudentProfileImageQuery(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.TopError.Type);
        Assert.Equal("Students.ProfileImageNotFound", result.TopError.Code);
        Assert.Null(fileStore.OpenedReference);
    }

    [Fact]
    public async Task Handle_WhenStudentDoesNotExist_ReturnsNotFound()
    {
        Guid currentUserId = Guid.NewGuid();
        RecordingImageFileStore fileStore = new();

        GetStudentProfileImageQueryHandler handler = new(
            new InMemoryRepository<Student>(),
            fileStore,
            new FakeCurrentUser(currentUserId));

        Result<StudentProfileImageFile> result = await handler.Handle(
            new GetStudentProfileImageQuery(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.TopError.Type);
        Assert.Equal("Students.NotFound", result.TopError.Code);
        Assert.Null(fileStore.OpenedReference);
    }

    [Fact]
    public async Task Handle_WhenStoredFileDoesNotExist_ReturnsNotFound()
    {
        Student student = new(Guid.NewGuid());
        student.SetProfileImageStorageReference("images/profile.jpg");
        RecordingImageFileStore fileStore = new(fileMissing: true);

        GetStudentProfileImageQueryHandler handler = new(
            new InMemoryRepository<Student>(student),
            fileStore,
            new FakeCurrentUser(student.UserId));

        Result<StudentProfileImageFile> result = await handler.Handle(
            new GetStudentProfileImageQuery(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.TopError.Type);
        Assert.Equal("Students.ProfileImageNotFound", result.TopError.Code);
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
