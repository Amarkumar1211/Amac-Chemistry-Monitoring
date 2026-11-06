using System.Linq.Expressions;
using FurnaceMonitor.Domain.Common;

namespace FurnaceMonitor.Application.Interfaces.Repositories;

/// <summary>
/// Generic repository contract for aggregate persistence.
/// </summary>
public interface IRepository<TEntity> where TEntity : EntityBase
{
    Task<TEntity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);

    /// <summary>Returns a single entity with specified navigation paths eager-loaded.</summary>
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct, params string[] includes);

    /// <summary>Returns all matching entities with specified navigation paths eager-loaded.</summary>
    Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct, params string[] includes);

    Task AddAsync(TEntity entity, CancellationToken ct = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);
    Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
