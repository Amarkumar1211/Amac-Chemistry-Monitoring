using System.Linq.Expressions;
using FurnaceMonitor.Application.Interfaces.Repositories;
using FurnaceMonitor.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace FurnaceMonitor.Infrastructure.Persistence;

/// <summary>
/// Generic EF Core repository over <see cref="AppDbContext"/>.
/// </summary>
public class GenericRepository<TEntity> : IRepository<TEntity> where TEntity : EntityBase
{
    protected readonly AppDbContext Context;
    protected readonly DbSet<TEntity> Set;

    public GenericRepository(AppDbContext context)
    {
        Context = context;
        Set = context.Set<TEntity>();
    }

    public virtual async Task<TEntity?> GetByIdAsync(int id, CancellationToken ct = default)
        => await Set.FindAsync(new object[] { id }, ct);

    public virtual async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken ct = default)
        => await Set.AsNoTracking().ToListAsync(ct);

    public virtual async Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        => await Set.AsNoTracking().Where(predicate).ToListAsync(ct);

    public virtual async Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        => await Set.AsNoTracking().FirstOrDefaultAsync(predicate, ct);

    public virtual async Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct, params string[] includes)
    {
        IQueryable<TEntity> query = Set.AsNoTracking();
        foreach (var include in includes)
            query = query.Include(include);
        return await query.FirstOrDefaultAsync(predicate, ct);
    }

    public virtual async Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct, params string[] includes)
    {
        IQueryable<TEntity> query = Set.AsNoTracking();
        foreach (var include in includes)
            query = query.Include(include);
        return await query.Where(predicate).ToListAsync(ct);
    }

    public virtual async Task AddAsync(TEntity entity, CancellationToken ct = default)
        => await Set.AddAsync(entity, ct);

    public virtual void Update(TEntity entity)
    {
        Set.Update(entity);
        entity.UpdatedAtUtc = DateTime.UtcNow;
    }

    public virtual void Remove(TEntity entity)
        => Set.Remove(entity);

    public virtual async Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken ct = default)
        => predicate is null
            ? await Set.CountAsync(ct)
            : await Set.CountAsync(predicate, ct);

    public virtual async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        => await Set.AnyAsync(predicate, ct);

    public virtual async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await Context.SaveChangesAsync(ct);
}

