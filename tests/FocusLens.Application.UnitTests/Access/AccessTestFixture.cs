using System.Linq.Expressions;
using System.Reflection;
using FocusLens.Application.Common.Interfaces;
using FocusLens.Domain.Notifications;
using DomainInterfaces = FocusLens.Domain.Common.Interfaces;

namespace FocusLens.Application.UnitTests.Access;

public sealed class InMemoryRepository<T> : DomainInterfaces.IBaseRepository<T> where T : class
{
    private readonly List<T> _entities = [];

    public InMemoryRepository(params T[] entities)
    {
        _entities.AddRange(entities);
    }

    public Task<T?> GetByIdAsync(
        Guid id,
        params Expression<Func<T, object>>[] includes)
    {
        return Task.FromResult(
            _entities.AsQueryable().FirstOrDefault(entity => GetId(entity) == id));
    }

    public Task<IEnumerable<T>> GetAllAsync(
        params Expression<Func<T, object>>[] includes) =>
        Task.FromResult<IEnumerable<T>>(_entities.ToList());

    public Task<IEnumerable<T>> GetAllAsync(
        Expression<Func<T, bool>> criteria,
        params Expression<Func<T, object>>[] includes)
    {
        return Task.FromResult<IEnumerable<T>>(
            _entities.AsQueryable().Where(criteria).ToList());
    }

    public IQueryable<T> GetAll() => _entities.AsQueryable();

    public Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> criteria,
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

    public void DeleteRange(IEnumerable<T> entities) => _entities.RemoveAll(entity => entities.Contains(entity));

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

public sealed class FakeCurrentUser(Guid userId, string? email = null) : ICurrentUser
{
    public Guid? UserId { get; } = userId;

    public string? Email { get; } = email;
}

public sealed class FakeUnitOfWork : DomainInterfaces.IUnitOfWork
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

public sealed class FakeNotificationWriter : INotificationWriter
{
    public Task AddAsync(
        Guid recipientUserId,
        NotificationAudience audience,
        NotificationCategory category,
        string title,
        string message,
        string? actionUrl = null,
        string? actionText = null,
        string? dedupeKey = null) => Task.CompletedTask;
}

public sealed class FakeEmailSender : IEmailSender
{
    public List<(string StudentEmail, string ParentEmail, Guid InvitationId)> Invitations { get; } = [];

    public List<(string ParentEmail, string StudentDisplayName, string InvitationUrl)> StudentParentInvitations
    {
        get;
    } = [];

    public List<(string ChildEmail, string InvitationUrl)> ChildSetupInvitations { get; } = [];

    public Task SendEmailVerificationCodeAsync(string email, string code, TimeSpan codeLifetime,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SendPasswordResetAsync(string email, string code, TimeSpan codeLifetime,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SendParentStudentInvitationAsync(
        string studentEmail,
        string parentEmail,
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        Invitations.Add((studentEmail, parentEmail, invitationId));
        return Task.CompletedTask;
    }

    public Task SendChildSetupInvitationAsync(
        string childEmail,
        string invitationUrl,
        CancellationToken cancellationToken = default)
    {
        ChildSetupInvitations.Add((childEmail, invitationUrl));
        return Task.CompletedTask;
    }

    public Task SendStudentParentInvitationAsync(
        string parentEmail,
        string studentDisplayName,
        string invitationUrl,
        CancellationToken cancellationToken = default)
    {
        StudentParentInvitations.Add((parentEmail, studentDisplayName, invitationUrl));
        return Task.CompletedTask;
    }
}

public sealed class FakeInvitationUrlBuilder : IInvitationUrlBuilder
{
    public string CreateStudentParentInvitationUrl(string token) =>
        $"https://parent.focuslens.test/invitations/parent/{token}";

    public string CreateChildSetupInvitationUrl(string token) =>
        $"https://child.focuslens.test/invitations/child-setup/{token}";
}

public sealed class FakeChildSetupInvitationTokenProtector : IChildSetupInvitationTokenProtector
{
    private const string Prefix = "protected:";

    public string Protect(string token) => $"{Prefix}{token}";

    public string Unprotect(string protectedToken)
    {
        if (!protectedToken.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The token is not protected by this fake protector.");
        }

        return protectedToken[Prefix.Length..];
    }
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
