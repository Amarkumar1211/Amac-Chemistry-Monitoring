using FurnaceMonitor.Application.Interfaces.Repositories;
using FurnaceMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;

namespace FurnaceMonitor.Infrastructure.Persistence;

/// <summary>
/// Aggregates repositories over the shared <see cref="AppDbContext"/> and
/// coordinates transactions.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
        Users = new GenericRepository<User>(context);
        Roles = new GenericRepository<Role>(context);
        Furnaces = new GenericRepository<Furnace>(context);
        Batches = new GenericRepository<FurnaceBatch>(context);
        Chemicals = new GenericRepository<FurnaceChemical>(context);
        SignalRConnections = new GenericRepository<SignalRConnection>(context);
        AuditLogs = new GenericRepository<AuditLog>(context);
        UserSessions = new GenericRepository<UserSession>(context);
        PartSpecifications = new GenericRepository<PartSpecification>(context);
    }

    public IRepository<User> Users { get; }
    public IRepository<Role> Roles { get; }
    public IRepository<Furnace> Furnaces { get; }
    public IRepository<FurnaceBatch> Batches { get; }
    public IRepository<FurnaceChemical> Chemicals { get; }
    public IRepository<SignalRConnection> SignalRConnections { get; }
    public IRepository<AuditLog> AuditLogs { get; }
    public IRepository<UserSession> UserSessions { get; }
    public IRepository<PartSpecification> PartSpecifications { get; }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
            await _transaction.DisposeAsync();
        _transaction = await _context.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
            return;
        await _transaction.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
            return;
        await _transaction.RollbackAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);

    public async Task<int> ExecuteAsync(Func<Task> action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        var strategy = _context.Database.CreateExecutionStrategy();
        
        return await strategy.ExecuteAsync(async () =>
        {
            await BeginTransactionAsync(ct);
            try
            {
                await action();
                var changes = await _context.SaveChangesAsync(ct);
                await CommitAsync(ct);
                return changes;
            }
            catch
            {
                await RollbackAsync(ct);
                throw;
            }
        });
    }
}

