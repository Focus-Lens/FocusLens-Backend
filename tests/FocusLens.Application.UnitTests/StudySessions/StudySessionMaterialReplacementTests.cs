using System.Linq.Expressions;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Application.StudySessions;
using FocusLens.Contracts.StudySessions;
using FocusLens.Domain;
using FocusLens.Domain.Common.Interfaces;
using FocusLens.Domain.StudySessions;
using FocusLens.Domain.Students;
using ContractStudyMaterialSource = FocusLens.Contracts.StudySessions.StudyMaterialSource;
using DomainStudyMaterialSource = FocusLens.Domain.StudySessions.StudyMaterialSource;

namespace FocusLens.Application.UnitTests.StudySessions;

public sealed class StudySessionMaterialReplacementTests
{
    [Fact]
    public async Task ReplaceMaterial_ReplacesMaterialAndSelectionInOneSave_ThenCleansRetiredFiles()
    {
        Fixture fixture = Fixture.Create(withSelection: true);

        var result = await fixture.Handler.Handle(
            new ChangeStudySessionMaterialCommand(fixture.Session.Id, "replacement.pdf", 12, [1, 2, 3], ContractStudyMaterialSource.Upload),
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
        Fixture fixture = Fixture.Create(withSelection: true, failSave: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Handler.Handle(
            new ChangeStudySessionMaterialCommand(fixture.Session.Id, "replacement.pdf", 12, [1, 2, 3], ContractStudyMaterialSource.Upload),
            CancellationToken.None));

        Assert.Equal(1, fixture.UnitOfWork.SaveChangesCalls);
        Assert.Contains("original/new.pdf", fixture.FileStore.DeletedReferences);
        Assert.DoesNotContain(fixture.OldMaterial.StorageReference, fixture.FileStore.DeletedReferences);
        Assert.DoesNotContain(fixture.OldSelection!.DerivedStorageReference!, fixture.FileStore.DeletedReferences);
    }

    private sealed record Fixture(
        StudySessionCommandHandler Handler,
        StudySession Session,
        StudyMaterial OldMaterial,
        StudySessionSelection? OldSelection,
        InMemoryRepository<StudyMaterial> Materials,
        InMemoryRepository<StudySessionSelection> Selections,
        RecordingFileStore FileStore,
        RecordingUnitOfWork UnitOfWork)
    {
        public static Fixture Create(bool withSelection, bool failSave = false)
        {
            Guid userId = Guid.NewGuid();
            var student = new Student(userId);
            StudySession session = StudySession.Create(student.Id, FocusLens.Domain.StudySessions.StudySessionMode.Digital).Value;
            StudyMaterial material = StudyMaterial.Create(student.Id, "old.pdf", 10, 5, "original/old.pdf", DomainStudyMaterialSource.Upload).Value;
            session.SetStudyMaterial(material);
            StudySessionSelection? selection = null;
            if (withSelection)
            {
                selection = StudySessionSelection.Create(session, material, 1, 2).Value;
                selection.SetDerivedStorageReference("derived/old.pdf");
                session.SetSelection(selection);
            }

            var materials = new InMemoryRepository<StudyMaterial>(material);
            var selections = selection is null ? new InMemoryRepository<StudySessionSelection>() : new InMemoryRepository<StudySessionSelection>(selection);
            var fileStore = new RecordingFileStore();
            var unitOfWork = new RecordingUnitOfWork(failSave);
            var handler = new StudySessionCommandHandler(
                new InMemoryRepository<Student>(student),
                new InMemoryRepository<StudySession>(session),
                materials,
                selections,
                new InMemoryRepository<StudyMaterialSection>(),
                new InMemoryRepository<StudySessionSelectedSection>(),
                fileStore,
                new StubPdfProcessor(),
                new StubCurrentUser(userId),
                unitOfWork);

            return new Fixture(handler, session, material, selection, materials, selections, fileStore, unitOfWork);
        }
    }

    private sealed class RecordingFileStore : IStudyMaterialFileStore
    {
        public List<string> DeletedReferences { get; } = [];

        public Task<string> SaveOriginalAsync(Guid studentId, string fileName, Stream content, CancellationToken cancellationToken)
            => Task.FromResult("original/new.pdf");

        public Task<Stream> OpenReadAsync(string storageReference, CancellationToken cancellationToken)
            => Task.FromResult<Stream>(new MemoryStream([1]));

        public Task<string> SaveDerivedAsync(Guid studentId, string fileName, Stream content, CancellationToken cancellationToken)
            => Task.FromResult("derived/new.pdf");

        public Task DeleteAsync(string storageReference, CancellationToken cancellationToken)
        {
            DeletedReferences.Add(storageReference);
            return Task.CompletedTask;
        }
    }

    private sealed class StubPdfProcessor : IStudyMaterialPdfProcessor
    {
        public Task<int> GetPageCountAsync(Stream content, CancellationToken cancellationToken) => Task.FromResult(5);

        public Task<Stream> ExtractPagesAsync(Stream content, StudySessionPageRange pageRange, CancellationToken cancellationToken)
            => Task.FromResult<Stream>(new MemoryStream([1]));
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
            return failSave ? Task.FromException<int>(new InvalidOperationException("Persistence failed.")) : Task.FromResult(1);
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
            => Task.FromResult(_entities.SingleOrDefault(item => (Guid)typeof(T).GetProperty("Id")!.GetValue(item)! == id));

        public Task<IEnumerable<T>> GetAllAsync(params Expression<Func<T, object>>[] includes)
            => Task.FromResult<IEnumerable<T>>(_entities.ToArray());

        public Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>> criteria, params Expression<Func<T, object>>[] includes)
            => Task.FromResult<IEnumerable<T>>(_entities.AsQueryable().Where(criteria).ToArray());

        public IQueryable<T> GetAll() => _entities.AsQueryable();
        public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> criteria, params Expression<Func<T, object>>[] includes) => Task.FromResult(_entities.AsQueryable().FirstOrDefault(criteria));
        public void Add(T entity) => _entities.Add(entity);
        public Task AddRangeAsync(IEnumerable<T> entities) { _entities.AddRange(entities); return Task.CompletedTask; }
        public void Update(T entity) { }
        public Task UpdateAsync(T entity) => Task.CompletedTask;
        public Task DeleteAsync(T entity) { _entities.Remove(entity); return Task.CompletedTask; }
        public void DeleteRange(IEnumerable<T> entities) => _entities.RemoveAll(entities.Contains);
        public void Delete(T entity) => _entities.Remove(entity);
    }
}
