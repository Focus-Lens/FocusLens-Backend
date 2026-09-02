using FocusLens.Domain.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace FocusLens.Infrastructure.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDBContext db;
        private IDbContextTransaction? transaction;

        public UnitOfWork(ApplicationDBContext db)
        {
            this.db = db;
        }

        public async Task BeginTransactionAsync()
        {
            transaction = await db.Database.BeginTransactionAsync();
        }

        public async Task CommitTransactionAsync()
        {
            if (transaction is null)
                return;

            try
            {
                await transaction.CommitAsync();
            }
            finally
            {
                await transaction.DisposeAsync();
                transaction = null;
            }
        }

        public void Dispose()
        {
            transaction?.Dispose();
            db.Dispose();
        }

        public async Task RollbackTransactionAsync()
        {
            if (transaction is null)
                return;

            try
            {
                await transaction.RollbackAsync();
            }
            finally
            {
                await transaction.DisposeAsync();
                transaction = null;
            }
        }

        public async Task<int> SaveChangesAsync()
        {
            return await db.SaveChangesAsync();
        }
    }
}
