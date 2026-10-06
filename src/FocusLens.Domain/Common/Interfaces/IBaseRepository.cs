using System.Linq.Expressions;

namespace FocusLens.Domain.Common.Interfaces;

public interface IBaseRepository<T> where T : class
{
    Task<T?> GetByIdAsync(
        Guid id,
        params Expression<Func<T, object>>[] includes
    );

    Task<IEnumerable<T>> GetAllAsync(
        params Expression<Func<T, object>>[] includes
    );

    Task<IEnumerable<T>> GetAllAsync(
        Expression<Func<T, bool>> criteria,
        params Expression<Func<T, object>>[] includes
    );

    IQueryable<T> GetAll();

    Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> criteria,
        params Expression<Func<T, object>>[] includes
    );

    Task<int> CountAsync(Expression<Func<T, bool>> criteria);

    Task<bool> AnyAsync(Expression<Func<T, bool>> criteria);

    void Add(T entity);

    Task AddRangeAsync(IEnumerable<T> entities);

    void Update(T entity);

    Task UpdateAsync(T entity);

    Task DeleteAsync(T entity);

    void DeleteRange(IEnumerable<T> entities);

    void Delete(T entity);
}
