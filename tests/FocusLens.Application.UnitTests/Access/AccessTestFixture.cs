using System.Linq.Expressions;
using System.Reflection;

using FocusLens.Domain.Common.Interfaces;

namespace FocusLens.Application.UnitTests.Access;

public sealed class InMemoryRepository<T> : IBaseRepository<T> where T : class
{
    private readonly List<T> _entities = [];

    public InMemoryRepository(params T[] entities)
        => _entities.AddRange(entities);

    public Task<T?> GetByIdAsync(
        Guid id,
        params Expression<Func<T, object>>[] includes)
        => Task.FromResult(
            _entities.AsQueryable().FirstOrDefault(
                entity => GetId(entity) == id));

    public Task<IEnumerable<T>> GetAllAsync(
        params Expression<Func<T, object>>[] includes)
        => Task.FromResult<IEnumerable<T>>(_entities.ToList());

    public IQueryable<T> GetAll() => _entities.AsQueryable();

    public Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> criteria,
        params Expression<Func<T, object>>[] includes)
        => Task.FromResult(_entities.AsQueryable().FirstOrDefault(criteria));

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

    public void DeleteRange(IEnumerable<T> entities)
        => _entities.RemoveAll(entity => entities.Contains(entity));

    public void Delete(T entity) => _entities.Remove(entity);

    private static Guid GetId(T entity)
    {
        PropertyInfo? property = typeof(T).GetProperty("Id");

        if (property is null)
        {
            throw new InvalidOperationException("Entity must have an Id property.");
        }

        return (Guid)property.GetValue(entity)!;
    }
}

public sealed class FakeCurrentUser(Guid userId) : ICurrentUser
{
    public Guid UserId { get; } = userId;
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCalls { get; private set; }

    public Task<int> SaveChangesAsync()
    {
        SaveChangesCalls++;
        return Task.FromResult(1);
    }

    public Task BeginTransactionAsync() => Task.CompletedTask;
    public Task CommitTransactionAsync() => Task.CompletedTask;
    public Task RollbackTransactionAsync() => Task.CompletedTask;
    public void Dispose() { }
}

public static class AccessTestObjectExtensions
{
    public static void SetPrivateProperty<T>(this T target, string propertyName, object value)
    {
        PropertyInfo? property = typeof(T).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (property is null)
        {
            throw new InvalidOperationException(
                $"Property {propertyName} was not found on {typeof(T).Name}.");
        }

        property.SetValue(target, value);
    }
}
