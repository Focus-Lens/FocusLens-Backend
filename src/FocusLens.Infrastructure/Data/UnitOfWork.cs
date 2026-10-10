using FocusLens.Domain.Common;
using FocusLens.Domain.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FocusLens.Infrastructure.Data;

public class UnitOfWork(ApplicationDbContext context) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync()
    {
        try
        {
            return await context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(exception);
        }
    }

    public async Task BeginTransactionAsync() => await context.Database.BeginTransactionAsync();

    public async Task CommitTransactionAsync() => await context.Database.CommitTransactionAsync();

    public async Task RollbackTransactionAsync() =>
        await context.Database.RollbackTransactionAsync();

    public void Dispose() => context.Dispose();
}