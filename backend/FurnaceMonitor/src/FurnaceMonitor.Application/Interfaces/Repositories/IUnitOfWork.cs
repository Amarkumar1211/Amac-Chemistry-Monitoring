namespace FurnaceMonitor.Application.Interfaces.Repositories;

/// <summary>
/// Unit of Work to coordinate multiple repositories within a single
/// database transaction and save changes atomically.
/// </summary>
public interface IUnitOfWork
{
    IRepository<Domain.Entities.User> Users { get; }
    IRepository<Domain.Entities.Role> Roles { get; }
    IRepository<Domain.Entities.Furnace> Furnaces { get; }
    IRepository<Domain.Entities.FurnaceBatch> Batches { get; }
    IRepository<Domain.Entities.FurnaceChemical> Chemicals { get; }
    IRepository<Domain.Entities.SignalRConnection> SignalRConnections { get; }
    IRepository<Domain.Entities.AuditLog> AuditLogs { get; }
    IRepository<Domain.Entities.UserSession> UserSessions { get; }
    IRepository<Domain.Entities.PartSpecification> PartSpecifications { get; }

Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<int> ExecuteAsync(Func<Task> action, CancellationToken ct = default);
}
