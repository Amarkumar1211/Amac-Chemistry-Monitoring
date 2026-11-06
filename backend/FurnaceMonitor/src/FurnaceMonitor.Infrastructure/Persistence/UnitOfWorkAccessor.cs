namespace FurnaceMonitor.Infrastructure.Persistence;

/// <summary>
/// A singleton holder that exposes the scoped <see cref="AppDbContext"/> to
/// services without creating a circular dependency. Services that need raw
/// query access (e.g. mapped projections) use this accessor.
/// </summary>
public sealed class UnitOfWorkAccessor
{
    /// <summary>The active scoped database context.</summary>
    public AppDbContext DbContext { get; }

    public UnitOfWorkAccessor(AppDbContext dbContext)
    {
        DbContext = dbContext;
    }
}
