using FurnaceMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurnaceMonitor.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.UserName)
            .HasMaxLength(100);

        builder.Property(a => a.Description)
            .HasMaxLength(1000);

        builder.Property(a => a.Entity)
            .HasMaxLength(100);

        builder.Property(a => a.EntityId)
            .HasMaxLength(50);

        builder.Property(a => a.IpAddress)
            .HasMaxLength(50);

        builder.HasIndex(a => a.OccurredAtUtc);
        builder.HasIndex(a => new { a.Action, a.OccurredAtUtc });
    }
}
