namespace FocusLens.Domain.Common.Interfaces;

public interface IUnitOfWork : IDisposable
{
    // Transaction Methods
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}