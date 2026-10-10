using System.Linq.Expressions;
using FocusLens.Domain.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FocusLens.Infrastructure.Data;

public class BaseRepository<T>(ApplicationDbContext context) : IBaseRepository<T>
    where T : class
{
    protected readonly ApplicationDbContext _context = context;
    private readonly DbSet<T> _dbSet = context.Set<T>();

    public async Task<T?> GetByIdAsync(
        Guid id,
        params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet;

        foreach (Expression<Func<T, object>> include in includes)
        {
            query = query.Include(include);
        }

        return await query.FirstOrDefaultAsync(entity => EF.Property<Guid>(entity, "Id") == id);
    }

    public async Task<IEnumerable<T>> GetAllAsync(
        params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet;

        foreach (Expression<Func<T, object>> include in includes)
        {
            query = query.Include(include);
        }

        return await query.ToListAsync();
    }

    public async Task<IEnumerable<T>> GetAllAsync(
        Expression<Func<T, bool>> criteria,
        params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet.Where(criteria);

        foreach (Expression<Func<T, object>> include in includes)
        {
            query = query.Include(include);
        }

        return await query.ToListAsync();
    }

    public IQueryable<T> GetAll() => _dbSet;

    public async Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> criteria,
        params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet;

        foreach (Expression<Func<T, object>> include in includes)
        {
            query = query.Include(include);
        }

        return await query.FirstOrDefaultAsync(criteria);
    }

    public Task<int> CountAsync(Expression<Func<T, bool>> criteria) =>
        _dbSet.CountAsync(criteria);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> criteria) =>
        _dbSet.AnyAsync(criteria);

    public void Add(T entity) => _dbSet.Add(entity);

    public async Task AddRangeAsync(IEnumerable<T> entities) => await _dbSet.AddRangeAsync(entities);

    public void Update(T entity) => _dbSet.Update(entity);

    public Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public void DeleteRange(IEnumerable<T> entities) => _dbSet.RemoveRange(entities);

    public void Delete(T entity) => _dbSet.Remove(entity);
}