using FocusLens.Domain.Common.Interfaces;

namespace FocusLens.Infrastructure.Data;

public class UnitOfWork(ApplicationDbContext context) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync() => await context.SaveChangesAsync();

    public async Task BeginTransactionAsync() => await context.Database.BeginTransactionAsync();

    public async Task CommitTransactionAsync() => await context.Database.CommitTransactionAsync();

    public async Task RollbackTransactionAsync() => await context.Database.RollbackTransactionAsync();

    public void Dispose() => context.Dispose();
}