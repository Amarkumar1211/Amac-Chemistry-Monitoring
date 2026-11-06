using FurnaceMonitor.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FurnaceMonitor.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core database context for the Furnace Monitor system.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Furnace> Furnaces => Set<Furnace>();
    public DbSet<FurnaceBatch> FurnaceBatches => Set<FurnaceBatch>();
    public DbSet<FurnaceChemical> FurnaceChemicals => Set<FurnaceChemical>();
    public DbSet<SignalRConnection> SignalRConnections => Set<SignalRConnection>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<PartSpecification> PartSpecifications => Set<PartSpecification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

